# Research: v0.3.0 NuGet Release — Dependency Upgrade, Stability Fixes, Packaging, CI

**Status: COMPLETE — Part A (codebase) and Part B (external) both populated**

---

## Summary of Findings

The four work areas for v0.3.0 are mechanically clear. **Risk turned out to be lower than initially feared.** DotNetWorkQueue 0.9.14 → **0.9.31** crosses three `**Breaking**` markers, but all of them are in unrelated subsystems (TFM drop, job scheduling / cron, dashboard UI). The 10 API touch-points this library consumes (`SmartThreadPoolTaskScheduler`, `ATaskScheduler`, `IContainer`, `ITaskSchedulerConfiguration`, `IWaitForEventOrCancelThreadPool`, `IMetrics`, `LifeStyles`, `Guard`, `SetWaitHandle`, `MaximumConcurrencyLevel`) have no documented breakage in that range. Expected upgrade outcome: mechanical version bump + recompile, zero or near-zero source edits in TaskSchedulerMultiple/TaskSchedulerSetup/TaskSchedulerJobCountSync.

**Key upgrade fact:** DotNetWorkQueue 0.9.19 dropped `net48` and `netstandard2.0`. Our TFM drop is therefore **mandatory**, not cosmetic — referencing 0.9.31 with a `net48` or `net472` target in our csproj is a compile error.

**Stability fixes are trivial one-liners** with precise file:line targets identified. **Packaging metadata** is a straightforward port from expression-json-serializer with two deviations: LGPL-2.1-or-later instead of MIT, and SourceLink 10.0.201 instead of 8.0.0. **CI augmentation** adds a ubuntu-latest `publish` job to the existing `ci.yml` workflow, triggered on `v*` tags, `needs: build`.

---

## Work Area 1 — Dependency & TFM Upgrade

### Current state (from csproj)

**File:** `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj`

```xml
<TargetFrameworks>net10.0;net8.0;net48;net472;</TargetFrameworks>
<Version>0.2.1</Version>
<PackageReference Include="DotNetWorkQueue" Version="0.9.14" />
<PackageReference Include="NetMQ" Version="4.0.2.2" />
```

**Required change:** `<TargetFrameworks>net10.0;net8.0</TargetFrameworks>` (drop `net48` and `net472`).

**Test project:** `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.Tests/`
- `<TargetFramework>net8.0</TargetFramework>` — unchanged per CONTEXT-1.md decision
- PackageReferences: `Microsoft.NET.Test.Sdk 17.*`, `xunit 2.*`, `xunit.runner.visualstudio 2.*`, `NSubstitute 5.*`, `coverlet.collector 6.*`

**No Directory.Build.props exists** in the repository. Version is set directly in the main csproj.

### DotNetWorkQueue API surface at risk

From reading `TaskSchedulerMultiple.cs` and `TaskSchedulerSetup.cs`, the following DotNetWorkQueue types are consumed:

| Type | How used | File |
|------|----------|------|
| `SmartThreadPoolTaskScheduler` | Base class — `TaskSchedulerMultiple` extends it | `TaskSchedulerMultiple.cs` line 30 |
| `ATaskScheduler` | Registration target in DI — `container.Register<ATaskScheduler, TaskSchedulerMultiple>` | `TaskSchedulerSetup.cs` line 42 |
| `ITaskSchedulerConfiguration` | Constructor parameter of `TaskSchedulerMultiple` | `TaskSchedulerMultiple.cs` line 43 |
| `IWaitForEventOrCancelThreadPool` | Constructor parameter of `TaskSchedulerMultiple` | `TaskSchedulerMultiple.cs` line 43 |
| `IMetrics` | Constructor parameter of `TaskSchedulerMultiple` | `TaskSchedulerMultiple.cs` line 43 |
| `IContainer` | Extension method receiver of `InjectDistributedTaskScheduler` | `TaskSchedulerSetup.cs` line 38 |
| `LifeStyles` | Used for DI lifetime constants (`.Singleton`) | `TaskSchedulerSetup.cs` lines 40–45 |
| `DotNetWorkQueue.Validation.Guard` | Null-check utility | `TaskSchedulerMultiple.cs` line 22 |
| `SetWaitHandle(null)` | Protected method called on base — must remain accessible | `TaskSchedulerMultiple.cs` line 126 |
| `MaximumConcurrencyLevel` | Property on base class | `TaskSchedulerMultiple.cs` line 79 |

