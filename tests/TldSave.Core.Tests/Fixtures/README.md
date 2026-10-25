# Test fixtures

Real Survival saves used by the tests:

| File | Save | Size |
|---|---|---|
| `sandbox1_GoDLC` | "GoDLC", day 2.8, 7 sections | 0.5 MB |
| `sandbox3_1000Days` | "1000Days", day 323, 169 sections | 13 MB |

Both were checked for personal data (Windows user/computer name, emails, paths, account IDs, journal notes,
screenshot metadata): none, only game state.

**Do not replace or update these files** unless a change in the game's save format requires it. They are
binary files in git: every new version adds its full size to the repository history permanently. If you must
replace one, run the same personal-data check on the new file first.
