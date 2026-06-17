#!/usr/bin/env bash
set -e
dotnet restore --project src/
dotnet build   --project src/
dotnet run     --project src/
