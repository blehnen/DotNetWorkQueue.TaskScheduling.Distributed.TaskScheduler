# PROJECT — DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler

## Project Name

DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler — v0.3.0 Modernization & First NuGet Release

## Description

This release modernizes the distributed task scheduler library to match the current DotNetWorkQueue ecosystem and ships it as a public NuGet package for the first time. The library's core responsibility — coordinating worker thread pool counts across multiple processes on a machine via a NetMQ P2P bus — is unchanged. What changes is: the library runs on the latest DotNetWorkQueue, drops legacy .NET Framework targets, fixes two known HIGH-severity crash/stability bugs, and becomes installable from `nuget.org`.

Packaging and CI conventions are ported verbatim from the `expression-json-serializer` project: deterministic builds, SourceLink, `.snupkg` symbols, tag-triggered GitHub Actions publish.

## Goals

1. Upgrade `DotNetWorkQueue` from `0.9.14` to the latest stable version on NuGet.org, fixing any API breakage in `TaskSchedulerMultiple`, `TaskSchedulerSetup`, and `TaskSchedulerJobCountSync`.
2. Drop `net48` and `net472` target frameworks. Main library targets `net10.0;net8.0` only (matching upstream DotNetWorkQueue, which also only supports these).
3. Publish the library as `DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler` to `nuget.org`, versioned `0.3.0`, using the `expression-json-serializer` packaging pattern.
4. Fix two HIGH-severity stability bugs identified by codebase analysis before first public release.

## Non-Goals

