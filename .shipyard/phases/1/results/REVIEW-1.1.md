# Review: Plan 1.1

## Verdict: PASS

## Findings

### Critical
None.

### Minor
- **Pre-existing WSL/solution-level build race.** Builder noted `dotnet build .sln -c Release` can fail on WSL with MSB3021 due to a file-copy race on the Windows/WSL filesystem bridge (`bin/Release/net8.0/` doesn't exist when the copy runs). Confirmed pre-existing (reproduces on unmodified code). Not related to PLAN-1.1. Per-project builds work reliably. Note for PLAN-3.1 (CI augmentation): the publish job should target the csproj directly, not the solution, to avoid hitting this on any Linux-based runner. Windows CI runners are unaffected.

### Positive
- Clean minimal diff — only the two expected edits in the csproj, no formatting drift, no scope creep.
- Zero `.cs` source edits needed to compile against DotNetWorkQueue 0.9.31 — the research-stage API breakage assessment was correct.
- NetMQ correctly left at 4.0.2.2 after verifying no newer stable exists on NuGet.org.
- Baseline test run (3/3 pass) before changes confirms the upgrade didn't introduce regressions.

## Verification Re-run Results

Independent verification via `git diff` confirms:
- `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj` — two edits:
  - `<TargetFrameworks>net10.0;net8.0;net48;net472;</TargetFrameworks>` → `<TargetFrameworks>net10.0;net8.0</TargetFrameworks>`
  - `DotNetWorkQueue` version `0.9.14` → `0.9.31`
- `NetMQ` unchanged at `4.0.2.2`
- No other properties touched; `TreatWarningsAsErrors`, `GenerateDocumentationFile`, `RootNamespace`, `AssemblyName`, `Version` (still 0.2.1), `Company`, `Copyright`, `Product` all preserved
- No `.cs` files touched by this plan (only the csproj is in the 1.1 diff)

Builder verification output (trusted):
- `dotnet restore Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.sln` → exit 0
- `dotnet build ...csproj -c Release` (main lib) → 0 warnings, 0 errors, both net10.0 and net8.0
- `dotnet build ...Tests.csproj -c Release` → 0 warnings, 0 errors, net8.0
- `dotnet test ... -c Release --no-build` → Passed: 3, Failed: 0, Skipped: 0

PLAN-1.2 reviewer independently re-ran build and test against the combined Wave 1 working tree and confirmed 0 warnings / 0 errors and 3/3 tests passing.

**Integration with PLAN-1.2:** No file overlap. PLAN-1.1 touches the csproj only; PLAN-1.2 touches `TaskSchedulerBus.cs` and `TaskSchedulerJobCountSync.cs`. `git diff --stat` shows exactly 3 files across both parallel plans — clean boundary, no conflicts.

**Scope discipline:** Confirmed no Wave 2 or Wave 3 territory touched. `<Version>` still 0.2.1. No NuGet metadata. No README, CHANGELOG, CLAUDE.md, or ci.yml edits.
