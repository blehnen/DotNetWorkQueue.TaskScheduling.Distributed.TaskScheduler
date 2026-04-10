# CONCERNS.md

## Overview

This codebase is a small but operationally sensitive library: it coordinates thread-pool concurrency limits across processes using an unauthenticated UDP/TCP mesh. The primary concerns are concurrency correctness bugs in the core synchronization path, a missing `int.Parse` guard that can crash the message poller, incomplete NuGet packaging metadata, and a lack of unit tests (all tests are slow real-network integration tests). Several issues are well-known and documented; this file adds evidence, severity, and effort estimates.

## Metrics

| Metric | Value |
|--------|-------|
| Source files (non-generated) | 8 |
| Test files | 1 |
| Test methods | 3 (all integration, none unit) |
| Minimum test suite wall-clock time | ~19 s |
| Target frameworks (library) | net10.0, net48, net472, net8.0 |
| Target frameworks (tests) | net8.0 only |
| CI systems | GitHub Actions + Jenkins |
| NuGet packaging metadata present | No (`IsPackable` absent; no `PackageId`, `Authors`, `Description`, `RepositoryUrl`) |
| Stale MSBuildTemp directories in repo root | 47 |
| Known TODO/FIXME/HACK markers | 0 (issues are in CLAUDE.md, not in code) |

---

## Findings

### 1. Concurrency — Non-volatile Boolean Flags (HIGH)

- **`_stopRequested` and `_running` are plain `bool` fields** read and written from multiple threads without `volatile`, `Interlocked`, or memory barriers.
  - Evidence: `Source/TaskSchedulerJobCountSync.cs` lines 36–37 (field declarations), line 131 (`_running = true`), line 171 (`_running = false`), line 242 (`_stopRequested = true`), line 258 (`while (_running) Thread.Sleep(100)`)
  - The `Start()` method runs on a `Task.Run` thread pool thread (`Source/TaskSchedulerMultiple.cs` line 59). `Dispose()` runs on the caller's thread. The compiler and JIT are permitted to cache `_running`/`_stopRequested` in registers, meaning `Dispose()` could spin forever on `while (_running)` and `Start()` may never observe `_stopRequested = true`.
  - Works in practice on x86/x64 due to strong memory model, but is technically undefined behavior under the C# memory model and will break on ARM or under aggressive JIT optimization.
  - **Severity: HIGH** — can cause a deadlock on dispose in future runtimes or non-x86 hardware.
  - **Effort to fix: LOW** — add `volatile` keyword to both declarations, or replace with `CancellationToken`.

### 2. Concurrency — `int.Parse` Without Error Handling in Beacon Handler (HIGH)

- **`OnBeaconReady` calls `int.Parse` on untrusted network input with no try/catch.**
  - Evidence: `Source/TaskSchedulerBus.cs` line 176: `var port = int.Parse(message.String);`
  - A malformed UDP beacon (e.g., from another application on the same broadcast port, or a truncated packet) will throw `FormatException` inside the NetMQ poller thread. The poller does not catch exceptions at this level; the exception propagates and crashes the actor, silently stopping all peer discovery and count synchronization. The calling code in `TaskSchedulerJobCountSync.ProcessMessages` catches exceptions but that loop runs on a different thread — it cannot catch exceptions thrown inside the NetMQ poller.
  - **Severity: HIGH** — causes silent, unrecoverable loss of distributed coordination on any malformed UDP packet.
  - **Effort to fix: LOW** — wrap `int.Parse` in `int.TryParse`; log and return on failure.

### 3. Concurrency — Lock Held During Blocking Socket Receive (MEDIUM)

- **`_lockSocket` is held during `TryReceiveFrameString` with a 10 ms timeout** in `ProcessMessages`, blocking `IncreaseCurrentTaskCount` and `DecreaseCurrentTaskCount` for up to 10 ms on every poll iteration.
  - Evidence: `Source/TaskSchedulerJobCountSync.cs` lines 176–228 (`ProcessMessages` holds `_lockSocket` throughout); lines 91–99 and 108–119 (`IncreaseCurrentTaskCount`/`DecreaseCurrentTaskCount` also acquire `_lockSocket` before sending).
  - Under task scheduling load, every increment/decrement of the task counter must wait up to 10 ms to acquire the lock. At high throughput this can serialize task dispatch behind the polling loop.
  - [Inferred] The lock was introduced to protect `_actor` socket sends from concurrent access. A cleaner design would use a dedicated channel or NetMQ's own thread-safe command pipe instead of a shared lock.
  - **Severity: MEDIUM** — degrades throughput under load but does not cause data corruption.
  - **Effort to fix: MEDIUM** — requires redesigning the message-send path to not share a lock with the receive loop.

### 4. Concurrency — `SetCount` Uses Non-Atomic Dictionary Update (MEDIUM)

