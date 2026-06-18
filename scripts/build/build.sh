#!/usr/bin/env bash
set -e
dotnet restore src/cli/
dotnet build   src/cli/
