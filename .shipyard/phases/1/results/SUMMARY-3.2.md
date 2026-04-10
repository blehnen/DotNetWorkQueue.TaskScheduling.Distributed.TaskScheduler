# SUMMARY-3.2: Version Bump 0.2.1 -> 0.3.0, CHANGELOG Entry, CLAUDE.md Sync

**Plan:** PLAN-3.2
**Date:** 2026-04-10
**Status:** All tasks complete. No deviations.
**Git strategy:** Manual -- no commits made.

---

## Task 1 -- Bump csproj version

**File:** `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj`

Changed `<Version>0.2.1</Version>` to `<Version>0.3.0</Version>`. No other properties touched.

Verification:
- grep -c '<Version>0.3.0</Version>' csproj -> 1 (pass)
- grep -c '<Version>0.2.1</Version>' csproj -> 0 (pass)

---

## Task 2 -- CHANGELOG.md entry

**File:** `CHANGELOG.md`

Prepended above the existing 0.2.1 entry:

### 0.3.0 2026-04-10

* **Breaking:** Drop net48 and net472 target frameworks; main library now targets net10.0 and net8.0 only
* Bump DotNetWorkQueue from 0.9.14 to 0.9.31
* Fix: TaskSchedulerBus.OnBeaconReady now uses int.TryParse instead of int.Parse
* Fix: TaskSchedulerJobCountSync._stopRequested and _running fields are now volatile
* First public NuGet release as DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler on nuget.org
* Add NuGet packaging metadata, SourceLink (Microsoft.SourceLink.GitHub 10.0.201), deterministic builds, .snupkg symbol packages
* Add new README.md targeted at NuGet consumers, packed into the NuGet package
* Add publish job to GitHub Actions workflow, tag-triggered on v* tags
* Deferred: lock contention in TaskSchedulerJobCountSync.ProcessMessages; see issue #6

Verification:
- grep -c '### 0.3.0 2026-04-10' CHANGELOG.md -> 1 (pass)
- grep -c '### 0.2.1 2026-04-05' CHANGELOG.md -> 1 (pass, untouched)
- grep -c 'issue #6' CHANGELOG.md -> 1 (pass)

---

## Task 3 -- CLAUDE.md sync

**File:** `CLAUDE.md`

Change 1 (TFM list):
  Before: "SDK-style project multi-targeting net10.0, net8.0, net48, and net472."
  After:  "SDK-style project targeting net10.0 and net8.0."

Change 2 (CI reference):
  Before: "CI runs on AppVeyor."
  After:  "CI runs on GitHub Actions (.github/workflows/ci.yml)."

Change 3 (Known Issues -- removed 2 fixed items, rewrote 1):
  Removed: _stopRequested/_running not volatile bullet
  Removed: int.Parse in OnBeaconReady bullet
  Kept/reworded: Lock contention in ProcessMessages now references issue #6

Verification:
- grep -c -i 'appveyor' CLAUDE.md -> 0 (pass)
- grep -c 'GitHub Actions' CLAUDE.md -> 1 (pass)
- grep -c 'net48' CLAUDE.md -> 0 (pass)
- grep -c 'net472' CLAUDE.md -> 0 (pass)
- grep -c 'volatile' CLAUDE.md -> 0 (pass)
- grep -c 'int.Parse' CLAUDE.md -> 0 (pass)
- grep -c 'issue #6' CLAUDE.md -> 1 (pass)

---

## Build and Pack Verification

dotnet build -c Release: Build succeeded. 0 Warning(s). 0 Error(s).
dotnet pack -c Release --no-build:
  - Source/bin/Release/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.0.3.0.nupkg (created)
  - Source/bin/Release/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.0.3.0.snupkg (created)

---

## Deviations

None. All tasks executed exactly as specified in PLAN-3.2.
