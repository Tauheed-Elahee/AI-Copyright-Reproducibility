#!/usr/bin/env bash
set -e
PROJECT="${1:-medical-texts.project}"
dotnet restore src/
dotnet build   src/
dotnet run --project src/ -- "$PROJECT"
