# Plan: Global Variables Page (Option C)

## Context

Queries use `{placeholder}` syntax in `system_message` and `user_prompt`. Currently the only substitution values come from the bound text entry (`{title}`, `{sections}`, `{sections_shuffled}`, and any extra title fields like `{year}`). A Global Variables page would let the user define project-wide key-value pairs (e.g. `{institution}`, `{jurisdiction}`) that are available in any query prompt, independent of which text is bound.

---

## Config Changes

### New file: `config/variables.json`

```json
{
  "variables": {
    "institution": "University of Example",
    "jurisdiction": "EU"
  }
}
```

### `project.json` wiring

Add a `files.variables` entry under `project.fs.config.files` (same pattern as `experiment`, `deployments`):

```json
"files": {
  "experiment":   "experiment.json",
  "deployments":  "deployments.json",
  "variables":    "variables.json"
}
```

### New config class: `VariablesConfig`

```csharp
// src/core/Config/VariablesConfig.cs
public sealed class VariablesConfig
{
    public Dictionary<string, string> Variables { get; set; } = new();
}
```

### `FsConfig` update

Add `Variables` property to the `FsFilesConfig` class (wherever `Experiment`/`Deployments` are declared in `RuntimeTypes.cs` or similar).

---

## Core Changes

### `ProjectLoader.cs`

Load `variables.json` the same way `secrets.json` is loaded — check existence, deserialize, pass to the harness config.

### `HarnessUtils.ResolvePrompt`

After built-in replacements (`{title}`, `{sections}`, `{sections_shuffled}`), apply text extras, **then** apply global variables. Resolution order (later wins):

1. Global variables (`variables.json`)
2. Text extra fields (`content.title.*`)
3. Built-ins (`{title}`, `{sections}`, `{sections_shuffled}`)

This means built-ins always override globals, preventing accidental shadowing of core placeholders.

### `HarnessUtils.ValidateTemplate`

Extend the valid placeholder set to include global variable keys.

### `BoundPrompt` / load pipeline

Pass `VariablesConfig` through from `ProjectLoader` to the harness so `HarnessRunner` can forward global variables to `ResolvePrompt`.

---

## GUI Changes

### New ViewModel: `VariablesViewModel.cs`

Mirror the Secrets page pattern:

- `ObservableCollection<VariableRowViewModel> Rows`
- `VariableRowViewModel? SelectedRow` — has `Key` and `Value` reactive properties
- `CanEdit`, `HasRows`, `SaveError`, `SaveSuccess`
- `SaveCommand`, `AddCommand`, `DeleteCommand`
- `LoadFrom(VariablesConfig cfg, string? filePath)`
- `ExecuteSave()` writes `{ "variables": { ... } }` to file

### `MainViewModel.cs`

Add `VariablesViewModel? Variables` property. In `ExecuteLoadConfig`, load `variables.json` (same pattern as secrets) and capture file path.

### Navigation

Add a new FANavigationViewItem "Variables" (e.g. between Secrets and Inputs, Tag="4", shifting Inputs/Run/Output/etc. indices by 1). Add `IsVariablesPage => _selectedTabIndex == 4` and update all downstream index constants.

### `MainWindow.axaml`

Add the Variables page using the same master-detail Grid layout as Secrets:
- Left 180px ListBox showing key names
- Right panel: Key TextBox + Value TextBox
- Save bar at the bottom

---

## Collision Resolution Display

In the Queries tab, show a note that global variables have lower priority than text extra fields and built-ins, so `{title}` always refers to the text title even if a global variable named `title` is defined.

---

## Verification

1. `dotnet build src/gui/ -c Release` — 0 errors
2. Add `config/variables.json` with `{ "variables": { "institution": "Test Uni" } }`
3. Open project → Variables page shows the entry
4. Edit value, save → file updated on disk
5. In a query, use `{institution}` in `user_prompt` → loads without validation error
6. Run → prompt correctly substituted
7. Define a variable named `title` → text's `{title}` still resolves to `content.title.full` (built-in wins)
8. Open project without `config/variables.json` → "No variables file found" message, controls disabled
