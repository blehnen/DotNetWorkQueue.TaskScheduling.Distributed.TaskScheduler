# Plan 1.2: HIGH-Severity Stability Fixes

## Context
Two HIGH-severity stability bugs must be fixed before the first public NuGet release:
1. `TaskSchedulerBus.OnBeaconReady` uses `int.Parse` on a UDP-received string; a malformed beacon payload crashes the NetMQ poller and silently kills the bus actor.
2. `TaskSchedulerJobCountSync._stopRequested` and `_running` are read/written across multiple threads without memory barriers — technically a data race that happens to work on x86.

Both fixes are mechanical one-liners with exact file:line targets identified in RESEARCH.md. This plan is parallel-safe with PLAN-1.1 because it touches entirely different files (`TaskSchedulerBus.cs` and `TaskSchedulerJobCountSync.cs`, never the csproj).

**Git strategy is manual. Do NOT run `git commit`, `git push`, `git tag`, or any history-mutating command. Leave changes staged/unstaged for the user to review and commit.**

## Non-Goals
- No lock contention fix (issue #6 — deferred to 0.4.0)
- No dependency bumps (PLAN-1.1 owns that)
- No packaging metadata (PLAN-2.1)
- No behavioral changes beyond converting a crash path to a silent skip
- No unit test additions
- Do NOT touch the csproj file in this plan

## Dependencies
None — Wave 1. Parallel-safe with PLAN-1.1 (disjoint files).

## Tasks

### Task 1: Replace int.Parse with int.TryParse in OnBeaconReady
**Files:** `Source/TaskSchedulerBus.cs`
**Action:** modify
**Description:**
In `TaskSchedulerBus.cs`, method `OnBeaconReady` (declared around line 171), locate the line (around line 176):

```csharp
var port = int.Parse(message.String);
```

Replace it with a `TryParse` guard that silently skips malformed beacon payloads rather than throwing:

```csharp
if (!int.TryParse(message.String, out var port))
{
    return;
}
```

Preserve the LGPLv2.1 license header and existing XML doc comments. Do not change the method signature, do not add logging (no logger is injected at this level), do not modify any other line in the method or file.

**Acceptance Criteria:**
- `grep -n "int.Parse(message.String)" Source/TaskSchedulerBus.cs` returns no matches
- `grep -n "int.TryParse(message.String" Source/TaskSchedulerBus.cs` returns exactly one match inside `OnBeaconReady`
- The method still compiles with `port` in scope for the subsequent `NodeKey` construction
- `dotnet build -c Release` produces zero warnings

### Task 2: Add volatile to _stopRequested and _running
**Files:** `Source/TaskSchedulerJobCountSync.cs`
**Action:** modify
**Description:**
In `TaskSchedulerJobCountSync.cs`, around lines 36–37, locate the two field declarations:

```csharp
private bool _stopRequested;
private bool _running;
```

Add the `volatile` keyword to both:

```csharp
private volatile bool _stopRequested;
private volatile bool _running;
```

Do not reorder the fields, do not change their visibility, do not touch any other declaration. Leave comments and whitespace around them intact.

**Acceptance Criteria:**
- `grep -n "private volatile bool _stopRequested" Source/TaskSchedulerJobCountSync.cs` returns exactly one match
- `grep -n "private volatile bool _running" Source/TaskSchedulerJobCountSync.cs` returns exactly one match
- `grep -n "private bool _stopRequested" Source/TaskSchedulerJobCountSync.cs` returns no matches (non-volatile form gone)
- `grep -n "private bool _running" Source/TaskSchedulerJobCountSync.cs` returns no matches
- `dotnet build -c Release` produces zero warnings

### Task 3: Verify full build and integration tests remain green
**Files:** no file edits; verification only
**Action:** verify
**Description:**
Rebuild the solution and rerun the 3 integration tests to confirm both fixes compile cleanly and do not perturb observable behavior. Tests exercise the real UDP beacon path, so the `TryParse` change is implicitly verified by the existing 2-node and 3-node sync tests still passing.

**Acceptance Criteria:**
- `dotnet build Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.sln -c Release` succeeds with zero warnings
- `dotnet test` reports all 3 integration tests passing
- No files other than `TaskSchedulerBus.cs` and `TaskSchedulerJobCountSync.cs` were modified by this plan

## Verification
```bash
cd /mnt/f/git/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler
grep -n "int.TryParse(message.String" Source/TaskSchedulerBus.cs
grep -n "private volatile bool _stopRequested" Source/TaskSchedulerJobCountSync.cs
grep -n "private volatile bool _running" Source/TaskSchedulerJobCountSync.cs
dotnet build Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.sln -c Release
dotnet test Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.Tests/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.Tests.csproj -c Release --no-build
```
All greps must return exactly one match each. Build must produce zero warnings. Tests must report `Passed: 3`.
