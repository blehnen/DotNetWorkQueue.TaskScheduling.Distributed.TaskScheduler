# Documentation Review: Phase 1

## Summary

The documentation posture for the 0.3.0 NuGet release is solid. The README is well-structured for
both GitHub and NuGet audiences, CLAUDE.md reflects the post-Phase 1 state accurately, and the
CHANGELOG entry covers every significant change with appropriate breaking-change callouts. The main
documentation risk is `<NoWarn>CS1591</NoWarn>` in the csproj: it suppresses the "missing XML doc"
build warning, meaning undocumented public members now compile silently. Spot-checks confirm the
actively-used public surface is well-documented, but the suppression means any future public members
added without docs will go undetected at build time. The `.shipyard/codebase/` reference docs are
stale on several factual points (TFMs, version, two now-fixed known issues) but this is an internal
reference, not consumer-facing.

---

## Public API XML Docs

**Verdict: Complete on checked members; CS1591 suppression is a latent risk.**

Spot-checks performed:

| File | Members checked | XML `<summary>` present |
|------|----------------|------------------------|
| `TaskSchedulerSetup.cs` | Class + `InjectDistributedTaskScheduler` | Yes — param docs include `broadCastPort`, `beaconInterface`, and the Linux caveat |
| `TaskSchedulerMultipleConfiguration.cs` | Class, constructor, `BroadCastPort`, `BeaconInterface` | Yes — constructor doc enumerates all four valid `beaconInterface` values with platform notes |
| `ITaskSchedulerBus.cs` | Interface + `Start()` method | Yes |
| `ITaskSchedulerJobCountSync.cs` | Interface + all 5 members | Yes |
| `TaskSchedulerBusCommands.cs` | Enum + all 6 values | Yes |
| `TaskSchedulerBus.cs` | Class + `Start()` + constructor | Yes (class is `internal`, so CS1591 does not apply) |

`<NoWarn>CS1591</NoWarn>` was added in the Phase 1 work. CS1591 is "Missing XML comment for
publicly visible type or member." With `TreatWarningsAsErrors` also set, this warning would
previously have been a build-breaking error — the suppression is what made it possible to ship
without documenting every member. For this release all checked public members DO have XML docs, so
the suppression is not hiding active gaps. However, the suppression removes the safety net for
future contributors.

**Gap:** No `<param>` or `<returns>` docs on `InjectDistributedTaskScheduler` for the `container`
parameter (it only has a `/// <param name="container">The container.</param>` terse entry), but
this is a known trivial omission and not worth blocking a release.

---

## NuGet-facing README

**Verdict: Sufficient for 0.3.0.**

The README covers:
- What it is and why (concise, accurate)
- Install via three methods (`dotnet add`, PMC, `PackageReference`)
- Quick start with both minimal and container-pattern examples
- UDP port semantics and dead-node pruning timing
- Linux/WSL beacon workaround with a working code snippet
- Limitations (4 bullets, honest and accurate)
- Requirements (.NET 8 / .NET 10, DotNetWorkQueue 0.9.31)
- License and links

One observation: the Linux section says "This issue ... was resolved in the default configuration as
of v0.2.1" — this is slightly misleading. The default remains `"loopback"` (which still fails on
Linux); v0.2.1 added the `BeaconInterface` option so users *can* fix it. The sentence could be read
as "Linux now works by default," which is not the case. This is a minor clarity issue, not a
blocker.

The `PackageReference` example pins `Version="0.3.0"` (line 31) — acceptable for a getting-started
example but consumers should be advised to check for newer versions. Not blocking.

No missing sections for a 0.3.0 initial public release.

---

## CHANGELOG

**Verdict: Complete.**

