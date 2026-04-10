# Plan 3.1: GitHub Actions Publish Job

## Context
Augment `.github/workflows/ci.yml` with a tag-triggered `publish` job that packs and pushes `.nupkg` + `.snupkg` to nuget.org. The existing single-job `build` workflow on `windows-latest` stays exactly as it is — no matrix expansion, no Linux addition to the test job (CONTEXT-1.md confirms UDP loopback differences make Linux test expansion out of scope). The `publish` job runs on `ubuntu-latest` because `dotnet pack` is OS-agnostic.

Pre-flight gate (user action, not builder): `NUGET_API_KEY` must be added to GitHub repo secrets before tagging `v0.3.0`. Not plannable here.

**Git strategy is manual. Do NOT run `git commit`, `git push`, `git tag`, or any history-mutating command. Leave changes staged/unstaged for the user to review and commit.**

## Non-Goals
- Do NOT modify the existing `build` job beyond leaving it alone
- Do NOT add a Linux build matrix
- Do NOT touch the tests csproj
- Do NOT add CHANGELOG or CLAUDE.md entries (PLAN-3.2)
- Do NOT bump the version (PLAN-3.2)

## Dependencies
Wave 2 complete (PLAN-2.1 + PLAN-2.2) — the csproj must be pack-ready and the README must exist before the publish job references them.

## Tasks

### Task 1: Add publish job to ci.yml
**Files:** `.github/workflows/ci.yml`
**Action:** modify (append new job)
**Description:**
Append a new `publish` job to the existing workflow, under the top-level `jobs:` map, without touching the existing `build` job. The job must:

- Be guarded with `if: startsWith(github.ref, 'refs/tags/v')` at the job level
- `needs: build`
- `runs-on: ubuntu-latest`
- Check out the repository (`actions/checkout@v4`)
- Set up both .NET 8.0.x and .NET 10.0.x SDKs via `actions/setup-dotnet@v4` (multi-version `dotnet-version` list)
- Run `dotnet restore Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.sln`
- Run `dotnet pack Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj -c Release --no-restore`
- Push all `.nupkg` and `.snupkg` under `Source/bin/Release/` to nuget.org using `dotnet nuget push ... --source https://api.nuget.org/v3/index.json --api-key ${{ secrets.NUGET_API_KEY }} --skip-duplicate`. Use a glob that matches both file types (e.g. two push steps, or a single push with `*.nupkg` which `dotnet nuget push` handles; ensure `.snupkg` symbol packages are also pushed — `dotnet nuget push *.nupkg` does NOT automatically push snupkg, so include an explicit `*.snupkg` push step as well).

Reference shape (illustrative, not literal):

```yaml
  publish:
    name: publish
    needs: build
    if: startsWith(github.ref, 'refs/tags/v')
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: |
            8.0.x
            10.0.x
      - name: Restore
        run: dotnet restore Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.sln
      - name: Pack
        run: dotnet pack Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj -c Release --no-restore
      - name: Push nupkg
        run: dotnet nuget push "Source/bin/Release/*.nupkg" --source https://api.nuget.org/v3/index.json --api-key ${{ secrets.NUGET_API_KEY }} --skip-duplicate
      - name: Push snupkg
        run: dotnet nuget push "Source/bin/Release/*.snupkg" --source https://api.nuget.org/v3/index.json --api-key ${{ secrets.NUGET_API_KEY }} --skip-duplicate
```

The existing `build` job's YAML must be left character-for-character intact except where YAML indentation requires the new job to be appended under the same `jobs:` map.

**Acceptance Criteria:**
- `grep -c "publish:" .github/workflows/ci.yml` returns at least `1`
- `grep -c "needs: build" .github/workflows/ci.yml` returns `1`
- `grep -c "startsWith(github.ref, 'refs/tags/v')" .github/workflows/ci.yml` returns `1`
- `grep -c "runs-on: ubuntu-latest" .github/workflows/ci.yml` returns `1`
- `grep -c "NUGET_API_KEY" .github/workflows/ci.yml` returns at least `2` (once per push step)
- `grep -c "\\-\\-skip-duplicate" .github/workflows/ci.yml` returns at least `2`
- `grep -c "\\*.snupkg" .github/workflows/ci.yml` returns at least `1`
- `grep -c "runs-on: windows-latest" .github/workflows/ci.yml` returns exactly `1` (existing build job untouched)
- `grep -c "net48\|net472" .github/workflows/ci.yml` returns `0`

### Task 2: Validate YAML syntax
**Files:** no file edits; verification only
**Action:** verify
**Description:**
Confirm the modified `ci.yml` is syntactically valid YAML. Use whatever YAML validator is available in the environment: `yamllint .github/workflows/ci.yml`, or a one-liner Python parse (`python3 -c "import yaml,sys; yaml.safe_load(open('.github/workflows/ci.yml'))"`), or `actionlint` if installed. If none are available, a visual inspection of the diff confirming indentation consistency under `jobs:` is acceptable.

**Acceptance Criteria:**
- YAML parser exits 0, OR visual confirmation that the `publish:` job is indented identically to the existing `build:` job under `jobs:`
- The file still begins with `name: CI` (workflow name preserved)
- The existing `build` job definition is byte-identical to before (diff only shows the appended `publish` job)

### Task 3: Confirm build still passes locally
**Files:** none
**Action:** verify
**Description:**
A workflow-only change should not affect local build or tests, but run a smoke check to ensure nothing unrelated was perturbed:

```bash
dotnet build Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.sln -c Release
```

**Acceptance Criteria:**
- Build completes with zero warnings
- No source files under `Source/` were modified by this plan

## Verification
```bash
cd /mnt/f/git/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler
grep -n "publish:" .github/workflows/ci.yml
grep -n "needs: build" .github/workflows/ci.yml
grep -n "startsWith(github.ref, 'refs/tags/v')" .github/workflows/ci.yml
grep -n "NUGET_API_KEY" .github/workflows/ci.yml
grep -n "snupkg" .github/workflows/ci.yml
python3 -c "import yaml; yaml.safe_load(open('.github/workflows/ci.yml'))" && echo "yaml ok"
dotnet build Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.sln -c Release
```
All greps must find the expected matches. YAML parse must succeed. Build must produce zero warnings.
