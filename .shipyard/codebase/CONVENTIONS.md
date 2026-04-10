# CONVENTIONS.md

## Overview

This is a small, single-namespace C# library (8 source files) with highly consistent conventions throughout. Every source file carries an LGPLv2.1 license header. All public and internal members carry XML documentation comments. The project enforces `TreatWarningsAsErrors`, making documentation and style adherence build-breaking.

## Metrics

| Metric | Value |
|--------|-------|
| Source files (non-generated) | 8 |
| Test files | 1 |
| Namespace(s) | 1 (source), 1 (tests) |
| TODO / FIXME / HACK comments | 0 |
| `#region` blocks | 1 (`IDisposable Support` in `TaskSchedulerJobCountSync.cs`) |
| Preprocessor `#if` directives | 0 |
| `.editorconfig` present | No |
| Nullable reference types enabled | No |

## Findings

### License Header

Every source file opens with the same 17-line LGPLv2.1 block before any `using` or `namespace` declarations. The format is:

```
// ---------------------------------------------------------------------
//This file is part of DotNetWorkQueue
//Copyright © 2017-2020 Brian Lehnen
//
//This library is free software; ...
// ---------------------------------------------------------------------
```

- Evidence: `Source/TaskSchedulerBus.cs` (lines 1–18), `Source/TaskSchedulerMultiple.cs` (lines 1–18), all other source files.
- Note: The header copyright year says "2017-2020" while the `.csproj` says "2019-2026". The header text has not been updated with the project version.
- The test file (`TaskSchedulerJobCountSyncTests.cs`) does **not** carry the license header — the header convention applies to source-only files.

### Namespace and File Structure

- Single root namespace: `DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler`
  - Evidence: `Source/TaskSchedulerBus.cs` (line 28), all source files.
- Test namespace: `DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.Tests`
  - Evidence: `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.Tests/TaskSchedulerJobCountSyncTests.cs` (line 10).
- One class or interface per file. File name exactly matches the type name. No partial classes.
- No subdirectories within `Source/` for production code — all 8 files are flat in the `Source/` directory.

### Naming Conventions

- **Classes**: PascalCase — `TaskSchedulerBus`, `TaskSchedulerJobCountSync`, `TaskSchedulerMultiple`, `TaskSchedulerMultipleConfiguration`, `NodeKey`.
  - Evidence: all source files.
- **Interfaces**: `I`-prefixed PascalCase — `ITaskSchedulerBus`, `ITaskSchedulerJobCountSync`.
  - Evidence: `Source/ITaskSchedulerBus.cs` (line 26), `Source/ITaskSchedulerJobCountSync.cs` (line 27).
- **Private fields**: `_camelCase` with underscore prefix — `_broadcastPort`, `_nodes`, `_randomPort`, `_log`, `_stopRequested`, `_disposedValue`.
  - Evidence: `Source/TaskSchedulerBus.cs` (lines 36–47), `Source/TaskSchedulerJobCountSync.cs` (lines 37–46).
- **Private methods**: PascalCase — `RunActor`, `OnShimReady`, `OnBeaconReady`, `ClearDeadNodes`, `ProcessMessages`.
  - Evidence: `Source/TaskSchedulerBus.cs` (lines 74, 129, 171, 193).
- **Public properties**: PascalCase — `BroadCastPort`, `BeaconInterface`, `MultipleConfiguration`.
  - Evidence: `Source/TaskSchedulerMultipleConfiguration.cs` (lines 54, 62), `Source/TaskSchedulerMultiple.cs` (line 72).
- **Enums**: PascalCase members — `Publish`, `BroadCast`, `GetHostAddress`, `AddedNode`, `RemovedNode`, `SetCount`.
  - Evidence: `Source/TaskSchedulerBusCommands.cs` (lines 29–71).
- **Events**: PascalCase — `RemoteCountChanged`.
  - Evidence: `Source/ITaskSchedulerJobCountSync.cs` (line 33), `Source/TaskSchedulerJobCountSync.cs` (line 51).
