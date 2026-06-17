#!/usr/bin/env bash
set -e
dotnet restore src/
dotnet build   --project src/
dotnet run     --project src/
