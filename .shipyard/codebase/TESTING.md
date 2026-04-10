# TESTING.md

## Overview

The project has a single test file containing three integration tests. All tests exercise real UDP beacon discovery and real NetMQ sockets on loopback — there are no mocked or unit-level tests. Tests are inherently slow (cumulative `Task.Delay` totals ~19 seconds) and are platform-sensitive: Linux requires a different beacon interface than Windows. The two-node test was skipped in Jenkins CI per commit history; the CI pipeline now runs on GitHub Actions (windows-latest) where all three tests are expected to pass.

## Metrics

| Metric | Value |
|--------|-------|
| Test projects | 1 |
| Test files | 1 |
| Total test methods | 3 |
| Test framework | xUnit 2.x |
| Test target framework | net8.0 only |
| Mocking library declared | NSubstitute 5.x (unused in current tests) |
| Coverage tool declared | coverlet.collector 6.x |
| Approximate total test runtime | ~19 seconds |
| Tests using real network sockets | 3 of 3 (100%) |
| Tests using mocks | 0 |
| CI platform | GitHub Actions, windows-latest |

## Findings

### Test Framework and Runner

- xUnit 2.x is the test framework; `xunit.runner.visualstudio` provides IDE and `dotnet test` runner integration.
  - Evidence: `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.Tests/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.Tests.csproj` (lines 9–11).
- `Microsoft.NET.Test.Sdk` 17.x is the test host.
  - Evidence: same `.csproj` (line 9).
- Tests are run via `dotnet test` with `--no-build` in CI — the build step precedes the test step.
  - Evidence: `.github/workflows/ci.yml` (lines 30–33).
- No `xunit.runner.json` or `RunSettings` file was found — xUnit uses default runner configuration.

### Test Project Layout

```
Source/
  DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.Tests/
    TaskSchedulerJobCountSyncTests.cs   ← only test file
    DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.Tests.csproj
```

- The test project is a sibling directory to the main project under `Source/`.
- The main `.csproj` excludes the test directory from compilation and grants `InternalsVisibleTo` to the test assembly, allowing tests to construct `TaskSchedulerBus` (internal) directly.
  - Evidence: `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj` (lines 16–18).

### Test Class Organization

- Single test class: `TaskSchedulerJobCountSyncTests`, in namespace `DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.Tests`.
  - Evidence: `Tests/TaskSchedulerJobCountSyncTests.cs` (line 14).
- The class is decorated with `[Collection("NetMQ")]`. This serializes test execution — only one test in the `"NetMQ"` collection runs at a time.
  - Evidence: `Tests/TaskSchedulerJobCountSyncTests.cs` (line 13).
  - [Inferred] This is required because NetMQ's global context is not safe for parallel disposal/creation across tests sharing the same process.
- No `[CollectionDefinition]` was found in the test file (and no other test files exist), so the collection is implicitly defined — this is valid xUnit behavior but produces a warning in some analyzers.

### Test Naming Convention

Test methods use PascalCase descriptive phrases that read as sentences describing the behavior under test:

| Method | What it tests |
|--------|--------------|
| `LocalCountIncrementAndDecrement` | Single-node local counter arithmetic |
| `TwoNodesDiscoverEachOtherAndSyncCounts` | Two-node beacon discovery and count propagation |
| `ThreeNodesAllSeeSharedCount` | Three-node convergence of shared total |

All three are plain `[Fact]` attributes — no `[Theory]` or parameterized tests exist.
- Evidence: `Tests/TaskSchedulerJobCountSyncTests.cs` (lines 33, 56, 103).

### Test Arrangement Pattern

Tests follow the **Arrange / Act / Assert** pattern, though not annotated with comments. The arrangement phase is always:

1. Allocate a unique port via `NextPort()` (thread-safe, random-offset static counter).
2. Construct `TaskSchedulerMultipleConfiguration` with that port and the platform-appropriate beacon interface.
3. Construct `TaskSchedulerBus` and `TaskSchedulerJobCountSync` directly (no DI container).
4. Call `sync.Start()` on a background `Task.Run` thread.
5. `await Task.Delay(N)` to let beacons fire and nodes discover each other.

- Evidence: `Tests/TaskSchedulerJobCountSyncTests.cs` (lines 35–51, 60–78, 107–126).

### Real Network Dependencies

All three tests use real UDP sockets and real TCP sockets via NetMQ. There are no fakes, stubs, or mocks of the networking layer.

- **Port allocation**: A static `_nextPort` field starts at `40000 + Random.Shared.Next(0, 10000)` and increments atomically per test. This reduces the chance of port conflicts across test runs and parallel CI jobs.
  - Evidence: `Tests/TaskSchedulerJobCountSyncTests.cs` (lines 15–16).
- **Beacon interface**: Platform-detected at class load time. On Linux the empty string `""` selects the first available non-loopback interface (subnet broadcast). On Windows `"loopback"` is used.
  - Evidence: `Tests/TaskSchedulerJobCountSyncTests.cs` (lines 19–23).
  - This matters because on Linux, `NetMQBeacon` with `"loopback"` binds to `127.0.0.1` but broadcasts to `255.255.255.255`, and the kernel does not deliver those broadcasts back to the originating socket.

### Assertion Style

- xUnit `Assert.Equal(expected, actual)` is used throughout. No fluent assertion library (FluentAssertions, Shouldly) is present.
  - Evidence: `Tests/TaskSchedulerJobCountSyncTests.cs` (lines 43, 46, 49, 52, 74, 75, 80, 81, etc.).
