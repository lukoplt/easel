# AI layer (opt-in)

`explain` and `fix` are optional helpers over pacheck's deterministic results. The core
analysis never uses AI — these commands are a thin layer on top of findings.

## Privacy first

**Nothing leaves your machine unless you pass `--send`.** The default is a dry run that
prints the exact prompt that *would* be sent, so you can review it before opting in.

```bash
# Dry run — no network. Prints the prompt.
pacheck explain ./MyApp --rule PA1009

# Actually call a provider (you choose which).
export PACHECK_AI_KEY=sk-...
pacheck explain ./MyApp --rule PA1009 --send \
  --endpoint https://api.openai.com/v1/chat/completions --model gpt-4o-mini
```

## Commands

### explain
Explains a finding with its formula context.

```bash
pacheck explain <path> --rule <id> [--send] [--endpoint <url>] [--model <id>] [--api-key-env <VAR>]
```

### fix
Suggests a corrected formula as a diff. **Never auto-applies.** Any suggestion is validated
by re-parsing it with Power Fx before it is shown.

```bash
pacheck fix <path> --rule PF0001 [--send] [--endpoint <url>] [--model <id>]
```

## Provider

The provider is **OpenAI-compatible** (`/v1/chat/completions`), so it works with:

- a local endpoint (LM Studio, Ollama's OpenAI shim, llama.cpp server) — data stays local,
- or a hosted API, if you accept sending finding context to that service.

| Option | Meaning | Default |
|---|---|---|
| `--endpoint` | Chat-completions URL | — (required with `--send`) |
| `--model` | Model id | `gpt-4o-mini` |
| `--api-key-env` | Env var holding the API key | `PACHECK_AI_KEY` |

## What is sent

Only when `--send` is set: the rule id, message, location and the single formula in
question. No file trees, no other formulas, no credentials. Review the exact payload any
time by dropping `--send` (dry run).
