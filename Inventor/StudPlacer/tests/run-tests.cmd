@echo off
REM Full verification of the StudPlacer engine, on Windows.
REM Requires the .NET SDK:  winget install Microsoft.DotNet.SDK.8
REM and Python 3 for the rule-table linter.
setlocal
set HERE=%~dp0
set ROOT=%HERE%..
set DOTNET_CLI_TELEMETRY_OPTOUT=1
set DOTNET_NOLOGO=1

echo ==^> 1/4  deployability (would a fresh clone run?)
python "%ROOT%\tools\check_deployable.py" || exit /b 1

echo.
echo ==^> 2/4  linting code-rule tables
python "%ROOT%\tools\check_rule_tables.py" || exit /b 1

echo.
echo ==^> 3/4  compile-checking the iLogic rules
python "%HERE%ilogic-syntax\wrap_rules.py" || exit /b 1
dotnet build "%HERE%ilogic-syntax\ILogicSyntax.vbproj" -v q --nologo || exit /b 1

echo.
echo ==^> 4/4  engine regression suite
dotnet run --project "%HERE%StudPlacer.Tests.vbproj" -v q --nologo || exit /b 1
