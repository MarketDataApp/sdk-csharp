# Final Go/No-Go Decision - v1.0.0-rc.1

## Release Information

| Field | Value |
|-------|-------|
| Version | 1.0.0-rc.1 |
| Tag | v1.0.0-rc.1 |
| Title | Version 1.0.0-rc.1 |
| Package | `marketdata.sdk` |
| Prerelease | Yes |
| Commit | b00d3d7 |
| Date | 2026-08-20 |

## Gate Summary

| Gate | Status |
|------|--------|
| 01 - API Contract | PASS |
| 02 - Quality/Tests | PASS |
| 03 - Compatibility | PASS |
| 04 - Security | PASS |
| 05 - Docs/DX | PASS |
| 06 - Release/Rollback | PASS |

## Key Evidence

- Unit suite 307/307 on **both** `net8.0` and `net10.0`
- Coverage 100% line, 100% branch, 100% method on both target frameworks
- Live integration suite 21/21, **zero skipped** (a skipped suite would void the gate)
- Full `{ubuntu, windows, macOS}` matrix green on `main` @ `b00d3d7`
- Zero known dependency vulnerabilities; Socket Security clean
- Token is header-only and redacted to at most 4 trailing characters, with tests
- Three example apps smoke-run green against the live API
- Publish is keyless via OIDC Trusted Publishing, with build provenance attested before push

## P0 Blockers

```
None
```

Issue #70 (docs-liveness check pointing at staging) is open, tracked, and non-blocking.

## Decision

**STATUS: GO**

---

## Sign-off

- [x] All gates PASS
- [x] No P0 blockers
- [ ] Release owner approval
