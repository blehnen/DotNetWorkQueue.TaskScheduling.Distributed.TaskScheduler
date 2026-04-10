# Plan 2.1: NuGet Packaging Metadata and SourceLink

## Context
Add the full NuGet packaging metadata block to the main library csproj so a `dotnet pack -c Release` produces a valid `.nupkg` + `.snupkg` pair ready for nuget.org. Pattern is ported from `expression-json-serializer` with two deviations: `LGPL-2.1-or-later` instead of MIT, and `Microsoft.SourceLink.GitHub` `10.0.201` instead of `8.0.0`. Also adds the repo-root `README.md` as a packed file using the relative path `..\README.md` (the csproj sits directly in `Source/`, one level below the repo root).

This plan modifies the same csproj as PLAN-1.1, so it MUST run after Wave 1. It does NOT bump `<Version>` — that is PLAN-3.2's responsibility so the version bump lands with the matching CHANGELOG entry.

**Git strategy is manual. Do NOT run `git commit`, `git push`, `git tag`, or any history-mutating command. Leave changes staged/unstaged for the user to review and commit.**

## Non-Goals
- No version bump (PLAN-3.2)
- No README.md authoring (parallel PLAN-2.2)
- No CI workflow edits (PLAN-3.1)
- No CHANGELOG or CLAUDE.md edits (PLAN-3.2)
- No behavioral changes, no new tests
- Do NOT touch the `<TargetFrameworks>` line (PLAN-1.1 already finalized it)

## Dependencies
Wave 1 complete: PLAN-1.1 (csproj TFM drop + DotNetWorkQueue 0.9.31) and PLAN-1.2 (stability fixes) must be done. This plan depends on PLAN-1.1 specifically because both edit the same csproj.

## Tasks

### Task 1: Add NuGet metadata PropertyGroup entries
**Files:** `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj`
**Action:** modify
**Description:**
Add the following properties inside the existing main `<PropertyGroup>` (or a new adjacent one) in the csproj. Preserve existing entries (`Version`, `Company`, `Copyright`, `Product`, `GenerateDocumentationFile`, `TreatWarningsAsErrors`, `RootNamespace`, `AssemblyName`, `TargetFrameworks`). Add these new entries:

```xml
<PackageId>DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler</PackageId>
<Authors>Brian Lehnen</Authors>
<Description>A replacement task scheduler for DotNetWorkQueue that coordinates worker thread pool counts across multiple processes on the same machine. Processes sharing a configured UDP broadcast port discover each other via NetMQ beacons and exchange task counts over a P2P bus, keeping the combined concurrency below each scheduler's configured maximum.</Description>
<PackageLicenseExpression>LGPL-2.1-or-later</PackageLicenseExpression>
<PackageProjectUrl>https://github.com/blehnen/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler</PackageProjectUrl>
<RepositoryUrl>https://github.com/blehnen/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler</RepositoryUrl>
<RepositoryType>git</RepositoryType>
<PackageReadmeFile>README.md</PackageReadmeFile>
<NoWarn>CS1591</NoWarn>
<Deterministic>true</Deterministic>
<ContinuousIntegrationBuild Condition="'$(CI)' == 'true'">true</ContinuousIntegrationBuild>
<IncludeSymbols>true</IncludeSymbols>
<SymbolPackageFormat>snupkg</SymbolPackageFormat>
<PublishRepositoryUrl>true</PublishRepositoryUrl>
<EmbedUntrackedSources>true</EmbedUntrackedSources>
```

Do NOT modify `<Version>` — it must remain `0.2.1` until PLAN-3.2. Do NOT overwrite or duplicate any existing properties.

**Acceptance Criteria:**
- `grep -c "PackageLicenseExpression>LGPL-2.1-or-later<" Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj` returns `1`
- `grep -c "PackageId>DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler<" Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj` returns `1`
- `grep -c "SymbolPackageFormat>snupkg<" Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj` returns `1`
- `grep -c "Deterministic>true<" Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj` returns `1`
- `grep -c "ContinuousIntegrationBuild" Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj` returns `1`
- `grep "Version>" Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj` still shows `0.2.1` (unchanged; PLAN-3.2 will bump)
- Neither `MIT` nor `LGPL-3` appears anywhere in the csproj

### Task 2: Add SourceLink PackageReference and README pack ItemGroup
**Files:** `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj`
**Action:** modify
**Description:**
1. Add a new `PackageReference` to `Microsoft.SourceLink.GitHub` version `10.0.201` with `PrivateAssets="All"`. Place it alongside the existing `DotNetWorkQueue` and `NetMQ` references (in a matching or new `<ItemGroup>`):

```xml
<PackageReference Include="Microsoft.SourceLink.GitHub" Version="10.0.201" PrivateAssets="All" />
```

2. Add a new `<ItemGroup>` (or append to an existing one) with the README pack directive. The csproj lives one directory level below the repo root (directly in `Source/`), so `..\README.md` resolves to the repo-root README:

```xml
<ItemGroup>
  <None Include="..\README.md" Pack="true" PackagePath="\" />
</ItemGroup>
```

Do NOT use `8.0.0` for SourceLink. Do NOT set `PrivateAssets="all"` (lowercase) — use `All` to match the expression-json-serializer convention.

**Acceptance Criteria:**
- `grep -c "Microsoft.SourceLink.GitHub\" Version=\"10.0.201\"" Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj` returns `1`
- `grep -c "\\.\\.\\\\README.md" Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj` returns `1`
- `grep "SourceLink.GitHub" Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj` does NOT contain `8.0.0`
- `dotnet restore` succeeds and resolves `Microsoft.SourceLink.GitHub` 10.0.201

### Task 3: Verify pack produces nupkg and snupkg with correct metadata
**Files:** no file edits; verification only
**Action:** verify
**Description:**
Run a Release build and pack to confirm the packaging metadata is well-formed and produces both package artifacts. Inspect the resulting `.nupkg` for the expected `.nuspec` contents. Note: README.md may not exist yet if PLAN-2.2 has not run — if pack fails solely on the missing README.md file (error `NU5039` or similar), that is acceptable for this task as long as the csproj XML is valid; rerun the verification after PLAN-2.2 lands. If PLAN-2.2 is already complete, the pack must succeed.

```bash
dotnet restore Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.sln
dotnet build Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.sln -c Release
dotnet pack Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj -c Release --no-build
```

If README.md exists (PLAN-2.2 done), assert the `.nupkg` contains the readme and its `.nuspec` declares `LGPL-2.1-or-later`. Otherwise, assert the csproj XML parses and `dotnet build -c Release` produces zero warnings.

**Acceptance Criteria:**
- `dotnet build -c Release` produces zero warnings, zero errors
- If README.md exists: a `.nupkg` file appears under `Source/bin/Release/` with version `0.2.1` (the version bump happens in PLAN-3.2)
- If README.md exists: a matching `.snupkg` file also appears
- If README.md exists: `unzip -p <path-to-nupkg> DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.nuspec | grep -c "LGPL-2.1-or-later"` returns `1`
- The 3 integration tests still pass (`dotnet test`)

## Verification
```bash
cd /mnt/f/git/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler
dotnet restore Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.sln
dotnet build Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.sln -c Release
dotnet pack Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj -c Release --no-build
ls Source/bin/Release/*.nupkg
ls Source/bin/Release/*.snupkg
grep "PackageLicenseExpression" Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj
```
Build must produce zero warnings. Pack must produce `.nupkg` and `.snupkg` (once README.md is in place from PLAN-2.2). License expression must be `LGPL-2.1-or-later`.
