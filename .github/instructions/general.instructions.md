---
applyTo: "**"
---

# General Project Notes

## Building the Project

The project is large and takes **~5 minutes** to compile. When building via terminal, always use a **background task** to avoid blocking the session.

### Recommended build workflow

1. **Start the build as a background process** — redirect output to files so you can inspect results later:

```powershell
Start-Process -FilePath "dotnet" `
  -ArgumentList "build","Content.Server/Content.Server.csproj","-v","q" `
  -Wait -NoNewWindow `
  -RedirectStandardOutput "build_out.txt" `
  -RedirectStandardError "build_err.txt"
```

Or use the VS Code background terminal (`isBackground: true`) with a long timeout.

2. **Check build results** from the output files:

```powershell
# Count CS errors
$errs = Get-Content build_err.txt | Select-String "error CS"
Write-Host "ERRORS=$($errs.Count)"
$errs | ForEach-Object { $_.Line }

# Check summary (last lines)
Get-Content build_out.txt | Select-Object -Last 5
```

3. **Clean up temp files** after inspecting results:

```powershell
Remove-Item build_out.txt, build_err.txt -ErrorAction SilentlyContinue
```

> **Always delete `build_out.txt` and `build_err.txt`** after checking results — they should never be committed to the repo.

4. The output summary is in Russian. Key lines to look for:
   - `Ошибок: 0` — **0 errors** (build succeeded)
   - `Предупреждений: N` — N warnings (existing upstream warnings are expected, ~100+)

### Important notes

- **Do NOT** run `dotnet build` as a foreground terminal command with short timeouts — it will get cancelled mid-build.
- Pre-existing warnings from `RobustToolbox/` (NU1510, CS0618) are expected and not project errors.
- The IDE error checker (`get_errors` tool) reflects compilation state faster than a full terminal build — use it for quick validation.
- When only CE files are changed, incremental builds are faster (~15-30s), but a cold build takes the full ~5 minutes.

### Build targets

| Target | Command |
|--------|---------|
| Server only | `dotnet build Content.Server/Content.Server.csproj` |
| Client only | `dotnet build Content.Client/Content.Client.csproj` |
| Full solution | `dotnet build` (builds everything) |
| YAML linter | `dotnet build Content.YAMLLinter/Content.YAMLLinter.csproj` |

## Testing

- Unit tests: `dotnet test Content.Tests/Content.Tests.csproj`
- Integration tests: `dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj`
- Always build before running tests (`--no-build` flag is used in VS Code tasks).
