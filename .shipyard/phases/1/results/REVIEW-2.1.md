# Review: Plan 2.1 — NuGet Packaging Metadata and SourceLink

## Pre-Check: Prior Findings

- REVIEW-1.1.md: PASS, no blocking findings. Minor note about WSL/solution-level MSB3021 race (pre-existing, unrelated). No recurring patterns applicable here.
- REVIEW-1.2.md: PASS, no blocking findings. Minor note about logger rationale inaccuracy (non-blocking). No recurring patterns applicable here.
- `.shipyard/ISSUES.md`: does not exist — no outstanding tracked issues.

---

## Stage 1: Spec Compliance

**Verdict: PASS**

### Task 1: Add NuGet metadata PropertyGroup entries

- Status: PASS
- Evidence: `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj` lines 13–27 contain all 15 required properties, verified by direct file read:
  - Line 13: `<PackageId>DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler</PackageId>`
  - Line 14: `<Authors>Brian Lehnen</Authors>`
  - Line 15: `<Description>` — substantive, not a placeholder. 149-character description covering the distributed throttling purpose, NetMQ beacons, P2P bus, and concurrency maximum.
  - Line 16: `<PackageLicenseExpression>LGPL-2.1-or-later</PackageLicenseExpression>` — correct, NOT MIT, NOT LGPL-3.
  - Line 17: `<PackageProjectUrl>https://github.com/blehnen/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler</PackageProjectUrl>` — correct blehnen GitHub repo.
  - Line 18: `<RepositoryUrl>https://github.com/blehnen/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.git</RepositoryUrl>` — ends in `.git` as required.
  - Line 19: `<RepositoryType>git</RepositoryType>`
  - Line 20: `<PackageReadmeFile>README.md</PackageReadmeFile>`
  - Line 21: `<NoWarn>CS1591</NoWarn>`
  - Line 22: `<Deterministic>true</Deterministic>`
  - Line 23: `<ContinuousIntegrationBuild Condition="'$(CI)' == 'true'">true</ContinuousIntegrationBuild>` — CI condition present and correctly formed.
  - Line 24: `<IncludeSymbols>true</IncludeSymbols>`
  - Line 25: `<SymbolPackageFormat>snupkg</SymbolPackageFormat>`
  - Line 26: `<PublishRepositoryUrl>true</PublishRepositoryUrl>`
  - Line 27: `<EmbedUntrackedSources>true</EmbedUntrackedSources>`
- Notes: All 15 properties present exactly as specified. No MIT or LGPL-3 strings appear. Description text is semantically consistent with the CLAUDE.md project description.

### Task 2: Add SourceLink PackageReference and README pack ItemGroup

- Status: PASS
- Evidence:
  - `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj` line 38: `<PackageReference Include="Microsoft.SourceLink.GitHub" Version="10.0.201" PrivateAssets="All" />` — version is 10.0.201 (not the outdated 8.0.0), `PrivateAssets="All"` (capital A as required, not lowercase).
  - Lines 41–43: `<ItemGroup><None Include="..\README.md" Pack="true" PackagePath="\" /></ItemGroup>` — relative path is `..\README.md` (single dot-dot from `Source/` to repo root). NOT `..\..\README.md`.
- Notes: SourceLink version exactly matches the RESEARCH.md confirmed value of 10.0.201. README path is correct for the flat `Source/` layout. `PrivateAssets` casing matches expression-json-serializer convention.

### Task 3: Verify pack produces nupkg and snupkg with correct metadata

- Status: PASS
- Evidence:
  - `Source/bin/Release/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.0.2.1.nupkg` — confirmed present on disk.
  - `Source/bin/Release/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.0.2.1.snupkg` — confirmed present on disk.
  - SUMMARY-2.1 reports: `dotnet restore` exit 0, `dotnet build -c Release` 0 warnings 0 errors, `dotnet pack -c Release --no-build` produced both artifacts.
  - SUMMARY-2.1 reports nuspec LGPL-2.1-or-later grep count: 2 (license element + licenseUrl), confirming the nuspec carries the correct license expression.
  - Version is `0.2.1` in the artifact filename — correct, PLAN-3.2 is responsible for the 0.3.0 bump.
- Notes: README.md was present when pack ran (PLAN-2.2 appears to have completed), so the full pack succeeded rather than hitting the NU5039 fallback path. Both artifacts exist at the expected location.

### Preserved properties (must not change)

