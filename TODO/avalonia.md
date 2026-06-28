# Plan: Avalonia Desktop GUI (`aicr-gui`)

## Context

The CLI harness (`aicr`) runs a long async experiment and streams logs to the terminal. Option C from `GUI.md` adds a proper native desktop window so users can pick a project folder, watch progress live, and browse results — all without a terminal. This plan implements a v1 `aicr-gui` binary: file picker → config summary → Run → live log + progress bar → results DataGrid.

Config editing and charts are deferred to v2 to keep scope bounded.

---

## Architecture

A new `src/gui/` project (`aicr-gui`) references `src/core/` and `src/shared/` unchanged. Two small changes go into `src/core/` to make config loading and logging reusable:

1. **Extract config loading** — Pull the inline `RunExperiment()` loading block out of `src/cli/Program.cs` into a new `ProjectLoader` class in `src/core/Utils/`.
2. **Fan-out Logger** — Add `SetExtraWriter(TextWriter?)` to `Logger` so the GUI can attach a channel-backed writer.

The CLI is refactored to call `ProjectLoader.Load()` but its behaviour is otherwise unchanged.

---

## Implementation Steps

### Step 1 — Move `FlattenJson` to `HarnessUtils`
**File:** `src/core/Utils/HarnessUtils.cs`

Add as `public static void FlattenJson(JsonElement element, string prefix, Dictionary<string,string> result)`. Currently a `private static` in `src/cli/Program.cs` (line 741); needed by `ProjectLoader`.

### Step 2 — Create `src/core/Utils/ProjectLoader.cs`

**`ProjectLoader.ReadOpts`** — shared `JsonSerializerOptions` (snake_case + enum converter + `SemanticVersionJsonConverter`). Currently duplicated four times in `Program.cs`.

**`LoadedProject` record:**
```csharp
public sealed record LoadedProject(
    RunConfig Config,
    List<BoundPrompt> BoundPrompts,
    Dictionary<string, IDeploymentExecutor> Executors,
    string OutDir,
    string Stamp);
```

**`LoadedProjectSummary` record:**
```csharp
public sealed record LoadedProjectSummary(
    string ProjectName,
    string? Author,
    int DeploymentCount,
    IReadOnlyList<string> DeploymentLabels,
    int Sets,
    int Reps,
    int BoundPromptCount,
    int TotalRuns,
    bool IsReadOnly);
```

**`ProjectLoader.Load(string projectDir, Logger logger, SemanticVersion? harnessVersion = null)`**
— Port of `Program.cs` `RunExperiment()` lines 199–378 (config reads + executor construction). Calls `HarnessUtils.FlattenJson()`. Throws `InvalidOperationException` on validation failures.

**`ProjectLoader.Preview(string projectDir)`**
— Loads only `project.json` + `experiment.json` + `deployments.json` (no secrets/executors). Returns `LoadedProjectSummary`. Best-effort: works even when `secrets.json` is absent.

### Step 3 — Refactor `src/cli/Program.cs`

In `RunExperiment()`:
- Replace lines 199–378 with `var loaded = ProjectLoader.Load(projectDir, logger, GetHarnessVersion())`.
- Remove private `FlattenJson` (moved to `HarnessUtils`).
- Replace four inline `JsonSerializerOptions` constructions with `ProjectLoader.ReadOpts`.
- `GetHarnessVersion()` stays private on `Program`.

Net change: `Program.cs` loses ~190 lines with no behaviour change.

### Step 4 — Extend `Logger`
**File:** `src/core/Utils/Logger.cs`

Add one field and one method:
```csharp
private TextWriter? _extraWriter;

public void SetExtraWriter(TextWriter? writer)
{
    lock (_lock) { _extraWriter = writer; }
}
```

In `Write()`, inside the `lock (_lock)` block, after `_sysFile?.WriteLine(sysTagged)` (line 41):
```csharp
_extraWriter?.WriteLine(sysTagged);
```

The extra writer receives timestamped lines at all levels (same as `_sysFile`).

