# C# SDK Release Process

This document defines the release process for `MarketDataApp/sdk-csharp`. The package is
[`MarketDataApp`](https://www.nuget.org/packages/MarketDataApp) on NuGet.org.

> The package id, the assembly and the root namespace are all `MarketDataApp`, which is
> the .NET convention. There is deliberately **no** `<PackageId>` in the csproj — it
> defaults to `AssemblyName`, so the two can never drift apart. Do not add one.

## 1. Scope

Use this process for:

- release candidates and other pre-releases (`vX.Y.Z-rc.N`)
- patch releases (`vX.Y.Z`)
- minor releases (`vX.Y.0`)
- major releases (`vX.0.0`)

### The first published version is a release candidate

The SDK has never been published. The first version pushed to NuGet.org is
**`1.0.0-rc.2`**, not `1.0.0`. Everything below supports pre-releases already; the
notes here cover what differs.

**Use the dotted form `1.0.0-rc.2`, not `1.0.0-rc1`.** SemVer compares dot-separated
pre-release identifiers *numerically* when they are all digits, so `rc.2` correctly
precedes `rc.10`. Without the dot the whole identifier is compared as text, and
`rc10` sorts *before* `rc2`. NuGet implements SemVer 2.0.0 ordering, so the wrong form
silently mis-orders your candidates from the tenth onward.

| | Value |
|---|---|
| Tag | `v1.0.0-rc.2` |
| `version` input | `1.0.0-rc.2` |
| `prerelease` input | **`true`** |
| CHANGELOG heading | `## [1.0.0-rc.2] - YYYY-MM-DD` |

**NuGet needs nothing extra.** Unlike npm, NuGet has no dist-tags — a package is a
pre-release purely because its version carries a pre-release suffix. `1.0.0-rc.2` is
hidden from default installs automatically, and `dotnet add package MarketDataApp`
keeps resolving the newest stable version. Users opt in with `--prerelease`.

**Set `prerelease: true`.** It only marks the GitHub Release; it does not change what
is pushed to NuGet. Getting it wrong makes an RC look like a stable release on the
releases page.

**One caveat on the changelog fallback.** GitHub fires the `released` activity type
only for non-prerelease releases, so `update-changelog.yml` will not run for an RC at
all — not even on the manual-UI path it normally covers. This is harmless when you
follow §4 and promote the CHANGELOG section yourself before releasing, which is the
required path anyway.

**Before the RC, update the alpha messaging.** `MarketDataApp.csproj` carries
`<Description>` and `<PackageReleaseNotes>` that both begin with "ALPHA (in active
development — not for production use)", and `README.md` shows a `status-alpha` badge.
These ship in the package and on the NuGet listing page. Change them to release-candidate
wording as part of the release PR — not before, or every dev build in the meantime
claims to be an RC.

Promote to `1.0.0` by running the same workflow again with `version: 1.0.0` and
`prerelease: false`, once the candidate has held up.

## 2. Release inputs

Before you start, confirm:

- the target version `X.Y.Z`
- the tag format: `vX.Y.Z`
- the release title format: `Version X.Y.Z`
- the release owner
- the included PRs and issues
- the intended release date

## 3. How versioning works here

