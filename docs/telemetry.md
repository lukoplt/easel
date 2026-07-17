# Telemetry

**Decision: pacheck collects no telemetry. None. There is no opt-in and no opt-out to
configure, because nothing is ever sent.**

## What this means

- No usage counts, no run counts, no timing, no crash reports.
- No network calls of any kind during analysis. The only commands that can reach the
  network are `explain` / `fix`, and only when you explicitly pass `--send` to a provider
  you configure (see [ai.md](ai.md)).
- `pac` is invoked as a local subprocess for unpack/pack only.

## Why

pacheck runs over source that often contains business logic and, occasionally, secrets it
is trying to help you find. Sending anything by default would be at odds with that purpose.
Being a deterministic, offline-by-default tool is a feature.

## If this ever changes

Any future telemetry would be **opt-in only**, off by default, documented here in full
(exact fields, destination, retention), and announced in the changelog. Until this file
says otherwise, assume zero.
