@echo off
setlocal
set "PROJECT=%~1"
if "%PROJECT%"=="" (
    echo Usage: run.bat ^<project-dir^> >&2
    exit /b 1
)
dotnet restore src\cli\
if %errorlevel% neq 0 exit /b %errorlevel%
dotnet build   src\cli\
if %errorlevel% neq 0 exit /b %errorlevel%
dotnet run --project src\cli\ -- "%PROJECT%"
