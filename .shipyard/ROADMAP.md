# ROADMAP — DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler v0.3.0

## Overview

This is a deliberately small, single-release modernization project. The goal is to bring the library up to the current DotNetWorkQueue ecosystem, fix two known HIGH-severity stability bugs, and ship the first public NuGet package. PROJECT.md explicitly recommends a single phase. The four work areas (dependency upgrade, stability fixes, packaging, CI + housekeeping) are cohesive enough that splitting them adds ceremony without adding safety.

## Release Target

- **Version:** 0.3.0
- **Distribution:** First public release on nuget.org
- **Git strategy:** `manual` — builder agents must NOT create commits or tags. They prepare changes for the user to review and commit locally.

---

## Phase 1 — v0.3.0 Modernization & First NuGet Release

**Risk: medium** — The risk is concentrated in the DotNetWorkQueue dependency bump. Upstream may have breaking changes to `ATaskScheduler`, `SmartThreadPoolTaskScheduler`, `IContainer`, or `ITaskSchedulerConfiguration` that ripple through `TaskSchedulerMultiple`, `TaskSchedulerSetup`, and `TaskSchedulerJobCountSync`, and the ~19s real-UDP integration tests must still go green on `windows-latest` under the upgraded stack. Packaging and stability-fix work areas are low-risk but must land in the same release to keep a single shippable artifact.

**Complexity: M** — Mechanical across four narrow work areas. No new features, no refactors, no architectural change. Scope is bounded by PROJECT.md non-goals.

### Goal

Ship `DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler` 0.3.0 to nuget.org with:
- Latest stable DotNetWorkQueue (and NetMQ if newer stable exists)
- Target frameworks reduced to `net10.0;net8.0` (main lib) and `net8.0` (tests)
- Two HIGH-severity stability fixes applied
- Full NuGet packaging metadata, SourceLink, snupkg symbols, package README
- Tag-triggered GitHub Actions publish job augmenting the existing workflow
- CHANGELOG and CLAUDE.md brought in sync with the new reality

### Scope (in-scope work items)

1. **Dependency & TFM upgrade**
   - Bump `DotNetWorkQueue` PackageReference to latest stable on nuget.org
   - Bump `NetMQ` if a newer stable exists
   - Reduce main csproj `<TargetFrameworks>` from `net10.0;net8.0;net48;net472` to `net10.0;net8.0`
   - Tests csproj remains `net8.0` only
   - Fix any API breakage in `TaskSchedulerMultiple`, `TaskSchedulerSetup`, `TaskSchedulerJobCountSync`, and the 3 integration tests
   - All source compiles cleanly under `TreatWarningsAsErrors`

2. **HIGH-severity stability fixes**
   - `TaskSchedulerBus.OnBeaconReady`: replace `int.Parse` with `int.TryParse`; log-and-skip malformed beacons rather than crashing the poller
   - `TaskSchedulerJobCountSync`: add `volatile` to `_stopRequested` and `_running` field declarations

3. **NuGet packaging metadata**
   - Add PackageId, Version 0.3.0, Authors, Description, PackageLicenseExpression `LGPL-2.1-or-later`, PackageProjectUrl, RepositoryUrl, RepositoryType, PackageReadmeFile, NoWarn CS1591, Deterministic, ContinuousIntegrationBuild (CI-conditional), IncludeSymbols, SymbolPackageFormat snupkg, PublishRepositoryUrl, EmbedUntrackedSources
   - Add `Microsoft.SourceLink.GitHub` 8.0.0 as a PrivateAssets=All PackageReference
   - Add a `<None Include="..\..\README.md" Pack="true" PackagePath="\" />` item (exact relative path determined during planning)
   - Write new repo-root `README.md`: what the library is, when to use it, minimal `InjectDistributedTaskScheduler(container, port)` quick-start, UDP broadcast port convention, LGPL-2.1-or-later license note
   - Pattern is ported from `/mnt/f/git/expression-json-serializer` but license is LGPL-2.1-or-later (NOT MIT)

4. **CI augmentation & housekeeping**
   - Augment (do not replace) existing GitHub Actions workflow with a `publish` job:
     - Trigger: `refs/tags/v*`
     - `needs:` existing build-and-test job
     - Sets up .NET 8 and .NET 10 SDKs
     - `dotnet restore` + `dotnet pack -c Release --no-restore` against the main csproj
     - Pushes `*.nupkg` and `*.snupkg` to nuget.org using `${{ secrets.NUGET_API_KEY }}` with `--skip-duplicate`
   - Remove any `net48`/`net472` entries from the existing build matrix
   - `CHANGELOG.md` 0.3.0 entry: TFM drop (breaking), DotNetWorkQueue upgrade, 2 HIGH-severity fixes, first NuGet release, issue #6 deferred reference
   - `CLAUDE.md` updates: AppVeyor → GitHub Actions, target frameworks `net10.0;net8.0`, drop the two fixed known issues, keep lock contention item with issue #6 reference
   - Bump csproj `<Version>` 0.2.1 → 0.3.0

