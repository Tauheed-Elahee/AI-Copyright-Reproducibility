# GUI Options

The harness is a .NET 8 cross-platform CLI. Key constraints for any GUI:
- Runs a long async operation (`RunAllAsync`) that must not block the UI thread
- Log output flows through `Logger` (stdout/stderr + two file writers)
- Deployed as a self-contained single-file binary — heavy frameworks inflate binary size
- Used on Linux, macOS, and Windows

---

## Option A — TUI (Terminal.Gui)

Adds a full-screen terminal UI inside the existing console window.

| Dimension | Detail |
|---|---|
| **Library** | `Terminal.Gui` (NuGet, ~500 KB, MIT) |
| **Cross-platform** | Yes — Linux, macOS, Windows |
| **Binary size impact** | Small (+~1 MB self-contained) |
| **Deployment change** | None — still a single binary run from the terminal |
| **What it can show** | Log panel (scrollable), progress bar, run summary table, status line |
| **Key integration point** | Logger gains a `TextWriter` that writes to a TUI `TextView` widget; `HarnessRunner` emits progress events |
| **Async model** | Terminal.Gui has its own event loop; `RunAllAsync` runs on a background thread and marshals updates via `Application.MainLoop.Invoke()` |
| **Effort estimate** | **Medium** — 2–3 days. Logger and HarnessRunner are already well-isolated. Main work is wiring the TUI layout and the thread-safe update path. |
| **Risk** | Terminal.Gui has occasional rendering quirks on some terminals; SSH sessions can be tricky |

---

## Option B — Local Browser Dashboard

Embeds a small HTTP server; the user opens `http://localhost:PORT` in their browser.

| Dimension | Detail |
|---|---|
| **Library** | `Microsoft.AspNetCore` (already in the .NET runtime for net8.0) |
| **Cross-platform** | Yes |
| **Binary size impact** | Medium (+~5–8 MB self-contained) |
| **Deployment change** | Harness opens a browser tab on startup (or prints the URL); user must not kill it before the run finishes |
| **What it can show** | Anything — real-time log stream via WebSocket, progress bar, full results table, JSON viewer |
| **Key integration point** | Logger gains a `TextWriter` that pushes lines into a `Channel<string>` fed to a WebSocket hub; a small HTML/JS page subscribes |
| **Async model** | Cleanest option — ASP.NET Core and `RunAllAsync` share the same async runtime naturally |
| **Effort estimate** | **Medium-High** — 3–5 days. The server plumbing and WebSocket broadcast are straightforward; the HTML/JS dashboard is the variable cost. A minimal dashboard (log stream + progress) is fast; a full results viewer takes longer. |
| **Risk** | Firewall/port availability on locked-down machines; user must keep the browser tab open |

---

## Option C — Native Desktop Window (Avalonia)

A proper windowed application with forms, panels, and native OS chrome.

| Dimension | Detail |
|---|---|
| **Library** | `Avalonia` (NuGet, MIT) |
| **Cross-platform** | Yes — Linux (X11/Wayland), macOS, Windows |
| **Binary size impact** | Large (+~30–50 MB self-contained) |
| **Deployment change** | Different execution model — the window is the entry point; no terminal needed |
| **What it can show** | Full config editor, file picker for project dir, live log panel, results DataGrid, charts |
| **Key integration point** | Requires MVVM restructuring — `MainViewModel` owns the run state; `HarnessRunner` reports progress via `IProgress<T>` or `IObservable`; Logger writes to an `ObservableCollection<string>` |
| **Async model** | Avalonia's dispatcher handles UI-thread marshalling; `RunAllAsync` runs via `Task.Run` |
| **Effort estimate** | **High** — 1–2 weeks for a usable first version. The execution model and config loading need significant restructuring. |
| **Risk** | Largest scope creep risk; Avalonia on Linux requires a display server (no headless); binary size may conflict with the single-file deployment story |

---

## Recommendation

**Option A (TUI)** if the goal is a richer view while keeping the terminal workflow unchanged.  
**Option B (Browser)** if the goal is a polished real-time dashboard with minimal deployment friction.  
**Option C (Avalonia)** only if a full desktop application (config editing, file pickers, etc.) is the end goal.

---

## Implemented — Blazor WASM Results Viewer

A read-only post-run results viewer has been built as a separate Blazor WASM project (`src-viewer/`), distinct from the above options (which are all about a live GUI during execution).

| Dimension | Detail |
|---|---|
| **Hosted at** | `ai-copyright-reproducibility.tauheed-elahee.com/viewer/` (GitHub Pages) |
| **Default data** | `medical-texts.project` run committed in `src-viewer/wwwroot/data/manifest.json` |
| **File picker** | Load any local `manifest.json` from a completed run |
| **What it shows** | Run summary, per-deployment stats table, identity groups (grouped by content SHA-256) |
| **Binary size** | ~21 MB `_framework/` (Blazor WASM runtime + IL-trimmed assemblies); not part of the CLI binary |
| **Deployment** | Built in CI (`deploy-pages` job), merged into `_site/viewer/` after Jekyll build |
| **Key technical note** | References `src-core/` for `RunRecord`; Azure.Identity and OpenAI SDK code is eliminated by the IL trimmer at publish time. Requires `src-viewer/Directory.Build.props` to give the project its own `obj/` — the shared `build/obj/` from `Directory.Build.props` only has `net8.0` targets and not `browser-wasm`. |

This does not replace Options A–C, which remain the paths forward for a live execution GUI.