### Step 5 — Create `src/gui/AICopyrightReproducibility.Gui.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <AssemblyName>aicr-gui</AssemblyName>
    <Version>0.9.0</Version>
    <AssemblyVersion>0.9.0.0</AssemblyVersion>
    <NoWarn>$(NoWarn);OPENAI001</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Avalonia"               Version="11.3.0" />
    <PackageReference Include="Avalonia.Desktop"       Version="11.3.0" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="11.3.0" />
    <PackageReference Include="Avalonia.ReactiveUI"    Version="11.3.0" />
    <PackageReference Include="ReactiveUI"             Version="20.1.1" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../core/AICopyrightReproducibility.Core.csproj" />
  </ItemGroup>
</Project>
```

No `Directory.Build.props` override needed — `src/gui/` targets `net8.0` only, so the root props (`build/bin/` + `build/obj/`) apply as-is. (The viewer needed its own override because Blazor WASM adds a `browser-wasm` target.)

### Step 6 — `src/gui/` file structure

```
src/gui/
├── AICopyrightReproducibility.Gui.csproj
├── Program.cs
├── App.axaml
├── App.axaml.cs
├── Services/
│   └── ChannelTextWriter.cs
├── ViewModels/
│   ├── ViewModelBase.cs
│   ├── MainViewModel.cs
│   └── DeploymentResultRow.cs
└── Views/
    ├── MainWindow.axaml
    └── MainWindow.axaml.cs
```

### Step 7 — `ChannelTextWriter` (`src/gui/Services/ChannelTextWriter.cs`)

`TextWriter` backed by a bounded `Channel<string>`. Logger calls `WriteLine()` on any thread (inside its lock); the GUI drains `Reader` on the UI thread.

```csharp
internal sealed class ChannelTextWriter : TextWriter
{
    private readonly Channel<string> _channel = Channel.CreateBounded<string>(
        new BoundedChannelOptions(4096) { FullMode = BoundedChannelFullMode.DropOldest });

    public ChannelReader<string> Reader => _channel.Reader;
    public override Encoding Encoding => Encoding.UTF8;
    public override void WriteLine(string? value) => _channel.Writer.TryWrite(value ?? "");
    protected override void Dispose(bool disposing)
        { _channel.Writer.TryComplete(); base.Dispose(disposing); }
}
```

Why `Channel<string>` and not write directly to `ObservableCollection`? Logger's `Write()` runs on worker threads inside `_lock`. Writing to `ObservableCollection` from non-UI threads is unsafe; the channel decouples producer (any thread) from consumer (UI drain loop) and the `DropOldest` policy bounds memory.

### Step 8 — `MainViewModel` (`src/gui/ViewModels/MainViewModel.cs`)

Reactive properties (`this.RaiseAndSetIfChanged`): `ProjectDir`, `Summary` (`LoadedProjectSummary?`), `State` (enum: Idle/Running/Completed/Failed), `ProgressCompleted`, `ProgressTotal`, `ErrorMessage`.

Observable collections: `LogLines`, `Results` (`ObservableCollection<DeploymentResultRow>`).

Derived booleans (raised from `State` setter): `IsRunning`, `HasResults`, `CanRun` (Idle + Summary non-null + not read-only).

Commands:
- `BrowseCommand` — opens `OpenFolderDialog` via injected `Func<Task<string?>>` delegate; sets `ProjectDir`.
- `LoadConfigCommand` — calls `ProjectLoader.Preview(ProjectDir)`; sets `Summary`.
- `RunCommand` (enabled when `CanRun`) — calls `ExecuteRun()`.

**`ExecuteRun()` outline:**
```csharp
State = Running;
_ = DrainLogChannelAsync();

