# Security Policy

## Reporting a vulnerability

Please report security issues privately via GitHub Security Advisories
("Report a vulnerability" on the repository's **Security** tab) rather than a public issue.

Include: affected version (`pacheck --version`), reproduction steps, and impact. We aim to
acknowledge within a few days.

## Scope

pacheck is a read-only static analysis CLI (the only writing command is `rename`, which
writes a new file and never modifies its input). Relevant concerns:

- **Untrusted input** — pacheck parses arbitrary `pa.yaml`. Parsing is tolerant and must
  never execute app logic; formulas are parsed, never evaluated. A crash on malformed input
  is a bug worth reporting.
- **Subprocess** — `pac` is invoked with an explicit argument list (no shell), only for
  unpack/pack.
- **No exfiltration** — analysis makes no network calls (see
  [docs/telemetry.md](docs/telemetry.md)). The `explain`/`fix` commands only reach the
  network with an explicit `--send` to a provider you configure.

## Secret scanning

The `secrets` command and `PA2xxx` rules flag likely secrets. Findings **redact** the
value (first/last characters + length). Machine output never contains the full secret.
