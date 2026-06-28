# Future: Post-Run Charts

## Goal

After a run completes, show visual summaries of results directly in the GUI without requiring the user to open CSV files externally.

## Proposed charts

- **Bar chart — success rate by deployment**: x-axis = deployment labels, y-axis = % of requests that returned HTTP 200. Makes cross-deployment comparison immediate.
- **Box / scatter chart — duration distribution**: Shows spread of `DurationMs` values per deployment. Useful for spotting outliers and latency differences.

## Data source

`manifest.json` written to the output directory at the end of each run. Already parsed into `List<RunRecord>` at run completion — the ViewModel already groups by deployment for the results DataGrid.

## Suggested library

**OxyPlot.Avalonia** (`OxyPlot.Avalonia`, `OxyPlot.Core`) — MIT licensed, Avalonia 11 compatible, widely used.

Package references to add to `src/gui/aicr-gui.csproj`:
```xml
<PackageReference Include="OxyPlot.Avalonia" Version="2.1.2" />
```

## Placement

A "Charts" section inside the Output tab, below the results DataGrid. Or a dedicated Charts sub-tab if the Output tab becomes crowded.

## Notes

- `PlotModel` constructed on the background thread; `OxyPlot.Avalonia.PlotView` renders on the UI thread.
- Colors should follow the system theme if possible (OxyPlot supports custom palettes).
- Consider making charts optional / behind a toggle if they slow down UI on large result sets.
