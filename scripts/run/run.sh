#!/usr/bin/env bash
set -e
if [ -z "$1" ]; then
    echo "Usage: run.sh <project-dir>" >&2
    exit 1
fi
PROJECT="$1"
dotnet restore src/cli/
dotnet build   src/cli/
dotnet run --project src/cli/ -- "$PROJECT"
