# Issue Workflow

This document defines the process for triaging and resolving bug reports in
`MarketDataApp/sdk-csharp`. It is written to be followed by a maintainer, human or
automated.

Companion document: [BUG_FINDING.md](./BUG_FINDING.md) finds bugs proactively. This
document processes bugs that users report.

## Overview

```
Verify Permissions → New Issue → Validate → [Valid]      → Reproduce → Accept → Fix → Close
                                          → [Needs Info] → Request Info → Wait 7 days → Close
                                          → [Not a Bug]  → Explain → Close
```

---

## Step 0: Verify permissions

Before processing issues, confirm you can manage them.

```bash
gh api repos/MarketDataApp/sdk-csharp/collaborators/$(gh api user --jq '.login')/permission --jq '.permission'
```

| Result | Meaning | Action |
|---|---|---|
| `admin`, `maintain`, `write`, `triage` | Sufficient permission | Go to Step 1 |
| `read` | Read-only access | Stop. Ask a maintainer to elevate your access |
| Error: `404 Not Found` | Not a collaborator | Stop. You cannot manage issues |
| Error: `401 Unauthorized` | Not authenticated | Run `gh auth login` first |

Quick check — exits 0 when you can manage issues:

```bash
gh api repos/MarketDataApp/sdk-csharp/collaborators/$(gh api user --jq '.login')/permission --jq '.permission' \
  | grep -qE '^(admin|maintain|write|triage)$'
```

---

## Step 1: Validate the bug report

Run this checklist against every new report. The fields map directly to
[`ISSUE_TEMPLATE/bug.yml`](./ISSUE_TEMPLATE/bug.yml).

| # | Criterion | How to check | Pass | Fail |
|---|---|---|---|---|
| 1 | **API docs verified** | "API documentation verification" checkboxes | Both checked | Either unchecked |
| 2 | **Has reproduction code** | "Reproduction code" field | Contains a real C#/F#/VB code block | Empty, pseudocode, or prose only |
| 3 | **Code is complete** | Look for client construction | Has `MarketDataClient.CreateAsync`, `new MarketDataClient`, or `AddMarketDataClient`, plus the `using` directives | Missing client setup or usings |
| 4 | **Names the resource and method** | "SDK resource" + "Method" | Both present, e.g. `stocks` / `GetCandlesAsync` | Empty or vague |
| 5 | **Specifies SDK version** | "SDK version" | A concrete version, e.g. `1.0.0` | Empty or "latest" |
| 6 | **Specifies .NET version** | ".NET SDK / runtime version" | A concrete version, e.g. `10.0.100` | Empty or vague, e.g. "10.x" |
| 7 | **Describes expected behavior** | "Expected behavior" | A clear statement | Empty or unclear |
| 8 | **Describes actual behavior** | "Actual behavior" | A clear statement, ideally with the exception message and stack trace | Empty or unclear |

**Bonus signal, not required:** the "Support info" field. When a `MarketDataException`
was thrown, the `SupportInfo` block carries `request_id`, `request_url`, `status_code`
and `timestamp`. That identifies the exact upstream request and usually settles whether
the fault is in the SDK or the API. Ask for it whenever an exception is involved and the
block is missing.

### Decision

- **All 8 pass** → Step 2 (Reproduce)
- **Any fail** → Step 4 (Request more information)

---

## Step 2: Reproduce the bug

1. Create a scratch console project, or add an xUnit test under
   `src/MarketDataApp.Tests`.
2. Use the reported SDK version:
   `dotnet add package MarketDataApp --version X.Y.Z`.
3. Target the reported framework (`net8.0` or `net10.0`) — this SDK multi-targets both,
   and behavior differences between them are themselves worth finding.
4. Run it and compare against the reported "Actual behavior".

### Decision

| Outcome | Next step |
|---|---|
| **Reproduces** — output matches the report | Step 3A (Accept) |
| **Does not reproduce** — the code works | Step 3B (Cannot reproduce) |
| **Different error** — fails, but not as reported | Step 4 (Request more information) |
| **API error, not SDK error** — the API itself returns the error | Step 3C (Not an SDK bug) |
| **Expected API behavior** — the SDK faithfully returns what the API sent | Step 3C (Not an SDK bug) |
| **User error** — the reproduction code is wrong | Step 3C (Not an SDK bug) |

> **Reproduces on one target framework only?** That is a real bug, not a non-repro.
> Record which of `net8.0` / `net10.0` is affected and go to Step 3A.

---

## Step 3A: Accept as a bug

1. Add the label `accepted`.
2. Comment with the template below.
3. Go to Step 5.

