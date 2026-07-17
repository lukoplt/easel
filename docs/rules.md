# Rule reference

Every rule has a stable id (`PAxxxx` / `PFxxxx`), a name, a category and a default
severity. Configure any rule by id or name in `.easel.yml` (see
[configuration](configuration.md)). Disable with `off`, or change `severity`.

Severities: `error`, `warning`, `info`.

---

## Performance

### PA1001 — non-delegable-query · warning
A `Filter` / `LookUp` / `Search` over a data source (not an in-memory collection or
variable) that contains a non-delegable function. Non-delegable operations run only over
the first 500–2000 rows. **Conservative:** fires only when the query root is a bare data
source and a known non-delegable function appears inside the query.

### PA1002 — n-plus-one · warning
A `ForAll(...)` whose body performs a per-row data operation
(`Patch`/`Collect`/`Remove`/`LookUp`/`Update`). Batch instead: build a table and
`Patch`/`Collect` once.

### PA1005 — screen-control-limit · warning
A screen with more controls than the limit (`max`, default **300**). Split into screens
or components. `{ max: <int> }`.

### PA1006 — heavy-onstart · warning
`App.OnStart` with many assignments or a large AST. Move derived values to named formulas
(`App.Formulas`) so they evaluate lazily. `{ max-assignments: 6, max-nodes: 80 }`.

---

## Maintainability

### PA1003 — unused-variable · warning
A variable (`Set`/`UpdateContext`) or collection (`Collect`/`ClearCollect`) that is
assigned but never read anywhere in the app.

### PA1004 — unused-media · info
A media asset that is not referenced by identifier or by name in any formula.

### PA1008 — hardcoded-color · info
A colour property (`Fill`, `Color`, `BorderColor`, …) using an `RGBA(...)` /
`ColorValue("#...")` / `Color.*` literal instead of a theme/global value.

### PA1010 — deep-nested-if · info
`If` nested deeper than `threshold` (default **2**). Flatten with `Switch(...)` or
intermediate variables. `{ threshold: <int> }`.

### PA1011 — timer-side-effect · info
A `Timer` whose `OnTimerEnd`/`OnTimerStart` performs writes or navigation. Hidden
periodic side-effects are hard to reason about.

### PA1012 — duplicate-formula · info
The same non-trivial formula repeated across the app. Extract into a named formula or a
component. `{ min-length: 40, min-occurrences: 3 }`.

### PA1013 — inconsistent-control-version · info
The same control type used at different `@version`s across the app.

---

## Accessibility

### PA1009 — missing-accessible-label · warning
An interactive control (button, input, toggle, …, or anything with `OnSelect`) with no
non-empty `AccessibleLabel`.

---

## Naming

### PA1007 — naming-convention · warning (opt-in)
Element names must match configured regexes. **Silent unless `patterns` are configured**,
so it never produces false positives out of the box.

```yaml
naming-convention:
  patterns:
    variable: "^(gbl|var|loc)[A-Z]"
    collection: "^col[A-Z]"
    screen: "^scr[A-Z]"
    control: "^(btn|lbl|txt|gal)[A-Z]"
```

---

## Security

Shared detectors with the `secrets` command (regex + Shannon entropy). Allowlist known-safe
values under the rule's `allowlist`.

### PA2001 — hardcoded-secret · error
A literal that looks like an API key, access token or connection string with a secret.

### PA2002 — high-entropy-literal · warning
A long, high-entropy literal that may be a generic secret.

### PA2003 — url-with-credentials · error
A URL of the form `https://user:pass@host`.

---

## Errors

### PF0001 — unparsable-formula · error
A formula Power Fx could not parse. Fix the syntax, or (for legacy apps) re-open and
re-save in Power Apps Studio to export the current `pa.yaml` format. easel never crashes
on an unparsable formula — it reports it and continues.