- **Remote count update is not atomic**: `_otherProcessorCounts[key] = value` after a `ContainsKey` check is a TOCTOU pattern on `ConcurrentDictionary`.
  - Evidence: `Source/TaskSchedulerJobCountSync.cs` lines 213–219:
    ```csharp
    if (!_otherProcessorCounts.ContainsKey(key))
        _otherProcessorCounts.TryAdd(key, value);
    else
        _otherProcessorCounts[key] = value;
    ```
  - This code runs while `_lockSocket` is held (line 177), so concurrent modification from the same thread path is prevented. However, `GetCurrentTaskCount` also acquires `_lockSocket` before reading (line 72), so the lock does provide mutual exclusion here. The pattern is still fragile — a future refactor removing the lock could introduce a bug.
  - The idiomatic fix is `_otherProcessorCounts.AddOrUpdate(key, value, (_, __) => value)`.
  - **Severity: MEDIUM** — currently safe due to lock, but a latent fragility.
  - **Effort to fix: LOW** — one-line `AddOrUpdate` replacement.

### 5. Security — Unauthenticated, Unencrypted P2P Bus (MEDIUM)

- **Any process on the same subnet can join the mesh**, inject arbitrary task counts, and force the scheduler to starve or over-admit work.
  - Evidence: `Source/TaskSchedulerBus.cs` lines 88, 108 — `_subscriber.Subscribe("")` subscribes to all topics; `_beacon.Subscribe("")` accepts all beacons. No authentication token, HMAC, or shared-secret check exists anywhere in the message pipeline.
  - Evidence: `Source/TaskSchedulerSetup.cs` line 38 — default `beaconInterface = "loopback"` limits exposure on Windows, but the `""` (all interfaces) mode documented for Linux exposes the bus to the local network segment.
  - A malicious or misconfigured process publishing a very large `SetCount` value would cause all nodes to believe the cluster is saturated and stop accepting tasks.
  - **Severity: MEDIUM** — exploitable in multi-tenant or shared-network environments, but the library is designed for controlled same-machine use.
  - **Effort to fix: HIGH** — would require adding a shared-secret handshake or ZAP authentication layer to NetMQ.

### 6. Missing NuGet Packaging Metadata (MEDIUM)

- **The `.csproj` has no NuGet-publish metadata**: no `IsPackable`, `PackageId`, `Authors`, `Description`, `PackageLicenseExpression`, `RepositoryUrl`, or `PackageTags`.
  - Evidence: `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj` lines 1–25 — only `Version`, `Company`, `Copyright`, `Product` are present.
  - Running `dotnet pack` will produce a `.nupkg` but with missing required fields for NuGet.org publishing (Authors and Description are mandatory). The generated package will have no license SPDX expression, no source link, and no repository URL.
  - No `.nuspec` file exists in the repo root.
  - **Severity: MEDIUM** — blocks NuGet.org publication in its current state.
  - **Effort to fix: LOW** — add ~8 lines of `<PropertyGroup>` metadata to the `.csproj`.

### 7. Testing — No Unit Tests; All Tests Are Slow Integration Tests (MEDIUM)

- **The entire test suite consists of 3 real-network integration tests** that bind UDP sockets, exchange beacons, and wait for propagation with `Task.Delay` totaling ~19 seconds of deliberate sleep.
  - Evidence: `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.Tests/TaskSchedulerJobCountSyncTests.cs` — `LocalCountIncrementAndDecrement` (2.5 s delay), `TwoNodesDiscoverEachOtherAndSyncCounts` (3.5 s + 1.5 s delays), `ThreeNodesAllSeeSharedCount` (5 s + 2.5 s delays + staggered starts).
  - `TaskSchedulerMultiple`, `TaskSchedulerSetup`, `TaskSchedulerMultipleConfiguration`, `NodeKey`, and `TaskSchedulerBusCommands` have zero test coverage.
  - `ITaskSchedulerJobCountSync` and `ITaskSchedulerBus` interfaces exist but are never used with mocks in tests; NSubstitute is a declared dependency but appears unused.
  - Evidence: `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.Tests/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.Tests.csproj` line 11 — `NSubstitute` is present but not exercised.
  - The 3-node test is skipped in Jenkins CI (`Jenkinsfile` does not exclude it explicitly, but a commit message references ignoring it: git log `140c87c "Ignore 2-node test in jenkins as well"`).
  - **Severity: MEDIUM** — logic bugs in scheduler integration and setup code cannot be caught without running the full network stack.
  - **Effort to fix: MEDIUM** — add unit tests for `TaskSchedulerMultiple` using NSubstitute mocks of `ITaskSchedulerJobCountSync`; add unit tests for `NodeKey` equality/hashing; mock `ITaskSchedulerBus` for `TaskSchedulerJobCountSync` unit tests.

### 8. CI — Tests Only Run on Windows (GitHub Actions); Linux Coverage is Jenkins-Only (LOW)