These 10 API touch-points are the full breakage surface. If any of these were renamed, removed, or had signatures changed between 0.9.14 and the latest stable, the upgrade requires corresponding edits.

### External findings

- **Latest stable DotNetWorkQueue:** **0.9.31** (published 2026-04-09 on nuget.org; confirmed via `gh api` against the `blehnen/DotNetWorkQueue` releases list).
- **Latest stable NetMQ:** **not verified** at research time — nuget.org fetch was unavailable and the NetMQ source repo was not polled. NetMQ 4.x has historically been very stable (releases years apart). **Action for architect/builder:** keep the upgrade as "bump to latest stable NetMQ at build time" and let the builder verify via `dotnet list package --outdated` or equivalent. If no newer stable exists, leave `4.0.2.2` in place. Do not block the plan on this.
- **Microsoft.SourceLink.GitHub:** latest stable is **10.0.201** (confirmed via nuget.org fetch). The expression-json-serializer reference uses `8.0.0`, which is older. **Use `10.0.201` in the new csproj**, not `8.0.0`.

### DotNetWorkQueue release notes between 0.9.14 and 0.9.31 — API breakage assessment

Pulled from the upstream CHANGELOG.md via `gh api`. Four releases to audit:

| Release | Date | Affects our API surface? |
|---------|------|--------------------------|
| **0.9.19** | 2026-04-07 | **YES (drives the TFM drop)** — "Drop .NET Framework 4.8 and .NET Standard 2.0 targets; now targets .NET 10.0 and .NET 8.0 only. Remove `#if NETFULL` / `#if NETSTANDARD2_0`." This is the upstream change that makes our `net48`/`net472` drop mandatory. Our csproj cannot keep net48/net472 TFMs once we reference DotNetWorkQueue 0.9.19 or later. |
| **0.9.30** | 2026-04-08 | **NO (job scheduling, not task scheduling)** — Breaking changes are in `IJobSchedule.Previous()` (return type `DateTimeOffset?`), cron format migration from Schyntax to Cronos, and heartbeat/job schedule strings. This library does NOT reference `IJobSchedule`, `JobScheduler`, Schyntax, Cronos, or heartbeat strings. `TaskSchedulerMultiple` inherits from `SmartThreadPoolTaskScheduler`, which is a **thread-pool task scheduler**, a completely separate subsystem from **job scheduling**. Zero impact. |
| **0.9.31** | 2026-04-09 | **NO (dashboard UI)** — Breaking changes are all in `DashboardApi` config (`BaseUrl`/`ApiKey` → `Sources[]` array, URL routing). This library has no dashboard dependency. Zero impact. |
| 0.9.15–0.9.18 | — | **NO** — Dashboard fixes, SourceLink adoption upstream, minor fixes. Zero impact on our API surface. |

**Conclusion:** Despite crossing four releases and three `**Breaking**` markers, **none of the DotNetWorkQueue 0.9.14 → 0.9.31 breaking changes touch the 10 API points listed above**. The upgrade is expected to be mechanical: bump the version number, confirm the 10 types still exist with the same signatures, and fix any compile errors (expected: zero or near-zero).

The single *indirect* consequence is TFM drop (from 0.9.19), which is already in our plan.

**Bonus observation (not a requirement, just useful):** DotNetWorkQueue 0.9.18 itself adopted "Deterministic builds, portable debug symbols, `.snupkg` symbol packages, Source Link via `Microsoft.SourceLink.GitHub`" — the exact same packaging pattern we are porting. This means our package's SourceLink will work transitively with the DotNetWorkQueue package in a debugger session; a consumer stepping through will reach correct sources on both packages.

---

## Work Area 2 — Stability Fixes

Both fixes are mechanical one-liners. No design decisions required.

### Fix 1 — `int.Parse` → `int.TryParse` in `OnBeaconReady`

**File:** `Source/TaskSchedulerBus.cs`
**Location:** Line 171 (method declaration), line 176 (the offending call)

```csharp
// Line 171
private void OnBeaconReady(object sender, NetMQBeaconEventArgs e)
{
    // we got another beacon
    // let's check if we already know about the beacon
    var message = _beacon.Receive();
    var port = int.Parse(message.String);   // LINE 176 — must become int.TryParse
    var node = new NodeKey(message.PeerHost, port);
```

