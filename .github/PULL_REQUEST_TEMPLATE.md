<!-- Thanks for contributing to pacheck! -->

## What & why

<!-- What does this change and why? Reference the architecture task if relevant, e.g. T2.4. -->

## Type

- [ ] New rule (`PAxxxx`)
- [ ] Rule/heuristic change
- [ ] New command / flag
- [ ] Bug fix
- [ ] Docs / fixtures / CI
- [ ] Other:

## Checklist

- [ ] `dotnet build` is warning-clean
- [ ] `dotnet test` passes (snapshot `*.received.*` reviewed and promoted if intended)
- [ ] New/changed behaviour has a test
- [ ] Docs updated (`docs/rules.md`, `docs/configuration.md`, README as needed)
- [ ] Output stays deterministic (stable sort, no timestamps in json/sarif)
- [ ] Read-only preserved (no command other than `rename` writes; `rename` never touches its input)

## Notes for reviewers

<!-- Anything to look at closely: false-positive risk, perf, edge cases. -->
