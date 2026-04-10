# SUMMARY-1.1: Dependency Bump + TFM Drop

**Status:** complete

---

## Tasks Completed

| Task | Description | Outcome |
|------|-------------|---------|
| Task 1 | Drop net48/net472 from TargetFrameworks; bump DotNetWorkQueue to 0.9.31 | Done |
| Task 2 | Verify NetMQ for newer stable version | Done — no bump needed (4.0.2.2 is current) |
| Task 3 | Full Release build + integration test run under upgraded dependencies | Done — 0 warnings, 0 errors, 3/3 tests pass |

---

## Files Modified

### `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj`

Two line-level changes:

1. **Line 4** — `<TargetFrameworks>` element:
   - Before: `net10.0;net8.0;net48;net472;`
   - After: `net10.0;net8.0`
   - Trailing semicolon also removed per plan spec.

2. **Line 21** — DotNetWorkQueue PackageReference:
   - Before: `Version="0.9.14"`
   - After: `Version="0.9.31"`

No other files were modified. `TaskSchedulerMultiple.cs`, `TaskSchedulerSetup.cs`, and `TaskSchedulerJobCountSync.cs` required zero edits — the 10 API touch-points all compiled cleanly against 0.9.31.

---

## Decisions Made

### NetMQ: no bump (leave at 4.0.2.2)

`dotnet list Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj package --outdated` reported:

> "The given project has no updates given the current sources."

NuGet.org confirmed 4.0.2.2 is the current stable release. No newer stable NetMQ version exists. The version is left unchanged.

### Solution-level build workaround

`dotnet build .sln -c Release` fails with MSB3021/MSB3027 — a file-copy race where the test project bin/Release/net8.0/ directory does not exist when the build starts on WSL. This is a pre-existing WSL filesystem timing issue unrelated to the dependency bump. Building the two projects separately resolves it. The plan's verification commands use per-project builds and per-project test invocation, so this is not a blocker.

---

## Issues Encountered

### No API breakage from DotNetWorkQueue 0.9.14 to 0.9.31

Research predicted zero breakage on the 10 API touch-points, and that proved exactly correct. The build produced zero warnings (with TreatWarningsAsErrors active) across both net10.0 and net8.0 TFMs. No edits were needed in any .cs source file.

### WSL solution-level build race (pre-existing, not introduced)

Symptom: `dotnet build .sln -c Release` fails with MSB3021 after successfully compiling both projects. The test project bin/Release directory is created during compilation but the copy step races with directory creation on the WSL/Windows filesystem bridge. Building library and test project separately is the reliable workaround on this machine.

---

## Verification Results

### 1. dotnet restore

```
Restored DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj (in 357 ms).
Restored ...Tests.csproj (in 357 ms).
```

Exit 0.

### 2. dotnet build -c Release (per project)

Main library:
```
Build succeeded.  0 Warning(s)  0 Error(s)
Targets: net10.0 and net8.0
```

Test project:
```
Build succeeded.  0 Warning(s)  0 Error(s)
Target: net8.0
```

### 3. dotnet test -c Release --no-build

```
Passed!  - Failed: 0, Passed: 3, Skipped: 0, Total: 3, Duration: 18 s
```

### Acceptance criteria greps

```
<TargetFrameworks>net10.0;net8.0</TargetFrameworks>
<PackageReference Include="DotNetWorkQueue" Version="0.9.31" />
```

No net48 or net472 strings remain in the csproj.