Versions come from git tags through [MinVer](https://github.com/adamralph/minver), with
`MinVerTagPrefix=v`. There is **no version number to edit in a `.csproj`**. The tag is the
version. The release workflows additionally pass `-p:MinVerVersionOverride=X.Y.Z` so the
gate builds and the published package carry the exact requested version.

This differs from the Java and PHP SDKs, where a version string is committed. Do not go
looking for one to bump.

## 4. Release preparation

1. Confirm `main` is current and CI is green for the commit you intend to release.

2. **Promote the CHANGELOG section.** `CHANGELOG.md` is the single source of truth for
   release notes. In a normal PR:

   - Change `## [Unreleased]` to `## [X.Y.Z] - YYYY-MM-DD`.
   - Add a fresh, empty `## [Unreleased]` section above it.
   - Update the link-reference block at the bottom of the file: point `[Unreleased]` at
     `compare/vX.Y.Z...HEAD` and add an `[X.Y.Z]` entry.
   - Confirm every breaking change has migration guidance.

   > **The release workflow matches `## [X.Y.Z]` exactly** (Keep a Changelog bracket
   > format). A `## vX.Y.Z` heading will not be found and the release will fail with a
   > clear error before any tag is created.

3. Confirm `README.md` and `docs/` describe the behavior you are about to ship.

4. Merge that PR to `main`.

5. Confirm the tag `vX.Y.Z` does not already exist.

## 5. Publish the release

One workflow drives the whole release: **Tag and Release**
(`.github/workflows/tag-and-release.yml`). Go to Actions → "Tag and Release" → "Run
workflow", and fill in:

| Input | Value |
|---|---|
| **version** | `X.Y.Z` (no `v` prefix) |
| **ref** | `main` (or a specific commit SHA) |
| **prerelease** | `false` unless this is a prerelease |
| **publish_to_nuget** | `true` (default) to chain into NuGet.org |
| **confirm** | `RELEASE` exactly |

The run proceeds through three gated stages. Each one must pass before the next starts.

1. **`gate`** — the full `{ubuntu, windows, macOS}` matrix. Restores, audits dependencies
   for known vulnerabilities, builds at the release version, runs the unit suite on
   **both** `net8.0` and `net10.0` under the 100% line-and-branch coverage gate, and
   verifies formatting. Nothing is tagged until every leg is green.

2. **`release`** — resolves `ref` to a concrete SHA, verifies the tag `vX.Y.Z` does not
   exist on origin, extracts the `## [X.Y.Z]` section from `CHANGELOG.md`, then creates
   the tag and the GitHub Release "Version X.Y.Z" pointing at that SHA.

3. **`publish-nuget`** — calls `release.yml`, which runs the **live integration suite as a
   release gate** (a missing `MARKETDATA_TOKEN` fails the release rather than silently
   skipping), packs, attests build provenance, and pushes the `.nupkg` and `.snupkg` to
   NuGet.org over OIDC Trusted Publishing.

> **Stopping before NuGet.** Set **publish_to_nuget** to `false` to cut only the tag and
> the GitHub Release. Publish later by running the **Release** workflow (`release.yml`)
> directly with **version** = `X.Y.Z` and **ref** = the tagged SHA.

### Why the publish is chained rather than triggered

`release.yml` also has a `push: tags` trigger, but that trigger **does not fire for an
automated release**. GitHub does not start a new workflow run from an event raised by the
default `GITHUB_TOKEN`, which is a deliberate guard against recursive triggering. The
`uses:` call at the end of `tag-and-release.yml` is what actually reaches NuGet.org.

The same rule explains why `update-changelog.yml` stays quiet on this path: it exists for
releases created **by hand in the GitHub UI**, where nobody promoted the CHANGELOG
section first. See the comment block at the top of that file.

## 6. One-time setup

These must exist before the first successful publish.

### NuGet identities

Two different NuGet.org identities are involved, and they are easy to confuse:

| | Value | Where it goes |
|---|---|---|
| **Account username** | `marketdata` | The `user:` input of `NuGet/login`, i.e. the `NUGET_USER` repository variable |
| **Package owner** | `MarketDataApp` (organization) | The **owner** of the Trusted Publishing policy, chosen in the nuget.org UI |

The account is who authenticates; the organization is who owns the packages. Putting the
organization name in `user:` fails with `No matching trust policy owned by user was
found`. `release.yml` defaults `user:` to `marketdata`, so the `NUGET_USER` variable is
only needed to override it.

### Trusted Publishing policies

Sign in to nuget.org as `marketdata` → click the username → **Trusted Publishing** → add
a policy. Set the **owner** to the **MarketDataApp** organization, not to yourself, so
the policy covers organization-owned packages.

**One policy** covers both release paths:

| Field | Value |
|---|---|
| Policy Name | `sdk-csharp — release.yml` (any identifying name) |
| Package Owner | `MarketDataApp` (org) |
| CI/CD Provider | GitHub Actions |
| Repository Owner | `MarketDataApp` |
| Repository | `sdk-csharp` |
| Workflow File | `release.yml` |
| Environment | `nuget` |
| Scopes | ☑ **Push** → *Push new packages and package versions* |
| | ☐ Unlist or relist package versions — leave unchecked |
| Glob Patterns and Packages | `MarketDataApp` |

> **Why `release.yml` and not `tag-and-release.yml`.** `NuGet/login` runs inside
> `release.yml` on both paths — a direct tag push, and the `workflow_call` from
> `tag-and-release.yml`. GitHub's OIDC token carries two names: `job_workflow_ref` (the
> file containing the job, `release.yml`) and `workflow_ref` (the entry workflow,
> `tag-and-release.yml`). NuGet matches `job_workflow_ref` — the workflow where the token
> is actually obtained — which is why the field is described as "the file name that
> *contains* publishing workflow".
>
> Corroborating evidence: [NuGet/login#6](https://github.com/NuGet/login/issues/6) reports
> a policy naming the *caller* failing with `No matching trust policy owned by user was
> found`. Had NuGet matched `workflow_ref`, that policy would have worked.
>
> If a publish ever does fail with that error, add a second identical policy with
> **Workflow File** = `tag-and-release.yml` and re-run. The failure happens at the login
> step, before anything is pushed, so there is no partial release to unwind.

> **Enter the workflow file name only** — `release.yml`, not
> `.github/workflows/release.yml`.

> **⚠ The glob must exactly match the package id.** If the two ever disagree, the
> `NuGet/login` step fails with `No matching trust policy owned by user was found` —
> loudly, and before anything is pushed, but it blocks the release until the policy is
> corrected.

> **Keep "Push new packages and package versions" until 1.0.0 ships.** `MarketDataApp`
> does not exist on NuGet yet, so the next publish has to *create* the package id. The
> narrower "Push only new package versions" option cannot do that and would fail.
> Tighten the policy to it once `1.0.0` is published — nothing legitimate creates new
> package ids after that.

> **Use the exact glob `MarketDataApp`, not `*`.** A `*` pattern would let this workflow
> publish any package owned by the MarketDataApp organization.

> **The Environment field must stay in sync.** It is set to `nuget` because the publish
> job declares `environment: nuget`. Removing that from the job, or renaming the
> environment, invalidates the policy until it is updated.

> **A GitHub owner migration breaks this.** NuGet locks the policy to the GitHub
> repository and owner *IDs* after the first successful publish, to block resurrection
> attacks. Moving these repos from the `MarketDataApp` user to a `MarketData-App`
> organization changes the owner ID, so the policy goes inactive and must be recreated.

### Everything else

Current state of this repository, all verified:

| Item | State |
|---|---|
| `MARKETDATA_TOKEN` secret | ✅ set — required, the release gates on the live integration suite |
| `CODECOV_TOKEN` secret | ✅ set — optional on a public repo, avoids rate limits |
| `NUGET_USER` variable | not set, and not needed — `release.yml` defaults `user:` to `marketdata` |
| `nuget` environment | ✅ created, no protection rules — **deliberate** |
| Branch protection on `main` | ✅ required checks: `Build / Test / Pack (ubuntu-latest)`, `Integration Tests (live)`; strict; no required reviews |
| Allow auto-merge | ✅ enabled (after branch protection, never before) |

> **The `nuget` environment has no required reviewers, by choice.** Publishing runs
> unattended once the workflow is dispatched. The `confirm: RELEASE` input and the
> gate/tag/publish chain are the controls; a reviewer approval is not wanted. Do not add
> protection rules back without asking.
>
> **Do not remove `environment: nuget` from the publish job.** It is not decorative even
> without protection rules: the Trusted Publishing policy on nuget.org specifies
> `Environment: nuget`, and the OIDC token only carries that claim when the job declares
> the environment. Deleting it makes the policy stop matching and every publish fails
> with `No matching trust policy owned by user was found`.

> **Order matters for auto-merge.** `dependabot-auto-merge.yml` calls
> `gh pr merge --auto`, which merges as soon as all *required* checks pass. Enabling
> "Allow auto-merge" while `main` has no required checks means there is nothing to wait
> for, so Dependabot PRs merge instantly and ungated. Always enable branch protection
> first.

> **Private-repository note.** A new policy on a private repository starts *temporarily*
> active for 7 days. If no publish happens in that window it goes inactive, because
> NuGet needs the repository and owner IDs from a real publish to lock the policy down.
> You can restart the window at any time. This repo is public, so the policy was active
> immediately.

## 7. Post-release checks

1. Verify the GitHub Release exists with the notes taken from `CHANGELOG.md`.
2. Confirm the package appears at <https://www.nuget.org/packages/MarketDataApp>. NuGet
   indexing can lag several minutes after the push.
3. Confirm the build-provenance attestation is listed on the repository's Attestations
   page.
4. Smoke-test resolution in a clean project:

   ```bash
   dotnet new console -o /tmp/md-smoke && cd /tmp/md-smoke
   dotnet add package MarketDataApp --version X.Y.Z
   dotnet build
   ```

## 8. Rollback and hotfix

NuGet.org versions are immutable — a published version cannot be replaced.

1. Stop any promotion messaging.
2. If the package is harmful, unlist it on NuGet.org (unlisting hides it from search and
   from new resolutions; it does not delete it).
3. Ship a patch release `vX.Y.(Z+1)` from `main` with the targeted fix.
4. Record the root cause and the remediation in the next `CHANGELOG.md` entry.
