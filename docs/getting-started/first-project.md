---
layout: default
title: Your first project
parent: Getting Started
nav_order: 2
---

# Your first project

## 1. Create a project directory

```bash
aicr create my-study.project/
```

This scaffolds a complete project directory with template configuration files.

## 2. Configure endpoints

Copy `config/endpoints.template.json` to `config/endpoints.json` and fill in your endpoint URLs and auth structure:

```json
{
  "endpoints": {
    "my_endpoint": {
      "url": "https://example.com/api",
      "auth": { "type": "api_key", "key": "my_key", "header": "Authorization", "scheme": "Bearer" },
      "fields": []
    }
  }
}
```

## 3. Configure secrets

Copy `config/secrets.template.json` to `config/secrets.json` and fill in your API key values:

```json
{
  "keys": {
    "my_key": "sk-..."
  }
}
```

`secrets.json` is gitignored and should never be committed. For Azure deployments using `DefaultAzureCredential`, no `secrets.json` is needed — run `az login` once instead (see [Azure prerequisites](/running/cli/#azure-prerequisites)).

## 4. Edit deployments and inputs

- `config/deployments.json` — add or adjust the model deployment arms to test
- [`input/text.json`](/input/texts/) — the text corpus and ground truth section headings
- [`input/queries.json`](/input/queries/) — query templates and task types
- [`input/prompts.json`](/input/prompts/) — bindings between texts and queries

## 5. Run

```bash
aicr run my-study.project/
```

Output lands in `my-study.project/output/<timestamp>/`. See [Output](/output/) for details.

## 6. What to expect

While running, `aicr` prints a progress line per request showing the deployment, set, and rep. At the end of each run it prints an [identity groups](/glossary/#identity-group) summary — responses grouped by content hash — and the total elapsed time.

If a request fails mid-run (network error, auth expiry), the error is logged and `aicr` continues with the remaining requests. The partial output directory is preserved as-is; re-running creates a new timestamped directory alongside it.

See [Troubleshooting](/running/troubleshooting/) if you encounter auth errors or repeated failures.