- **Extension methods**: static class named `TaskSchedulerSetup`, method named `InjectDistributedTaskScheduler` — follows the `XxxSetup` / `InjectXxx` verb pattern.
  - Evidence: `Source/TaskSchedulerSetup.cs` (lines 25, 38).
- **Test methods**: Descriptive PascalCase sentences — `LocalCountIncrementAndDecrement`, `TwoNodesDiscoverEachOtherAndSyncCounts`, `ThreeNodesAllSeeSharedCount`.
  - Evidence: `Tests/TaskSchedulerJobCountSyncTests.cs` (lines 33, 56, 103).

### Visibility Rules

- Implementation classes that are only used internally are `internal`: `TaskSchedulerBus`, `TaskSchedulerBusCommands`, `NodeKey`.
  - Evidence: `Source/TaskSchedulerBus.cs` (line 32), `Source/TaskSchedulerBusCommands.cs` (line 24), `Source/NodeKey.cs` (line 26).
- Public API surface: `TaskSchedulerJobCountSync`, `TaskSchedulerMultiple`, `TaskSchedulerMultipleConfiguration`, `TaskSchedulerSetup`, both interfaces.
- `InternalsVisibleTo` grants test project access to internals.
  - Evidence: `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj` (line 17).

### XML Documentation

- Every `public` and `protected` member carries a `<summary>` tag. Parameter tags (`<param>`) and return tags (`<returns>`) are included on all non-trivial members.
- Multi-paragraph docs use `<para/>` and `<list>` XML elements where appropriate.
  - Evidence: `Source/TaskSchedulerMultipleConfiguration.cs` (lines 32–46) — constructor doc uses `<list type="bullet">` with four `<item>` entries.
- `<remarks>` tags appear on some members to add supplementary information.
  - Evidence: `Source/ITaskSchedulerJobCountSync.cs` (line 25), `Source/TaskSchedulerSetup.cs` (line 37), `Source/NodeKey.cs` (line 33).
- `<seealso cref="..."/>` is used to cross-reference related types.
  - Evidence: `Source/TaskSchedulerMultiple.cs` (line 29), `Source/TaskSchedulerSetup.cs` (line 22).
- `GenerateDocumentationFile` is enabled in the `.csproj` with no configuration distinction — XML docs are generated for all configurations (Debug and Release).
  - Evidence: `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj` (line 7).
- [Inferred] Because `TreatWarningsAsErrors` is enabled, missing XML doc on any public member would be a build error (CS1591).

### Error Handling Patterns

- The outer processing loop in `TaskSchedulerJobCountSync.Start()` wraps the inner `ProcessMessages()` call in a `try/catch(Exception)` that logs via `ILogger` and continues — preventing a transient error from killing the loop.
  - Evidence: `Source/TaskSchedulerJobCountSync.cs` (lines 153–163).
- A second outer `try/catch(Exception)` in `Start()` catches fatal errors (e.g., bus startup failure) and logs them.
  - Evidence: `Source/TaskSchedulerJobCountSync.cs` (lines 132–172).
- `Dispose(bool)` catches `SocketException` and selectively re-throws: error codes 10035 and 10054 (WSAEWOULDBLOCK / WSAECONNRESET) are swallowed as expected socket-close noise.
  - Evidence: `Source/TaskSchedulerJobCountSync.cs` (lines 251–256).
- Guard clauses (`Guard.NotNull`) validate constructor arguments in `TaskSchedulerMultiple`.
  - Evidence: `Source/TaskSchedulerMultiple.cs` (lines 46–47).
- **No error handling** on `int.Parse` in `OnBeaconReady` — a malformed UDP beacon payload would throw uncaught.
  - Evidence: `Source/TaskSchedulerBus.cs` (line 176).

### Logging Patterns

- `ILogger` (from `Microsoft.Extensions.Logging`) is injected via constructor in both `TaskSchedulerBus` and `TaskSchedulerJobCountSync`.
  - Evidence: `Source/TaskSchedulerBus.cs` (lines 53–59), `Source/TaskSchedulerJobCountSync.cs` (lines 57–63).