await Task.Run(async () =>
{
    using StreamWriter fileWriter = new StreamWriter(logPath) { AutoFlush = true };
    using Logger logger = new Logger(
        TextWriter.Null, TextWriter.Null,   // no console in GUI mode
        fileWriter, Logger.Level.Verbose);
    logger.SetExtraWriter(_channelWriter);

    // Progress<T> constructed on UI thread → callback auto-marshals via SynchronizationContext
    var progress = new Progress<RunProgressEvent>(evt => {
        ProgressCompleted = evt.Completed;
        ProgressTotal     = evt.Total;
    });

    var loaded = ProjectLoader.Load(ProjectDir, logger, GetGuiVersion());

    using HarnessRunner runner = new HarnessRunner(
        loaded.Config, loaded.BoundPrompts, loaded.Executors,
        loaded.OutDir, logger, progress);

    List<RunRecord> records = await runner.RunAllAsync();
    // OutputWriter.WriteManifestCsv / manifest.json / project.json writes here

    var rows = records.GroupBy(r => r.Deployment)
        .Select(g => new DeploymentResultRow {
            Deployment    = g.Key,
            SuccessCount  = g.Count(r => r.Status == 200),
            ErrorCount    = g.Count(r => r.Status != 200),
            AvgDurationMs = g.Average(r => r.DurationMs)
        }).OrderBy(r => r.Deployment).ToList();

    await Dispatcher.UIThread.InvokeAsync(() => {
        foreach (var r in rows) Results.Add(r);
        State = Completed;
    });
});
```

`DrainLogChannelAsync()` reads from `_channelWriter.Reader` and dispatches each line to `LogLines` via `Dispatcher.UIThread.InvokeAsync`.

### Step 9 — `MainWindow.axaml` layout

5-row `Grid`:

| Row | Content |
|-----|---------|
| 0 | Directory `TextBox` + Browse button + Load Config button |
| 1 | Config summary panel (`IsVisible` ← `Summary != null`) — project name, deployment count, sets/reps, total runs, Read-Only badge, **Run** button |
| 2 | Scrollable log `ItemsControl` (monospace, `LogLines`) |
| 3 | `ProgressBar` (`IsVisible` ← `IsRunning`) |
| 4 | Results `DataGrid` (`IsVisible` ← `HasResults`) — columns: Deployment, Success, Errors, Avg ms |

`MainWindow.axaml.cs`: subscribe to `LogLines.CollectionChanged` and call `LogScrollViewer.ScrollToEnd()` via `Dispatcher.UIThread.Post`.

### Step 10 — `App.axaml.cs` wiring

```csharp
var vm = new MainViewModel();
desktop.MainWindow = new MainWindow { DataContext = vm };
vm.SetBrowseDelegate(() => new OpenFolderDialog().ShowAsync(desktop.MainWindow));
```

### Step 11 — `src/gui/Program.cs`

```csharp
AppBuilder.Configure<App>()
    .UsePlatformDetect()
    .WithInterFont()
    .LogToTrace()
    .StartWithClassicDesktopLifetime(args);
```

### Step 12 — Update `scripts/build/build.sh`

```bash
dotnet restore src/gui/
dotnet build   src/gui/
```

---

## Critical Files

| File | Change |
|------|--------|
| `src/core/Utils/HarnessUtils.cs` | Add `FlattenJson` as public static |
| `src/core/Utils/ProjectLoader.cs` | **New** — `LoadedProject`, `LoadedProjectSummary`, `Load()`, `Preview()`, `ReadOpts` |
| `src/core/Utils/Logger.cs` | Add `_extraWriter` field + `SetExtraWriter()` + one call inside `Write()` |
| `src/cli/Program.cs` | Replace ~190 lines with `ProjectLoader.Load()` call; remove `FlattenJson`; use `ProjectLoader.ReadOpts` |
| `src/gui/*` | **New project** — 9 new files |
| `scripts/build/build.sh` | Add two lines for `src/gui/` |

Unchanged and reused directly: `HarnessRunner`, `IDeploymentExecutor`, `RunRecord`, `RunProgressEvent`, `OutputWriter`, all executor implementations.

---

## Verification

1. `dotnet build src/core/` — no new warnings.
2. `dotnet build src/cli/` — succeeds; `aicr version` prints correctly.
3. `dotnet build src/gui/` — succeeds.
4. `dotnet run --project src/gui/` — window opens; Browse picks a project dir; Load Config shows summary; Run executes and populates log + progress + results DataGrid.
5. `dotnet test tests/` — existing tests pass (no `HarnessUtils` or `Logger` behaviour changed).
