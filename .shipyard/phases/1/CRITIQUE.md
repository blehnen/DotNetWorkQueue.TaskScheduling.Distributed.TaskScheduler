## Revision 1 Re-Verification
**Date:** 2026-04-10
**Verifier:** build-verify (post-architect revision)

### Three Previously-Flagged Issues

**Issue H1 — Non-.Tests subdirectory path in plans**

Command: grep across all six plans for `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler/` (with trailing slash).
Result: zero matches found.
Verdict: RESOLVED. No plan contains the non-existent subdirectory path. All file references now use the flat layout (`Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj`, `Source/TaskSchedulerBus.cs`, `Source/TaskSchedulerJobCountSync.cs`, `Source/bin/Release/`).

**Issue H2 — README.md relative path in PLAN-2.1**

PLAN-2.1 Task 2 context paragraph (line 4): `"..\README.md" (the csproj sits directly in Source/, one level below the repo root)`.
PLAN-2.1 Task 2 XML snippet (line 73): `<None Include="..\README.md" Pack="true" PackagePath="\" />`.
PLAN-2.1 acceptance criterion (line 80): `grep -c "\\.\\.\\\\README.md"` (one backslash-dot pair = `..\`).
Verdict: RESOLVED. Path is `..\README.md` throughout — correct for a csproj one level inside the repo root.

**Issue H3 — PLAN-3.2 Verification ls glob**

PLAN-3.2 Verification section (lines 97-98):
```
ls Source/bin/Release/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.0.3.0.nupkg
ls Source/bin/Release/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.0.3.0.snupkg
```
Directory prefix is `Source/bin/Release/` (correct). File names are exact (no wildcard ambiguity). The original wrong pattern `Source/DotNetWorkQueue.../bin/Release/*.0.3.0*.nupkg` is gone.
Verdict: RESOLVED.

### File Existence Spot-Check

All seven files referenced by the corrected plans confirmed present via Glob:

| File | Exists |
|------|--------|
| `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj` | YES |
| `Source/TaskSchedulerBus.cs` | YES |
| `Source/TaskSchedulerJobCountSync.cs` | YES |
| `.github/workflows/ci.yml` | YES |
| `CHANGELOG.md` | YES |
| `CLAUDE.md` | YES |
| `README.md` | YES |

### PLAN-1.2 Line Number Stability Check

`int.Parse(message.String)` in `Source/TaskSchedulerBus.cs`: confirmed at **line 176** (inside `OnBeaconReady`, declared at line ~171). Matches PLAN-1.2 Task 1 claim ("around line 176"). PASS.

`private bool _stopRequested` in `Source/TaskSchedulerJobCountSync.cs`: confirmed at **line 36**. Matches PLAN-1.2 Task 2 claim ("around lines 36-37"). PASS.

`private bool _running` in `Source/TaskSchedulerJobCountSync.cs`: confirmed at **line 37**. Matches PLAN-1.2 Task 2 claim. PASS.

### Per-Plan Summary

| Plan | Previously-Flagged Path Error | Status |
|------|-------------------------------|--------|
| PLAN-1.1 | Wrong csproj path in all tasks and verification | RESOLVED |
| PLAN-1.2 | Wrong .cs paths in all tasks and verification | RESOLVED |
| PLAN-2.1 | Wrong csproj path + `..\..\README.md` in pack item | RESOLVED |
| PLAN-2.2 | No blocking issues (unchanged, still clean) | N/A |
| PLAN-3.1 | No blocking issues (unchanged, still clean) | N/A |
| PLAN-3.2 | Wrong csproj path + bad ls glob in verification | RESOLVED |

### Verdict: READY

All three previously-blocking issues are resolved. No new issues introduced. All referenced files exist. Line numbers for both stability-fix targets are stable and match plan claims. Plans are ready to build.

---

# Phase 1 Plan Critique
**Date:** 2026-04-10
**Type:** plan-review
**Plans reviewed:** PLAN-1.1, PLAN-1.2, PLAN-2.1, PLAN-2.2, PLAN-3.1, PLAN-3.2

---

## Summary

**Verdict: REVISE**

Every plan in the set contains incorrect file paths because the architect assumed a subdirectory layout
(`Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler/`) that does not exist. The actual
layout is flat: source files live directly in `Source/` and the csproj lives at
`Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj`. This single structural error
cascades into wrong file paths in all task descriptions and wrong `grep` acceptance-criterion commands
in PLAN-1.1, PLAN-1.2, PLAN-2.1, PLAN-3.2 (and partially PLAN-3.1). It also produces a wrong
`..\..\README.md` relative path in PLAN-2.1 — the csproj is only one directory level below repo root
inside `Source/`, so the correct relative path is `..\README.md`.

The coverage map, wave ordering, task counts, and within-wave file-conflict analysis are all sound.
The research-derived line numbers for the two stability fixes are confirmed correct. No coverage gaps
exist against the 12 success criteria.

The path errors are mechanical to fix but they are in every plan, so the plans must be revised before
a builder can execute them safely.

---

## Per-Plan Findings

### PLAN-1.1 — Dependency Upgrade and TFM Drop

**Blocking issue — wrong csproj path throughout.**

Every task description and acceptance-criterion command references:
```
Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj
```
The actual csproj path is:
```
Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj
```
Evidence: `glob **/*.csproj` returns only `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj`.

Impact: every `grep` acceptance criterion in Tasks 1 and 2 will silently fail with "no such file" rather
than testing the csproj. The Verification block at the bottom repeats the wrong path in five commands.

The solution path `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.sln` is used in Task 3
— correct (solution file exists there). Task 3's test project path also uses the long subdirectory form;
that is also wrong (actual: `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.Tests/...`
which does exist as a subdirectory of `Source/` — this one is fine, only the main csproj is affected).

**Non-blocking observations:**
- Task 3's `dotnet test` uses `--no-build` but runs against a csproj whose build was driven by the
  solution — fine in practice, but the test project path in Task 3 is correct so no path error there.
- Acceptance criterion for Task 2 says "capture output and inspect" — this is verifiable but not
  automatically assertable; the criterion is effectively MANUAL. Acceptable given the conditional nature
  of the NetMQ bump.

**Files touched by PLAN-1.1:**
- `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj` (modify)
- `Source/TaskSchedulerMultiple.cs`, `Source/TaskSchedulerSetup.cs`, `Source/TaskSchedulerJobCountSync.cs`
  (conditional fix if API breakage found)

---

### PLAN-1.2 — HIGH-Severity Stability Fixes

**Blocking issue — wrong source file paths throughout.**

Every task description and acceptance-criterion command references:
```
Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler/TaskSchedulerBus.cs
Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler/TaskSchedulerJobCountSync.cs
```
The actual paths are:
```
Source/TaskSchedulerBus.cs
Source/TaskSchedulerJobCountSync.cs
```
Evidence: `glob **/*.cs` returns `Source/TaskSchedulerBus.cs` and `Source/TaskSchedulerJobCountSync.cs`.

Impact: all six `grep -n` acceptance-criterion commands in Tasks 1 and 2 will silently fail with "no
such file". The Verification block repeats the same wrong paths.

**Line numbers are confirmed correct (non-blocking — research was accurate):**
- `int.Parse(message.String)` is at line 176 of `Source/TaskSchedulerBus.cs`. PASS.
- `private bool _stopRequested;` is at line 36, `private bool _running;` is at line 37 of
  `Source/TaskSchedulerJobCountSync.cs`. PASS.
- `OnBeaconReady` method declaration is at line 171. PASS.

**Non-blocking — fix shape is correct:** The proposed `int.TryParse` guard with early `return` is the
right approach. The `volatile` addition to both field declarations is correct. No design concerns.

**Files touched by PLAN-1.2:**
- `Source/TaskSchedulerBus.cs` (modify)
- `Source/TaskSchedulerJobCountSync.cs` (modify)

Disjoint from PLAN-1.1. Wave 1 conflict check: PASS.

---

### PLAN-2.1 — NuGet Packaging Metadata and SourceLink

**Blocking issue 1 — wrong csproj path throughout.**

Same structural error as PLAN-1.1. Every task and every acceptance-criterion command uses the
non-existent subdirectory path instead of `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj`.

**Blocking issue 2 — wrong README.md relative path.**

PLAN-2.1 Task 2 instructs the builder to add:
```xml
<None Include="..\..\README.md" Pack="true" PackagePath="\" />
```
The csproj is at `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj`.
- `..` from `Source/` = repo root.
- `../..` from `Source/` = the parent of the repo root (outside the repository).

The correct relative path is:
```xml
<None Include="..\README.md" Pack="true" PackagePath="\" />
```
because the csproj is one level inside the repo (`Source/`), not two levels deep.

RESEARCH.md states "the csproj lives at `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj`
— one level deep from `Source/`" and then correctly notes "one level from repo root" but then derives
`..\..\README.md` claiming the csproj is "2 directory levels below repo root." This is a reasoning
error: the csproj is only 1 directory level below repo root (inside `Source/`), so only one `..` is
needed. If `..\..\README.md` is written as-is, pack will fail with `NU5116` or `NU5100` because
`README.md` resolves outside the repo, and the file will not exist at that computed path.

**Non-blocking — SourceLink version is correct:** RESEARCH.md correctly identified 10.0.201 and the
plan uses it. PASS.

**Non-blocking — license expression is correct:** `LGPL-2.1-or-later`. PASS.

**Non-blocking — metadata list is complete:** All 15 required properties from PROJECT.md are present
in Task 1. PASS.

**Task 3 conditional pack verification is acceptable:** The plan correctly notes that if PLAN-2.2
README has not landed, `NU5039` is expected and the task still passes on csproj validity alone.

**Files touched by PLAN-2.1:**
- `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj` (modify)

Disjoint from PLAN-2.2 (`README.md`). Wave 2 conflict check: PASS.

---

### PLAN-2.2 — Repo-Root README for NuGet Consumers

**No blocking issues.**

File path: `README.md` at repo root — confirmed to exist (`glob README.md` returns a match). The plan
says to rewrite it; the current file is a candidate for reuse per RESEARCH.md. PASS.

**Non-blocking — acceptance-criterion line-count range is reasonable:** `wc -l` between 40 and 100.
The existing file is 83 lines; the new consumer-focused version should fit comfortably. PASS.

**Non-blocking — Task 2 smoke test depends on PLAN-2.1 being complete:** The plan handles this
correctly with the conditional "if PLAN-2.1 is complete" caveat. PASS.

**Testability:** All acceptance criteria use `grep -c`, `wc -l` — concrete and runnable. PASS.

**Files touched by PLAN-2.2:**
- `README.md` (rewrite)

Disjoint from PLAN-2.1 (csproj). Wave 2 conflict check: PASS.

---

### PLAN-3.1 — GitHub Actions Publish Job

**No blocking file-path errors.** The only file modified is `.github/workflows/ci.yml` — confirmed
to exist at that path. PASS.

**ci.yml build job name confirmed:** The existing job is named `build` (line 10 of ci.yml). The plan's
`needs: build` reference is correct. PASS.

**Non-blocking — acceptance criterion on `runs-on: ubuntu-latest` count:** The criterion checks that
`grep -c "runs-on: ubuntu-latest"` returns `1`. After adding the publish job this will be `1`
(only the new publish job uses ubuntu-latest; the existing build job uses windows-latest). This is
correct but fragile — if a future editor adds another ubuntu job the count changes. Acceptable for now.

**Non-blocking — `grep -c "runs-on: windows-latest"` must return `1`:** Current ci.yml has exactly
one occurrence. Adding only the publish job (ubuntu-latest) leaves this at 1. Correct.

**Non-blocking — YAML syntax validator:** The plan offers `python3` as first option; Python 3 is
almost certainly available in the build environment. Acceptable.

**Non-blocking — `--no-restore` in pack step:** The PLAN-3.1 publish job template uses `--no-restore`
after a `dotnet restore` step. Correct pattern.

**Files touched by PLAN-3.1:**
- `.github/workflows/ci.yml` (modify)

Disjoint from PLAN-3.2 (csproj, CHANGELOG.md, CLAUDE.md). Wave 3 conflict check: PASS.

---

### PLAN-3.2 — Version Bump, CHANGELOG, and CLAUDE.md Sync

**Blocking issue — wrong csproj path in Task 1 and Verification.**

Task 1 acceptance criterion and Verification section use the non-existent subdirectory path. Same
error as PLAN-1.1 and PLAN-2.1.

**Non-blocking — ls glob in Verification section uses unusual pattern:**
```bash
ls Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler/bin/Release/*.0.3.0*.nupkg
```
Two problems: (a) the directory prefix is wrong (should be `Source/bin/Release/`), and (b) the glob
`*.0.3.0*` may not match `DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.0.3.0.nupkg` on
all shells — a more reliable pattern is `*0.3.0*.nupkg`. Minor, but would fail as written.

**Non-blocking — CHANGELOG date format:** The plan says to use "today's date" (system context provides
2026-04-10). The acceptance criterion checks `grep -c "^### 0.3.0"` without requiring the date — so
any date written is acceptable to the acceptance test. PASS.

**Non-blocking — CLAUDE.md Known Issues criterion is conservative:** `grep -c "_stopRequested" CLAUDE.md`
returns `0` is the correct post-fix expectation. But the CURRENT CLAUDE.md has `_stopRequested` in
the known-issues list — the builder must remove it. The criterion correctly captures the post-state. PASS.

**Non-blocking — `grep -c "int.Parse" CLAUDE.md` criterion:** Same — current CLAUDE.md mentions
`int.Parse` in known issues; plan correctly removes it and checks for absence. PASS.

**Files touched by PLAN-3.2:**
- `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj` (modify — version bump)
- `CHANGELOG.md` (modify — prepend entry)
- `CLAUDE.md` (modify — surgical updates)

Note: PLAN-2.1 also touches the csproj, but in Wave 2 (earlier). PLAN-3.2 is Wave 3 and runs after
Wave 2 completes. The sequencing resolves this correctly — no conflict. PASS.

---

## Coverage Map: Success Criteria vs Plans

| # | ROADMAP Success Criterion | Covered By |
|---|--------------------------|------------|
| 1 | `dotnet build -c Release` zero warnings on clean clone | PLAN-1.1 T3, PLAN-1.2 T3, PLAN-2.1 T3, PLAN-3.2 Verification |
| 2 | `dotnet test` passes all 3 integration tests on GH Actions windows-latest | PLAN-1.1 T3, PLAN-1.2 T3, PLAN-3.2 Verification |
| 3 | `dotnet pack -c Release` produces `.nupkg` + `.snupkg` with correct metadata | PLAN-2.1 T3, PLAN-3.2 T1 + Verification |
| 4 | Repo-root `README.md` exists and renders cleanly | PLAN-2.2 T1 |
| 5 | `OnBeaconReady` contains `int.TryParse` (not `int.Parse`) | PLAN-1.2 T1 |
| 6 | `_stopRequested` and `_running` both carry `volatile` | PLAN-1.2 T2 |
| 7 | Main csproj `<TargetFrameworks>` is exactly `net10.0;net8.0`; no `net48`/`net472` anywhere | PLAN-1.1 T1 |
| 8 | GH Actions has `publish` job guarded by tag ref, `needs: build`, pushes both package types | PLAN-3.1 T1 |
| 9 | `CHANGELOG.md` 0.3.0 entry + `CLAUDE.md` sync (GH Actions, no fixed known issues, issue #6) | PLAN-3.2 T2 + T3 |
| 10 | csproj `<Version>` reads `0.3.0` | PLAN-3.2 T1 |
| 11 | SourceLink debugging works end-to-end | PLAN-2.1 T1+T2 (metadata added); post-publish manual verification |
| 12 | After tagging `v0.3.0`, publish job runs and package appears on nuget.org | PLAN-3.1 T1 (job defined); pre-flight gate (user adds NUGET_API_KEY) |

**Coverage verdict: all 12 criteria are addressed.** Criteria 11 and 12 have components that are
inherently post-publish manual steps (SourceLink verified in a debugger, package visible on nuget.org
after a real tag push) — this is acknowledged in the roadmap as expected.

---

## Hidden Issues Found

### H1 — Wrong source layout assumed in ALL plans (BLOCKING)

All six plans assume source files live under a subdirectory named
`Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler/`. The actual layout is:

| Assumed (wrong) | Actual |
|----------------|--------|
| `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj` | `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj` |
| `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler/TaskSchedulerBus.cs` | `Source/TaskSchedulerBus.cs` |
| `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler/TaskSchedulerJobCountSync.cs` | `Source/TaskSchedulerJobCountSync.cs` |
| `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler/bin/Release/` | `Source/bin/Release/` |

Every acceptance-criterion `grep` command and every file path in task descriptions must be corrected.
The fix is mechanical — replace the long subdirectory prefix with the correct short path — but it must
be done in all six plans before execution.

### H2 — README.md relative path in csproj is wrong (BLOCKING)

PLAN-2.1 Task 2 instructs the builder to write `..\..\README.md` in the pack item. Because the csproj
is at `Source/Foo.csproj` (one level inside repo root), the correct path is `..\README.md`. Using
`..\..\README.md` resolves outside the repository and will cause `dotnet pack` to fail finding the file.

This error originates in RESEARCH.md which correctly stated the csproj depth but then miscounted the
`..` hops.

### H3 — PLAN-3.2 Verification ls path and glob pattern (non-blocking)

The verification `ls` commands use the wrong directory prefix (carries the non-existent subdirectory)
and an unusual glob `*.0.3.0*.nupkg`. Should be `ls Source/bin/Release/*0.3.0*.nupkg`.

### H4 — Task count in PLAN-2.1 Task 3 is conditional on PLAN-2.2 completion (minor, non-blocking)

PLAN-2.1's Task 3 pack verification is only fully assertable if PLAN-2.2 has already landed the README.
Since PLAN-2.1 and PLAN-2.2 are parallel within Wave 2, execution order is not guaranteed. The plan
handles this with an explicit conditional, which is appropriate. Builders should be aware they may need
to run the PLAN-2.1 Task 3 verification a second time after PLAN-2.2 completes.

---

## Standard Plan Verification Results

| Check | Result | Evidence |
|-------|--------|----------|
| Coverage — all 12 criteria covered | PASS | Coverage map above: all 12 mapped to at least one plan task |
| Task count — no plan exceeds 3 tasks | PASS | PLAN-1.1: 3, PLAN-1.2: 3, PLAN-2.1: 3, PLAN-2.2: 2, PLAN-3.1: 3, PLAN-3.2: 3 |
| Wave ordering — Wave 2 depends only on Wave 1; Wave 3 only on Wave 2 | PASS | PLAN-2.1 deps: Wave 1. PLAN-2.2 deps: Wave 1. PLAN-3.1 deps: Wave 2. PLAN-3.2 deps: Wave 2. No circular or forward references. |
| File conflicts within Wave 1 | PASS | PLAN-1.1 touches csproj; PLAN-1.2 touches .cs files. Disjoint. |
| File conflicts within Wave 2 | PASS | PLAN-2.1 touches csproj; PLAN-2.2 touches README.md. Disjoint. |
| File conflicts within Wave 3 | PASS | PLAN-3.1 touches ci.yml; PLAN-3.2 touches csproj + CHANGELOG + CLAUDE.md. Disjoint. |
| Cross-wave csproj touches (PLAN-2.1 and PLAN-3.2 both modify csproj) | PASS | Wave 3 runs after Wave 2 completes. Sequential, no conflict. |
| Testable acceptance criteria | PASS with caveats | All criteria use grep/wc/dotnet commands. PLAN-1.1 Task 2 (NetMQ bump) is effectively MANUAL due to conditional logic. PLAN-2.2 wc-l lower bound (40) is slightly loose. |
| File paths exist — csproj | FAIL | Plans say `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler/...csproj`; actual: `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj` |
| File paths exist — TaskSchedulerBus.cs | FAIL | Plans say `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler/TaskSchedulerBus.cs`; actual: `Source/TaskSchedulerBus.cs` |
| File paths exist — TaskSchedulerJobCountSync.cs | FAIL | Plans say `Source/.../TaskSchedulerJobCountSync.cs`; actual: `Source/TaskSchedulerJobCountSync.cs` |
| File paths exist — ci.yml | PASS | `.github/workflows/ci.yml` confirmed present |
| File paths exist — CHANGELOG.md | PASS | `CHANGELOG.md` confirmed present at repo root |
| File paths exist — CLAUDE.md | PASS | `CLAUDE.md` confirmed present at repo root |
| File paths exist — README.md | PASS | `README.md` confirmed present at repo root (will be rewritten by PLAN-2.2) |
| API line numbers — int.Parse at line 176 | PASS | grep confirms `int.Parse(message.String)` at line 176 of `Source/TaskSchedulerBus.cs` |
| API line numbers — _stopRequested at line 36, _running at line 37 | PASS | grep confirms exact positions in `Source/TaskSchedulerJobCountSync.cs` |
| README relative path in csproj | FAIL | Plans specify `..\..\README.md`; correct is `..\README.md` (csproj is one level from repo root) |
| Verification commands — solution path | PASS | `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.sln` is used consistently and is correct |
| Verification commands — test project path | PASS | `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.Tests/...Tests.csproj` is correct |
| Verification commands — csproj path in grep commands | FAIL | Wrong subdirectory prefix in all grep commands across PLAN-1.1, PLAN-1.2, PLAN-2.1, PLAN-3.2 |
| Forward references within waves | PASS | No plan depends on sibling wave changes not listed as dependencies |
| Complexity — files touched or directories | PASS | No plan touches more than 3 files or more than 2 distinct directories |

---

## Overall Verdict

**REVISE**

Three blocking issues require correction before any builder executes these plans:

1. **(H1) All 6 plans use a non-existent source subdirectory path.** The csproj is at
   `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj` and all `.cs` source
   files are directly under `Source/`. Every file path reference in task descriptions and every
   `grep` acceptance-criterion command must be updated to match the actual layout.

2. **(H2) PLAN-2.1 specifies `..\..\README.md` in the pack item — this is wrong.** The csproj is
   only one directory level inside the repo root, so the correct relative path is `..\README.md`.
   Using the wrong path causes `dotnet pack` to fail to find the README.

3. **(H3, minor but will break verification) PLAN-3.2 Verification `ls` paths** carry the wrong
   prefix and an unusual glob pattern — correct to `Source/bin/Release/*0.3.0*.nupkg`.

No coverage gaps, no wave-ordering errors, no within-wave file conflicts. The research line numbers
for both stability fixes are confirmed correct. Once the path errors are corrected the plans are
otherwise sound and feasible.