- Log calls use string interpolation (`$"..."`) rather than structured logging message templates — this forgoes structured log provider benefits.
  - Evidence: `Source/TaskSchedulerBus.cs` (lines 93, 99, 103, 107), `Source/TaskSchedulerJobCountSync.cs` (lines 79, 161, 167).
- Exception: test helper `XunitLogger` in the test file uses the structured overload (`_logger.LogInformation("Node {Index} sees count {Count}", i, count)`).
  - Evidence: `Tests/TaskSchedulerJobCountSyncTests.cs` (line 141).
- Log levels used: `LogDebug` for tracing, `LogError` for exceptions.

### Disposable Pattern

- Standard .NET Dispose pattern used: `Dispose(bool disposing)` virtual protected method plus public `Dispose()` that calls it.
- A `_disposedValue` bool guards against redundant calls.
- A `#region IDisposable Support` / `#endregion` block wraps the dispose implementation — the only region in the codebase.
  - Evidence: `Source/TaskSchedulerJobCountSync.cs` (lines 230–274).
- The comment "Do not change this code. Put cleanup code in Dispose(bool disposing) above." is retained verbatim from the Visual Studio code snippet.
  - Evidence: `Source/TaskSchedulerJobCountSync.cs` (line 271).

### String Formatting

- Numeric values sent over the bus use `CultureInfo.InvariantCulture` explicitly for formatting — preventing locale-dependent decimal separators.
  - Evidence: `Source/TaskSchedulerJobCountSync.cs` (line 97).
- Received numeric strings are parsed with plain `int.Parse` / `long.Parse` (no culture or `TryParse`) — inconsistent with the invariant-culture send side.
  - Evidence: `Source/TaskSchedulerJobCountSync.cs` (lines 210–211).

### Preprocessor and Multi-targeting

- No `#if` / `#else` preprocessor directives exist anywhere in the production source. Multi-targeting (net10.0, net8.0, net48, net472) is handled entirely at the project level via `<TargetFrameworks>` — the code itself is framework-agnostic.
  - Evidence: `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj` (line 4).

### Nullability

- Nullable reference types (`#nullable enable`) are not enabled anywhere in the source. [Inferred] This is consistent with the project's use of classic .NET Framework targets (net48, net472) where NRT adoption is less common.
- In the test file, one null-forgiving operator is used: `return null!` in the `BeginScope` stub.
  - Evidence: `Tests/TaskSchedulerJobCountSyncTests.cs` (line 160).

### Import Ordering

- `using` directives appear after the license header block and before the `namespace` declaration (file-scoped `namespace` syntax is not used).
- No consistent alphabetical ordering is enforced — `System` namespaces are not necessarily first.
  - Evidence: `Source/TaskSchedulerJobCountSync.cs` (lines 19–28) — `System.Net.Sockets` and `System.Text` appear after `System.Collections.Concurrent`.
- No `.editorconfig` or `StyleCop`/`Roslyn analyzer` configuration found to enforce import ordering.

## Summary Table

| Item | Detail | Confidence |
|------|--------|------------|
| License header | 17-line LGPLv2.1 block, every source file | Observed |
| Namespace count | 1 production, 1 test | Observed |
| Naming: private fields | `_camelCase` prefix | Observed |
| Naming: classes/methods | PascalCase | Observed |
| XML docs | All public/protected members | Observed |
| Error handling | Log-and-continue for transient errors | Observed |
| Logging style | Interpolated strings, not structured templates | Observed |
| Disposable pattern | Standard `Dispose(bool)` + region | Observed |
| Preprocessor directives | None in production source | Observed |
| Nullable reference types | Not enabled | Observed |
| `.editorconfig` | Absent | Observed |
| Multi-targeting in code | None — all TFM variance in `.csproj` | Observed |
| Import ordering | No enforced order | Observed |

## Open Questions

- Should the license header copyright year ("2017-2020") be updated to match the `.csproj` ("2019-2026")?
- Is structured logging (`{Named}` templates) intended for a future change, or is interpolation accepted permanently?
- Will nullable reference types be enabled as net48/net472 support is eventually dropped?
- No `.editorconfig` or Roslyn analyzer ruleset is present — was one intentionally omitted, or is this a gap?
