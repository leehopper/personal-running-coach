# Questions Round 1 — PR #18 Review Round 2

## Scope
**Q:** Which review findings should be in scope?
**A:** All 7 warnings + 8 advisories (15 total findings)

## Branch Strategy
**Q:** Target branch?
**A:** This branch (`feature/poc1-eval-refactor`) — additional commits before merging PR #18

## Cache TTL (W1)
**Q:** How to handle 14-day expiration on committed cache fixtures?
**A:** Research first — generate deep research prompt for best practices on LLM eval cache management for CI. Document decision before implementing. Pending research artifact from user.

## SHA Pinning (W2)
**Q:** Pin all GitHub Actions or just security-critical?
**A:** Pin ALL actions to commit SHAs with version comments

## DB Password (A8)
**Q:** Address pre-existing DB password in appsettings.json?
**A:** Yes — address now, move to placeholder pattern
