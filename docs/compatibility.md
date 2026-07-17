# Compatibility

easel depends on three moving targets: the `pac` CLI, the `pa.yaml` schema, and the
Microsoft.PowerFx parser. This page records what each build targets and how compatibility
is guarded.

## Matrix

| Component | Targeted | Notes |
|---|---|---|
| .NET | 10 (LTS) | Single self-contained binary + `dotnet tool`. |
| Microsoft.PowerFx.Core | 1.8.1 | Parse-only; validated by `PowerFxParseSpike`. |
| pa.yaml schema | v3.0 | Unknown keys are tolerated, not rejected. |
| pac CLI | ≥ 1.30.0 | `pac canvas unpack/pack` (Preview). Verified by `easel doctor`. |

## How compatibility is guarded (TX.1 / TX.2)

- **PowerFx** — `PowerFxParseSpike` parses a spread of real formulas and asserts a stable
  pass count. A parser regression across a version bump fails the suite. This is the
  project's designated go/no-go guard.
- **pac** — `PacRunner.MinSupportedVersion` gates unpack/pack; `doctor` reports the local
  version against it. A scheduled CI job (`pac-matrix.yml`) installs the **latest** pac and
  runs `doctor` so a breaking pac change surfaces early.
- **pa.yaml schema** — the loader interprets only recognised sections and ignores unknown
  keys, so additive schema changes do not break analysis. A file that cannot be parsed at
  all surfaces as a `PA0002` finding rather than being silently dropped.

## Known limitations

- `pac canvas unpack/pack` is marked **Preview** and prints a deprecation notice in
  pac 2.6+. easel pins a minimum version and tracks the replacement command; the
  round-trip used by `rename` is preview accordingly.
- Legacy pre-YAML apps are detected and reported with an instruction to re-save in Power
  Apps Studio; they are not analysed directly.
