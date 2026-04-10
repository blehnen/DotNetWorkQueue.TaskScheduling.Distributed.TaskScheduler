# Shipyard Lessons Learned

## 2026-04-10 — Phase 1: v0.3.0 Modernization & First NuGet Release

### What Went Well

- **Research de-risked the DotNetWorkQueue upgrade.** Before any code edits, we catalogued the 10 specific API touch-points (`SmartThreadPoolTaskScheduler`, `ATaskScheduler`, `IContainer`, etc.) and then checked every breaking-change marker in the 0.9.14 → 0.9.31 changelog against that list. The research was right — zero source edits were needed to compile against the new dependency. This approach avoided the "bump dependency and pray" pattern.
- **Parallel waves with explicit disjoint-file boundaries worked cleanly.** Each wave had two plans with no file overlap (Wave 1: csproj vs two `.cs` files; Wave 2: csproj vs README; Wave 3: ci.yml vs CHANGELOG+CLAUDE+csproj-version). No merge conflicts surfaced across the six parallel builder runs.
- **Manual git strategy held up.** The builders stayed clear of commits even though the build protocol template said "commit per task" — the override from PROJECT.md / config.json was enough, because it was stated explicitly in every builder prompt. Worth keeping in the dispatch template.
- **Porting the packaging pattern from `/mnt/f/git/expression-json-serializer` was almost entirely mechanical.** The only two deviations (LGPL-2.1-or-later instead of MIT, SourceLink 10.0.201 instead of 8.0.0) were caught during research and flagged to every downstream plan.

### Surprises / Discoveries

- **Architect assumed a nested source layout.** RESEARCH.md's "README path verification" section claimed the csproj lived in a `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler/` subdirectory (two levels deep). The actual layout is flat: the csproj sits directly at `Source/Foo.csproj` and all `.cs` files are under `Source/`. The error propagated into all six plans on the first attempt and was caught by the verifier's feasibility stress test. One revision round fixed everything mechanically. **Lesson: always verify directory structure with a direct `ls` before writing path-heavy plans.**
- **Simplifier caught an incomplete stability fix that made it past two reviewers.** The HIGH-severity "fix malformed-input crash" task (PLAN-1.2) only covered `TaskSchedulerBus.OnBeaconReady`. The same bug pattern existed in `TaskSchedulerJobCountSync.ProcessMessages` (lines 210-211) — bare `int.Parse` / `long.Parse` on peer frames inside a `lock`. Neither the PLAN-1.2 builder nor the PLAN-1.2 reviewer flagged it because their scope was narrowly "the two locations named in the plan." The simplifier's cumulative cross-plan review was what caught it. **Lesson: when fixing a bug class, grep the whole codebase for the anti-pattern — don't rely on the plan's listed line numbers being exhaustive.**
- **DotNetWorkQueue 0.9.31 has three `**Breaking**` markers in its changelog, but none touched our API surface.** 0.9.19 dropped net48/netstandard2.0 (forced our TFM drop, which we were doing anyway). 0.9.30 broke `IJobSchedule.Previous()` and cron format — unrelated job-scheduling subsystem. 0.9.31 broke dashboard config — unrelated. A surface-rooted breakage check is much cheaper than panic-scanning release notes.
- **The plan's rationale for not logging malformed beacons was factually wrong.** PLAN-1.2 said "no logger is injected at this level" — but `TaskSchedulerBus` has an injected `ILogger _log` field used in six other places in the class. The builder correctly followed the instruction (silent return, no logging) but noted the discrepancy. **Lesson: research should verify constructor signatures, not just grep for method names.**
- **WSL solution-level `dotnet build` has a pre-existing file-copy race** (MSB3021 on the Windows/WSL filesystem bridge) unrelated to our changes. Per-project builds work reliably. Noted so future local dev on WSL knows to build the csproj directly, and the CI publish job targets the csproj directly anyway (correct for a different reason: pack output path).
- **`Microsoft.SourceLink.GitHub` latest stable is 10.0.201**, not 8.0.0 as in the expression-json-serializer reference. Always check nuget.org for current versions even when copying a known-good reference project.

### Pitfalls to Avoid

- **Don't trust a research doc's "path verification" section until you've run `ls` on the actual layout.** The architect did exactly what the research told it to do, and the research was wrong. A 10-second `ls` at plan-writing time would have saved a revision round across 6 files.
- **Fix-a-bug-class tasks should include a codebase grep for the anti-pattern**, not just fix the specific line numbers a reviewer initially spotted. The CONCERNS.md writeup said "int.Parse in OnBeaconReady" — which was true but not the whole story.
- **Reviewers operating on a fixed scope can miss cumulative issues** that only become visible when looking at all plan diffs together. The simplifier gate was the backstop that caught S1; without it, a HIGH-class bug would have survived into 0.3.0.
- **Agent rate limits and partial file writes** surfaced multiple times in this session. Agents can return a "complete" summary in chat but fail to save the corresponding file to disk, or get cut off mid-task. Workarounds used: explicit "write file FIRST as your primary deliverable" instructions, manual file writes from the agent's chat output as a fallback, and direct verification via `git diff` when reviewer output was in doubt. **Consider:** teaching dispatch prompts to always open with a Write call so the file exists even if the agent is subsequently cut off.

### Process Improvements

- **Research phase: run an explicit `ls Source/` or `find Source/ -name '*.csproj'` near the start** to confirm the layout before writing any path-heavy content. Add this as a checklist item.
- **Plan critique / feasibility stress test is load-bearing.** Without the verifier's critique pass, the six nested-path plans would have gone to the builder and failed. Keep this step on for any phase with path-heavy plans.
- **Simplifier gate must run** even on small phases. This phase's simplifier caught the S1 finding that two reviewers missed. Skipping simplification review (`--light`) to save time would have cost more time in a follow-up 0.3.1 release.
- **Build protocol: teach builder prompts to Write the SUMMARY file at the START of their work** (even if partial) and update it as they go. Prevents complete loss of state when an agent is cut off by rate limits.
- **When a builder runs verification commands that the reviewer will also run**, teach the reviewer to trust the builder's output unless they see a specific reason to re-run. Re-running all verification from scratch wastes tokens and agent turns.

---
