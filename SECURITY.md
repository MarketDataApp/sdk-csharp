# Security Policy

## Reporting a Vulnerability

**This is a public repository. Do not open a public GitHub issue for a security
vulnerability** — that discloses it to everyone before a fix is available.

Instead, report privately through GitHub's **Private Vulnerability Reporting**:

1. Go to the **Security** tab of this repository.
2. Click **Report a vulnerability**.
3. Describe the problem, including steps to reproduce, affected version(s), and the impact.

We will acknowledge the report, keep you informed as we investigate, and coordinate
the disclosure timeline and a fixed release with you. Please give us a reasonable
window to ship a fix before any public disclosure.

## Scope

This repo is the **Market Data C#/.NET SDK** — a client library published to NuGet
and pulled into consumers' applications. It runs on the consumer's machine (or their
servers), not on Market Data infrastructure. The security concerns that matter here
are therefore about how the library treats *its consumers*:

- **Credential handling** — the caller's API token must never be logged verbatim,
  leaked in exception messages, or written to disk. The token is **never placed in the
  URL**: it is sent only as an `Authorization: Bearer` header and is redacted to its
  last four characters (`RedactToken`) wherever it is logged. Full request URLs,
  *including their query strings*, are intentionally included in logs, telemetry
  (`url.full`), and exceptions for diagnostics — the endpoint's own query parameters
  (symbols, dates, columns, etc.) are diagnostic, not secret, and carry no credential.
  Regressions in the token guarantee are in scope.
- **Transport security** — TLS is validated by default and the SDK exposes no
  skip-verify option. Anything that weakens this is in scope.
- **Injection into outbound requests** — request-building that lets caller input
  smuggle headers, path segments, or query parameters it shouldn't.
- **Deserialization safety** — the `System.Text.Json` response-decoding path handling
  hostile or malformed API responses without crashes or resource exhaustion a
  consumer can't defend against.
- **Supply-chain integrity of the published package** — the build, OIDC Trusted
  Publishing, and build-provenance attestation pipeline (`release.yml`), and the
  dependency tree of the shipped NuGet package.

Out of scope:

- **The Market Data API backend** itself. Report API/server vulnerabilities through
  the API's own support channel, not here.
- **Third-party dependencies.** Vulnerabilities in NuGet dependencies are tracked by
  Dependabot (see `.github/dependabot.yml`); we bump the affected package here once a
  fixed version exists.

## Security Fix Policy

This policy governs how security fixes are applied to this repository, including
fixes made by automated agents (e.g. Claude Code) working in the repo. It sorts every
security fix into one of two tiers.

The dividing line for a **library** is *consumer compatibility*. A fix that any
consumer can pick up by upgrading, with no source or behavior change on their side,
is low-risk. A fix that forces consumers to change their code, recompile against a
changed API, or adapt to changed runtime behavior is a breaking change, follows
SemVer, and gets the maintainer gate.

### Tier 1 — Fix immediately (no approval needed)

Security fixes that are **API- and behavior-compatible for legitimate consumers**.
Existing callers keep compiling and keep working the same way after upgrading; only
the vulnerability is closed. These may be fixed, tested, and committed right away, and
must be called out in the commit message, `CHANGELOG.md`, and the summary to the
maintainer. Typical Tier 1 fixes:

- Tightening token/secret redaction or plugging a leak into logs or exception messages
- Fixing injection in request building (header/path/query smuggling) where valid
  caller input is unaffected
- Hardening the `System.Text.Json` response-decoding path against malformed or hostile
  responses (bounds, resource limits, null handling)
- Correcting a logic flaw in an existing security check without changing its public contract
- Patching a vulnerable dependency by bumping to a compatible version (no public API or
  behavior change)
- Hardening internal, non-public code paths (`ApiClient`, `StatusGate`, `RequestQuery`,
  the parser) that consumers cannot observe or depend on
- Fixing the build/publish/attestation pipeline (CI workflows)

### Tier 2 — Requires maintainer approval first

Any security fix that **breaks consumer compatibility or changes observable runtime
behavior**. These must NOT be applied unilaterally — write up the issue, the proposed
fix, and the specific consumer impact, and wait for approval. A fix is Tier 2 if it:

- Removes, renames, or changes the signature of any **public** type, method, or
  parameter (a source/binary-incompatible change — SemVer major)
- Tightens input validation so requests the SDK previously accepted are now rejected
- Changes a user-visible default (the fixed 99s request timeout, retry count/backoff,
  concurrency cap, base URL, API version, or `ValidateTokenOnStartup`)
- Changes an API/response contract — response record shapes, the exception types
  thrown, or the `MarketDataException` hierarchy consumers `switch` over
- Raises the minimum target framework, changes the package id, or otherwise forces a
  consumer to change their build
- Adds a new required dependency to the published package

### Classification rules

- **When in doubt, it's Tier 2.** If it's unclear which tier a fix falls into, treat
  it as Tier 2 and ask for approval.
- **No urgency exception.** Even for a critical, actively-exploitable vulnerability, a
  compatibility-breaking (Tier 2) fix waits for maintainer approval. Flag the urgency,
  propose the fix, and wait — cutting a major version to close a critical hole is a
  maintainer decision, not an agent's.

### Release of security fixes

Tiering governs *what* may change; the repo's normal release rules govern *what ships*.
A Tier 1 fix may be committed and merged via the usual flow. **Publishing a release to
NuGet** — cutting the `v*` tag and running `release.yml` — requires explicit maintainer
confirmation, exactly like every other release. Automated agents never cut or publish a
release on their own.
