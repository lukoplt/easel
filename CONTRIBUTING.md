# Contributing to easel

Thanks for helping. The most valuable contributions are **new lint rules** and **real-world
fixtures**.

## Getting started

```bash
git clone https://github.com/lukoplt/easel
cd easel
dotnet build
dotnet test
dotnet run --project src/Easel.Cli -- lint tests/fixtures/SampleApp
```

Requires the .NET 10 SDK. `pac` is only needed to work with `.msapp` inputs.

## Project layout

See [the architecture doc](easel-architektura-a-tasky.md). In short:

- `Easel.Core` — model, loader, symbol table, dependency graph, config, baseline.
- `Easel.Fx` — Power Fx parse + AST walker.
- `Easel.Rules` — `IRule` implementations + engine.
- `Easel.Analysis` — stats, secrets, analyze, diff, rename.
- `Easel.Output` — console/json/sarif/html renderers.
- `Easel.Pac` — `pac` runner + input resolver (no dependency on Core).
- `Easel.Cli` — command wiring.

## Adding a rule

1. Implement `IRule` (usually via `RuleBase`) in `src/Easel.Rules/Builtin/`.
   Pick the next free id in its category (`PA1xxx` perf/maintainability/naming/a11y,
   `PA2xxx` security).
2. Rules are discovered by reflection — no registration needed.
3. Add a fixture (or extend `ComponentApp`) that trips it, and a test in
   `RuleCoverageTests`.
4. Document it in [`docs/rules.md`](docs/rules.md).
5. **Favour fewer false positives.** A noisy rule erodes trust; prefer conservative
   heuristics with an easy suppression path (config + baseline).

## Conventions

- `.editorconfig` is authoritative; keep the build warning-clean.
- Everything read-only except `rename`. Never modify the input.
- Deterministic output — findings sort stably; no timestamps in machine formats.
- New behaviour gets a test. Snapshot tests use [Verify](https://github.com/VerifyTests/Verify);
  run `dotnet test`, review the `*.received.*` diff, and promote it to `*.verified.*`.

## Pull requests

Keep them focused. Reference the architecture task (e.g. `T2.3`) where relevant. CI must be
green. Be kind — see the [Code of Conduct](CODE_OF_CONDUCT.md).
