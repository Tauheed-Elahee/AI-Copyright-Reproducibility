#!/usr/bin/env bash
set -e
dotnet restore src/
dotnet build   src/
dotnet run     --project src/AICopyrightReproducibility.csproj
