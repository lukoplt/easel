---
title: easel
---

# easel

Static analysis, dependency analysis, metrics, secrets scanning, semantic diff and rename
for **Power Apps canvas source** (`pa.yaml`). A single CLI that treats your canvas app as
source code — deterministic, CI-first, read-only by default.

## Install

```bash
dotnet tool install --global Microsoft.PowerApps.CLI.Tool   # pac (for .msapp)
dotnet tool install --global EaselCli                    # easel
easel doctor
```

## Docs

- [**Usage guide** — every command with examples](usage.md)
- [Quickstart & commands](https://github.com/lukoplt/easel#commands)
- [Rule reference](rules.md)
- [Configuration](configuration.md)
- [CI integration](ci.md)
- [diff](diff.md) · [rename (preview)](rename.md)
- [AI layer (opt-in)](ai.md)
- [Compatibility](compatibility.md)
- [Telemetry (there is none)](telemetry.md)
- [Example output](examples.md)

## At a glance

```
$ easel lint ./MyApp
Src/scrHome.pa.yaml
  23:9    warning PA1009 Interactive control 'btnSubmit' (Button) has no AccessibleLabel.
  27:23   info    PA1010 'If' nested 3 deep (threshold 2).
3 findings — 2 warnings, 1 info
```