### Success Criteria (concrete, testable)

1. `dotnet build Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.sln -c Release` succeeds with **zero warnings** on a clean clone.
2. `dotnet test` passes all **3** integration tests on GitHub Actions `windows-latest` under the upgraded dependencies.
3. `dotnet pack -c Release` against the main csproj produces both `.nupkg` and `.snupkg` artifacts. Inspecting the `.nuspec` inside the `.nupkg` shows: PackageId `DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler`, Version `0.3.0`, `LGPL-2.1-or-later` license expression, repo URL, and the README.md file present at package root.
4. The repo-root `README.md` exists and renders cleanly on GitHub and as the NuGet package readme.
5. `Source/TaskSchedulerBus.cs` `OnBeaconReady` contains `int.TryParse` (not `int.Parse`); a malformed UDP payload can be fed in (manual or targeted) without crashing the poller.
6. `Source/TaskSchedulerJobCountSync.cs` field declarations for `_stopRequested` and `_running` both carry the `volatile` keyword.
7. Main csproj `<TargetFrameworks>` is exactly `net10.0;net8.0`. No `net48` or `net472` remain anywhere in csproj or workflow matrices.
8. The GitHub Actions workflow has a `publish` job guarded by `if: startsWith(github.ref, 'refs/tags/v')` and `needs:` the existing test job. The job references `secrets.NUGET_API_KEY` and pushes both `.nupkg` and `.snupkg` with `--skip-duplicate`.
9. `CHANGELOG.md` has a `0.3.0` entry covering TFM drop, DotNetWorkQueue upgrade, both HIGH fixes, first NuGet release, and the deferred-issue-#6 reference. `CLAUDE.md` Build section lists only `net10.0` and `net8.0`, references GitHub Actions (not AppVeyor), and the known-issues list drops the two fixed items while retaining the lock contention item pointing at issue #6.
10. csproj `<Version>` reads `0.3.0`.
11. SourceLink debugging works end-to-end: consumer can step into package sources from a debugger and reach the correct commit on GitHub. (Verified after publish.)
12. After the user tags `v0.3.0` and pushes, the publish job runs and the package appears on nuget.org. A separate consumer project can `dotnet add package DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler --version 0.3.0` and wire `.InjectDistributedTaskScheduler(container, port)` successfully.

### Dependencies

- **None** — this is the first and only phase. No prior roadmap phase is a prerequisite.

### Pre-Flight Gate (human action, not a plannable task)

- **`NUGET_API_KEY` must be added to the GitHub repository secrets** before the user tags `v0.3.0`. The publish job will fail without it. This is a one-time manual step performed by the repository owner via the GitHub UI; no builder agent can or should do it.

---

## Notes

### Deferred / Out of Scope

- **Issue #6 — Lock contention in `TaskSchedulerJobCountSync.ProcessMessages`** (MEDIUM severity in CONCERNS.md finding #3) is explicitly **deferred** from 0.3.0 and targeted for a follow-up release. The current release does not touch the `_lockSocket` + `TryReceiveFrameString` interaction. CHANGELOG and CLAUDE.md must reference this as a known, tracked, deferred concern.
- Adding unit tests (CONCERNS finding #7)
- Removing the unused `NSubstitute` dependency
- Restructuring integration tests to avoid the ~19s real-UDP run time
- A `netstandard2.0` bridge target (DotNetWorkQueue upstream forecloses this)
- Behavioral changes to the scheduler contract, bus message protocol, or beacon discovery semantics
- Security hardening of the unauthenticated P2P bus (CONCERNS finding #5)
- Non-atomic `ContainsKey`+assign on `_otherProcessorCounts` (CONCERNS finding #4)
- `CancellationToken` refactor of `Start()` (CONCERNS finding #11)
- Linux job on the GitHub Actions matrix (CONCERNS finding #8)
- Cleanup of 47 `MSBuildTemp*` directories (CONCERNS finding #9)
- Stale copyright years in source file headers (CONCERNS finding #10)
- Namespace or project-layout refactoring

### Git Strategy

`git_strategy` is **`manual`**. Builder agents executing plans under this roadmap must:
- Stage changes but **not** run `git commit`, `git push`, `git tag`, or any other history-mutating command
- Prepare a clear summary of changes for the user to review
- Leave the working tree in a state the user can inspect, amend, and commit manually
- Never create the `v0.3.0` release tag — that is the user's action and also the trigger for the publish job

### Why a single phase

PROJECT.md section "Process" explicitly directs: *"This is a single-phase project. `/shipyard:plan 1` should produce one plan covering all four work areas"*. The four work areas share a single release artifact and a single success criterion (package on nuget.org), so there is no natural dependency seam worth splitting into two roadmap phases. Internal sequencing (upgrade+fixes before packaging+publish) is handled at the plan/wave level inside Phase 1, not at the roadmap level.
