# Plan 3.2: Version Bump, CHANGELOG, and CLAUDE.md Sync

## Context
Final housekeeping for 0.3.0: bump the csproj `<Version>` from `0.2.1` to `0.3.0`, add a `### 0.3.0 YYYY-MM-DD` entry to `CHANGELOG.md` covering all the changes landing in this release, and update `CLAUDE.md` so the Build/Tests/Known-Issues sections match the new reality (GitHub Actions instead of AppVeyor, `net10.0;net8.0` only, dropped fixed known issues, lock contention item references issue #6).

This plan is parallel-safe within Wave 3 with PLAN-3.1 because the csproj, CHANGELOG, and CLAUDE.md are all disjoint from `.github/workflows/ci.yml`.

**Git strategy is manual. Do NOT run `git commit`, `git push`, `git tag`, or any history-mutating command. Leave changes staged/unstaged for the user to review and commit.**

## Non-Goals
- No CI workflow edits (PLAN-3.1)
- No README edits (PLAN-2.2)
- No packaging metadata edits (PLAN-2.1)
- No source code changes
- Do NOT create the `v0.3.0` git tag — that is the user's action and also the publish trigger

## Dependencies
Wave 2 complete (PLAN-2.1 packaging metadata and PLAN-2.2 README in place). Parallel-safe with PLAN-3.1 in Wave 3.

## Tasks

### Task 1: Bump csproj version to 0.3.0
**Files:** `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj`
**Action:** modify
**Description:**
Change `<Version>0.2.1</Version>` to `<Version>0.3.0</Version>`. Do not touch any other property. Do not touch `<Copyright>` unless the user has explicitly requested a year bump (not in scope). All other metadata added by PLAN-2.1 stays as-is.

**Acceptance Criteria:**
- `grep -c "<Version>0.3.0</Version>" Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj` returns `1`
- `grep -c "<Version>0.2.1</Version>" Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj` returns `0`
- `dotnet pack -c Release` produces a `.nupkg` whose filename contains `0.3.0`

### Task 2: Add 0.3.0 entry to CHANGELOG.md
**Files:** `CHANGELOG.md`
**Action:** modify (prepend entry)
**Description:**
Insert a new `### 0.3.0 YYYY-MM-DD` section above the existing `### 0.2.1 2026-04-05` entry, matching the flat `*` bullet format already in use. Use today's date (see system context). The entry must cover:

- **Breaking:** Drop `net48` and `net472` target frameworks. Main library now targets `net10.0` and `net8.0` only (driven by upstream DotNetWorkQueue 0.9.19+ dropping net48/netstandard2.0).
- Bump `DotNetWorkQueue` from `0.9.14` to `0.9.31`.
- Bump `NetMQ` if the builder upgraded it in PLAN-1.1 (otherwise omit this bullet).
- **Fix (HIGH):** `TaskSchedulerBus.OnBeaconReady` now uses `int.TryParse`; malformed UDP beacon payloads are silently skipped instead of crashing the NetMQ poller.
- **Fix (HIGH):** `TaskSchedulerJobCountSync._stopRequested` and `_running` are now declared `volatile` to guarantee cross-thread visibility of the stop signal and dispose spin-wait.
- First public NuGet release. Adds full packaging metadata, deterministic builds, SourceLink via `Microsoft.SourceLink.GitHub` 10.0.201, `.snupkg` symbol packages, and a NuGet-focused README.
- Add tag-triggered `publish` job to GitHub Actions workflow (`ci.yml`).
- Known issue (deferred to 0.4.0): lock contention in `TaskSchedulerJobCountSync.ProcessMessages` — tracked as [issue #6](https://github.com/blehnen/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler/issues/6).

Use the exact same `* ` bullet style and no Markdown headings beyond `###`. Do not reword the existing 0.2.1 / 0.2.0 entries.

**Acceptance Criteria:**
- `grep -c "^### 0.3.0" CHANGELOG.md` returns `1`
- `grep -c "^### 0.2.1 2026-04-05" CHANGELOG.md` returns `1` (existing entry untouched)
- `grep -c "int.TryParse" CHANGELOG.md` returns at least `1`
- `grep -c "volatile" CHANGELOG.md` returns at least `1`
- `grep -c "0.9.31" CHANGELOG.md` returns at least `1`
- `grep -c "issues/6" CHANGELOG.md` returns at least `1`
- `grep -c "LGPL\|NuGet\|nuget" CHANGELOG.md` — the 0.3.0 entry section mentions first NuGet release
- The new entry appears above the 0.2.1 entry in file order

### Task 3: Update CLAUDE.md to reflect the new reality
**Files:** `CLAUDE.md`
**Action:** modify
**Description:**
Make these surgical updates to `CLAUDE.md`:

1. **Build section:** Change the TFM list from `net10.0`, `net8.0`, `net48`, and `net472` to `net10.0` and `net8.0`. Remove any sentence that calls out `net48`/`net472`.
2. **Tests section:** Replace the "CI runs on AppVeyor" sentence with "CI runs on GitHub Actions (`.github/workflows/ci.yml`), single job on `windows-latest`."
3. **Known Issues section:** Remove the two items that are now fixed:
   - Remove the `_stopRequested`/`_running` volatile bullet.
   - Remove the `int.Parse` in `OnBeaconReady` bullet.
   Leave the lock contention bullet in place, and append `— tracked as issue #6, deferred to 0.4.0` to that bullet.
4. Do NOT touch the Project Overview, Architecture, Key Classes, Communication Flow, or Key Dependencies sections except where they contain AppVeyor or `net48`/`net472` references.

**Acceptance Criteria:**
- `grep -c "AppVeyor" CLAUDE.md` returns `0`
- `grep -c "GitHub Actions" CLAUDE.md` returns at least `1`
- `grep -c "net48\|net472" CLAUDE.md` returns `0`
- `grep -c "_stopRequested" CLAUDE.md` returns `0` (fix landed, known issue removed)
- `grep -c "int.Parse" CLAUDE.md` returns `0` (fix landed, known issue removed)
- `grep -c "Lock contention" CLAUDE.md` returns at least `1`
- `grep -c "issue #6\|issues/6" CLAUDE.md` returns at least `1`
- `grep -c "net10.0" CLAUDE.md` returns at least `1`
- `grep -c "net8.0" CLAUDE.md` returns at least `1`

## Verification
```bash
cd /mnt/f/git/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler
grep "<Version>" Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj
head -20 CHANGELOG.md
grep -c "AppVeyor" CLAUDE.md       # must be 0
grep -c "GitHub Actions" CLAUDE.md  # must be >= 1
grep -c "net48\|net472" CLAUDE.md   # must be 0
grep -c "issue" CLAUDE.md           # must reference issue #6
dotnet restore Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.sln
dotnet build Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.sln -c Release
dotnet pack Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj -c Release --no-build
ls Source/bin/Release/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.0.3.0.nupkg
ls Source/bin/Release/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.0.3.0.snupkg
dotnet test Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.Tests/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.Tests.csproj -c Release --no-build
```
Version must read `0.3.0`. Build must produce zero warnings. Pack must produce `0.3.0`-versioned `.nupkg` and `.snupkg`. All 3 integration tests must pass.