The fix: replace with `int.TryParse(message.String, out var port)` and return early if parsing fails (optionally logging the malformed payload). The surrounding context confirms the exception would propagate through the NetMQ poller and silently kill the actor — there is no catch at this level.

### Fix 2 — `volatile` on `_stopRequested` and `_running`

**File:** `Source/TaskSchedulerJobCountSync.cs`
**Location:** Lines 36–37 (field declarations)

```csharp
private bool _stopRequested;   // LINE 36
private bool _running;         // LINE 37
```

The fix: add `volatile` keyword to both declarations. The fields are read/written from multiple threads (confirmed by grep: `_running` set at line 131, read at line 258; `_stopRequested` set at line 242, read at lines 153 and 179).

---

## Work Area 3 — NuGet Packaging

### Current csproj metadata (complete inventory)

The main csproj currently has:
- `Version` — 0.2.1
- `Company` — Brian Lehnen
- `Copyright` — Copyright © Brian Lehnen 2019-2026
- `Product` — DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler
- `GenerateDocumentationFile` — true
- `TreatWarningsAsErrors` — true
- `RootNamespace` — DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler
- `AssemblyName` — DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler

**Missing** (must be added for NuGet.org publication):
`PackageId`, `Authors`, `Description`, `PackageLicenseExpression`, `PackageProjectUrl`, `RepositoryUrl`, `RepositoryType`, `PackageReadmeFile`, `NoWarn CS1591`, `Deterministic`, `ContinuousIntegrationBuild`, `IncludeSymbols`, `SymbolPackageFormat`, `PublishRepositoryUrl`, `EmbedUntrackedSources`

**New PackageReference to add:**
`Microsoft.SourceLink.GitHub` **version `10.0.201`** (confirmed latest stable on nuget.org). Note: the expression-json-serializer reference csproj uses `8.0.0`, which is outdated — do not copy that version literally; use `10.0.201`.

### README path verification (CORRECTED)

**Actual source layout is FLAT, not nested.** The csproj lives directly at:

```
Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj
```

All `.cs` source files live directly under `Source/` (e.g. `Source/TaskSchedulerBus.cs`, `Source/TaskSchedulerJobCountSync.cs`). The only subdirectory is `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.Tests/` for the test project.

Therefore the csproj is **1 directory level** below repo root (inside `Source/`). The pack item path is:

```xml
<None Include="..\README.md" Pack="true" PackagePath="\" />
```

`..` from csproj directory (`Source/`) = repo root, where `README.md` lives.

**Confirmed: `..\README.md` is the correct relative path. NOT `..\..\README.md`.**

### Current README.md summary

The existing `README.md` at repo root is already substantive (83 lines). It contains: overview of the problem (forked child processes sharing thread limits), a features list, a limitations section, a usage example showing `container.InjectDistributedTaskScheduler(1234)`, build and test commands, license statement, and third-party library credits. It does NOT currently have NuGet install instructions (`dotnet add package ...`). The PROJECT.md scope calls for replacing it with a NuGet-oriented readme — the existing content covers the required topics (what it is, when to use it, quick-start, UDP port convention, license) and can serve as the basis.

### CHANGELOG.md format

The changelog uses a flat `### version date` header style with bullet points. Two most recent entries verbatim:

```
### 0.2.1 2026-04-05

* Add `BeaconInterface` to `TaskSchedulerMultipleConfiguration` and `InjectDistributedTaskScheduler` (default `"loopback"`). Pass `""` on Linux — NetMQ's `"loopback"` mode binds to 127.0.0.1 but sends to 255.255.255.255, which Linux won't loop back to a loopback-bound socket, so discovery silently fails
* Fall back to `127.0.0.1` in `GetHostAddress` when `NetMQBeacon.HostName` is empty (reverse DNS failure on CI/WSL crashed URI parsing)
* Bump DotNetWorkQueue from 0.9.0 to 0.9.14
* Add Jenkinsfile with XPlat code coverage and Codecov upload
* Add `coverlet.collector` to the test project
* Add `.gitattributes` to stop line-ending drift between Windows and WSL edits

### 0.2.0 2026-03-05

* Modernize to SDK-style csproj with PackageReference (removes packages.config, app.config, AssemblyInfo.cs)
* Multi-target net10.0, net8.0, net48, and net472
* Add integration tests (xUnit, net8.0) covering local counting, two-node sync, and three-node sync
* Add AppVeyor CI (appveyor.yml)
```

