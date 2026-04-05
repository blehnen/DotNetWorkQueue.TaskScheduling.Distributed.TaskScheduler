# Changelog

### 0.2.1 2026-04-05

* Fix: beacon interface is now configurable via `BeaconInterface` on `TaskSchedulerMultipleConfiguration` (also exposed via `InjectDistributedTaskScheduler`). Default `"loopback"` preserves prior Windows behavior; pass `""` on Linux where NetMQ's `"loopback"` mode does not deliver broadcasts back to sibling sockets
* Fix: guard against empty `NetMQBeacon.HostName` to avoid URI-parse crash when reverse DNS fails (common on CI hosts and WSL)
* Update DotNetWorkQueue reference from 0.9.0 to 0.9.14
* Add Jenkins pipeline (`Jenkinsfile`) with XPlat code coverage collection and Codecov upload
* Add `coverlet.collector` to test project
* Add `.gitattributes` to normalize line endings across Windows/Linux/macOS

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
