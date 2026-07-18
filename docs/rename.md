# rename (preview)

Rename a symbol across an app and repack it.

```bash
easel rename <msapp> --from <old> --to <new> [--output <file.msapp>] [--keep-temp]
```

> **Preview.** After it runs, open the output `.msapp` in Power Apps Studio and verify the
> app behaves identically before shipping.

## Rules

- **Input must be a `.msapp`.** A `pa.yaml` folder from Git integration is treated as
  read-only and rejected — pack it first, or work from the `.msapp`.
- **The input is never modified.** Output is written beside the input as
  `<name>.renamed.msapp` (override with `--output`).
- **Pre-flight collision check.** If `--to` is already defined in the app, the rename is
  refused before anything is repacked.

## How it works

```
pac canvas unpack   →   rename identifier in pa.yaml   →   pac canvas pack   →   new .msapp
```

Renaming targets Power Fx identifier tokens across the unpacked `pa.yaml` **and** legacy
`fx.yaml` sources (a temp copy — never the input). It supports global variables, context
variables, collections and named formulas, including their definition sites. YAML comments,
scalar styles and unrelated formatting are preserved. After packing, the tool prints how many
occurrences changed and reminds you to verify in Studio.

### Caveats (why it's preview)

- **String literals.** Values inside `"..."` are intentionally left unchanged. If the old
  name also appears in a literal, the tool warns
  (`⚠ 'x' also appears in N string literal(s) — these were intentionally left unchanged`)
  so you can decide whether those textual values need a separate edit in Studio.
- **Collision.** If `--to` is already defined, the rename is refused (exit 2) before
  anything is written.
- Verified end-to-end (unpack → rename → pack → unpack) against real apps; see the
  `RenameE2ETests` suite (enable with `EASEL_TEST_MSAPP`).

## Example

```bash
easel rename MyApp.msapp --from gblUsr --to gblCurrentUser
# ✓ Renamed 'gblUsr' → 'gblCurrentUser'. (7 occurrences in 3 files)
# → MyApp.renamed.msapp
# preview: open the new .msapp in Power Apps Studio and verify before shipping.
```