The 0.3.0 entry must follow the same `### 0.3.0 YYYY-MM-DD` format with `*` bullet items.

### SourceLink.GitHub version

**Confirmed: `Microsoft.SourceLink.GitHub` latest stable = `10.0.201`.** Use this in the new csproj. The expression-json-serializer reference's `8.0.0` is outdated.

---

## Work Area 4 — CI Augmentation

### Current CI workflow facts

**File:** `.github/workflows/ci.yml`
- Workflow name: `CI`
- Single job named: `build`
- Runs on: `windows-latest`
- Triggers: push/PR to `master`
- Steps: checkout → `actions/setup-dotnet@v4` (installs 8.0.x and 10.0.x) → display version → restore → build (Debug, no-restore) → test (no-build, no matrix)
- **No `net48`/`net472` references in the workflow** — nothing to remove there.
- No pack or publish steps exist.

### Required additions

A `publish` job must be added:
- Trigger guard: `if: startsWith(github.ref, 'refs/tags/v')`
- `needs: build`
- `runs-on: ubuntu-latest` (pack is OS-agnostic; matches expression-json-serializer pattern per CONTEXT-1.md)
- Sets up .NET 8.0.x and 10.0.x SDKs
- Steps: restore → pack `-c Release --no-restore` against the main csproj → push `*.nupkg` and `*.snupkg` to nuget.org via `NUGET_API_KEY` secret with `--skip-duplicate`

The existing `build` job name is `build`. The `publish` job's `needs:` value must reference `build` exactly.

**Pre-flight gate (not a code change):** The user must add `NUGET_API_KEY` to GitHub repository secrets before tagging `v0.3.0`.

---

## Part B — External Lookups (COMPLETE)

| Item | Status | Result |
|------|--------|--------|
| Latest stable DotNetWorkQueue version | DONE | **0.9.31** (nuget.org / `gh api` releases) |
| Latest stable NetMQ version | DEFERRED | Not fetched (nuget.org unavailable; NetMQ 4.x very stable; builder to verify at build time) |
| Microsoft.SourceLink.GitHub latest stable | DONE | **10.0.201** (nuget.org) |
| DotNetWorkQueue breaking changes since 0.9.14 | DONE | **None affecting our API surface.** All breaking changes in 0.9.19 / 0.9.30 / 0.9.31 are in TFM targets, job scheduling, and dashboard UI — none touch `SmartThreadPoolTaskScheduler`, `ATaskScheduler`, `IContainer`, `ITaskSchedulerConfiguration`, `IWaitForEventOrCancelThreadPool`, or `IMetrics`. Upgrade expected to be mechanical. |

---

## Open Questions / Gotchas for the Architect

1. **DotNetWorkQueue API breakage** — The 10 API touch-points listed in Work Area 1 must be verified against the latest release. If `SmartThreadPoolTaskScheduler`'s constructor signature changed (it currently takes `ITaskSchedulerConfiguration`, `IWaitForEventOrCancelThreadPool`, `IMetrics`, `ILogger`), the `TaskSchedulerMultiple` constructor must be updated to match. This is the highest-risk item.

2. **`SetWaitHandle` accessibility** — `TaskSchedulerMultiple.JobCountHasChanged` calls `SetWaitHandle(null)` which is a protected method on the base `SmartThreadPoolTaskScheduler`. If upstream changed this method's visibility or signature, it is a compile error.

3. **No `Directory.Build.props`** — All NuGet metadata goes directly in the main csproj's `<PropertyGroup>`. The architect should not reference a shared props file — it does not exist.

4. **Existing README is reusable** — The current `README.md` already covers the required topics for the NuGet package readme. The architect's plan should update it (add NuGet install instructions, drop build/test commands or move them to a dev section) rather than write from scratch.

5. **CI workflow build job is named `build`** — The `publish` job's `needs:` field must be `needs: build` (lowercase, no spaces).

6. **`net48`/`net472` entries in the `obj/` directory** — The glob found `Source/obj/Debug/net472/` and `Source/obj/Debug/net48/` directories with generated `.cs` files. These are build artifacts, not tracked source. The architect's plan need not delete them; they disappear on next clean build.
