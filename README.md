# easel

[![CI](https://github.com/lukoplt/easel/actions/workflows/ci.yml/badge.svg)](https://github.com/lukoplt/easel/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/EaselCli.svg)](https://www.nuget.org/packages/EaselCli)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

Static analysis, dependency analysis, metrics, secrets scanning, semantic diff and
rename for **Power Apps canvas source** (`pa.yaml`).

A single self-contained CLI (+ `dotnet tool`) that treats your canvas app as source
code: lint it, measure it, find dead code, scan for secrets, diff two versions, and
rename symbols — all deterministic, CI-first, read-only by default.

📖 **Docs:** <https://lukoplt.github.io/easel/>

```
$ easel lint ./MyApp
Src/scrHome.pa.yaml
  23:9    warning PA1009 Interactive control 'btnSubmit' (Button) has no AccessibleLabel.
  27:23   info    PA1010 'If' nested 3 deep (threshold 2).
Src/App.pa.yaml
  3:14    warning PA1003 Variable 'gblUnused' is assigned but never read.

3 findings — 2 warnings, 1 info
```

Validated against real production apps: **2500+ formulas parsed with zero parse errors.**

## Why

Power CAT and the checker target review inside a tenant. easel targets the **dev loop**:
run it locally and in CI over the `pa.yaml` you already commit, get machine-readable
output (JSON / SARIF), and gate pull requests.

## Install

```bash
dotnet tool install --global Microsoft.PowerApps.CLI.Tool   # pac (only needed for .msapp)
dotnet tool install --global EaselCli                     # easel
easel doctor                                                # verify the environment
```

Or grab a self-contained binary (win-x64 / osx-arm64 / linux-x64) from
[Releases](https://github.com/lukoplt/easel/releases).

**Requirements:** [.NET 10 SDK](https://dotnet.microsoft.com/) (for the `dotnet tool` or
building from source). [pac CLI](https://learn.microsoft.com/power-platform/developer/cli/introduction)
is required only to read `.msapp` / solution `.zip` inputs — unpack/pack goes exclusively
through `pac`. Already-unpacked `pa.yaml` folders (Git integration) are read directly.

## Commands

| Command | What it does |
|---|---|
| `easel lint <path>` | Run rules, report findings (`--format console\|json\|sarif\|html`). |
| `easel stats <path>` | Metrics: controls/screen, media size, formula complexity. |
| `easel analyze <path>` | `--find <sym>`, `--dead-code`, `--impact <sym>`, `--graph mermaid\|dot`. |
| `easel secrets <path>` | Secret scan + data-source inventory. |
| `easel diff <base> <head>` | Semantic diff (`--format console\|markdown\|json`). |
| `easel rename <msapp> --from X --to Y` | Rename a symbol and repack (**preview**). |
| `easel explain <path> --rule <id>` | Explain a finding via an LLM (opt-in; nothing sent without `--send`). |
| `easel fix <path> --rule <id>` | Suggest a formula fix (opt-in AI; never auto-applies). |
| `easel doctor` | Environment diagnostics. |

`<path>` is an unpacked source folder, a `.msapp` file, or a solution `.zip` (with a
single canvas app). **Full [usage guide with examples](docs/usage.md).**
See also [docs/](docs/): [rules](docs/rules.md) ·
[configuration](docs/configuration.md) · [CI](docs/ci.md) · [diff](docs/diff.md) ·
[rename](docs/rename.md) · [AI](docs/ai.md) · [compatibility](docs/compatibility.md).

## Exit codes

| Code | Meaning |
|---|---|
| 0 | OK |
| 1 | Findings at or above `--fail-on` threshold |
| 2 | Input error (bad path, pre-YAML format) |
| 3 | pac missing or incompatible |
| 4 | Internal error |

## Configuration

Drop a `.easel.yml` at your repo root (searched upward). See
[`docs/configuration.md`](docs/configuration.md) and the sample [`.easel.yml`](.easel.yml).

```yaml
rules:
  screen-control-limit: { max: 300 }
  deep-nested-if: { threshold: 2 }
  naming-convention:
    patterns: { variable: "^(gbl|var|loc)[A-Z]", collection: "^col[A-Z]" }
ignore:
  - "**/Legacy*.pa.yaml"
```

**Baseline** — adopt easel on a legacy app without drowning in findings:

```bash
easel lint ./MyApp --write-baseline   # record today's findings
easel lint ./MyApp                     # subsequent runs report only new ones
```

## Rules

Full reference: [`docs/rules.md`](docs/rules.md).

| ID | Rule | Category |
|---|---|---|
| PA1001 | Non-delegable query over a data source | Performance |
| PA1002 | N+1 pattern (`ForAll` + data op) | Performance |
| PA1003 | Unused variable / collection | Maintainability |
| PA1004 | Unused media asset | Maintainability |
| PA1005 | Screen control limit | Performance |
| PA1006 | Heavy `App.OnStart` | Performance |
| PA1007 | Naming conventions (opt-in) | Naming |
| PA1008 | Hardcoded colour outside theme | Maintainability |
| PA1009 | Missing `AccessibleLabel` | Accessibility |
| PA1010 | Deeply nested `If` | Maintainability |
| PA1011 | Timer side-effects | Maintainability |
| PA1012 | Duplicate formula | Maintainability |
| PA1013 | Inconsistent control version | Maintainability |
| PA2001 | Hardcoded secret (key/token/connection string) | Security |
| PA2002 | High-entropy literal | Security |
| PA2003 | URL with embedded credentials | Security |
| PA0002 | Source file failed to load | Error |
| PF0001 | Unparsable formula | Error |

## CI

Use the bundled GitHub Action to lint on every PR and upload SARIF to code scanning —
see [`docs/ci.md`](docs/ci.md).

```yaml
- uses: lukoplt/easel/action@v0
  with:
    path: ./src/MyApp
    format: sarif
    fail-on: warning
```

## Build from source

```bash
dotnet build
dotnet test
dotnet run --project src/Easel.Cli -- lint tests/fixtures/SampleApp
```

## Architecture

See [`easel-architektura-a-tasky.md`](easel-architektura-a-tasky.md) for the full design.
In short: `pac`/folder/zip → YAML loader → immutable `AppModel` → Power Fx AST (cached) →
symbol table + dependency graph → commands → renderers. The model, symbols and graph are
built **once** and shared across commands.

Projects: `Easel.Core` (model/loader/symbols/graph/config/baseline), `Easel.Fx` (Power Fx
parse + AST), `Easel.Rules` (rule engine), `Easel.Analysis` (stats/secrets/analyze/diff/
rename), `Easel.Output` (renderers), `Easel.Pac` (pac runner + input resolver), `Easel.Cli`.

## Contributing

New rules and real-world fixtures are the most valuable contributions — see
[CONTRIBUTING.md](CONTRIBUTING.md) and the [roadmap](ROADMAP.md). No telemetry, ever
([why](docs/telemetry.md)).

## License

[MIT](LICENSE).

---

Made with ❤ by Lukáš Oplt
