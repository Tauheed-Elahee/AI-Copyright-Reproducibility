# Plan: GUI v2 — Experiment Settings Editor

## Context

The v1 GUI can load a project and run an experiment, but all settings are read-only. Before a run, users may want to adjust iteration counts, parallelism, retry policy, timing pauses, or random seeds without leaving the app to edit `experiment.json` by hand. This plan adds an in-app settings editor as a second tab in the main window.

Config editing for `deployments.json` is deferred (harder: untyped dicts, secret templating, mode-dependent schemas).

---

## Architecture

`ExperimentConfig` lives in a separate file pointed to by `project.json → Project.Fs.Config.Files.Experiment`. The GUI reads it after `Preview()` succeeds and writes it back on explicit Save. The run pipeline re-reads it from disk when the user clicks Run, so no in-memory hand-off is needed.

A new `ExperimentSettingsViewModel` owns all editable fields. `MainViewModel` holds an instance of it (null until config is loaded). The AXAML gets a `TabControl` wrapping the existing "Run" content and the new "Settings" tab.

---

## Changes

### 1. New `src/gui/ViewModels/ExperimentSettingsViewModel.cs`

Reactive properties (all `RaiseAndSetIfChanged`), grouped by section:

**Iterations**
- `decimal Sets` (≥ 1) ← `ExperimentConfig.Iterations.Set`
- `decimal Reps` (≥ 1) ← `ExperimentConfig.Iterations.Rep`

**Parallelism**
- `bool ParallelDeployment` ← `Parallel.Level.Deployment`
- `bool ParallelRep` ← `Parallel.Level.Rep`
- `bool ParallelPrompt` ← `Parallel.Level.Prompt`
- `decimal MaxConcurrency` (≥ 0, 0 = unlimited) ← `Parallel.MaxConcurrency`

**Retry**
- `decimal RetryMaxAttempts` (≥ 1) ← `Retry.MaxAttempts`
- `decimal RetryInitialDelayS` (≥ 0) ← `Retry.InitialDelayS`
- `decimal RetryMaxDelayS` (≥ 0) ← `Retry.MaxDelayS`

**Timing pauses (seconds)**
- `decimal PauseSetS` ← `Timing.Pause.Run.Set`
- `decimal PausePromptS` ← `Timing.Pause.Run.Prompt`
- `decimal PauseRepS` ← `Timing.Pause.Run.Rep`
- `decimal PauseDeploymentS` ← `Timing.Pause.Run.Deployment`

**Seeds**
- `decimal SeedShufflePrompt` ← `Seed.Shuffle.Run.Prompt`
- `decimal SeedShuffleDeployment` ← `Seed.Shuffle.Run.Deployment`
- `decimal SeedShuffleContent` ← `Seed.Shuffle.Content`

**Output**
- `string LogLevel` ← `ExperimentConfig.LogLevel`; valid values: `"info"`, `"verbose"`, `"warning"`, `"error"`
- `bool OmitNullFields` ← `ExperimentConfig.OmitNullFields`

**Other members:**
- `string? ExperimentFilePath` — public reactive property, absolute path to `experiment.json`
- `bool CanEdit` — computed from `ExperimentFilePath != null`; raised from `ExperimentFilePath` setter
- `string? SaveError` — reactive property set on serialization failure
- `static string[] LogLevels` — `{ "info", "verbose", "warning", "error" }` for ComboBox binding
- `ReactiveCommand SaveCommand` — enabled when `ExperimentFilePath != null` (via `.Select()` to avoid overload ambiguity)

**`LoadFrom(ExperimentConfig cfg, string? filePath)`** — populates all properties, stores `filePath`.

**`SaveCommand`** — reconstructs `ExperimentConfig` with clamped values (`Math.Max` guards), serializes with `new JsonSerializerOptions(ProjectLoader.ReadOpts) { WriteIndented = true }`, writes to `ExperimentFilePath`. Sets `SaveError` on exception.

> Note: All numeric ViewModel properties are `decimal` (not `int`/`double`) because Avalonia's `NumericUpDown.Value` is `decimal?`. Conversion to `int`/`double` happens in `SaveCommand` via cast + clamp.

### 2. Modify `src/gui/ViewModels/MainViewModel.cs`

Add `ExperimentSettingsViewModel? Settings` reactive property.

In `ExecuteLoadConfig()`:
- Set `Settings = null` at the top (alongside clearing `ErrorMessage`)
- After `Summary` is set, populate `Settings` via `Task.Run`:
  - Re-read `project.json` to resolve `Fs.Config.Dir` and `Fs.Config.Files.Experiment`
  - Construct absolute path; if file exists, deserialize `ExperimentConfig` from it
  - `new ExperimentSettingsViewModel(); vm.LoadFrom(expCfg, expFilePath ?? null)`

### 3. Modify `src/gui/Views/MainWindow.axaml`

Replace the top-level `<Grid>` with a `<TabControl>` containing two `<TabItem>`s:

- **Tab "Run"**: existing 6-row Grid unchanged
- **Tab "Settings"**: `IsEnabled` bound to `Settings` via `ObjectConverters.IsNotNull`
  - `<ScrollViewer>` containing a `<StackPanel x:CompileBindings="False" DataContext="{Binding Settings}">`
  - `x:CompileBindings="False"` avoids needing a second `x:DataType` declaration for the inner bindings
  - `IsVisible="{Binding !CanEdit}"` note shown when experiment is embedded in project.json
  - `IsVisible="{Binding CanEdit}"` form shown otherwise
  - Six `<Border>` sections: Iterations, Parallelism, Retry, Timing pauses, Seeds, Output
  - `<NumericUpDown>` for all numeric fields; `<CheckBox>` for booleans; `<ComboBox ItemsSource="{x:Static vm:ExperimentSettingsViewModel.LogLevels}">` for log level
  - `[Save settings]` button + red `SaveError` TextBlock at bottom

---

## Implementation Notes

- Overload ambiguity with `WhenAnyValue(x => x.Prop, p => p != null)` — use `.Select()` instead: `this.WhenAnyValue(x => x.ExperimentFilePath).Select(p => p != null)`
- `using System.Reactive.Linq` required in both `ExperimentSettingsViewModel.cs` and `MainViewModel.cs`
- `new JsonSerializerOptions(existingOptions)` copy constructor requires .NET 8 (fine — project targets net8.0)

---

## Critical files

| File | Change |
|------|--------|
| `src/gui/ViewModels/ExperimentSettingsViewModel.cs` | **New** |
| `src/gui/ViewModels/MainViewModel.cs` | Add `Settings` property; populate in `ExecuteLoadConfig()` |
| `src/gui/Views/MainWindow.axaml` | Wrap in `TabControl`; add Settings tab |

No changes to `src/core/`.

---

## Verification

1. `dotnet build src/gui/` — 0 errors, 0 warnings.
2. `dotnet test tests/` — all 36 tests pass.
3. Manual smoke-test:
   - Load a project → Settings tab enables.
   - Change Sets to 5 → Save → open `experiment.json` and confirm `"set": 5`.
   - Reload config → Settings tab reflects saved value.
   - Run tab still works with newly saved settings.
   - If experiment is embedded in `project.json` (no separate file): form is hidden; note shown.
