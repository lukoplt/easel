# Roadmap

Status against the [architecture plan](pacheck-architektura-a-tasky.md). Versions are
milestones, not dates.

## Shipped (implemented in this repo)

- **Core** — YAML loader (source locations), immutable AppModel, Power Fx parse + AST
  walker (cached, tolerant), symbol table, dependency graph.
- **lint** — rule engine (reflection discovery, per-rule config, severity override, ignore
  globs), 16 rules (`PA1001–1013`, `PA2001–2003`, `PF0001`) + `PA0002` load errors,
  baseline, console/json/sarif/html, exit codes, `--fail-on`.
- **stats**, **analyze** (find / dead-code / impact / graph), **secrets** (+ inventory).
- **diff** — matching, rename heuristic, AST-level formula diff, console/markdown/json.
- **rename** (preview) — collision-checked, `pac` round-trip.
- **explain / fix** (opt-in AI, offline by default).
- **Ecosystem** — GitHub Action, CI + release workflows, `dotnet tool` package, docs.

## Needs a live environment (code done, not yet exercised)

- Running CI / release / the Action on GitHub; SARIF in code scanning.
- `.msapp` round-trip fixtures for `rename` E2E and the pac pack round-trip spike (T0.2).

## Planned

- **v0.2** — richer delegation/N+1 heuristics; more real-world fixtures.
- **v0.3** — diff → PR comment wired into the Action.
- **v0.4** — rename promoted from preview after E2E hardening.
- **Distribution** — winget + Homebrew publishing, Azure DevOps task (manifests included
  under `packaging/` and `azure-devops/`), docs site on GitHub Pages.
- **Rules** — community-contributed rules are the priority; see
  [CONTRIBUTING](CONTRIBUTING.md).

## Non-goals

- Telemetry (see [docs/telemetry.md](docs/telemetry.md)).
- Re-implementing `pac` unpack/pack.
- Replacing in-tenant review tools (Power CAT) — pacheck targets the dev loop.
