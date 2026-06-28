# Plan: GUI UX Improvements

## Context

The v1 GUI compiles and runs but has four usability problems that surface on the first real use:

1. **Dead after first run** — `canRun` requires `State == Idle`; after Completed/Failed the Run button is permanently disabled until the app restarts.
2. **Blind progress** — the progress bar shows `N / total` but `RunProgressEvent.LastRecord` (which carries deployment, text label, query label) is never read, so the user has no idea what is currently executing.
3. **No cancellation** — `HarnessRunner.RunAllAsync()` takes no `CancellationToken`. There is no Cancel button and no way to stop a long run.
4. **No way to open results** — after a run completes the output directory path is logged but there is no one-click way to open it.

---

## Changes

### 1. `src/core/HarnessRunner.cs` — add cancellation

`RunAllAsync()` gains `CancellationToken ct = default`. Propagated throughout:

- `RunDeployment()`: call `ct.ThrowIfCancellationRequested()` before `_sem?.WaitAsync(ct)`  
- All five `Task.Delay(...)` calls become `Task.Delay(..., ct)`  
- `_sem.WaitAsync()` → `_sem.WaitAsync(ct)`  

`OperationCanceledException` propagates naturally out of `RunAllAsync()`; the CLI is unaffected (it never passes a token).

### 2. `src/gui/ViewModels/MainViewModel.cs`

**New fields / properties:**
- `CancellationTokenSource? _runCts`  
- `string? _outputDir` → public `OutputDir` property (raises `PropertyChanged`)
- `string? _currentOperation` → public `CurrentOperation` property (raises `PropertyChanged`)

**Re-run capability:**  
Change `canRun` observable from `state == RunState.Idle` to:
```csharp
(summary, state) => summary != null && !summary.IsReadOnly &&
    (state == RunState.Idle || state == RunState.Completed || state == RunState.Failed)
```
`ExecuteRun()` already resets `State = Running`, `LogLines.Clear()`, `Results.Clear()` at the top, so re-entry is clean. Also clear `OutputDir` at that point.

**Cancel command:**  
Add `CancelCommand = ReactiveCommand.Create(() => _runCts?.Cancel(), this.WhenAnyValue(x => x.IsRunning))`.

**Progress callback update** (inside `ExecuteRun`, the existing `Progress<RunProgressEvent>` lambda):
```csharp
var progress = new Progress<RunProgressEvent>(evt =>
{
    ProgressCompleted  = evt.Completed;
    ProgressTotal      = evt.Total;
    CurrentOperation   = $"{evt.LastRecord.Deployment}  ·  {evt.LastRecord.TextLabel} / {evt.LastRecord.QueryLabel}";
});
```

**CancellationToken wiring:**  
Before `Task.Run(...)`:
```csharp
_runCts = new CancellationTokenSource();
var ct = _runCts.Token;
```
Pass `ct` to `runner.RunAllAsync(ct)`.  
In `finally`: `_runCts?.Dispose(); _runCts = null;`

**OutputDir:**  
Set at the same point `State = RunState.Completed` is set inside `Dispatcher.UIThread.InvokeAsync`.

**Cancellation vs. failure:**  
Wrap the existing `catch (Exception ex)` to distinguish cancellation:
```csharp
catch (OperationCanceledException)
{
    await Dispatcher.UIThread.InvokeAsync(() =>
    {
        CurrentOperation = null;
        State            = RunState.Idle;   // not Failed — user chose to stop
    });
}
catch (Exception ex)
{
    await Dispatcher.UIThread.InvokeAsync(() =>
    {
        ErrorMessage     = ex.Message;
        CurrentOperation = null;
        State            = RunState.Failed;
    });
}
```

**Clear on new run:** Set `OutputDir = null; CurrentOperation = null;` at the top of `ExecuteRun()`.

**Run log markers:** Inside the `Task.Run` block, immediately after the logger is created:
```csharp
logger.Info($"═══ RUN STARTED {stamp} ═══");
```
And immediately before setting `State = Completed`:
```csharp
logger.Info("═══ RUN COMPLETED ═══");
```

### 3. `src/gui/Views/MainWindow.axaml`

**Progress row (Row 3):** Expand from 2 columns to 3; add Cancel button in column 2:
```xml
<Grid Grid.Row="3" ColumnDefinitions="*,Auto,Auto" IsVisible="{Binding IsRunning}" Margin="0,0,0,4">
  <ProgressBar Grid.Column="0" ... />
  <TextBlock   Grid.Column="1" Text="{Binding ProgressCompleted}" Margin="8,0,4,0" VerticalAlignment="Center" />
  <Button      Grid.Column="2" Content="Cancel" Command="{Binding CancelCommand}" />
</Grid>
```

**Current operation label** — new row between progress bar and results panel. Change root `Grid` `RowDefinitions` from `"Auto,Auto,*,Auto,Auto"` to `"Auto,Auto,*,Auto,Auto,Auto"` and shift the results Panel from `Grid.Row="4"` to `Grid.Row="5"`:
```xml
<TextBlock Grid.Row="4"
           Text="{Binding CurrentOperation}"
           FontSize="11"
           IsVisible="{Binding CurrentOperation, Converter={x:Static StringConverters.IsNotNullOrEmpty}}"
           Margin="0,0,0,6" />
```

**Open output folder button** — add inside the results Panel at Row 5, above the results DataGrid:
```xml
<Button Content="Open output folder"
        Command="{Binding OpenOutputFolderCommand}"
        IsVisible="{Binding OutputDir, Converter={x:Static StringConverters.IsNotNullOrEmpty}}"
        HorizontalAlignment="Left" Margin="0,0,0,6" />
```

### 4. `OpenOutputFolderCommand` in `MainViewModel`

```csharp
OpenOutputFolderCommand = ReactiveCommand.Create(() =>
{
    if (OutputDir is not null)
        System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo { FileName = OutputDir, UseShellExecute = true });
},
this.WhenAnyValue(x => x.OutputDir, d => d != null));
```

`UseShellExecute = true` with a directory path opens the file manager on all three platforms (Windows Explorer, macOS Finder, xdg-open on Linux).

---

## Critical files

| File | Change |
|------|--------|
| `src/core/HarnessRunner.cs` | Add `CancellationToken ct` to `RunAllAsync` and propagate to all 5 `Task.Delay` calls, `_sem.WaitAsync`, and one `ThrowIfCancellationRequested` check |
| `src/gui/ViewModels/MainViewModel.cs` | Re-run gate, CancelCommand, CurrentOperation, OutputDir, OpenOutputFolderCommand, cancellation-vs-failure handling, log markers |
| `src/gui/Views/MainWindow.axaml` | Cancel button, CurrentOperation label (new row), Open output folder button; add one `RowDefinition` |

---

## Verification

1. `dotnet build src/core/ src/gui/` — 0 errors, 0 warnings.
2. `dotnet test tests/` — all 36 tests pass (HarnessRunner signature change is backwards-compatible; `ct = default` means existing CLI call `runner.RunAllAsync()` is unchanged).
3. Manual smoke-test:
   - Run experiment → Cancel mid-run → Run button re-enables immediately, log shows no error, State returns to Idle.
   - Run experiment to completion → Run button re-enables → run a second time without restarting.
   - After completion → "Open output folder" button appears and opens the file manager at the correct path.
   - Progress row shows deployment/text/query label updating as records complete.
