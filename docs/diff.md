# diff

Semantic comparison of two app versions.

```bash
easel diff <base> <head> [--format console|markdown|json] [--output <file>]
```

Both `<base>` and `<head>` are unpacked source folders or `.msapp` files.

## What it detects

| Change | How it's found |
|---|---|
| **Added** / **Removed** | Screen or control present in only one side (matched by name). |
| **Renamed** | An added and a removed control of the same type with a high property-match ratio (default ≥ 0.6). Reported as `old → new (probable rename)`. |
| **Moved** | A control whose parent or screen changed. |
| **PropertyChanged** | A property whose formula changed — described at **AST level**, not as a text diff. |

## AST-level property diff

Rather than a character diff, easel reports what changed semantically: functions and
identifiers added/removed. Whitespace-only edits are ignored.

```
* changed  Property lblTitle.Text (-ref AppTitle; +fn Concatenate)
```

## Formats

- `console` — coloured summary (default).
- `markdown` — a table, ideal as a PR comment (`--output diff.md`).
- `json` — structured changelog with a summary block and per-change records.

## In CI

Generate a Markdown changelog between the PR base and head and post it as a comment — see
[ci.md](ci.md#diff-in-a-pr).
