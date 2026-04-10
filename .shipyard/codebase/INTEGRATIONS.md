# INTEGRATIONS.md

## Overview

This library integrates with two external dependencies: **DotNetWorkQueue** (the host work-queue framework whose scheduler it replaces) and **NetMQ** (the ZeroMQ binding used for inter-process P2P messaging and UDP beacon discovery). There are no database connections, HTTP APIs, message brokers, or cloud services. All communication is local-machine TCP/UDP between sibling processes.

## Metrics

| Metric | Value |
|--------|-------|
| External NuGet packages (production) | 2 |
| DotNetWorkQueue base classes extended | 1 (`SmartThreadPoolTaskScheduler`) |
| DotNetWorkQueue interfaces consumed | 5 (`ITaskSchedulerConfiguration`, `IWaitForEventOrCancelThreadPool`, `IMetrics`, `IContainer`, `ILogger` via `DotNetWorkQueue.Logging`) |
| DotNetWorkQueue abstractions replaced | 1 (`ATaskScheduler`) |
| NetMQ socket types used | 3 (`PublisherSocket`, `SubscriberSocket`, `PairSocket`) |
| NetMQ actor/utility types used | 3 (`NetMQActor`, `NetMQBeacon`, `NetMQPoller`) |
| NetMQ message commands defined | 6 (`Publish`, `BroadCast`, `GetHostAddress`, `AddedNode`, `RemovedNode`, `SetCount`) |
| UDP broadcast port (default) | 9999 |
| Dead-node timeout | 10 seconds |
| Beacon heartbeat interval | 1 second |

## Findings

### DotNetWorkQueue Integration

#### Base Class Extension

- **`TaskSchedulerMultiple` extends `SmartThreadPoolTaskScheduler`**: The library's primary class inherits from DotNetWorkQueue's built-in thread-pool-backed scheduler and overrides four hook methods to substitute distributed counts for local-only counts.
  - Evidence: `Source/TaskSchedulerMultiple.cs` (line 30) — `public class TaskSchedulerMultiple: SmartThreadPoolTaskScheduler`
  - Overridden members: `HaveRoomForTask` (line 79), `CurrentTaskCount` (line 87), `IncrementCounter` (line 93), `DecrementCounter` (line 100)

#### Abstract Base Class Replaced

- **`ATaskScheduler` registration replaced**: The DI registration call replaces the DotNetWorkQueue default `ATaskScheduler` with `TaskSchedulerMultiple`.
  - Evidence: `Source/TaskSchedulerSetup.cs` (line 42) — `container.Register<ATaskScheduler, TaskSchedulerMultiple>(LifeStyles.Singleton);`

#### Container / DI Integration

- **`IContainer` from DotNetWorkQueue**: The library integrates via DotNetWorkQueue's SimpleInjector-backed `IContainer` abstraction. Registration is performed through a single extension method.
  - Evidence: `Source/TaskSchedulerSetup.cs` (line 38) — `public static void InjectDistributedTaskScheduler(this IContainer container, int broadCastPort = 9999, string beaconInterface = "loopback")`
  - Three singleton registrations are made: `ITaskSchedulerBus`, `ITaskSchedulerJobCountSync`, `ATaskScheduler`. The `TaskSchedulerMultipleConfiguration` instance is registered as a factory lambda.
  - Evidence: `Source/TaskSchedulerSetup.cs` (lines 40–45)
- **`LifeStyles.Singleton`**: All registrations use DotNetWorkQueue's `LifeStyles.Singleton` lifetime constant.

#### Interfaces Consumed from DotNetWorkQueue

