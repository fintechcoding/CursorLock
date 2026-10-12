$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
& dotnet run --project (Join-Path $projectRoot 'tests\CursorLock.Tests\CursorLock.Tests.csproj') -c Release -- $projectRoot
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }

