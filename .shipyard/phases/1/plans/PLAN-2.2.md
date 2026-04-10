# Plan 2.2: Repo-Root README for NuGet Consumers

## Context
Author a concise (~50–80 line) NuGet-focused `README.md` at the repo root. It becomes the package README via the `<PackageReadmeFile>README.md</PackageReadmeFile>` metadata and `<None Include="..\README.md" Pack="true" PackagePath="\" />` pack item added by PLAN-2.1. The file must cover: what the library is, when to use it, install command, quick-start snippet using `InjectDistributedTaskScheduler`, UDP port convention, Linux `BeaconInterface` note, requirements, license, and repo/issue tracker links.

The existing README has reusable content (overview, limitations, usage example) but lacks NuGet install instructions and is more developer-focused. The builder should read it for reference but write fresh, consumer-focused content rather than mechanically edit.

Parallel-safe with PLAN-2.1 within Wave 2: the csproj and the README are disjoint files. Wave 2 depends on Wave 1 so the upgraded dependencies are in place before documenting the quick-start snippet.

**Git strategy is manual. Do NOT run `git commit`, `git push`, `git tag`, or any history-mutating command. Leave changes staged/unstaged for the user to review and commit.**

## Non-Goals
- No csproj edits (PLAN-2.1 owns packaging metadata)
- No CHANGELOG or CLAUDE.md edits (PLAN-3.2)
- No CI workflow edits
- No LICENSE file changes (the existing LGPLv2.1 LICENSE file stands unchanged)
- Do not add badges, GIFs, or images that require binary assets

## Dependencies
Wave 1 complete (PLAN-1.1, PLAN-1.2). Does NOT depend on PLAN-2.1 file-wise — parallel-safe in Wave 2.

## Tasks

### Task 1: Replace README.md with NuGet-focused content
**Files:** `README.md` (repo root, absolute path `/mnt/f/git/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler/README.md`)
**Action:** rewrite
**Description:**
Read the existing `README.md` for reference, then overwrite it with a concise, NuGet-consumer-focused version covering (in roughly this order):

1. **Title** — `# DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler`
2. **One-line description** — replacement `ATaskScheduler` for DotNetWorkQueue that coordinates worker thread pool counts across multiple processes on one machine via a NetMQ P2P bus.
3. **When to use it** — short paragraph describing the scenario: multiple consumer processes on the same host that each run a DotNetWorkQueue consumer and collectively need to stay under a shared concurrency ceiling.
4. **Install** — `dotnet add package DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler` in a fenced bash block.
5. **Quick start** — a minimal C# fenced code block showing the DI wire-up. Use the existing usage pattern, condensed to the essential lines:
   ```csharp
   using DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler;

   // Inside your DotNetWorkQueue consumer setup, after creating the container
   // but before Start(), replace the default scheduler with the distributed one.
   // The int argument is the UDP broadcast port — all cooperating processes
   // on this machine must pass the same port.
   container.InjectDistributedTaskScheduler(9999);
   ```
6. **UDP broadcast port convention** — a short paragraph: the port argument is a UDP port used for NetMQ beacon discovery; all cooperating processes on the same machine MUST pass the same port; processes on different ports form independent pools. Default suggestion: 9999 (but any free UDP port works).
7. **Linux note on BeaconInterface** — brief callout: on Linux the default `"loopback"` interface does not loop UDP back; pass `""` (empty string) for `beaconInterface` via `TaskSchedulerMultipleConfiguration` or the overload. Reference commit 9cde4b2 is NOT required; just document the workaround.
8. **Requirements** — `.NET 8` or `.NET 10`. Windows or Linux. DotNetWorkQueue 0.9.31 or newer.
9. **License** — `LGPL-2.1-or-later`. Link to the `LICENSE` file.
10. **Links** — GitHub repo URL `https://github.com/blehnen/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler` and issue tracker URL.

Target length: 50–80 lines of Markdown. No badges. No emoji. No images. Do not mention AppVeyor (it's dead). Do not include developer build commands — those belong in `CLAUDE.md`, not the NuGet README. Preserve an LGPL-appropriate license statement.

**Acceptance Criteria:**
- `wc -l README.md` reports between 40 and 100 lines
- `grep -c "dotnet add package DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler" README.md` returns at least `1`
- `grep -c "InjectDistributedTaskScheduler" README.md` returns at least `1`
- `grep -c "LGPL-2.1-or-later" README.md` returns at least `1`
- `grep -ci "loopback" README.md` returns at least `1` (Linux beacon note present)
- `grep -ci "UDP" README.md` returns at least `1`
- `grep -c "AppVeyor" README.md` returns `0`
- `grep -c "https://github.com/blehnen/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler" README.md` returns at least `1`
- Markdown renders without parse errors (visual inspection or `markdownlint README.md` if available — not required)

### Task 2: Confirm pack picks up README (post-PLAN-2.1 smoke test)
**Files:** no file edits; verification only
**Action:** verify
**Description:**
If PLAN-2.1 has already landed, run `dotnet pack -c Release` and confirm the README.md is embedded at the root of the resulting `.nupkg`. If PLAN-2.1 has not yet landed, this task is a no-op verification (just confirm README.md exists and has non-trivial content). The Wave 3 CI and final verification step will re-check the packed README presence.

```bash
dotnet restore Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.sln
dotnet build Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.sln -c Release
dotnet pack Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj -c Release --no-build
```

**Acceptance Criteria:**
- `README.md` exists at repo root with content matching Task 1's criteria
- `dotnet build -c Release` produces zero warnings
- If PLAN-2.1 is complete: a `.nupkg` is produced and `unzip -l <nupkg> | grep -c "README.md"` returns at least `1`

## Verification
```bash
cd /mnt/f/git/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler
wc -l README.md
grep -c "dotnet add package DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler" README.md
grep -c "InjectDistributedTaskScheduler" README.md
grep -c "LGPL-2.1-or-later" README.md
grep -c "AppVeyor" README.md  # must be 0
dotnet build Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.sln -c Release
```
All content checks must pass. Build must produce zero warnings.
