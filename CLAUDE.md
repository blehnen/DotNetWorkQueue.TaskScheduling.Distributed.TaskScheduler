# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

A replacement task scheduler for [DotNetWorkQueue](https://github.com/blehnen/DotNetWorkQueue) that throttles worker thread pools across multiple processes on the same machine. Processes sharing the same UDP broadcast port coordinate their thread counts via a P2P messaging bus, keeping the combined concurrency below each scheduler's configured maximum.

License: LGPLv2.1. All source files include a license header block.

## Build

SDK-style project targeting `net10.0` and `net8.0`. Solution file is at `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.sln`.

```bash
dotnet restore Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.sln
dotnet build Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.sln
```

## Tests

Integration tests use xUnit and target net8.0. Tests involve real UDP beacon discovery on loopback, so they take ~19 seconds.

```bash
dotnet test Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.Tests/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.Tests.csproj
```

Build settings: warnings are treated as errors (`TreatWarningsAsErrors`), and XML documentation is generated in both Debug and Release. CI runs on GitHub Actions (`.github/workflows/ci.yml`).

## Releasing

Packages are published to NuGet.org by `.github/workflows/publish.yml` using NuGet trusted publishing (OIDC). There is no API key secret; the workflow exchanges its OIDC token for a one-hour key via `NuGet/login`. The nuget.org trusted-publishing policy is tied to this repository and the `publish.yml` file name, so renaming the workflow or moving the push breaks it.

1. Bump `<Version>` in `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj` and add a `CHANGELOG.md` entry.
2. Merge to `master` and wait for Jenkins (`continuous-integration/jenkins/branch`) to report `success` on the merge commit.
3. Tag the merge commit and push the tag: `git tag v<version> && git push origin v<version>`.

`publish.yml` then runs `verify-gate` (the tag matches `^v\d+\.\d+\.\d+(-suffix)?$` and equals the csproj `<Version>`, and the Jenkins status on the tagged commit is `success`), `build-pack` (Release pack with `-p:CI=true` into `deploy/`, expects 1 `.nupkg` + 1 `.snupkg`), and `publish` (pushes the `.nupkg` and `.snupkg` as separate commands with `--skip-duplicate`).

Dry run: Actions → Publish → Run workflow → `dry_run=true`. This runs `verify-gate`, `build-pack` and `verify-trusted-publishing` (proves the OIDC exchange) and pushes nothing. Running it with `dry_run=false` publishes whatever version the selected branch's csproj holds, without the tag checks.

All actions in the workflows are pinned to commit SHAs; keep them pinned when updating.

## Architecture

All source is in `Source/` under the single namespace `DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler`.

### Key Classes

- **`TaskSchedulerMultiple`** — The replacement scheduler. Extends `SmartThreadPoolTaskScheduler` (from DotNetWorkQueue) and overrides `HaveRoomForTask`, `CurrentTaskCount`, `IncrementCounter`, and `DecrementCounter` to factor in remote node counts via `ITaskSchedulerJobCountSync`.

- **`TaskSchedulerJobCountSync`** — Tracks local task count (`Interlocked` counter) plus remote counts (keyed by port in a `ConcurrentDictionary`). On every local increment/decrement, publishes the new count to all peers over the bus. Fires `RemoteCountChanged` when a peer's count changes, which triggers the scheduler to re-evaluate whether it can accept more tasks.

- **`TaskSchedulerBus`** — NetMQ-based P2P messaging layer. Uses `NetMQBeacon` for UDP peer discovery on a configurable broadcast port, `PublisherSocket`/`SubscriberSocket` for data exchange, and `NetMQActor` for thread-safe command dispatch. Dead nodes are pruned after 10 seconds without a beacon heartbeat.

- **`TaskSchedulerSetup`** — Extension method `InjectDistributedTaskScheduler(IContainer, int)` that registers all components into DotNetWorkQueue's SimpleInjector container, replacing the default `ATaskScheduler`.

- **`TaskSchedulerBusCommands`** — Enum defining the NetMQ message protocol (Publish, BroadCast, GetHostAddress, AddedNode, RemovedNode, SetCount) with frame layout documented in XML comments.

- **`TaskSchedulerMultipleConfiguration`** — Holds the UDP broadcast port. Only configuration needed beyond standard DotNetWorkQueue settings.

### Communication Flow

1. On `Start()`, `TaskSchedulerJobCountSync` creates a `TaskSchedulerBus` actor
2. The bus binds a subscriber on a random TCP port and advertises it via UDP beacon
3. When beacons from other nodes arrive, the bus connects its publisher to their subscriber ports
4. Task count changes are published as multi-frame NetMQ messages (SetCount command with port + count)
5. Each node maintains a dictionary of peer counts; `GetCurrentTaskCount()` returns local + sum of all peer counts

### Known Issues

- **Lock contention in `ProcessMessages`** in `TaskSchedulerJobCountSync` — `_lockSocket` is held during `TryReceiveFrameString` (10ms timeout) and contends with `IncreaseCurrentTaskCount`/`DecreaseCurrentTaskCount` on every task dispatch. Tracked for a future release as [issue #6](https://github.com/blehnen/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler/issues/6).

### Key Dependencies

- **DotNetWorkQueue** — provides `ATaskScheduler`, `SmartThreadPoolTaskScheduler`, `IContainer`, `ITaskSchedulerConfiguration`
- **NetMQ** (ZeroMQ for .NET) — P2P messaging and UDP beacon discovery
