# Distributed.TaskScheduler

[![License LGPLv2.1](https://img.shields.io/badge/license-LGPLv2.1-green.svg)](http://www.gnu.org/licenses/lgpl-2.1.html)
[![Build status](https://ci.appveyor.com/api/projects/status/p6nov0fd9axwsdh8/branch/master?svg=true)](https://ci.appveyor.com/project/blehnen/dotnetworkqueue-taskscheduling-distributed-tasksch/branch/master)

A replacement task scheduler for [DotNetWorkQueue](https://github.com/blehnen/DotNetWorkQueue) that throttles worker thread pools across multiple processes on the same machine using UDP peer discovery.

## Overview

This module solves a specific problem: a legacy system that forks work into child processes, where each child process runs queued work via its own scheduler. The goal is to have a **soft limit** on how many threads the entire collection uses as a group.

For example, if there are 4 child processes running, each with their own scheduler:

| Process | Current Workers | Max Workers |
|---------|:-:|:-:|
| Child A | 1 | 8 |
| Child B | 3 | 8 |
| Child C | 4 | 8 |
| Child D | 3 | 10 |

Child D can use up to 10 workers, but it's only using 3 because the other three schedulers are using a combined total of 8 already. There is no hard limit — concurrency is throttled based on the current instance count and the last known status of instances sharing the same UDP port.

## Features

- Worker thread pool is throttled across processes (loose, not exact or atomic)
- Minimal configuration — UDP port number is the only setting
- Each scheduler instance can have its own max threads and internal queue size
- Stale instances are automatically removed after they haven't been seen for a while

## Limitations

- **Loose throttling** — may temporarily exceed desired thread count depending on timing
- **No work group support** — work groups function but aren't throttled between schedulers, only within the same instance
- **Same machine only** — relies on UDP broadcast, which most cloud providers disable
- **Worker spikes on new nodes** — usage may spike until a new node syncs with existing nodes
- **Best with similar limits** — if one scheduler has a max of 20 but others have a max of 2, the smaller schedulers risk being starved

## Usage

When creating your scheduler container, add an override method:

```csharp
using (var schedulerContainer = new SchedulerContainer(RegisterService))
{
    // etc...
}
```

Inside that override method, call the extension method to register the new task scheduler. This replaces the default one. You can leave the default port at 9999 or explicitly set one.

> **Note:** All schedulers that should share thread counts must use the **same** port number. You can have multiple independent groups on the same machine by using different ports.

```csharp
private static void RegisterService(IContainer container)
{
    container.InjectDistributedTaskScheduler(1234);
}
```

## Building

```bash
dotnet restore Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.sln
dotnet build Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.sln
```

## Running Tests

```bash
dotnet test Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.Tests/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.Tests.csproj
```

## License

Copyright © 2019-2026 Brian Lehnen

This program is free software: you can redistribute it and/or modify it under the terms of the [GNU Lesser General Public License v2.1](http://www.gnu.org/licenses/lgpl-2.1.html) or any later version.

## 3rd Party Libraries

- [DotNetWorkQueue](https://github.com/blehnen/DotNetWorkQueue)
- [NetMQ](https://github.com/zeromq/netmq)
