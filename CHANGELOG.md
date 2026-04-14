# Changelog

### 0.4.0 2026-04-14

* Fix: rewrite `TaskSchedulerJobCountSync` message loop to eliminate the lock-contention deadlock between `IncreaseCurrentTaskCount` / `DecreaseCurrentTaskCount` and the legacy `ProcessMessages` loop. The old `_lockSocket` + polling pattern has been replaced with a `NetMQPoller` driving the existing `NetMQActor` plus a new `NetMQQueue<SetCountMsg>` for outbound counter updates; all socket I/O now runs on a dedicated background thread (`TaskSchedulerJobCountSync.Poller`, `IsBackground = true`) owned exclusively by the poller. Closes [issue #6](https://github.com/blehnen/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler/issues/6).
* **Behavior change:** `TaskSchedulerJobCountSync.Start()` is now non-blocking. It still performs the host-address handshake, the ~1.1s beacon grace sleep, and the initial `BroadCast` synchronously on the caller thread, but socket-poll wiring (`ReceiveReady` handlers + `NetMQPoller` construction) is now spawned onto a dedicated background thread and `Start()` returns as soon as that thread is running. Callers that subclass or wrap `TaskSchedulerJobCountSync` should not rely on `Start()` blocking for the lifetime of the poller. The public interface signature on `ITaskSchedulerJobCountSync` is unchanged.
* `Dispose` now calls `_poller.Stop()`, joins the poller thread with a 5-second timeout (logging a warning on timeout), and disposes `_outbound`, `_actor`, and `_poller` in order. Existing socket-close error suppression (Win32 `10035` / `10054`) is preserved.
* Add unit and integration tests covering the new poller lifecycle, outbound queue draining, and shutdown timing.

### 0.3.0 2026-04-10

* **Breaking:** Drop `net48` and `net472` target frameworks; main library now targets `net10.0` and `net8.0` only (matches upstream DotNetWorkQueue 0.9.19+ which also dropped .NET Framework support)
* Bump `DotNetWorkQueue` from 0.9.14 to 0.9.31 (no API-breakage in the `ATaskScheduler`/`SmartThreadPoolTaskScheduler` surface we consume)
* Fix: replace `int.Parse` / `long.Parse` with `TryParse` guards in both `TaskSchedulerBus.OnBeaconReady` and `TaskSchedulerJobCountSync.ProcessMessages`; malformed UDP beacons and malformed `SetCount` frames are now silently dropped rather than crashing the NetMQ poller or throwing inside the message lock
* Fix: `TaskSchedulerJobCountSync._stopRequested` and `_running` fields are now `volatile`, ensuring correct cross-thread visibility of the stop signal on non-x86 hardware
* First public NuGet release as `DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler` on nuget.org
* Add NuGet packaging metadata, SourceLink (`Microsoft.SourceLink.GitHub` 10.0.201), deterministic builds, `.snupkg` symbol packages
* Add new README.md targeted at NuGet consumers, packed into the NuGet package
* Add `publish` job to GitHub Actions workflow, tag-triggered on `v*` tags, pushes `.nupkg` + `.snupkg` to nuget.org
* Deferred: lock contention in `TaskSchedulerJobCountSync.ProcessMessages` remains a known issue; see [issue #6](https://github.com/blehnen/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler/issues/6)

### 0.2.1 2026-04-05

* Add `BeaconInterface` to `TaskSchedulerMultipleConfiguration` and `InjectDistributedTaskScheduler` (default `"loopback"`). Pass `""` on Linux — NetMQ's `"loopback"` mode binds to 127.0.0.1 but sends to 255.255.255.255, which Linux won't loop back to a loopback-bound socket, so discovery silently fails
* Fall back to `127.0.0.1` in `GetHostAddress` when `NetMQBeacon.HostName` is empty (reverse DNS failure on CI/WSL crashed URI parsing)
* Bump DotNetWorkQueue from 0.9.0 to 0.9.14
* Add Jenkinsfile with XPlat code coverage and Codecov upload
* Add `coverlet.collector` to the test project
* Add `.gitattributes` to stop line-ending drift between Windows and WSL edits

### 0.2.0 2026-03-05

* Modernize to SDK-style csproj with PackageReference (removes packages.config, app.config, AssemblyInfo.cs)
* Multi-target net10.0, net8.0, net48, and net472
* Add integration tests (xUnit, net8.0) covering local counting, two-node sync, and three-node sync
* Add AppVeyor CI (appveyor.yml)

### 0.1.1 2019-06-02

* Update all references to current
* Remove usage of ILMerge

### 0.1.0 2015-10-23

* Initial release to GitHub
