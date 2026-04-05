# Changelog

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
