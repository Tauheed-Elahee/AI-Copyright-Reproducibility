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

`secrets.json` is gitignored and should never be committed. For Azure deployments using `DefaultAzureCredential`, no `secrets.json` is needed — run `az login` once instead (see [Azure prerequisites](../running/cli#azure-prerequisites)).

## 4. Edit deployments and inputs

- `config/deployments.json` — add or adjust the model deployment arms to test
- `input/text.json` — the text corpus
- `input/queries.json` — query templates
- `input/prompts.json` — bindings between texts and queries

## 5. Run

```bash
aicr run my-study.project/
```

Output lands in `my-study.project/output/<timestamp>/`. See [Output](../output) for details.
