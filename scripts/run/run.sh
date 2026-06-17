#!/usr/bin/env bash
set -e
if [ -z "$1" ]; then
    echo "Usage: run.sh <project-dir>" >&2
    exit 1
fi
PROJECT="$1"
dotnet restore src/
dotnet build   src/
dotnet run --project src/ -- "$PROJECT"
