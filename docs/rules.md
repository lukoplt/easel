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

### PA1014 — nested-forall · warning
A `ForAll` nested inside another `ForAll` — O(n²) row iteration. Precompute the inner
lookup once (`With` / `AddColumns` / `GroupBy`) instead of re-iterating per row.

### PA1015 — repeated-expensive-call · warning
The same expensive call (`LookUp`/`Filter`/`Sort`/`Sum`/…) repeated verbatim within one
formula — each occurrence re-evaluates the query. Wrap it in
`With({result: <call>}, …)` and reuse. `{ min-length: 20, min-occurrences: 2 }`.

### PA1016 — countrows-filter · info
`CountRows(Filter(source, condition))` materialises the filtered table just to count it.
Use `CountIf(source, condition)` — it counts without materialising and delegates on more
sources.

### PA1017 — first-filter · info
`First(Filter(source, condition))` fetches a whole filtered table to use one row. Use
`LookUp(source, condition)` (with an optional third argument for the field).

### PA1018 — sequential-data-loads · info
Two or more `Collect`/`ClearCollect` calls running sequentially in `OnStart`/`OnVisible`
outside a `Concurrent(...)`. Independent loads in `Concurrent` run in parallel and cut
startup time. `{ min-loads: 2 }`.

### PA1019 — inefficient-delayed-load · warning
A formula references a control that lives on another screen, forcing that screen to load
eagerly (App checker: *Inefficient delay loading*). Share the value via a variable,
collection or `Navigate` context record instead. **Conservative:** fires only for control
names with exactly one definition in the app, so shadowed or reused names never produce a
false positive.

### PA1028 — delay-output-text-input · info
A Text input whose `.Text` feeds a `Filter`/`LookUp`/`Search` without `DelayOutput`
enabled — the query re-runs on every keystroke (solution checker:
*app-use-delayoutput-text-input*). Set `DelayOutput` to `true`.

---

## Maintainability

### PA1003 — unused-variable · warning
A variable (`Set`/`UpdateContext`) or collection (`Collect`/`ClearCollect`) that is
assigned but never read anywhere in the app.

### PA1004 — unused-media · info
A media asset that is not referenced by identifier or by name in any formula. Name
matching is whole-token: `logo` counts as referenced in `"https://x/logo.png"` but not in
`"logotype"`, so a substring can never mask a genuinely unused asset.

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

### PA1027 — unused-screen · info
A screen that nothing references — no `Navigate`, no `StartScreen`, no formula. Fires
only when `App.StartScreen` is set: with a known start screen a zero-reference screen is
provably unreachable; without it the start screen cannot be determined from source, so
the rule stays silent rather than guess.

---

## Accessibility

Aligned with the Power Apps Studio Accessibility checker — each rule maps to one of its
issue types.

### PA1009 — missing-accessible-label · warning
An interactive control (button, input, toggle, …, or anything with `OnSelect`) with no
non-empty `AccessibleLabel`.

### PA1020 — focus-not-visible · warning
A control with `FocusedBorderThickness` set to `0` — keyboard users cannot see where
focus is. (*Focus isn't showing*.)

### PA1021 — missing-captions · warning
An `Audio`/`Video` control with no `ClosedCaptionsUrl`. (*Missing captions*.)

### PA1022 — default-screen-name · info
A screen still named `Screen1`, `Screen2`, … — screen readers announce screen names on
navigation. (*Revise screen name*.)

### PA1023 — positive-tab-index · info
A control with `TabIndex` greater than `0`. Custom tab orders are hard to maintain and
break screen readers — use `0`/`-1` and reorder with layout. (*Check the order of the
screen items*.)

### PA1024 — autostart-media · warning
An `Audio`/`Video` control with `AutoStart` true. Autoplaying media is disorienting and
hard to stop for keyboard users. (Solution checker: *app-avoid-autostart*.)

### PA1025 — state-indication · info
A stateful control (toggle, slider, rating, checkbox) with `ShowValue` false — users get
no confirmation of its state. (*Add State indication text*.)

### PA1026 — pen-alternative-input · info
A `Pen` input on a screen with no `Text input` — some users cannot use a pen and need
another way to provide the information. (*Add another input method*.)

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

### PA0002 — file-load-error · error
A whole source file failed to parse (malformed YAML). Reported with its location so a
broken file is never silently dropped from analysis — fix the YAML, or re-open and re-save
in Power Apps Studio.

### PF0001 — unparsable-formula · error
A formula Power Fx could not parse. Fix the syntax, or (for legacy apps) re-open and
re-save in Power Apps Studio to export the current `pa.yaml` format. easel never crashes
on an unparsable formula — it reports it and continues.