```markdown
Thanks for the detailed report. I've reproduced this.

**Reproduction confirmed:**
- SDK version: [version]
- .NET version: [version]
- Target framework: [net8.0 / net10.0 / both]
- Behavior: [what you observed]

Working on a fix.
```

---

## Step 3B: Cannot reproduce

1. Add the label `needs-info`.
2. Comment with the template below.

```markdown
I wasn't able to reproduce this with the information provided.

**My environment:**
- SDK version: [version]
- .NET version: [version]
- Target framework: [tfm]
- OS: [os]

**What I observed:**
[What actually happened — worked correctly, different output, etc.]

Could you provide:
- [ ] The `SupportInfo` block from the exception (`Console.WriteLine(ex.SupportInfo)`) — it contains the request id and URL we need, and never includes your API token
- [ ] Any custom configuration: `MarketDataClientOptions` values or `MARKETDATA_*` environment variables
- [ ] The complete exception output including the stack trace
- [ ] Your exact `<PackageReference>` version and the output of `dotnet --version`

I'll keep this open for 7 days for additional information.
```

---

## Step 3C: Not an SDK bug

1. Add the label `wontfix`.
2. Comment with the applicable template.
3. Close the issue.

### API issue, not the SDK

```markdown
Thanks for the report. After investigation this is behavior of the Market Data API itself rather than the C#/.NET SDK.

**What's happening:**
[Explain the API behavior]

**Suggested next steps:**
- Check the [API documentation](https://www.marketdata.app/docs/api) for this endpoint
- Contact Market Data support if you believe the API behavior is wrong
- Join the [Discord](https://discord.com/invite/GmdeAVRtnT) for community help

Closing as outside the SDK's scope. Please open a new issue if you find an SDK-specific problem.
```

### Expected API behavior

```markdown
Thanks for the report. After checking the [API documentation](https://www.marketdata.app/docs/api), this matches how the API is designed to work.

**What you're seeing:**
[Describe the behavior]

**Documentation reference:**
[Link or quote]

The SDK returns data exactly as the API provides it. If you believe the documentation is wrong, or the API should behave differently, please contact Market Data support or join the [Discord](https://discord.com/invite/GmdeAVRtnT).

Closing as working-as-designed.
```

### User error

~~~markdown
Thanks for the report. Reviewing the reproduction code, this looks like an issue in the calling code rather than a bug in the SDK.