| Interface / Type | Source namespace | How used |
|-----------------|-----------------|----------|
| `ATaskScheduler` | `DotNetWorkQueue` | Base type replaced at registration |
| `SmartThreadPoolTaskScheduler` | `DotNetWorkQueue.TaskScheduling` | Direct base class of `TaskSchedulerMultiple` |
| `ITaskSchedulerConfiguration` | `DotNetWorkQueue` | Constructor-injected into `TaskSchedulerMultiple`, passed to base |
| `IWaitForEventOrCancelThreadPool` | `DotNetWorkQueue` | Constructor-injected into `TaskSchedulerMultiple`, passed to base |
| `IMetrics` | `DotNetWorkQueue` | Constructor-injected into `TaskSchedulerMultiple`, passed to base |
| `IContainer` | `DotNetWorkQueue` | Extension method target in `TaskSchedulerSetup` |
| `ILogger` | `DotNetWorkQueue.Logging` / `Microsoft.Extensions.Logging` | Constructor-injected into `TaskSchedulerBus` and `TaskSchedulerJobCountSync` |
| `Guard` | `DotNetWorkQueue.Validation` | Null-argument guard in `TaskSchedulerMultiple` constructor |

Evidence: `Source/TaskSchedulerMultiple.cs` (lines 43–44), `Source/TaskSchedulerBus.cs` (lines 22–24), `Source/TaskSchedulerJobCountSync.cs` (lines 26–27)

#### Logging

- **`Microsoft.Extensions.Logging.ILogger`** is used directly (not a DotNetWorkQueue-specific abstraction), but is injected by DotNetWorkQueue's container.
  - Evidence: `Source/TaskSchedulerBus.cs` (line 22) — `using Microsoft.Extensions.Logging;`; `Source/TaskSchedulerJobCountSync.cs` (line 27)
  - Log levels used: `LogDebug` for node join/leave/count events, `LogError` for NetMQ processing failures.

---

### NetMQ Integration

NetMQ (ZeroMQ for .NET, version 4.0.2.2) is the only inter-process communication mechanism. All communication is between sibling processes on the same machine. No network communication leaves the host.

#### Socket Pattern: Publish/Subscribe

- **`PublisherSocket`** and **`SubscriberSocket`** implement a fan-out publish/subscribe topology for count broadcasts.
  - `SubscriberSocket` binds to a random TCP port on startup; the port is advertised via UDP beacon.
  - `PublisherSocket` connects to subscriber ports of discovered peers.
  - All topics subscribed with empty string (subscribe-all).
  - Evidence: `Source/TaskSchedulerBus.cs` (lines 81–88) — `_subscriber.Subscribe("")`, `_randomPort = _subscriber.BindRandomPort("tcp://*")`

#### Actor Model: NetMQActor + PairSocket

- **`NetMQActor`** wraps the bus run-loop in a background thread, exposing a `PairSocket` shim for thread-safe command dispatch from the foreground.
  - `ITaskSchedulerBus.Start()` returns the `NetMQActor` directly, which is then used by `TaskSchedulerJobCountSync` to send/receive frames.
  - Evidence: `Source/ITaskSchedulerBus.cs` (line 31) — `NetMQActor Start();`
  - Evidence: `Source/TaskSchedulerBus.cs` (lines 64–68) — `_actor = NetMQActor.Create(RunActor);`

#### UDP Beacon Discovery: NetMQBeacon

- **`NetMQBeacon`** performs UDP peer discovery. Each process broadcasts its subscriber TCP port number as its beacon payload, allowing peers to connect.
  - Beacon interval: 1 second (`TimeSpan.FromSeconds(1)`).
  - Beacon port is configurable (default 9999).
  - Beacon interface is configurable: `"loopback"` (Windows default), `""` (first non-loopback, cross-platform), `"*"` (all interfaces), or a specific IP.
  - Evidence: `Source/TaskSchedulerBus.cs` (lines 99–108)
  - Evidence: `Source/TaskSchedulerMultipleConfiguration.cs` (lines 33–46) — full documentation of valid `beaconInterface` values

#### Poller: NetMQPoller

- **`NetMQPoller`** drives all socket and timer events on a single background thread within the actor. Sockets polled: `_shim`, `_subscriber`, `_beacon`. A 1-second `NetMQTimer` triggers dead-node cleanup.
  - Evidence: `Source/TaskSchedulerBus.cs` (lines 115–118) — `_poller = new NetMQPoller { _shim, _subscriber, _beacon, timer };`

#### Message Protocol

All messages are multi-frame NetMQ messages. The command enum `TaskSchedulerBusCommands` defines the protocol:

