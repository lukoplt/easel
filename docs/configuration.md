# Configuration

pacheck reads `.pacheck.yml` (or `.pacheck.yaml`), searching **upward** from the analysed
path. Pass `--config <file>` to override discovery.

## Shape

```yaml
rules:
  <rule-id-or-name>:
    severity: info | warning | error | off
    # ...rule-specific options
  <another-rule>: off          # shorthand to disable

ignore:
  - "<glob>"                    # matched against the reported file path

output:
  format: console | json | sarif | html
```

## Rule keys

A rule can be addressed by **id** (`PA1005`) or **name** (`screen-control-limit`). Id wins
if both are present.

- `severity: off` (or the bare value `off`) disables the rule.
- `severity: warning` overrides the default severity; findings are re-tagged.
- Remaining keys are rule-specific options (see [rules](rules.md)).

```yaml
rules:
  screen-control-limit: { max: 250 }
  deep-nested-if: { threshold: 3 }
  heavy-onstart: { max-assignments: 8, max-nodes: 120 }
  duplicate-formula: { min-length: 60, min-occurrences: 2 }
  naming-convention:
    severity: error
    patterns:
      variable: "^(gbl|var|loc)[A-Z]"
      collection: "^col[A-Z]"
      screen: "^scr[A-Z]"
      control: "^(btn|lbl|txt|gal|con|ico|img)[A-Z]"
  hardcoded-secret:
    allowlist:
      - "https://contoso.example.com"
```

## Ignore globs

`*` matches within a path segment, `**` matches across segments, `?` a single character.
Matched case-insensitively against the finding's file path.

```yaml
ignore:
  - "**/Legacy*.pa.yaml"
  - "Src/Experimental/**"
```

## Baseline

Separate from config. `pacheck lint --write-baseline` records current findings into
`.pacheck-baseline.json` (line-independent fingerprints). Later runs auto-suppress
baselined findings and report only new ones. Point elsewhere with `--baseline <file>`.

## Tolerance

An unknown key or a malformed config never aborts a run — pacheck falls back to defaults.
This mirrors the loader's tolerance of unknown `pa.yaml` keys, so schema evolution does not
break analysis.
