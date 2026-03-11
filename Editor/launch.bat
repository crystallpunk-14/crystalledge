@echo off
cd /d "%~dp0.."
echo Starting SS14 Prototype Editor...
dotnet run --project Content.Editor\Content.Editor.csproj -- %*
