# rename (preview)

Rename a symbol across an app and repack it.

```bash
pacheck rename <msapp> --from <old> --to <new> [--output <file.msapp>] [--keep-temp]
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

Renaming is whole-word across the unpacked `pa.yaml` sources (a temp copy — never the
input). Best suited to variables, collections and named formulas. After packing, the tool
prints how many occurrences changed and reminds you to verify in Studio.

## Example

```bash
pacheck rename MyApp.msapp --from gblUsr --to gblCurrentUser
# ✓ Renamed 'gblUsr' → 'gblCurrentUser'. (7 occurrences in 3 files)
# → MyApp.renamed.msapp
# preview: open the new .msapp in Power Apps Studio and verify before shipping.
```