The 0.3.0 entry (dated 2026-04-10, matching today's date):
- Flags the `net48`/`net472` drop as **Breaking** — correct and clearly marked
- Documents the DotNetWorkQueue bump (0.9.14 to 0.9.31)
- Documents both bug fixes (`int.TryParse`, `volatile` fields)
- Notes the first NuGet publication and all packaging additions
- References issue #6 for the deferred lock contention work

Date format matches prior entries (semver heading + ISO date). No gaps detected.

---

## CLAUDE.md accuracy

**Verdict: In sync with one minor note.**

The current CLAUDE.md correctly reflects:
- TFMs updated to `net10.0` and `net8.0` only (net48/net472 removed)
- CI updated from "AppVeyor" to "GitHub Actions (`.github/workflows/ci.yml`)"
- Known Issues section reduced to the single remaining issue (lock contention, with issue #6 link)
- The two fixed issues (`_stopRequested`/`_running` not volatile; `int.Parse` crash) are correctly
  absent

One stale detail: the Build section mentions "`TreatWarningsAsErrors`" and "XML documentation is
generated" but does not note `<NoWarn>CS1591</NoWarn>`. This is not wrong — just incomplete for a
developer trying to understand why a missing doc comment doesn't fail the build. Minor; not a
blocker.

---

## Codebase docs (.shipyard/codebase/*)

**Verdict: Needs targeted refresh. Several facts are stale.**

These are internal reference docs (generated before Phase 1 changes). The following are out of date:

### ARCHITECTURE.md — stale items

| Section | Stale content | Current reality |
|---------|--------------|-----------------|
| Metrics table | `net10.0, net8.0, net48, net472` | `net10.0, net8.0` only |
| Metrics table | `Library version: 0.2.1` | `0.3.0` |
| Threading Model — "Known race" bullet | States `_stopRequested`/`_running` are plain `bool` fields; a data race | Fixed in 0.3.0 — both are now `volatile` |
| Peer Discovery Flow | `int.Parse(message.String)` (implied crash on bad input) | Fixed — `int.TryParse` with early return |
| Summary Table row | `_stopRequested/_running volatility: Missing volatile` | Now `volatile` |

### CONCERNS.md — stale items

| Concern | Status |
|---------|--------|
| #1 — `_stopRequested`/`_running` not volatile (HIGH) | **Fixed in 0.3.0.** Should be moved to a "Resolved" section or removed. |
| #2 — `int.Parse` crashes poller (HIGH) | **Fixed in 0.3.0.** Same — should be retired. |
| #6 — Missing NuGet packaging metadata (MEDIUM) | **Fixed in 0.3.0.** Should be marked resolved. |
| Metrics table | Shows `net10.0, net48, net472, net8.0` and `NuGet packaging metadata present: No` | Both stale |
| Open Questions — "Is NuGet.org publication planned?" | Resolved: yes, it shipped. |
| Open Questions — `loopback` default Linux question | Still valid as an open question. |

The two HIGH-severity concerns (#1 and #2) being marked as unresolved in CONCERNS.md is the most
misleading stale item, since a reader would think the most critical known bugs are still open.

---

## Recommended updates

Ordered by priority:

1. **CONCERNS.md** — Mark concerns #1, #2, and #6 as resolved (or move them to a "Resolved in
   0.3.0" section). The two HIGH-severity items being listed as open is actively misleading.
   File: `/mnt/f/git/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler/.shipyard/codebase/CONCERNS.md`

2. **ARCHITECTURE.md** — Update the Metrics table (TFMs, version), the "Known race" bullet in the
   Threading Model section, and the corresponding Summary Table row to reflect the `volatile` fix.
   Also update the Peer Discovery Flow note about `int.Parse` to reflect `int.TryParse`.
   File: `/mnt/f/git/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler/.shipyard/codebase/ARCHITECTURE.md`

3. **README.md Linux section** (line 79) — Reword "was resolved in the default configuration as of
   v0.2.1" to make clear the *option* was added in v0.2.1 but the default still requires the
   workaround on Linux. Suggested replacement: "Pass `""` on Linux — the `"loopback"` default
   silently fails on Linux (this is a NetMQ limitation, not a bug in this library)."
   File: `/mnt/f/git/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler/README.md`

4. **CLAUDE.md** — Add a note under the Build section that `<NoWarn>CS1591</NoWarn>` is set,
   explaining that XML doc warnings are suppressed (so a missing doc comment will not break the
   build). Low priority — mostly relevant to contributors.

---

## Verdict

**READY_TO_SHIP**

The consumer-facing documentation (README, CHANGELOG, XML docs on public API) is complete and
accurate for a 0.3.0 NuGet release. Items 1 and 2 in Recommended Updates are worth doing soon as
housekeeping on internal docs, but none of the identified gaps block publication.
