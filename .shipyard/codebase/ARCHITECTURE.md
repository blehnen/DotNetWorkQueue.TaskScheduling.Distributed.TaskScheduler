# ARCHITECTURE.md

## Overview

This library is a small, focused extension to DotNetWorkQueue that adds cross-process concurrency throttling on a single machine. It replaces the default `ATaskScheduler` with `TaskSchedulerMultiple`, which consults a distributed count — the sum of local and all peer node task counts — before accepting new work. Peer discovery and count propagation are handled by a NetMQ-based actor that uses UDP beacon broadcasts to find siblings and a pub/sub socket pair for message exchange.

The architecture follows a decorator/extension pattern: it does not replace DotNetWorkQueue's threading model, only intercepts the four scheduler hooks (`HaveRoomForTask`, `CurrentTaskCount`, `IncrementCounter`, `DecrementCounter`) to substitute a distributed count for the local one.

## Metrics

| Metric | Value |
|--------|-------|
| Source files (library) | 7 |
| Source files (tests) | 1 |
| Public types | 6 (`TaskSchedulerMultiple`, `TaskSchedulerJobCountSync`, `TaskSchedulerBus` — via interfaces, `TaskSchedulerMultipleConfiguration`, `TaskSchedulerSetup`, plus 2 interfaces) |
| Internal types | 2 (`TaskSchedulerBus`, `NodeKey`, `TaskSchedulerBusCommands` enum) |
| NetMQ message commands | 6 (`Publish`, `BroadCast`, `GetHostAddress`, `AddedNode`, `RemovedNode`, `SetCount`) |
| Dead-node timeout | 10 seconds (`TaskSchedulerBus.cs` line 35) |
| Beacon heartbeat interval | 1 second (`TaskSchedulerBus.cs` line 104) |
| Startup beacon wait | 1100 ms (`TaskSchedulerJobCountSync.cs` line 142) |
| Target frameworks | net10.0, net8.0, net48, net472 |
| Library version | 0.2.1 |

## Findings

### Overall Pattern

- **Decorator over DotNetWorkQueue scheduler**: `TaskSchedulerMultiple` extends `SmartThreadPoolTaskScheduler` and overrides exactly four `protected` hooks. All thread-pool management, wait handles, and queue logic remain in the base class.
  - Evidence: `Source/TaskSchedulerMultiple.cs` lines 79–103

- **Actor-based isolation for NetMQ**: All NetMQ socket operations run inside a `NetMQActor`, which owns a dedicated background thread (`RunActor` shim). The rest of the application interacts with the actor exclusively through `PairSocket` frame messages, keeping NetMQ's single-threaded socket rule intact.
  - Evidence: `Source/TaskSchedulerBus.cs` lines 64–127

### Component Boundaries and Responsibilities

| Component | Boundary | Responsibility |
|-----------|----------|----------------|
| `TaskSchedulerMultiple` | Public — implements `ATaskScheduler` | Scheduler hook overrides; starts sync on `Task.Run`; wires `RemoteCountChanged` event |
| `ITaskSchedulerJobCountSync` / `TaskSchedulerJobCountSync` | Public interface / public impl | Local `Interlocked` counter; peer count dictionary; message-loop thread; fires `RemoteCountChanged` |
| `ITaskSchedulerBus` / `TaskSchedulerBus` | Public interface / internal impl | NetMQ actor lifecycle; beacon, publisher, subscriber sockets; node tracking; dead-node pruning |
| `NodeKey` | Internal | Value-type identity for a peer (host + port); builds `tcp://host:port` address |
| `TaskSchedulerBusCommands` | Internal enum | Defines the 6-command message protocol |
| `TaskSchedulerMultipleConfiguration` | Public | Holds broadcast UDP port and beacon interface string; immutable after construction |
| `TaskSchedulerSetup` | Public static | Single extension method that wires all three singletons into the SimpleInjector container |

### DI / Integration Point

- **Single-call wiring**: `TaskSchedulerSetup.InjectDistributedTaskScheduler(IContainer, int, string)` registers `ITaskSchedulerBus`, `ITaskSchedulerJobCountSync`, and `ATaskScheduler` as singletons and creates/registers the configuration object. This is the only public entry point for consumers.
  - Evidence: `Source/TaskSchedulerSetup.cs` lines 38–46

- **Container abstraction**: The method accepts DotNetWorkQueue's `IContainer` abstraction (SimpleInjector underneath). Lifetimes are `LifeStyles.Singleton` for all three service registrations plus the configuration instance.
  - Evidence: `Source/TaskSchedulerSetup.cs` lines 40–45

### Threading Model