- **GitHub Actions CI runs only on `windows-latest`** and does not collect coverage.
  - Evidence: `.github/workflows/ci.yml` lines 11, 33 — `runs-on: windows-latest`; no `--collect` flag on the test step.
  - Jenkins runs on a Linux Docker agent and does collect coverage, but Jenkins is a private CI not visible in the repository.
  - The `loopback` vs `""` beacon interface difference is OS-specific and platform-specific bugs will not be caught by the Windows-only GHA workflow.
  - **Severity: LOW** — Jenkins provides Linux coverage, but contributors without Jenkins access cannot verify Linux behavior.
  - **Effort to fix: LOW** — add a Linux job to the GitHub Actions matrix.

### 9. Stale MSBuildTemp Directories in Repo Root (LOW)

- **47 `MSBuildTemp*` directories exist in the repository root**, indicating MSBuild temporary files are being created in the working directory and not cleaned up.
  - Evidence: repo root listing shows `MSBuildTempBY49Nm`, `MSBuildTempC9uscL`, ... (47 total).
  - These are not tracked by git (not in `.gitignore` review scope) but clutter the working directory. If they are tracked, they add unnecessary bulk to the repository.
  - **Severity: LOW** — cosmetic/maintenance issue.
  - **Effort to fix: LOW** — add `MSBuildTemp*/` to `.gitignore`; clean up existing directories.

### 10. License Header Copyright Year Stale (LOW)

- **All source file license headers read `Copyright © 2017-2020`** while the `.csproj` copyright field reads `2019-2026`.
  - Evidence: every `.cs` file header (e.g., `Source/TaskSchedulerBus.cs` line 2, `Source/TaskSchedulerJobCountSync.cs` line 2) — `//Copyright © 2017-2020 Brian Lehnen`.
  - Evidence: `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj` line 11 — `<Copyright>Copyright © Brian Lehnen 2019-2026</Copyright>`.
  - **Severity: LOW** — cosmetic inconsistency, no legal risk given single-author project.
  - **Effort to fix: LOW** — global find/replace in source headers.

### 11. `Start()` Blocks Indefinitely; No Cancellation Token Support (LOW)

- **`TaskSchedulerJobCountSync.Start()` runs an infinite loop** with no cancellation token, only checking the non-volatile `_stopRequested` flag.
  - Evidence: `Source/TaskSchedulerJobCountSync.cs` lines 152–163 — `while (!_stopRequested)` loop with no timeout or external cancellation support.
  - Evidence: `Source/TaskSchedulerMultiple.cs` lines 58–62 — `Start()` is called via `Task.Run()` with no `CancellationToken` passed.
  - If `Dispose()` is called before `Start()` fully initializes (before `_running = true` at line 131), the `while (_running) Thread.Sleep(100)` spin in `Dispose()` will exit immediately, and the actor may be disposed while `Start()` is still mid-initialization.
  - **Severity: LOW** — edge case race on startup/immediate-dispose; normal usage is unaffected.
  - **Effort to fix: MEDIUM** — refactor to use `CancellationToken` throughout.

---

## Summary Table

| # | Concern | Severity | Effort | Confidence |
|---|---------|----------|--------|------------|
| 1 | `_stopRequested`/`_running` not volatile — data race on dispose | HIGH | LOW | Observed |
| 2 | `int.Parse` on beacon message crashes poller on bad input | HIGH | LOW | Observed |
| 3 | `_lockSocket` held during 10 ms socket receive — throughput bottleneck | MEDIUM | MEDIUM | Observed |
| 4 | Non-atomic `ContainsKey`+assign on `ConcurrentDictionary` | MEDIUM | LOW | Observed |
| 5 | No authentication/encryption on P2P message bus | MEDIUM | HIGH | Observed |
| 6 | Missing NuGet packaging metadata blocks publication | MEDIUM | LOW | Observed |
| 7 | Zero unit tests; all tests are slow real-network integration tests | MEDIUM | MEDIUM | Observed |
| 8 | GitHub Actions CI is Windows-only; no Linux job | LOW | LOW | Observed |
| 9 | 47 stale `MSBuildTemp*` directories in repo root | LOW | LOW | Observed |
| 10 | Source file copyright headers stale (2017-2020 vs 2019-2026) | LOW | LOW | Observed |
| 11 | No `CancellationToken` in `Start()`; startup/dispose race | LOW | MEDIUM | Observed |

---

## Open Questions

- Is NuGet.org publication planned? If so, the packaging metadata (concern 6) needs to be added before the next release.
- Is the `loopback` default on Linux an accepted limitation, or should the default be changed to `""` now that the Linux behavior is documented? The `TaskSchedulerSetup.InjectDistributedTaskScheduler` signature defaults to `"loopback"` which silently fails on Linux.
  - Evidence: `Source/TaskSchedulerSetup.cs` line 38; `Source/TaskSchedulerMultipleConfiguration.cs` lines 35–36.
- The 2-node and 3-node tests are skipped on Jenkins (`git log 140c87c`, `9cde4b2`). Is this a permanent skip or a known flakiness issue being tracked?
- Are the 47 `MSBuildTemp*` directories tracked by git or gitignored? If tracked, the repo history contains unnecessary bulk.
