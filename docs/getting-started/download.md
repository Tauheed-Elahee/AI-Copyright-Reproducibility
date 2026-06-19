---
layout: default
title: Download
parent: Getting Started
nav_order: 1
---

# Download

Pre-built self-contained binaries are available from [GitHub Releases](https://github.com/Tauheed-Elahee/AI-Copyright-Reproducibility/releases).

| Platform | File |
|----------|------|
| Linux x64 | `aicr-linux-x64` |
| Windows x64 | `aicr-win-x64.exe` |
| macOS x64 | `aicr-osx-x64` |

Also download `example.project.tar.gz` from the same release — this is a template project directory you can copy to start a new study.

## Build from source

Requires .NET SDK 8.0 or later.

```bash
dotnet run --project src/cli/ -- run <project-dir>
```

Or build a self-contained binary:

```bash
dotnet publish src/cli/ -c Release -r linux-x64 --self-contained \
  -p:PublishSingleFile=true -p:AssemblyName=aicr -o dist/
./dist/aicr run <project-dir>
```

Replace `linux-x64` with `win-x64` or `osx-x64` for other platforms.
