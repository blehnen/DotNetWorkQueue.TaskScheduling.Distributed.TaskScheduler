# STACK.md

## Overview

This is a small, single-library .NET project (version 0.2.1) that provides a distributed task scheduler add-on for DotNetWorkQueue. It is written entirely in C#, uses the SDK-style project format, and multi-targets four .NET versions simultaneously. The test project is a separate SDK-style project targeting net8.0 only. CI runs on GitHub Actions (Windows runner).

## Metrics

| Metric | Value |
|--------|-------|
| Production source files (non-generated, non-test) | 8 `.cs` files |
| Production source lines (non-generated, non-test) | ~700 lines |
| Total `.cs` files including tests and generated | 10 |
| Total lines across all `.cs` files (excluding obj/) | 1,184 |
| Library target frameworks | 4 (net10.0, net8.0, net48, net472) |
| Test target framework | 1 (net8.0) |
| Production NuGet dependencies | 2 |
| Test NuGet dependencies | 5 |
| Library version | 0.2.1 |

## Findings

### Language

- **C# only**: All source code is C#. No other languages are present.
  - Evidence: `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj` (line 1) — `<Project Sdk="Microsoft.NET.Sdk">`
  - Evidence: All 8 production `.cs` files under `Source/` (non-obj, non-Tests)

### Target Frameworks

- **Library multi-targets**: `net10.0`, `net8.0`, `net48`, `net472`
  - Evidence: `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj` (line 4) — `<TargetFrameworks>net10.0;net8.0;net48;net472;</TargetFrameworks>`
- **Test project single-targets**: `net8.0`
  - Evidence: `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.Tests/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.Tests.csproj` (line 4) — `<TargetFramework>net8.0</TargetFramework>`

### SDK and Build Tool

- **SDK-style project format**: Both `.csproj` files use `<Project Sdk="Microsoft.NET.Sdk">`.
  - Evidence: `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj` (line 1)
- **Build tool**: `dotnet` CLI / MSBuild. No custom build scripts detected.
  - Evidence: `.github/workflows/ci.yml` (lines 27–33) — uses `dotnet restore`, `dotnet build`, `dotnet test`
- **Solution file**: `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.sln`

### Build Settings

- **Warnings treated as errors**: `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`
  - Evidence: `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj` (line 8)
- **XML documentation generated**: `<GenerateDocumentationFile>true</GenerateDocumentationFile>`
  - Evidence: `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj` (line 7)
  - All public and internal types have XML doc comments throughout the source.
- **`IsPackable` disabled for tests**: `<IsPackable>false</IsPackable>`
  - Evidence: `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.Tests/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.Tests.csproj` (line 5)
- **InternalsVisibleTo**: Test assembly is granted access to internals (`TaskSchedulerBus`, `TaskSchedulerBusCommands`, `NodeKey` are all `internal`).
  - Evidence: `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj` (line 17)

### Package Manager

- **NuGet** via `dotnet restore`. All dependencies declared as `<PackageReference>` elements; no `packages.config` present.

### Runtime Requirements

- Minimum runtime: .NET Framework 4.7.2 (net472) or .NET 8.0, depending on target. net10.0 is the highest targeted TFM.
- No environment variable configuration mechanism is present in the library itself; the broadcast port and beacon interface are passed in as constructor arguments.
  - Evidence: `Source/TaskSchedulerMultipleConfiguration.cs` (lines 42–46)

### Production NuGet Dependencies

| Package | Version Constraint | Purpose |
|---------|-------------------|---------|
| `DotNetWorkQueue` | `0.9.14` (exact) | Base scheduler framework — provides `ATaskScheduler`, `SmartThreadPoolTaskScheduler`, `IContainer`, `ITaskSchedulerConfiguration`, `IMetrics`, `IWaitForEventOrCancelThreadPool`, logging abstractions |
| `NetMQ` | `4.0.2.2` (exact) | ZeroMQ for .NET — provides P2P messaging, UDP beacon discovery, publisher/subscriber sockets |

Evidence: `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj` (lines 21–23)

### Test NuGet Dependencies

| Package | Version Constraint | Purpose |
|---------|-------------------|---------|
| `Microsoft.NET.Test.Sdk` | `17.*` | Test host/runner |
| `xunit` | `2.*` | Test framework |
| `xunit.runner.visualstudio` | `2.*` | VS test runner adapter |
| `NSubstitute` | `5.*` | Mocking library |
| `coverlet.collector` | `6.*` | Code coverage collection |

Evidence: `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.Tests/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.Tests.csproj` (lines 8–17)

### Test Framework and Runner

- **xUnit v2** with `Microsoft.NET.Test.Sdk` as the runner host.
- Tests target **net8.0** only.
- Coverage collected via **coverlet** (data collector mode); no coverage threshold or report configuration detected.
  - [Inferred] Coverage reports are not published in CI — no `--collect` or `reportgenerator` step in `.github/workflows/ci.yml`.
- Tests involve real UDP loopback networking (not fully mocked), so they take approximately 19 seconds.
  - Evidence: `CLAUDE.md` — "Tests involve real UDP beacon discovery on loopback, so they take ~19 seconds."

### CI System

- **GitHub Actions** (not AppVeyor as noted in CLAUDE.md — the appveyor reference in CLAUDE.md appears outdated).
  - Evidence: `.github/workflows/ci.yml` — workflow named `CI`, triggers on push/PR to `master`.
- Runs on **`windows-latest`** runner.
  - Evidence: `.github/workflows/ci.yml` (line 11)
- Installs both .NET **8.0.x** and **10.0.x** SDKs.
  - Evidence: `.github/workflows/ci.yml` (lines 19–21)
- CI steps: `checkout` → `Setup .NET` → `dotnet --version` → `dotnet restore` → `dotnet build -c Debug` → `dotnet test`
- No publish, pack, or NuGet push steps in CI.

### License

- **LGPLv2.1**. Every source file contains the standard LGPL license header block.
  - Evidence: `Source/TaskSchedulerBus.cs` (lines 1–18), and all other `.cs` files

## Summary Table

| Item | Detail | Confidence |
|------|--------|------------|
| Language | C# | Observed |
| SDK format | Microsoft.NET.Sdk (SDK-style) | Observed |
| Library TFMs | net10.0, net8.0, net48, net472 | Observed |
| Test TFM | net8.0 | Observed |
| Library version | 0.2.1 | Observed |
| Build tool | dotnet CLI / MSBuild | Observed |
| TreatWarningsAsErrors | true | Observed |
| XML docs generated | true | Observed |
| DotNetWorkQueue version | 0.9.14 | Observed |
| NetMQ version | 4.0.2.2 | Observed |
| Test framework | xUnit 2.x | Observed |
| Mocking library | NSubstitute 5.x | Observed |
| Coverage tool | coverlet 6.x | Observed |
| CI system | GitHub Actions (windows-latest) | Observed |
| CI publishes NuGet package | No | Observed |
| AppVeyor CI | Not present (CLAUDE.md outdated) | Observed |
| License | LGPLv2.1 | Observed |

## Open Questions

- No NuGet publish step exists in CI. It is unclear whether releases are published to NuGet.org manually or not at all. No `.nuspec` file was found. Whether the library is publicly listed on NuGet.org cannot be determined from the repository alone.
- The `CLAUDE.md` references AppVeyor CI, but only a GitHub Actions workflow exists. It is unclear whether AppVeyor was removed at some point or was never actually configured.
- Version `0.2.1` is set directly in the `.csproj`; there is no `Directory.Build.props` or centralized versioning file. Version bumps require manual edits to the `.csproj`.