**The issue:**
[Explain what's wrong]

**Suggested fix:**
```csharp
// Corrected code
```

**Documentation reference:**
[Link if applicable]

Feel free to ask in [GitHub Discussions](https://github.com/MarketDataApp/sdk-csharp/discussions) if you need more help. Closing this, but reopen if you believe there is still an SDK bug.
~~~

### Works as designed

```markdown
Thanks for the report. The SDK is behaving as designed here.

**Expected behavior:**
[Why the current behavior is correct]

**Documentation reference:**
[Link]

To suggest a change to this behavior, please open a feature request in [Discussions](https://github.com/MarketDataApp/sdk-csharp/discussions/new?category=ideas).
```

---

## Step 4: Request more information

1. Add the label `needs-info`.
2. Comment, keeping only the items you actually need.
3. Check back in 7 days.

```markdown
Thanks for the report. To investigate I need some additional information:

- [ ] **API documentation verification**: Please confirm you've checked the [API documentation](https://www.marketdata.app/docs/api) and that the behavior differs from what it describes
- [ ] **Complete reproduction code**: A self-contained C# snippet including `using` directives and the client construction (`MarketDataClient.CreateAsync`, `new MarketDataClient`, or `AddMarketDataClient`)
- [ ] **Support info**: If an exception was thrown, paste `ex.SupportInfo` — it carries the request id, URL, status code and timestamp, and never includes your API token
- [ ] **SDK version**: The `MarketDataApp` version from your `<PackageReference>`
- [ ] **.NET version**: The output of `dotnet --version`
- [ ] **Target framework**: Your project's `<TargetFramework>`
- [ ] **Expected behavior**: What did you expect?
- [ ] **Actual behavior**: What happened? Include the full exception message and stack trace
- [ ] **Additional context**: [Specify]

I'll keep this open for 7 days. Without a response I'll close it, but you're always welcome to reopen with the details.
```

### 7-day follow-up

```markdown
Closing due to inactivity. If you can provide the requested information, feel free to reopen or open a new issue with the additional details.
```

---

## Step 5: Fix the bug

1. [ ] **Write a failing test** in `src/MarketDataApp.Tests` that reproduces the bug, and
       confirm it fails. Unit tests mock all HTTP through `StubHttpMessageHandler` — they
       never reach the network.
2. [ ] **Implement the minimal fix.**
3. [ ] **Confirm the new test passes.**
4. [ ] **Run the full suite and the coverage gate.** This repo requires **100% line and
       branch coverage**; CI fails below it:

       ```bash
       dotnet test src/MarketDataApp.Tests/MarketDataApp.Tests.csproj -c Release \
         -p:CollectCoverage=true -p:CoverletOutputFormat=opencover \
         -p:Threshold=100 -p:ThresholdType=line,branch -p:ThresholdStat=total
       ```

5. [ ] **Check formatting**: `dotnet format MarketDataApp.slnx --verify-no-changes`.
6. [ ] **If the fix touches live-API behavior**, run the integration suite:

       ```bash
       MARKETDATA_RUN_INTEGRATION_TESTS=true MARKETDATA_TOKEN=... \
         dotnet test src/MarketDataApp.IntegrationTests/MarketDataApp.IntegrationTests.csproj -c Release
       ```

7. [ ] **Add a CHANGELOG entry** under `## [Unreleased]`.
8. [ ] **Commit** as `fix: Description (closes #NNN)`.
9. [ ] **Open a PR.** Consider commenting `/run-all-os` on it if the fix could behave
       differently on Windows or macOS — PRs otherwise only run the ubuntu leg.

Examples:

- `fix: Handle null values in the candles response decoder (closes #45)`
- `fix: Honor the columns projection on the news endpoint (closes #67)`

---

## Step 6: Close the issue

1. GitHub auto-closes from a `closes #NNN` commit message once merged.
2. If it did not, close it by hand with a comment.

~~~markdown
Fixed in [commit or PR link].

This ships in the next release. To use it immediately, build from source and pack a local package:

```bash
dotnet pack src/MarketDataApp/MarketDataApp.csproj -c Release -o ./artifacts
dotnet nuget add source ./artifacts -n marketdata-local
```
~~~

---

## Labels reference

| Label | Meaning | When to apply |
|---|---|---|
| `bug` | Default label from the template | Automatic on new issues |
| `accepted` | Validated and reproduced | After successful reproduction |
| `needs-info` | Waiting on the reporter | Report incomplete, or cannot reproduce |
| `wontfix` | Not a bug, or will not be fixed | When closing as not-a-bug |
| `dependencies` | Dependency update | Automatic on Dependabot PRs |

---

## CLI reference

```bash
# Labels
gh issue edit NUMBER --add-label "accepted"
gh issue edit NUMBER --add-label "needs-info"
gh issue edit NUMBER --remove-label "bug"

# State
gh issue close NUMBER
gh issue reopen NUMBER

# Comment and inspect
gh issue comment NUMBER --body "Comment text here"
gh issue view NUMBER

# Lists
gh issue list --label "bug"
gh issue list --label "needs-info"
```

---

## Examples

### Example A: valid bug report

**Issue #42** — resource `stocks`, method `GetCandlesAsync`, complete reproduction code
with `using` directives and `CreateAsync`, expected "returns candle data", actual
`NullReferenceException`, SDK `1.0.0`, .NET `10.0.100`, TFM `net10.0`.

**Action:** passes all criteria → reproduce → accept and fix.

---

### Example B: incomplete report

**Issue #43** — resource `options`, method `GetChainAsync`, reproduction code reads "I
called the chain method and it broke", expected "it should work", actual "it doesn't
work", SDK version empty, .NET version "10.x".

**Action:** fails criteria 2, 3, 5, 6, 7, 8 → request more information, naming each
missing item.

---

### Example C: not a bug (API behavior)

**Issue #44** — `stocks` / `GetQuoteAsync`, complete code, expected "should return the
after-hours price", actual "returns the regular session price".

**Investigation:** the API returns regular-session prices by default.

**Action:** close as "Not an SDK bug" with a pointer to the API documentation.

---

### Example D: expected API behavior

**Issue #45** — `stocks` / `GetEarningsAsync`, complete code, expected "percentages like
`5.2` for 5.2%", actual "returns `0.052`", both docs checkboxes checked.

**Investigation:** the API documents percentage fields as decimals (`0.052` = 5.2%). The
SDK passes the response through unchanged.

**Action:** close as "Expected API behavior", quoting the documentation.

---

### Example E: target-framework-specific failure

**Issue #46** — `stocks` / `GetCandlesAsync`, reproduces on `net8.0`, works on `net10.0`.

**Action:** this is a real bug. Accept it, and write the regression test so it runs on
both target frameworks — the suite already executes against `net8.0` and `net10.0`
separately in CI.
