# Phase 1 — Planning Context

Decisions captured before research and plan generation. These resolve ambiguities that PROJECT.md and ROADMAP.md intentionally left to the planning step.

## CI & Test Strategy

### Test project target frameworks

**Decision:** Test project stays **net8.0 only**.

**Rationale:** Simpler, faster CI (~19s vs ~38s). Tests pin to the LTS runtime. The production library still multi-targets net10.0 and net8.0 — compile-time coverage of both TFMs is provided by building the main csproj; the tests validate runtime behavior on net8.0 only.

**Implication for plans:** Do NOT add `net10.0` to `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.Tests/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.Tests.csproj`. Leave `<TargetFramework>net8.0</TargetFramework>` as-is.

### CI build matrix

**Decision:** Keep the CI build-and-test as a **single `windows-latest` job**. Do not expand to an ubuntu matrix.

**Rationale:** The 3 integration tests use real UDP beacon discovery. CONCERNS.md and recent git history show Linux-vs-Windows differences in beacon interface selection (commit `9cde4b2` fixed Linux discovery). Expanding the matrix now introduces test flakiness risk that is out of scope for this release. The expression-json-serializer pattern used ubuntu+windows because that library has no OS-sensitive networking code; this library does.

**Implication for plans:** The `publish` job that will be added to `.github/workflows/ci.yml` runs on ubuntu-latest (like the expression-json-serializer example) because `dotnet pack` is OS-agnostic, but the existing `build` job stays exactly as it is (`runs-on: windows-latest`, single job, no matrix). Only the TFM changes in the build job are removing any `net48`/`net472` references — inspection of the current file shows there are none to remove, so the build job body may not need any edits beyond leaving it alone.

## Git Strategy Reminder

**Manual.** Builder agents must stage changes but not commit, push, or tag. The `v0.3.0` tag is the user's action and also the trigger for the publish job.

## Deferred (reiterated from ROADMAP)

- Issue #6 (lock contention) — not in scope
- Unit tests, NSubstitute cleanup — not in scope
- Linux CI matrix — explicitly deferred above
