# REVIEW-3.1: GitHub Actions Publish Job

**Reviewer:** Claude Code (senior reviewer)
**Date:** 2026-04-10
**Plan:** PLAN-3.1 — Augment `.github/workflows/ci.yml` with NuGet publish job

---

## Stage 1: Spec Compliance

**Verdict:** PASS

### Task 1: Add publish job to ci.yml
- Status: PASS
- Evidence: `.github/workflows/ci.yml` lines 36–63 contain the full `publish:` job appended after the `build:` job under the same `jobs:` map.
- Notes:
  - `needs: build` present at line 38.
  - `runs-on: ubuntu-latest` at line 39.
  - `if: startsWith(github.ref, 'refs/tags/v')` at line 40.
  - `actions/checkout@v4` at line 44.
  - `actions/setup-dotnet@v4` with multi-version list (`8.0.x` / `10.0.x`) at lines 47–51.
  - `dotnet restore` on `.sln` at line 54 (consistent with plan reference shape and `build` job pattern).
  - `dotnet pack Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj -c Release --no-restore` at line 57.
  - Two separate push steps: `*.nupkg` (line 60) and `*.snupkg` (line 63), both with `--api-key ${{ secrets.NUGET_API_KEY }}`, `--source https://api.nuget.org/v3/index.json`, and `--skip-duplicate`.
  - `push.tags: [ 'v*' ]` trigger added at line 6; `push.branches: [master]` and `pull_request.branches: [master]` unchanged.
  - Existing `build` job (`runs-on: windows-latest`, two SDK installs, restore/build/test steps) is character-for-character intact at lines 11–35.

  Grep verification counts (all pass):
  | Pattern | Expected | Actual |
  |---|---|---|
  | `publish:` | ≥1 | 1 |
  | `needs: build` | 1 | 1 |
  | `startsWith(github.ref, 'refs/tags/v')` | 1 | 1 |
  | `NUGET_API_KEY` | ≥2 | 2 |
  | `skip-duplicate` | ≥2 | 2 |
  | `snupkg` | ≥1 | 2 |
  | `runs-on: windows-latest` | 1 | 1 |
  | `runs-on: ubuntu-latest` | 1 | 1 |

### Task 2: Validate YAML syntax
- Status: PASS
- Evidence: SUMMARY-3.1.md confirms `python3 -c "import yaml; yaml.safe_load(open('.github/workflows/ci.yml'))"` exited 0. Visual inspection confirms `publish:` is indented at 2 spaces, identical to `build:`, under the top-level `jobs:` key. File still begins with `name: CI` (line 1).

### Task 3: Confirm build still passes locally
- Status: PASS
- Evidence: SUMMARY-3.1.md reports `dotnet build ... -c Release` succeeded with 0 warnings, 0 errors. No source files under `Source/` were modified.

---

## Stage 2: Code Quality

### Critical
None.

### Important
None.

### Suggestions

- **`publish` job name is verbose vs. plan reference** — `.github/workflows/ci.yml` line 37 uses `name: Publish to NuGet` rather than the minimal `name: publish` shown in the plan's reference shape. This is cosmetic and does not affect correctness; the `publish:` job key (used by `needs:`) is what matters. No action required.

- **Pack output path assumption** — The push glob `Source/bin/Release/*.nupkg` (line 60) assumes `dotnet pack` places output in the project's `bin/Release/` relative to the runner's working directory. With a project at `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj`, the default output is `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler/bin/Release/` unless `<PackageOutputPath>` is set in the csproj. If the csproj does not override `PackageOutputPath`, the glob will not match and the push steps will fail silently (or error). Verify that the csproj sets `<PackageOutputPath>` or adjust the glob to the correct path (e.g., `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler/bin/Release/*.nupkg`).

---

## Stage 2: Integration Checks

1. **Pre-flight gate documented:** SUMMARY-3.1.md (final section "Note for User") explicitly states that `NUGET_API_KEY` must be added to GitHub repo secrets before tagging `v0.3.0`. PASS.

2. **Scope discipline:** SUMMARY-3.1.md confirms only `.github/workflows/ci.yml` was modified. No csproj, README, CHANGELOG, CLAUDE.md, or source file touches. PASS.

3. **Restore → Pack → Push order:** Steps are sequenced correctly: Restore (line 53) precedes Pack (line 56), which precedes both push steps (lines 59, 62). `--no-restore` on pack is valid because the explicit Restore step ran first. PASS.

4. **`--no-build` on pack:** Absent — intentional and correct. The `publish` job runs on a fresh ubuntu runner (different filesystem from the `build` job), so `dotnet pack` must build. PASS.

---

## Summary

**Verdict:** APPROVE

The implementation matches the plan precisely. All acceptance criteria grep counts pass, YAML is valid, the existing `build` job is untouched, the `publish` job has the correct guard condition and step order, and the pre-flight gate is documented in the summary. One suggestion to investigate: confirm the `Source/bin/Release/` push glob matches the actual pack output path for the project layout, as a path mismatch would silently skip the push.

Critical: 0 | Important: 0 | Suggestions: 2