- **Three concurrent execution contexts** operate at runtime:
  1. **NetMQ actor thread** — `RunActor` shim, owned by `NetMQActor`. Runs `NetMQPoller` with subscriber socket, beacon socket, shim pair socket, and a 1-second cleanup timer. All NetMQ socket reads/writes happen here.
  2. **Message-loop thread** — `Task.Run(() => sync.Start())` in `TaskSchedulerMultiple.Start()`. Loops on `ProcessMessages()`, polling the actor via `TryReceiveFrameString(10 ms timeout)` under `_lockSocket`.
  3. **DotNetWorkQueue worker threads** — call `IncrementCounter`/`DecrementCounter` on `TaskSchedulerMultiple`, which call `IncreaseCurrentTaskCount`/`DecreaseCurrentTaskCount` under the same `_lockSocket`.

- **Local counter**: `_currentTaskCount` is a `long` modified with `Interlocked.Increment`/`Decrement`/`Read`, but always under `_lockSocket` as well (the lock is actually load-bearing for coordinating the socket send that follows the counter change).
  - Evidence: `Source/TaskSchedulerJobCountSync.cs` lines 89–99, 108–119

- **Peer count dictionary**: `ConcurrentDictionary<int, long>` keyed by peer's subscriber port. Written on the message-loop thread under `_lockSocket`; summed in `GetCurrentTaskCount()` also under `_lockSocket`.
  - Evidence: `Source/TaskSchedulerJobCountSync.cs` lines 40, 68–80

- **Known race**: `_stopRequested` and `_running` are plain `bool` fields (not `volatile`), written on one thread and read on another without a memory barrier. Functionally safe on x86 TSO but formally a data race.
  - Evidence: `Source/TaskSchedulerJobCountSync.cs` lines 36–37, 152–153, 242

### Messaging Protocol

All inter-actor communication is over `PairSocket` frames. External (bus) messages flow: caller sends to actor over PairSocket → actor's `OnShimReady` decides to forward to `PublisherSocket` or reply inline.

#### Shim → Actor commands (caller-to-bus direction)

| Command | Frames sent | Description |
|---------|-------------|-------------|
| `Publish` | `"Publish"` + forwarded frames | Forward subsequent frames verbatim to the `PublisherSocket` |
| `GetHostAddress` | `"GetHostAddress"` | Actor replies with `"host:port"` string on the same PairSocket |
| `NetMQActor.EndShimMessage` | (built-in) | Stops the poller and tears down sockets |

#### Actor → Shim notifications (bus-to-caller direction, async)

| Notification | Frames | Trigger |
|--------------|--------|---------|
| `AddedNode` + address | 2 frames | New beacon received from unknown peer |
| `RemovedNode` + address | 2 frames | Peer's `DateTime` entry exceeds 10-second dead-node timeout |
| Forwarded subscriber frames | N frames | Any message received on `SubscriberSocket` from a peer |

#### Published message layouts (sent over pub/sub to peers)

| Command | Frame 0 | Frame 1 | Frame 2 | Frame 3 |
|---------|---------|---------|---------|---------|
| `BroadCast` | `"Publish"` | `"BroadCast"` | our `host:port` | — |
| `SetCount` | `"Publish"` | `"SetCount"` | our subscriber port (string) | count (invariant string) |
| Broadcast reply | `"Publish"` | `"host:port received"` | — | — |

Note: The `Publish` frame is stripped by `OnShimReady` before forwarding to the `PublisherSocket`; peers receive frames 1 onward.
  - Evidence: `Source/TaskSchedulerBusCommands.cs` lines 28–72; `Source/TaskSchedulerJobCountSync.cs` lines 94–98, 147–149; `Source/TaskSchedulerBus.cs` lines 140–145

### Lifecycle: Start

1. Consumer calls `container.InjectDistributedTaskScheduler(port)` — registrations created.
2. DotNetWorkQueue calls `TaskSchedulerMultiple.Start()`.
3. `TaskSchedulerMultiple.Start()` subscribes `JobCountHasChanged` to `RemoteCountChanged`, then fires `Task.Run(() => _jobCount.Start())` and calls `base.Start()`.
4. `TaskSchedulerJobCountSync.Start()` acquires `_lockSocket`, calls `_bus.Start()` (which calls `NetMQActor.Create(RunActor)` — blocks until the actor signals OK), stores the returned `NetMQActor`.
5. The actor's `RunActor` shim: binds subscriber to a random TCP port, configures beacon on the broadcast port, begins publishing its port every 1 second, subscribes to all beacons, attaches a 1-second dead-node timer, signals OK, starts the poller.
6. `TaskSchedulerJobCountSync.Start()` queries `GetHostAddress` from the actor, waits 1100 ms (second beacon fire), broadcasts `BroadCast` to peers, then enters the `ProcessMessages` loop.
  - Evidence: `Source/TaskSchedulerMultiple.cs` lines 55–63; `Source/TaskSchedulerJobCountSync.cs` lines 124–163; `Source/TaskSchedulerBus.cs` lines 74–126

### Lifecycle: Stop / Dispose

