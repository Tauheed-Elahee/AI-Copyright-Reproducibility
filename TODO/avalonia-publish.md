# Future: Publish Pipeline for aicr-gui

## Goal

Ship `aicr-gui` as a release binary alongside the CLI `aicr` binary, so users don't need to build from source.

## Current state

- The CLI (`src/cli/`) is published via the release workflow.
- The GUI (`src/gui/`) builds successfully but is not included in any release artifact.

## What needs to change

### 1. `scripts/build/build.sh` / `build.bat`

Add a `dotnet publish` step for the GUI in addition to (or replacing) the current `dotnet build`:
```bash
dotnet publish src/gui/ \
  -c Release \
  -r linux-x64 \         # or win-x64 / osx-x64
  --self-contained true \
  -p:PublishSingleFile=true \
  -o build/publish/gui/
```

### 2. Release workflow (`.github/workflows/` or equivalent)

- Build both `aicr` (CLI) and `aicr-gui` for each target platform.
- Upload `aicr-gui` as a release asset alongside `aicr`.

### 3. Platform considerations

Avalonia GUI apps on Linux require a display (X11 or Wayland). The release binary will work on desktop Linux but not headless servers. Document this in the release notes.

On macOS, Avalonia apps need a `.app` bundle or must be run via `open`. Consider whether to ship a raw binary or a bundled app.

On Windows, the binary should work as-is (no additional runtime needed if self-contained).

### 4. Binary naming

Current output binary is `aicr-gui` (set via `<AssemblyName>` in the csproj). This is fine for Linux/macOS. On Windows, add `.exe` via the publish target.

## Notes

- Self-contained publish increases binary size (~60–100 MB) but removes the need for the user to install the .NET runtime.
- Framework-dependent publish is smaller but requires .NET 8 installed on the target machine.
- Consider adding a `--version` flag to `aicr-gui` (currently only the CLI has this) so the version appears in the binary.