| Command | Direction | Frame layout |
|---------|-----------|-------------|
| `Publish` (0) | Shim → Actor | Prefix frame for all outbound messages |
| `BroadCast` (1) | Peer → Peer | F0=Publish, F1=BroadCast, F2=sender address |
| `GetHostAddress` (2) | Foreground → Actor | Request; response is F0=address string |
| `AddedNode` (3) | Actor → Foreground | F0=AddedNode, F1=node TCP address |
| `RemovedNode` (4) | Actor → Foreground | F0=RemovedNode, F1=node TCP address |
| `SetCount` (5) | Peer → Peer | F0=Publish, F1=SetCount, F2=port (key), F3=count |

Evidence: `Source/TaskSchedulerBusCommands.cs` (lines 26–72)

#### Node Lifecycle

- **Discovery**: Beacon received → parse port from beacon string → build `tcp://<host>:<port>` address → `_publisher.Connect(address)` → notify shim with `AddedNode`.
- **Heartbeat**: Each beacon updates a `DateTime.UtcNow` timestamp in `_nodes` dictionary.
- **Dead-node pruning**: Nodes not seen for >10 seconds are removed; `_publisher.Disconnect(address)` is called; `RemovedNode` notification sent; peer's count removed from `_otherProcessorCounts`.
  - Evidence: `Source/TaskSchedulerBus.cs` (lines 192–207), `Source/TaskSchedulerJobCountSync.cs` (lines 199–206)

#### Transport

- **Protocol**: TCP (for publisher/subscriber data), UDP (for beacon discovery)
- **Scope**: Loopback / local subnet only. No external network egress.
- **Port allocation**: Subscriber TCP port is OS-assigned (random); only the UDP beacon port is configured.

---

### NuGet Publishing

- No `.nuspec` file exists in the repository.
- No NuGet pack or push step exists in the CI workflow.
  - Evidence: `.github/workflows/ci.yml` — workflow ends after `dotnet test`; no `dotnet pack` or `dotnet nuget push`
- [Inferred] Releases may be published to NuGet.org manually by the maintainer, or not at all. The library version `0.2.1` is set in the `.csproj` and a `CHANGELOG` is maintained (referenced in recent commits), suggesting periodic manual releases.

---

### No Other Integrations

The following integration categories are absent from this codebase:

- No database or ORM
- No HTTP client or REST API calls
- No cloud provider SDKs
- No message broker clients (RabbitMQ, Kafka, Azure Service Bus, etc.)
- No gRPC or other RPC frameworks
- No external configuration services (Consul, etcd, etc.)
- No telemetry/APM SDKs (OpenTelemetry, Application Insights, etc.)

## Summary Table

| Integration | Type | Version | Confidence |
|-------------|------|---------|------------|
| DotNetWorkQueue | NuGet library (base framework) | 0.9.14 | Observed |
| NetMQ | NuGet library (ZeroMQ) | 4.0.2.2 | Observed |
| `SmartThreadPoolTaskScheduler` (extends) | DotNetWorkQueue base class | — | Observed |
| `ATaskScheduler` (replaces) | DotNetWorkQueue abstract type | — | Observed |
| `IContainer` (SimpleInjector wrapper) | DotNetWorkQueue DI | — | Observed |
| `NetMQBeacon` (UDP discovery) | NetMQ | — | Observed |
| `PublisherSocket` / `SubscriberSocket` | NetMQ pub/sub | — | Observed |
| `NetMQActor` (actor model) | NetMQ | — | Observed |
| `NetMQPoller` (event loop) | NetMQ | — | Observed |
| NuGet.org publish | Manual or absent | — | Inferred |

## Open Questions

- Is the library published to NuGet.org? If so, under what package ID? No publish pipeline is present in the repository.
- The `beaconInterface` default of `"loopback"` does not work on Linux (noted in the configuration docs and `TaskSchedulerSetup` XML comment). Is there a plan to change the default for cross-platform support?
- Does DotNetWorkQueue 0.9.14 still provide `SmartThreadPoolTaskScheduler` via its public API, or has that been internalised in newer DotNetWorkQueue versions? This determines upgrade path risk.
