# Test fixtures

## SampleApp

A minimal unpacked canvas app (`Src/*.pa.yaml` + `Assets/`) crafted to exercise the loader,
symbol table, dependency graph and rules. It intentionally contains:

- `gblUnused`, `colOrphan` — assigned, never read (PA1003)
- `Assets/logo.png` — never referenced (PA1004)
- hardcoded `RGBA(...)` colours (PA1008)
- `btnNoLabel`, `txtSearch` — interactive, no `AccessibleLabel` (PA1009)
- a triple-nested `If` (PA1010)
- `scrHome → scrDetail` navigation (graph / dead-screen checks)

This fixture is hand-authored (not produced by `pac`) so it stays small and reviewable.

## Regenerating real .msapp fixtures

For round-trip / rename E2E fixtures, generate them from a real app with `pac`:

```bash
# unpack an existing .msapp into reviewable sources
pac canvas unpack --msapp MyApp.msapp --sources ./MyAppSrc

# ...edit / anonymise...

# pack back into a .msapp for msapp-input tests
pac canvas pack --msapp MyApp.rebuilt.msapp --sources ./MyAppSrc
```

Keep real apps anonymised. Prefer adding coverage as small hand-authored `pa.yaml` where a
full `.msapp` is not required.
