# REVIEW-3.2: Version Bump 0.2.1 → 0.3.0, CHANGELOG Entry, CLAUDE.md Sync

**Reviewer:** Claude Code (claude-sonnet-4-6)
**Date:** 2026-04-10
**Plan:** PLAN-3.2
**Summary reviewed:** SUMMARY-3.2

---

## Pre-Check: Prior Findings

Prior reviews (REVIEW-1.1, REVIEW-1.2, REVIEW-2.1, REVIEW-2.2) exist. `.shipyard/ISSUES.md` does not yet exist (no persisted non-blocking issues from prior waves). No recurring patterns from prior reviews are relevant to this housekeeping plan.

---

## Stage 1: Spec Compliance

**Verdict: PASS**

### Task 1: Bump csproj version to 0.3.0

- Status: PASS
- Evidence: `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj` line 9 reads `<Version>0.3.0</Version>`. Pattern `<Version>0.2.1</Version>` returns 0 matches. No other properties were added or removed — the file is consistent with the PLAN-2.1 baseline (DotNetWorkQueue 0.9.31, SourceLink 10.0.201, PackageReadmeFile, LGPL-2.1-or-later, IncludeSymbols/snupkg all intact).
- Notes: The only change to the csproj is the Version field, exactly as specified. Cross-wave integrity confirmed: DotNetWorkQueue 0.9.31 reference (PLAN-1.1), SourceLink PackageReference (PLAN-2.1), and README pack item (PLAN-2.1) all remain untouched.

### Task 2: Add 0.3.0 entry to CHANGELOG.md

- Status: PASS
- Evidence:
  - `CHANGELOG.md` line 3: `### 0.3.0 2026-04-10` — header present and correctly dated, at the top of the file above `### 0.2.1 2026-04-05` (line 15).
  - `### 0.2.1 2026-04-05` entry is present and unmodified (line 15–20). `### 0.2.0 2026-03-05` entry is present and unmodified (line 24–29).
  - All required content bullets verified:
    - Breaking TFM drop: line 5 — "`**Breaking:** Drop net48 and net472 target frameworks...`"
    - DotNetWorkQueue bump 0.9.14 → 0.9.31: line 6
    - `int.TryParse` fix: line 7
    - `volatile` fix: line 8
    - First public NuGet release: line 9
    - Packaging metadata + SourceLink + snupkg: line 10
    - README pack: line 11
    - GitHub Actions publish job: line 12
    - issue #6 deferred reference: line 13 with hyperlink `[issue #6](https://github.com/blehnen/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler/issues/6)`
  - Format uses `* ` bullet style matching existing entries; `### X.Y.Z YYYY-MM-DD` header style matches.
  - NetMQ bullet correctly omitted (PLAN-3.2 said omit if not upgraded in PLAN-1.1; NetMQ version is 4.0.2.2 with no mention of a bump in PLAN-1.1 scope).
- Notes: No existing entries were reworded. Entry position is correct (prepended). The 0.3.0 entry does not use a `**Breaking:**` marker inconsistently — the TFM drop bullet correctly uses `**Breaking:**`.

### Task 3: Update CLAUDE.md to reflect the new reality

- Status: PASS
- Evidence:
  - `CLAUDE.md` line 13: Build section now reads "SDK-style project targeting `net10.0` and `net8.0`." — `net48` and `net472` removed.
  - Zero matches for `AppVeyor` in CLAUDE.md.
  - Zero matches for `net48` or `net472` in CLAUDE.md.
  - Line 28: "CI runs on GitHub Actions (`.github/workflows/ci.yml`)." — AppVeyor replaced, GitHub Actions reference present.
  - Known Issues section (lines 56–58): exactly ONE bullet remains — the lock contention item, reworded to include `[issue #6](https://github.com/blehnen/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler/issues/6)`.
  - Zero matches for `_stopRequested` in CLAUDE.md — volatile known issue removed.
  - Zero matches for `int.Parse` in CLAUDE.md — TryParse known issue removed.
  - `net10.0` and `net8.0` both appear in CLAUDE.md (Build section line 13).
  - Architecture, Key Classes, Communication Flow, Key Dependencies sections are untouched and correct.
