# Security Audit: Phase 1 — v0.3.0 Modernization & First NuGet Release

**Date:** 2026-04-10
**Auditor:** Security & Compliance Audit Agent
**Branch:** master
**Scope:** 7 uncommitted files (git diff HEAD)

---

## STRIDE Threat Model (Pre-scan Prioritization)

| Threat | Relevant Surface | Priority |
|--------|-----------------|----------|
| Spoofing | Unauthenticated UDP beacon — any local-network process can impersonate a node | Pre-existing; out of scope 0.3.0 |
| Tampering | SetCount messages carry no integrity check; a rogue peer can inject arbitrary counts | Pre-existing; out of scope 0.3.0 |
| Repudiation | No audit log of peer join/leave or count changes | Pre-existing; out of scope 0.3.0 |
| Information Disclosure | NuGet secrets in CI workflow; secrets in committed files | **Audited — see below** |
| Denial of Service | Malformed UDP beacon crashing poller (the `int.Parse` bug) | **Fixed in this phase** |
| Elevation of Privilege | Tag-triggered publish job; overly broad tag glob | **Audited — see below** |

---

## Summary

The Phase 1 changeset is a tightly scoped modernization: two HIGH-severity stability bugs are fixed, NuGet packaging metadata is added, a publish CI job is wired up, and documentation is updated. **No new exploitable vulnerabilities are introduced.** The two most significant security-adjacent improvements are the `int.TryParse` fix (eliminating a remote DoS path via malformed UDP) and the `volatile` fix (eliminating a potential infinite spin on dispose on non-x86 hardware). The NuGet API key is correctly stored as a repository secret and never appears in plaintext. No credentials, private keys, or sensitive data were found in any changed file. `dotnet list package --vulnerable` reports zero vulnerable packages across all direct and transitive dependencies.

---

## Critical Findings

None.

---

## High Findings