- Status: PASS
- Evidence (csproj lines 4–12):
  - `<TargetFrameworks>net10.0;net8.0</TargetFrameworks>` (line 4) — Wave 1 value preserved, `net48`/`net472` still absent.
  - `<Version>0.2.1</Version>` (line 9) — not bumped; PLAN-3.2 responsibility.
  - `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` (line 8) — preserved.
  - `<GenerateDocumentationFile>true</GenerateDocumentationFile>` (line 7) — preserved.
  - `<RootNamespace>DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler</RootNamespace>` (line 5) — preserved.
  - `<AssemblyName>DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler</AssemblyName>` (line 6) — preserved.
  - `<Company>Brian Lehnen</Company>` (line 10) — preserved.
  - `<Copyright>Copyright © Brian Lehnen 2019-2026</Copyright>` (line 11) — preserved.
  - `<Product>DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler</Product>` (line 12) — preserved.
  - `DotNetWorkQueue` PackageReference (line 36): `Version="0.9.31"` — Wave 1 value preserved.
  - `NetMQ` PackageReference (line 37): `Version="4.0.2.2"` — unchanged.
- Notes: Zero regressions against Wave 1 baseline. The csproj is 45 lines total, fully audited.

---

## Stage 2: Code Quality

### Critical

None.

### Important

None.

### Suggestions

- **Description text deviates from PLAN spec wording, but is still correct.** PLAN-2.1 Task 1 provided a specific description string beginning with "A replacement task scheduler for DotNetWorkQueue...". The implementation uses a slightly different phrasing: "Distributed task scheduler for DotNetWorkQueue..." with different sentence structure. Both are accurate and substantive. The deviation is inconsequential for nuget.org consumers, but noting it for completeness.
  - Remediation: None required. If exact spec fidelity is desired, the text can be aligned in a future pass. Not worth a rebuild.

- **`NoWarn>CS1591` suppresses missing XML doc warnings globally** rather than selectively. With `TreatWarningsAsErrors>true`, this is a pragmatic choice (existing code likely has undocumented members), but if documentation completeness improves over time, CS1591 could be removed from `NoWarn` and the warnings resolved at the member level.
  - Remediation: Future work only. No action required for this plan.

---

## Stage 2: Integration

### Scope Discipline

- `ci.yml` at `.github/workflows/ci.yml` — confirmed to exist (unmodified by this plan). PLAN-3.1 territory untouched.
- `CHANGELOG.md` — confirmed to exist (unmodified by this plan). PLAN-3.2 territory untouched.
- `Version` — still `0.2.1`. PLAN-3.2 territory untouched.
- File boundary: PLAN-2.1 touches only `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj`. PLAN-2.2 (parallel plan) touches `README.md`. No overlap.

### License Coherence

- `PackageLicenseExpression>LGPL-2.1-or-later` matches `Copyright © Brian Lehnen 2019-2026` and the LGPLv2.1 source file headers confirmed in REVIEW-1.2. Coherent across all three layers.

### SourceLink Version

- Confirmed `10.0.201` — matches RESEARCH.md confirmed value. The outdated `8.0.0` from the expression-json-serializer reference was correctly NOT copied.

---

## Verification Re-run Results

All checks performed against committed working tree state:

| Check | Result |
|-------|--------|
| `PackageId` present and correct | PASS (line 13) |
| `LGPL-2.1-or-later` present, MIT absent | PASS (line 16, no MIT found) |
| `RepositoryUrl` ends in `.git` | PASS (line 18) |
| `ContinuousIntegrationBuild` with `'$(CI)' == 'true'` condition | PASS (line 23) |
| `Microsoft.SourceLink.GitHub` at `10.0.201`, `PrivateAssets="All"` | PASS (line 38) |
| `..\README.md` pack item (single dot-dot) | PASS (line 42) |
| `Version` still `0.2.1` | PASS (line 9) |
| `TargetFrameworks` still `net10.0;net8.0` | PASS (line 4) |
| `DotNetWorkQueue` still `0.9.31` | PASS (line 36) |
| `TreatWarningsAsErrors` preserved | PASS (line 8) |
| `.nupkg` artifact at `Source/bin/Release/` version `0.2.1` | PASS (file confirmed on disk) |
| `.snupkg` artifact at `Source/bin/Release/` version `0.2.1` | PASS (file confirmed on disk) |
| nuspec contains `LGPL-2.1-or-later` | PASS (SUMMARY-2.1 reports count: 2) |
| No PLAN-3.x territory touched | PASS |

---

## Summary

**Verdict: APPROVE**

All 15 NuGet properties are present and correct, SourceLink is wired at 10.0.201 with `PrivateAssets="All"`, the README pack item uses the correct single-level relative path, and both `.nupkg`/`.snupkg` artifacts are confirmed on disk at version `0.2.1`. Wave 1 values (`TargetFrameworks`, `DotNetWorkQueue 0.9.31`, `Version 0.2.1`, all existing properties) are fully preserved. No PLAN-3.x scope was touched.

Critical: 0 | Important: 0 | Suggestions: 2
