# Plan 1.1: Dependency Upgrade and TFM Drop

## Context
Upgrade `DotNetWorkQueue` from `0.9.14` to `0.9.31` (latest stable), optionally bump `NetMQ` if a newer stable exists, and drop `net48`/`net472` from `<TargetFrameworks>` on the main library csproj. DotNetWorkQueue 0.9.19 dropped `net48`/`netstandard2.0` upstream, so this TFM drop is mandatory once we reference 0.9.31. RESEARCH.md confirms none of the breaking changes in 0.9.19/0.9.30/0.9.31 touch the 10 API points this library consumes — expected outcome is a mechanical version bump with zero or near-zero source edits. This plan is the foundation for Wave 2 packaging, which modifies the same csproj.

**Git strategy is manual. Do NOT run `git commit`, `git push`, `git tag`, or any history-mutating command. Leave changes staged/unstaged for the user to review and commit.**

## Non-Goals
- No packaging metadata additions (deferred to PLAN-2.1)
- No stability fixes (parallel PLAN-1.2)
- No CHANGELOG / CLAUDE.md edits (deferred to PLAN-3.2)
- No unit test additions, no NSubstitute cleanup, no behavioral changes
- Do NOT touch the tests csproj target framework — it stays `net8.0` only per CONTEXT-1.md

## Dependencies
None — Wave 1.

## Tasks

### Task 1: Drop net48/net472 and bump DotNetWorkQueue
**Files:** `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj`
**Action:** modify
**Description:**
In the main library csproj:
1. Change `<TargetFrameworks>net10.0;net8.0;net48;net472;</TargetFrameworks>` to `<TargetFrameworks>net10.0;net8.0</TargetFrameworks>` (no trailing semicolon).
2. Update `<PackageReference Include="DotNetWorkQueue" Version="0.9.14" />` to `Version="0.9.31"`.
3. Do NOT touch `<Version>0.2.1</Version>` in this plan (PLAN-3.2 bumps it to 0.3.0).
4. Do NOT add any new PackageReference or metadata elements — PLAN-2.1 owns that.

**Acceptance Criteria:**
- `grep -c "net48" Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj` returns `0`
- `grep -c "net472" Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj` returns `0`
- `grep "TargetFrameworks" Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj` shows exactly `net10.0;net8.0`
- `grep "DotNetWorkQueue\" Version" Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj` shows `Version="0.9.31"`

### Task 2: Verify NetMQ and bump only if newer stable exists
**Files:** `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj`
**Action:** modify (conditional)
**Description:**
Run `dotnet restore Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.sln` then `dotnet list Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj package --outdated`. If a newer stable `NetMQ` version is reported (non-prerelease), update the `<PackageReference Include="NetMQ" Version="4.0.2.2" />` line to that version. If no newer stable is reported, leave the line unchanged. Do not upgrade to prerelease versions.

**Acceptance Criteria:**
- `dotnet list package --outdated` output (or equivalent) has been captured and inspected
- NetMQ reference in csproj is either `4.0.2.2` (unchanged) or the latest stable non-prerelease version
- No prerelease version strings (`-preview`, `-rc`, `-beta`) appear in the NetMQ version

### Task 3: Full solution rebuild and test run under upgraded dependencies
**Files:** no file edits; verification only — but fix any compile errors surfaced in `TaskSchedulerMultiple.cs`, `TaskSchedulerSetup.cs`, or `TaskSchedulerJobCountSync.cs` as needed
**Action:** verify + conditional fix
**Description:**
Run a clean build and test cycle against the upgraded DotNetWorkQueue:
1. `dotnet restore Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.sln`
2. `dotnet build Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.sln -c Release`
3. `dotnet test Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.Tests/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.Tests.csproj -c Release --no-build`

If the build fails due to DotNetWorkQueue API breakage on any of the 10 touch-points listed in RESEARCH.md (`SmartThreadPoolTaskScheduler`, `ATaskScheduler`, `IContainer`, `ITaskSchedulerConfiguration`, `IWaitForEventOrCancelThreadPool`, `IMetrics`, `LifeStyles`, `Guard`, `SetWaitHandle`, `MaximumConcurrencyLevel`), apply minimal edits to restore compilation. Do NOT refactor, rename, or change behavior beyond what is strictly required to compile. Do NOT touch `TaskSchedulerBus.cs` OnBeaconReady or `TaskSchedulerJobCountSync.cs` `_stopRequested`/`_running` fields — those belong to PLAN-1.2.

**Acceptance Criteria:**
- `dotnet build -c Release` completes with zero warnings and zero errors on both `net10.0` and `net8.0`
- All 3 integration tests pass
- `TreatWarningsAsErrors` is still enabled (unchanged)
- No files outside the main csproj and the three production source files named above were modified

## Verification
```bash
cd /mnt/f/git/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler
dotnet restore Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.sln
dotnet build Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.sln -c Release
dotnet test Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.Tests/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.Tests.csproj -c Release --no-build
grep "TargetFrameworks" Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj
grep "DotNetWorkQueue\" Version" Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj
```
All commands must exit 0. Build must produce zero warnings. Tests must report `Passed: 3`.
