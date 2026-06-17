#!/usr/bin/env bash
set -e
dotnet restore tests/
dotnet test    tests/
