# HostClientPresetTests ausführen

Das Testprojekt ist ein klassisches, nicht SDK-basiertes .NET-Framework-4.8.1-Projekt. Deshalb funktioniert `dotnet run --project ...` hier nicht.

Vom Workspace-Stamm aus in PowerShell zuerst mit MSBuild kompilieren und anschließend die erzeugte EXE ausführen:

```powershell
dotnet msbuild '_inspect\HostClientPresetTests\HostClientPresetTests.csproj' /t:Build /p:Configuration=Release /verbosity:minimal
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& '.\_inspect\HostClientPresetTests\bin\HostClientPresetTests.exe'
exit $LASTEXITCODE
```

Ein erfolgreicher Lauf endet mit einer `PASS:`-Zeile und Exitcode `0`.