- **Lock contention rework** in `TaskSchedulerJobCountSync.ProcessMessages` — deferred to [issue #6](https://github.com/blehnen/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler/issues/6), targeted for 0.4.0.
- Adding unit tests or removing the unused `NSubstitute` dependency.
- Restructuring the test suite to avoid real-UDP integration tests (~19s run time preserved).
- Keeping a `netstandard2.0` bridge target for older consumers.
- Behavioral changes to the scheduler contract, bus message protocol, or beacon discovery semantics.
- Restructuring the project layout or namespace.
- Repo-wide refactors beyond what is strictly required for the DotNetWorkQueue upgrade and packaging.

## Requirements

### Functional — Dependency & TFM Upgrade

- Main csproj `<TargetFrameworks>` becomes `net10.0;net8.0`. The `net48` and `net472` entries are removed.
- `DotNetWorkQueue` PackageReference is updated to the latest stable version available on NuGet.org at the time of planning. `NetMQ` is updated if a newer stable is available.
- All source files in `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler/` compile cleanly against the upgraded dependencies under `TreatWarningsAsErrors`.
- The 3 existing integration tests in `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.Tests/` are updated as needed to match any DotNetWorkQueue API changes and pass on GitHub Actions `windows-latest`.
- Tests continue to target `net8.0` only.

### Functional — Stability Fixes

- `TaskSchedulerBus.OnBeaconReady` uses `int.TryParse` rather than `int.Parse`. Malformed beacon payloads are silently skipped and do not crash the poller or terminate the bus actor.
- `TaskSchedulerJobCountSync._stopRequested` and `_running` fields are declared `volatile` to guarantee cross-thread visibility of the stop signal and the dispose spin-wait loop.

### Functional — NuGet Packaging

The main library csproj adds the following metadata (ported from `expression-json-serializer`, with LGPL replacing MIT):

- `<PackageId>DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler</PackageId>`
- `<Version>0.3.0</Version>`
- `<Authors>Brian Lehnen</Authors>`
- `<Description>` — one-paragraph summary describing the distributed task scheduler, its DotNetWorkQueue integration, and the NetMQ-based cross-process coordination.
- `<PackageLicenseExpression>LGPL-2.1-or-later</PackageLicenseExpression>`
- `<PackageProjectUrl>` and `<RepositoryUrl>` pointing at the GitHub repo; `<RepositoryType>git</RepositoryType>`
- `<PackageReadmeFile>README.md</PackageReadmeFile>` plus a `<None Include="..\..\README.md" Pack="true" PackagePath="\" />` item (exact relative path determined by repo layout).
- `<GenerateDocumentationFile>true</GenerateDocumentationFile>` (already present — keep)
- `<NoWarn>CS1591</NoWarn>` added to suppress missing-doc warnings on non-public API while still enforcing docs on public API
- `<Deterministic>true</Deterministic>`
- `<ContinuousIntegrationBuild Condition="'$(CI)' == 'true'">true</ContinuousIntegrationBuild>`
- `<IncludeSymbols>true</IncludeSymbols>`, `<SymbolPackageFormat>snupkg</SymbolPackageFormat>`
- `<PublishRepositoryUrl>true</PublishRepositoryUrl>`, `<EmbedUntrackedSources>true</EmbedUntrackedSources>`
- Add `<PackageReference Include="Microsoft.SourceLink.GitHub" Version="8.0.0" PrivateAssets="All" />`

A new `README.md` is written at the repo root, suitable for inclusion in the NuGet package. It covers: what the library is, when to use it, a minimal quick-start showing `InjectDistributedTaskScheduler(container, port)` wired into a DotNetWorkQueue consumer, the UDP broadcast port convention, and the license.

### Functional — CI Augmentation

- The existing GitHub Actions CI workflow is **augmented** (not replaced) with a `publish` job.
- The publish job:
  - Runs only on pushes matching `refs/tags/v*`
  - Depends on (`needs:`) the existing build-and-test job(s) succeeding
  - Sets up .NET 8 and .NET 10 SDKs
  - Runs `dotnet restore` on the solution
  - Runs `dotnet pack Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj -c Release --no-restore` (path verified during planning)
  - Pushes both `*.nupkg` and `*.snupkg` to `https://api.nuget.org/v3/index.json` using `${{ secrets.NUGET_API_KEY }}` with `--skip-duplicate`
- The existing build-and-test matrix is updated to remove any `net48`/`net472` entries that no longer apply.
- The `NUGET_API_KEY` secret must be added to the GitHub repo secrets before tagging `v0.3.0`. This is a pre-flight step, not a code change.

### Functional — Housekeeping

- `CHANGELOG.md` gets a `0.3.0` entry documenting: TFM drop (breaking), DotNetWorkQueue upgrade, two HIGH-severity fixes, first NuGet release, reference to issue #6 as deferred.
- `CLAUDE.md` is updated: the stale AppVeyor reference becomes GitHub Actions; target frameworks in the "Build" section become `net10.0`, `net8.0`; known-issues list drops the two fixed items and leaves the lock contention item with a reference to issue #6.
- The `<Version>` in csproj is bumped from `0.2.1` to `0.3.0`.

## Non-Functional Requirements

- **Build discipline** — `TreatWarningsAsErrors` remains enabled. Zero warnings on both TFMs in Release.
- **Deterministic/reproducible builds** — CI builds are deterministic and include source link metadata. Consumers can step into package sources from a debugger.
- **Package correctness** — a fresh `dotnet pack -c Release` produces a valid `.nupkg` that passes `dotnet nuget verify`-style checks: has readme, license, icon (optional), repo URL, and symbols in a separate `.snupkg`.
- **License accuracy** — `PackageLicenseExpression` is `LGPL-2.1-or-later`, matching the LGPLv2.1 headers in every source file. Not MIT.
- **No behavioral drift** — the scheduler's observable behavior (count synchronization, peer discovery, dispose semantics) is unchanged except where the two stability fixes explicitly change crash/hang behavior to graceful handling.
- **Test stability** — the 3 integration tests must remain green on GitHub Actions `windows-latest` after the upgrade. Flakiness introduced by the dependency bump is treated as a bug to fix, not tolerate.

## Success Criteria

1. `dotnet build Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.sln -c Release` succeeds with zero warnings on a clean clone.
2. `dotnet test` passes all 3 integration tests on GitHub Actions `windows-latest`.
3. `dotnet pack -c Release` produces both `.nupkg` and `.snupkg` artifacts with the correct metadata (verified by inspection of the `.nuspec` inside the package).
4. Tagging a commit as `v0.3.0` triggers the GitHub Actions publish job and the package appears on nuget.org under the ID `DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler`.
5. A separate consumer project can `dotnet add package DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler --version 0.3.0`, call `.InjectDistributedTaskScheduler(container, port)`, and run successfully.
6. SourceLink debugging works: stepping into the package from a consumer reaches the correct source on GitHub.
7. Feeding a malformed UDP payload to the beacon port no longer crashes the bus poller (manual verification or a targeted test).
8. Issue #6 remains open as the tracked next-release concern, with PROJECT.md and CHANGELOG referencing it as deferred.

## Constraints

### Technical

- **Upstream coupling** — DotNetWorkQueue itself targets only `net10.0;net8.0` now; this forces the TFM drop and forecloses any `netstandard2.0` bridge.
- **Integration-test model** — the existing tests use real UDP loopback and take ~19 seconds. We are not changing that model in this release; the tests must continue to work under the upgraded dependencies.
- **TreatWarningsAsErrors** — any warning introduced by the dependency upgrade is a blocker and must be fixed, not suppressed (except via the existing `<NoWarn>CS1591</NoWarn>` for missing XML docs on non-public API).
- **LGPLv2.1 license** — source headers, repo LICENSE, and NuGet `PackageLicenseExpression` must all agree. The expression-json-serializer pattern uses MIT; we must diverge here.
- **Manual git strategy** — Shipyard config is set to `git_strategy: manual`. All commits and the release tag are created by the user, not automatically by Shipyard agents.

### Timeline / Budget

- No hard external deadline. Scope is deliberately minimal to make this a single short phase. HIGH-severity fixes are included precisely because they are small and mechanical; anything larger is deferred.

### Process

- Interactive mode with detailed review gates. Security audit, simplification review, and docs generation all run after the phase build.
- This is a single-phase project. `/shipyard:plan 1` should produce one plan covering all four work areas (dependency upgrade, stability fixes, packaging, CI + housekeeping), not split them further unless the architect determines otherwise.

## Tracked Related Work

- **Issue #6** — Reduce lock contention in `TaskSchedulerJobCountSync.ProcessMessages`. Deferred from 0.3.0, targeted for a follow-up release.
