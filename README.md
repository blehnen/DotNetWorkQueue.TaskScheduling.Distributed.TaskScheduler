# DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler

[![NuGet](https://img.shields.io/nuget/v/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.svg)](https://www.nuget.org/packages/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler)
[![License](https://img.shields.io/badge/license-LGPL--2.1--or--later-blue.svg)](LICENSE)
[![Build status](https://github.com/blehnen/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler/actions/workflows/ci.yml/badge.svg)](https://github.com/blehnen/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler/actions/workflows/ci.yml)

A replacement `ATaskScheduler` for [DotNetWorkQueue](https://github.com/blehnen/DotNetWorkQueue) that coordinates worker thread pool counts across multiple processes on the same machine via a NetMQ P2P bus.

## When to use it

If you run several DotNetWorkQueue consumer processes on a single machine, each process manages its own thread pool and has no awareness of the others. Without coordination, those pools can collectively oversubscribe the machine.

This library replaces the default scheduler with one that broadcasts each process's current worker count over UDP and listens for counts from peers. Every participating process uses the combined total when deciding whether it has room for another task. The result is a soft cross-process concurrency ceiling — loose rather than atomically exact, but sufficient to prevent runaway oversubscription.

## Install

```bash
dotnet add package DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler
```

Or via the Package Manager Console:

```powershell
Install-Package DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler
```

Or as a `PackageReference`:

```xml
<PackageReference Include="DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler" Version="0.3.0" />
```

## Quick start

```csharp
using DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler;

// Inside your DotNetWorkQueue consumer setup, after creating the container
// but before calling Start(), replace the default scheduler:
//
//   Every process that should share the thread-count ceiling must pass
//   the same UDP broadcast port. Processes using different ports form
//   independent, uncoordinated pools.
container.InjectDistributedTaskScheduler(udpBroadcastPort: 9999);
```

A fuller example using `SchedulerContainer`:

```csharp
using (var schedulerContainer = new SchedulerContainer(RegisterService))
{
    // ... create queue consumers, start, etc.
}

private static void RegisterService(IContainer container)
{
    container.InjectDistributedTaskScheduler(9999);
}
```

## UDP broadcast port

The integer argument to `InjectDistributedTaskScheduler` is the UDP port used for NetMQ beacon peer discovery. All cooperating processes on the same machine must pass the same port. You can run independent coordination groups on one machine by assigning each group a different port. Any free UDP port works; 9999 is a reasonable default.

Dead nodes (processes that stop beaconing) are pruned automatically after approximately 10 seconds without a heartbeat.

## Linux / WSL note

On Linux the default beacon interface value `"loopback"` does not loop UDP broadcast back to the sending host. Pass an empty string instead via `TaskSchedulerMultipleConfiguration`:

```csharp
var config = new TaskSchedulerMultipleConfiguration
{
    BeaconInterface = ""
};
container.InjectDistributedTaskScheduler(9999, config);
```

This issue affects Linux and WSL environments and was resolved in the default configuration as of v0.2.1, but if you see no peers discovered on Linux, this is the first thing to check.

## Limitations

- Throttling is loose; thread counts may temporarily exceed the ceiling depending on timing across processes.
- Same-machine only. UDP broadcast is not available across cloud provider VM boundaries.
- Worker count may spike briefly on a new node until it synchronizes with existing peers.
- Works best when all participating schedulers have similar max-thread values.

## Requirements

- .NET 8 or .NET 10
- Windows or Linux
- [DotNetWorkQueue](https://github.com/blehnen/DotNetWorkQueue) 0.9.31 or newer (pulled in transitively)

## Third-party libraries

- [DotNetWorkQueue](https://github.com/blehnen/DotNetWorkQueue)
- [NetMQ](https://github.com/zeromq/netmq)

## License

Copyright &copy; 2019&ndash;2026 Brian Lehnen

Licensed under the [GNU Lesser General Public License v2.1 or later (LGPL-2.1-or-later)](LICENSE).

## Links

- GitHub repository: <https://github.com/blehnen/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler>
- Issue tracker: <https://github.com/blehnen/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler/issues>
- DotNetWorkQueue upstream: <https://github.com/blehnen/DotNetWorkQueue>