None introduced by this phase. The two pre-existing HIGH findings from CONCERNS.md (#1 and #2) are **resolved** by this changeset:

- CONCERNS.md #1 (`_stopRequested`/`_running` not volatile) — **FIXED** by `volatile` keyword addition in `TaskSchedulerJobCountSync.cs`.
- CONCERNS.md #2 (`int.Parse` crashes poller on malformed beacon) — **FIXED** by `int.TryParse` guard in `TaskSchedulerBus.cs`.

---

## Medium Findings

### [M1] Port Number Not Bounds-Checked After `int.TryParse`

- **Location:** `Source/TaskSchedulerBus.cs`, `OnBeaconReady` method (line following the new TryParse block)
- **Description:** `int.TryParse` succeeds for any value in the full `int` range, including negative values and values above 65535. The parsed `port` is passed directly to `new NodeKey(message.PeerHost, port)` without a bounds check.
- **Impact:** A crafted UDP beacon with a payload of `-1` or `99999` would produce a `NodeKey` with an invalid port and be inserted into the node dictionary with that key. The corresponding TCP publisher connection attempt would fail silently, but a sufficient volume of such beacons could pollute the `_otherProcessorCounts` dictionary with entries that never expire. This is a minor resource-exhaustion vector, not a crash. (CWE-20, CWE-400)
- **Remediation:** Add a range check immediately after `TryParse`:
  ```csharp
  if (!int.TryParse(message.String, out var port) || port < 1 || port > 65535)
  {
      return;
  }
  ```
- **Severity note:** This is a residual gap left by the fix scope. The fix as implemented is strictly better than `int.Parse` (no crash); the range check is a hardening step appropriate for a follow-up. Not blocking.

---

## Low / Informational

### [L1] GitHub Actions — No SHA Pinning on Third-Party Actions

- **Location:** `.github/workflows/ci.yml`, lines 15, 16, 43, 47
- **Description:** `actions/checkout@v4` and `actions/setup-dotnet@v4` are pinned to a mutable version tag rather than an immutable commit SHA. A compromised tag at the upstream repo could substitute a malicious action. This is a supply-chain hardening concern, not an active vulnerability.
- **Remediation (advisory):** Pin to SHA digests, e.g. `actions/checkout@11bd71901bbe5b1630ceea73d27597364c9af683` (the commit behind v4 at time of writing). Reference: [GitHub's security hardening for Actions guide](https://docs.github.com/en/actions/security-guides/security-hardening-for-github-actions#using-third-party-actions).
- **Effort:** Trivial — two-line change per job.

### [L2] Publish Trigger Allows Any Contributor with Tag-Push Rights to Publish

- **Location:** `.github/workflows/ci.yml`, line 40 — `if: startsWith(github.ref, 'refs/tags/v')`
- **Description:** The publish job fires on any `v*` tag push. If the repository has collaborators with push access (vs. only the owner), any of them could trigger a NuGet publish by pushing a tag. This is an operational concern, not a code vulnerability.
- **Remediation (advisory):** In GitHub repository settings, restrict tag creation to repository admins via branch protection rules applied to the tag glob `v*`. Optionally require signed tags (`git tag -s`) and configure the workflow to verify tag signatures before publishing.
- **Effort:** Small — repository settings change, no code change.

### [L3] `ubuntu-latest` Runner — Mutable Target

- **Location:** `.github/workflows/ci.yml`, line 39 — `runs-on: ubuntu-latest`
- **Description:** `ubuntu-latest` resolves to whatever GitHub considers "latest" at dispatch time. This is standard practice and GitHub communicates runner updates in advance. It is slightly less reproducible than pinning to `ubuntu-24.04`. Not a vulnerability.
- **Remediation (advisory):** Replace with a pinned label like `ubuntu-24.04` for full reproducibility. Low urgency.

### [L4] `SourceLink.GitHub` Version 10.0.201 — Verify Availability

- **Location:** `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj`, line 38
- **Description:** `Microsoft.SourceLink.GitHub` 10.0.201 is specified. This is a `PrivateAssets="All"` build-time-only dependency that does not ship in the produced `.nupkg` or `.snupkg` beyond embedding source mapping metadata. No security concern with the package itself; flagged only to confirm the version is available on NuGet.org at release time (it was at time of audit per `dotnet list package --vulnerable` which resolved it successfully).
- **Remediation:** None required.

### [L5] `PackageTags` Absent from csproj

- **Location:** `Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.csproj`
- **Description:** No `<PackageTags>` element is present. This does not affect security but reduces discoverability on NuGet.org. Not a security finding; noted for completeness.
- **Remediation (advisory):** Add `<PackageTags>dotnetworkqueue;taskscheduler;distributed;netmq;threading</PackageTags>`.

---

## Pre-existing Concerns (Out of Scope for 0.3.0, per PROJECT.md Non-Goals)

These findings existed before Phase 1 and are explicitly deferred. They are carried forward from CONCERNS.md and are not new issues introduced by this changeset.

| # | Concern | CONCERNS.md Ref | Disposition |
|---|---------|-----------------|-------------|
| P1 | Unauthenticated, unencrypted P2P bus — any local-network peer can inject task counts | #5 | Deferred; documented in README Limitations |
| P2 | `_lockSocket` held during 10 ms socket receive — throughput bottleneck | #3 | Deferred to issue #6; referenced in CLAUDE.md Known Issues |
| P3 | Non-atomic `ContainsKey`+assign on `ConcurrentDictionary` | #4 | Deferred; currently safe under existing lock |
| P4 | No unit tests; all tests are slow real-network integration tests | #7 | Deferred per PROJECT.md non-goals |
| P5 | `Start()` has no `CancellationToken`; startup/dispose race edge case | #11 | Deferred |
| P6 | GitHub Actions CI is Windows-only (Linux covered by private Jenkins) | #8 | Pre-existing; no change in this phase |

---

## Cross-Component Analysis

**Auth + Authz coherence:** Not applicable — this library has no authentication layer by design. The unauthenticated bus is a documented architectural property.

**Data flow security:** Task count integers flow from `Interlocked` counter → NetMQ SetCount message → peer's `ConcurrentDictionary`. The `volatile` fix ensures the stop flag flows correctly across threads in the local process. No sensitive data (credentials, user data, PII) transits this path at any point.

**Secret handling in CI:** The NuGet API key flows exclusively via `${{ secrets.NUGET_API_KEY }}` in the workflow expression. It is not logged, not echoed, not embedded in any artifact, and not present in any committed file. The `--skip-duplicate` flag prevents accidental re-push of an existing version from leaking information about the key's validity beyond what NuGet.org already reveals.

**Trust boundary — UDP input:** The `int.TryParse` fix correctly treats the beacon payload as untrusted. The remaining gap (no port bounds check, finding M1) is the only trust boundary that is not fully hardened in this phase.

**Error handling:** The `TryParse` fix changes behavior from exception-propagation-crash to silent-drop on malformed input. Silent drops are appropriate for UDP beacons where the sender is unknown and the payload is not authenticated. No information is returned to the sender; no state is mutated on bad input.

---

## Dependency Audit

| Package | Version | Change | CVEs | Notes |
|---------|---------|--------|------|-------|
| DotNetWorkQueue | 0.9.14 → 0.9.31 | Bumped | None found | `dotnet list package --vulnerable` clean |
| NetMQ | 4.0.2.2 | Unchanged | None found | No CVEs in NuGet advisory DB |
| Microsoft.SourceLink.GitHub | — → 10.0.201 | Added | None | `PrivateAssets="All"`; build-time only; does not ship in package |

Lock files: this is a `PackageReference`-based SDK project. NuGet lock files (`packages.lock.json`) are not committed, which is standard for library projects. No concern.

---

## Analysis Coverage

| Area | Checked | Notes |
|------|---------|-------|
| Code Security (OWASP) | Yes | All modified `.cs` files reviewed; no injection, auth, or access-control issues in changed code |
| Secrets & Credentials | Yes | Full `git diff` scanned; no plaintext secrets found; CI secret reference is correct |
| Dependencies | Yes | `dotnet list package --vulnerable` run; zero vulnerable packages reported |
| IaC / Container | N/A | No Terraform, Helm, Docker, or Ansible files in changeset |
| Configuration | Yes | `.github/workflows/ci.yml` fully reviewed; csproj packaging metadata reviewed |

---

## Verdict

**PASS**

Zero critical findings. Zero high findings introduced by this phase. One medium finding (M1, port bounds check) is a hardening gap in the new `int.TryParse` fix — worth addressing in a follow-up but not blocking. All pre-existing architectural concerns are correctly documented and explicitly deferred per PROJECT.md. The NuGet publish pipeline is correctly secured. The changeset improves the overall security posture of the library relative to the prior state.
