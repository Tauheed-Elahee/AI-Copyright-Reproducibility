@echo off
setlocal
dotnet restore tests\
if %errorlevel% neq 0 exit /b %errorlevel%
dotnet test    tests\
