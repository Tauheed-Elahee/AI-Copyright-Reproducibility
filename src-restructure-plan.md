# Plan: Reorganise src/ into src/cli, src/core, src/viewer

## Context

Currently the repo has three flat sibling directories: `src/`, `src-core/`, `src-viewer/`.
Moving them to `src/cli/`, `src/core/`, `src/viewer/` groups all source code under one tree,
which is cleaner for a multi-project repo and scales better if more projects are added.

---

## Directory moves

```
src/        → src/cli/
src-core/   → src/core/
src-viewer/ → src/viewer/
```

---

## File updates (path strings only — no logic changes)

| File | Change |
|---|---|
| `src/cli/AICopyrightReproducibility.csproj` | ProjectRef `../src-core/` → `../core/` |
| `src/viewer/AICopyrightReproducibility.Viewer.csproj` | ProjectRef `../src-core/` → `../core/` |
| `tests/AICopyrightReproducibility.Tests.csproj` | ProjectRef `../src-core/` → `../src/core/` |
| `scripts/build/build.sh` + `build.bat` | `src/` → `src/cli/` |
| `scripts/run/run.sh` + `run.bat` | `src/` → `src/cli/` |
| `scripts/viewer/update-data.sh` + `update-data.bat` | `src-viewer/` → `src/viewer/` |
| `.github/workflows/pages.yml` | `src-viewer/` → `src/viewer/` |
| `.github/workflows/release.yml` | `src/` → `src/cli/` |
| `.gitignore` | `src-viewer/obj/` → `src/viewer/obj/` |
| `README.md` | Layout section paths |
| `GUI.md` | `src-viewer/` references |

**Does NOT need changes:** `src/viewer/Directory.Build.props` — `GetPathOfFileAbove` searches
upward from `src/`, finds nothing there, continues to repo root and locates
`Directory.Build.props` automatically.

---

## Order of operations

1. `git mv src src/cli` — wait, git mv can't move a parent into itself.
   Correct approach:
   ```bash
   mkdir src/core src/viewer
   git mv src-core/* src/core/
   git mv src-viewer/* src/viewer/
   # src/ content moves last — rename it to a temp name first
   git mv src src-cli-tmp
   git mv src-cli-tmp src/cli
   ```
   Actually simpler: use a staging temp directory outside src/:
   ```bash
   mv src src_cli_tmp
   mkdir src
   mv src_cli_tmp src/cli
   git mv src-core src/core
   git mv src-viewer src/viewer
   git add -A
   ```

2. Update all path strings in the files listed above.

3. Verify: `dotnet build src/cli/ && dotnet test tests/ && dotnet build src/viewer/`

---

## Verification

```bash
dotnet build src/cli/
dotnet test tests/
dotnet build src/viewer/
bash scripts/build/build.sh
bash scripts/run/run.sh example.project/
```