- No `Assert.Throws` or exception-assertion tests exist — error paths in `TaskSchedulerJobCountSync` are not covered.

### Cleanup Pattern

- The single-node test (`LocalCountIncrementAndDecrement`) uses `using var sync = ...` for automatic disposal.
  - Evidence: `Tests/TaskSchedulerJobCountSyncTests.cs` (line 38).
- The multi-node tests use explicit `try/finally` blocks, calling `sync.Dispose()` in the `finally` to guarantee cleanup even when assertions fail.
  - Evidence: `Tests/TaskSchedulerJobCountSyncTests.cs` (lines 69–99, 115–151).
- No `IAsyncLifetime`, `IClassFixture`, or shared setup/teardown exists — each test is fully self-contained.

### Timing and Delays

The tests encode deliberate `Task.Delay` waits to allow asynchronous UDP beacon discovery to complete. These are the dominant contributors to total test duration:

| Test | Key Delays | Approximate Total |
|------|-----------|-------------------|
| `LocalCountIncrementAndDecrement` | 2500ms startup | ~3s |
| `TwoNodesDiscoverEachOtherAndSyncCounts` | 3500ms startup + 3×500ms propagation | ~6s |
| `ThreeNodesAllSeeSharedCount` | 3×500ms staggered start + 5000ms discovery + 3×500ms increment + 1000ms settle | ~10s |

Total: approximately 19 seconds. There is no retry or polling loop — if the network is slow the fixed delays may be insufficient, making tests timing-sensitive.

### Known Slow and Skipped Tests

- **`TwoNodesDiscoverEachOtherAndSyncCounts`**: Was skipped in Jenkins CI (commit `140c87c`: "Ignore 2-node test in jenkins as well", commit `9cde4b2`: "Make beacon interface configurable; fix Linux discovery"). This test was fragile on the Jenkins environment; the fix was making `BeaconInterface` configurable rather than skipping permanently.
- As of the current codebase, **no test is marked `[Fact(Skip = ...)]`** — all three tests are expected to run in the GitHub Actions CI.
  - Evidence: `Tests/TaskSchedulerJobCountSyncTests.cs` (lines 33, 56, 103) — all plain `[Fact]`.
- [Inferred] The `ThreeNodesAllSeeSharedCount` test (three-node) was the test excluded from Jenkins (commit message: "Ignore 2-node test in jenkins as well" implies the 3-node test had been excluded earlier). Both multi-node tests are now expected to pass on the GitHub Actions windows-latest runner.

### Test Logger

A private `XunitLogger` inner class adapts `ITestOutputHelper` to `ILogger`, forwarding all log output to the xUnit test output sink. It catches `InvalidOperationException` silently to handle log calls that arrive after the test has ended.

- Evidence: `Tests/TaskSchedulerJobCountSyncTests.cs` (lines 154–168).
- The `BeginScope` method returns `null!` — scope support is not implemented.

### Coverage Configuration

- `coverlet.collector` 6.x is declared as a test dependency, enabling coverage collection via `dotnet test --collect:"XPlat Code Coverage"`.
  - Evidence: `Tests/.csproj` (lines 13–16).
- No coverage threshold, exclusion attributes, or `coverlet.json` config file was found. [Inferred] Coverage is not gated in CI — the CI workflow does not pass `--collect` or any coverage flags.
  - Evidence: `.github/workflows/ci.yml` (line 33) — `dotnet test` with no coverage arguments.

### Mocking Library

- NSubstitute 5.x is listed as a dependency but is not used in the current test file. [Inferred] It was added in anticipation of future unit tests or was a project template default.
  - Evidence: `Tests/.csproj` (line 12).

### CI Configuration

- CI runs on GitHub Actions, `windows-latest` runner.
- Pipeline: checkout → setup .NET 8.0.x + 10.0.x → restore → build (Debug, no-restore) → test (no-build).
- The test step does not pass `--configuration`, so it defaults to `Debug`.
- No parallelism limits, timeout, or retry configuration is applied to the test step.
  - Evidence: `.github/workflows/ci.yml` (lines 1–34).

## Summary Table

| Item | Detail | Confidence |
|------|--------|------------|
| Framework | xUnit 2.x | Observed |
| Test target | net8.0 only | Observed |
| Test files | 1 | Observed |
| Test count | 3 | Observed |
| Collection attribute | `[Collection("NetMQ")]` — serialized | Observed |
| Real sockets used | Yes, all tests | Observed |
| Mocks used | No (NSubstitute declared but unused) | Observed |
| Assertion library | xUnit `Assert.*` only | Observed |
| Timing sensitivity | High — fixed `Task.Delay` waits | Observed |
| Skipped tests | None currently | Observed |
| Previously skipped | 2-node + 3-node tests on Jenkins | Observed (git history) |
| CI platform | GitHub Actions windows-latest | Observed |
| Coverage gating | Not enforced in CI | Observed |
| Error path coverage | None | Observed |

## Open Questions

- NSubstitute is declared but unused — are unit tests (with mocked `ITaskSchedulerBus`) planned?
- Should the fixed `Task.Delay` waits be replaced with polling loops to reduce flakiness on slow CI runners?
- Coverage is collected by `coverlet` but not reported or gated — should a minimum threshold be enforced?
- The `[Collection("NetMQ")]` tag has no corresponding `[CollectionDefinition]` — should one be added for clarity?
- Is there intentional coverage of `TaskSchedulerMultiple` or `TaskSchedulerSetup`? Neither is tested directly.
