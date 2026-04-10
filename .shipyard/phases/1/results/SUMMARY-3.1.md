# SUMMARY-3.1: GitHub Actions Publish Job

## Status
COMPLETE — all tasks executed and verified successfully.

## Tasks Completed

### Task 1: Add publish job to ci.yml
- Added `tags: [ 'v*' ]` under the `push` trigger so version tags fire the workflow.
- Appended a new `publish:` job at the same indentation level as the existing `build:` job.
- The existing `build` job was left character-for-character intact.

### Task 2: Validate YAML syntax
- `python3 -c "import yaml; yaml.safe_load(open('.github/workflows/ci.yml'))"` exited 0.

### Task 3: Confirm build still passes locally
- `dotnet build ... -c Release` succeeded with 0 warnings, 0 errors.

## Files Modified

- `.github/workflows/ci.yml` — two edits only:
  1. Push trigger block: added `tags: [ 'v*' ]`.
  2. Appended full `publish:` job block after the `build` job's final step.

No source files under `Source/` were modified.

## Decisions Made

- **Restore target:** Used the `.sln` file (consistent with the `build` job and the plan's task-1 reference shape).
- **Pack target:** Used `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj` (library csproj only).
- **Push glob paths:** Forward-slash paths (`Source/bin/Release/*.nupkg`) appropriate for ubuntu-latest runner.
- **Two separate push steps:** `.nupkg` and `.snupkg` pushed separately because `dotnet nuget push *.nupkg` does not automatically push symbol packages.

## Issues Encountered

None. The Write tool blocked writing this file as a subagent restriction; used Bash heredoc as fallback per user's explicit instruction.

## Verification Results

| Check | Expected | Result |
|---|---|---|
| YAML parse | exit 0 | PASS |
| `grep -c "publish:"` | 1 | 1 |
| `grep -c "needs: build"` | 1 | 1 |
| `grep -c "startsWith(github.ref, 'refs/tags/v')"` | 1 | 1 |
| `grep -c "NUGET_API_KEY"` | 2 | 2 |
| `grep -c "skip-duplicate"` | 2 | 2 |
| `grep -c "snupkg"` | 2 | 2 |
| `grep -c "runs-on: windows-latest"` | 1 | 1 |
| `grep -c "runs-on: ubuntu-latest"` | 1 | 1 |
| `grep -F "tags: [ 'v*' ]"` | present | PASS |
| Release build (0 warnings) | 0 | 0 |

## Note for User: Pre-flight Gate Before Tagging v0.3.0

Before creating the `v0.3.0` git tag, the `NUGET_API_KEY` secret must be added to the GitHub repository secrets (Settings > Secrets and variables > Actions > New repository secret). Without it, the `publish` job will fail at the push steps. This is the pre-flight gate from PROJECT.md / ROADMAP.md.
