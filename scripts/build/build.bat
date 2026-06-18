@echo off
setlocal
dotnet restore src\cli\
if %errorlevel% neq 0 exit /b %errorlevel%
dotnet build   src\cli\