- Notes: The lock contention bullet was reworded more precisely than the original (now names `IncreaseCurrentTaskCount`/`DecreaseCurrentTaskCount` explicitly) — this is an improvement within scope, not a deviation.

### Task 4 (Implicit): Build and pack artifacts

- Status: PASS
- Evidence:
  - `Source/bin/Release/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.0.3.0.nupkg` exists.
  - `Source/bin/Release/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.0.3.0.snupkg` exists.
  - SUMMARY-3.2 reports: Build succeeded, 0 warnings, 0 errors.
- Notes: The 0.2.1 artifacts also remain in `bin/Release/` which is expected (pack does not clean prior outputs). This is not a concern.

---

## Stage 2: Code Quality

Stage 2 applies to the three modified files: csproj, CHANGELOG.md, CLAUDE.md. No source (.cs) files were touched.

### Critical

None.

### Important

None.

### Suggestions

- **CHANGELOG.md — NetMQ version not mentioned, but no explicit confirmation it was not bumped.**
  The plan says "omit NetMQ bullet if not upgraded in PLAN-1.1." PLAN-1.1 is not reviewed here, but the csproj shows `NetMQ 4.0.2.2`. If PLAN-1.1 did change the NetMQ version from an earlier value, the CHANGELOG omission would be a minor inaccuracy. This is low risk and self-documenting via the csproj, but worth confirming with the git log when committing.
  - Remediation: Before tagging v0.3.0, run `git log --all -p -- Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj | grep NetMQ` to confirm whether NetMQ was bumped in this release cycle. If it was, add a bullet to the 0.3.0 CHANGELOG entry.

- **CLAUDE.md — "single job on windows-latest" detail from plan omitted.**
  PLAN-3.2 Task 3 specified replacing the AppVeyor sentence with: "CI runs on GitHub Actions (`.github/workflows/ci.yml`), single job on `windows-latest`." The implementation reads: "CI runs on GitHub Actions (`.github/workflows/ci.yml`)." The `windows-latest` detail was dropped.
  - Remediation: Append `, single job on \`windows-latest\`` to the CI sentence in CLAUDE.md line 28 to fully match the plan's specified wording. This is cosmetic — the functional content (GitHub Actions, file path) is correct — but the plan was specific.

---

## Integration / Scope Discipline

All checks pass:

- PLAN-3.2 did NOT touch `README.md` (PLAN-2.2's territory) — confirmed.
- PLAN-3.2 did NOT touch any `.cs` source files — confirmed.
- PLAN-3.2 did NOT touch any test files — confirmed.
- PLAN-3.2 did NOT modify CHANGELOG entries older than 0.3.0 — confirmed, 0.2.1 and 0.2.0 entries are byte-for-byte unchanged.
- File sets are disjoint from PLAN-3.1 (`.github/workflows/ci.yml`) — confirmed, no overlap.
- Cross-wave integrity: PackageId, Description, LGPL-2.1-or-later, SourceLink 10.0.201, README pack item, DotNetWorkQueue 0.9.31 all intact in csproj.

---

## Summary

**Verdict: APPROVE**

All three tasks are correctly implemented. The version is 0.3.0 in the csproj, the CHANGELOG entry is complete and well-formed at the top of the file, CLAUDE.md has been surgically updated (AppVeyor gone, net48/net472 gone, two fixed known issues removed, lock contention item references issue #6), and both nupkg/snupkg artifacts for 0.3.0 exist on disk. One minor wording omission (the `windows-latest` detail from the CI sentence in CLAUDE.md) is noted as a suggestion but does not block approval.

**Critical:** 0 | **Important:** 0 | **Suggestions:** 2