1. `TaskSchedulerMultiple.Dispose(bool)` unsubscribes `RemoteCountChanged`, calls `_jobCount.Dispose()`, then calls `base.Dispose()`.
2. `TaskSchedulerJobCountSync.Dispose(bool)` sets `_stopRequested = true`, acquires `_lockSocket`, calls `_actor.Dispose()` (sends `EndShimMessage` to the actor's shim), then busy-spins on `Thread.Sleep(100)` until `_running` becomes `false`.
3. The actor's `OnShimReady` receives `EndShimMessage`, calls `_poller.Stop()`. The `using` blocks in `RunActor` dispose the subscriber, publisher, and beacon sockets.
  - Evidence: `Source/TaskSchedulerMultiple.cs` lines 109–117; `Source/TaskSchedulerJobCountSync.cs` lines 231–273

### Peer Discovery Flow

1. Every node's beacon publishes its subscriber TCP port as a UDP string every 1 second on the shared broadcast port.
2. When `OnBeaconReady` fires: the port string is parsed to an `int`, a `NodeKey(host, port)` is constructed, and — if new — the `PublisherSocket` connects to `tcp://host:port` and an `AddedNode` notification is sent to the sync layer.
3. Known nodes get their `DateTime` entry updated (heartbeat).
4. The 1-second timer calls `ClearDeadNodes`: any node last seen more than 10 seconds ago is removed; the publisher disconnects and a `RemovedNode` notification is sent.
  - Evidence: `Source/TaskSchedulerBus.cs` lines 171–207

### Count Synchronization Flow

1. A DotNetWorkQueue worker thread calls `IncrementCounter` or `DecrementCounter` on `TaskSchedulerMultiple`.
2. These delegate to `IncreaseCurrentTaskCount`/`DecreaseCurrentTaskCount` on `TaskSchedulerJobCountSync`.
3. Under `_lockSocket`, `Interlocked.Increment`/`Decrement` updates `_currentTaskCount`, then a 4-frame `SetCount` message is sent to the actor (which publishes it to all connected peers).
4. Peers' message-loop threads receive a `SetCount` notification from the actor, update `_otherProcessorCounts[port] = value`, then fire `RemoteCountChanged`.
5. `RemoteCountChanged` calls `JobCountHasChanged` on `TaskSchedulerMultiple`, which calls `SetWaitHandle(null)` — waking any thread waiting for a free slot.
6. `HaveRoomForTask` re-evaluates: `GetCurrentTaskCount()` (local `_currentTaskCount` + sum of all peer counts) vs `MaximumConcurrencyLevel`.
  - Evidence: `Source/TaskSchedulerJobCountSync.cs` lines 89–119, 209–220; `Source/TaskSchedulerMultiple.cs` lines 79, 87, 123–127

### State Management

- No persistent state. All counts are in-memory and reconstructed from live peer messages on (re)connection.
- A freshly started node waits 1100 ms before broadcasting, giving existing nodes time to connect their publishers. Peer counts are considered zero until a `SetCount` message arrives.
- [Inferred] If a node restarts, its old entry ages out of peer dictionaries within 10 seconds; the new entry is re-added when its beacon arrives.

## Summary Table

| Item | Detail | Confidence |
|------|--------|------------|
| Architectural pattern | Decorator extension over DotNetWorkQueue scheduler | Observed |
| Messaging transport | NetMQ ZeroMQ (pub/sub + beacon) | Observed |
| Actor model | `NetMQActor` wrapping `NetMQPoller` on dedicated thread | Observed |
| Peer discovery | UDP broadcast beacon, configurable interface | Observed |
| Dead-node detection | 10-second heartbeat timeout, 1-second check interval | Observed |
| Lock strategy | Single `_lockSocket` object guards socket + counter ops | Observed |
| Local counter primitive | `Interlocked.Increment/Decrement/Read` on `long` | Observed |
| Peer count store | `ConcurrentDictionary<int, long>` keyed by subscriber port | Observed |
| DI framework | SimpleInjector via DotNetWorkQueue `IContainer` | Observed |
| All registrations | `LifeStyles.Singleton` | Observed |
| `_stopRequested`/`_running` volatility | Missing `volatile` — formal data race, benign on x86 | Observed |
| Startup delay | 1100 ms `Thread.Sleep` to await second beacon | Observed |
| Dispose strategy | `_actor.Dispose()` + busy-spin on `_running` | Observed |
| Linux beacon quirk | `"loopback"` does not work on Linux; must use `""` | Observed |

## Open Questions

- What happens when `MaximumConcurrencyLevel` differs across nodes sharing the same broadcast port? Each node applies its own configured limit independently; there is no consensus on a shared cap. This could result in asymmetric throttling.
- The `GetCurrentTaskCount()` call in `HaveRoomForTask` and `CurrentTaskCount` acquires `_lockSocket` twice in rapid succession — is the double-lock intentional or an oversight?
- `ProcessMessages` sends a bare string reply to `BroadCast` (`_hostAddress + " received"`) without a command prefix; the `else` branch in the same method logs this. Could unrecognized frames from future protocol additions silently fall into this log branch?
