[CmdletBinding()]
param([switch]$SkipInstaller, [string]$InnoCompiler)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $projectRoot 'src\CursorLock\CursorLock.csproj'
$artifacts = Join-Path $projectRoot 'artifacts'
$publish = Join-Path $artifacts 'publish'
New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
& dotnet publish $project -c Release -r win-x64 --self-contained true -o $publish -p:PublishSingleFile=false -p:RuntimeFrameworkVersion=10.0.12
if ($LASTEXITCODE -ne 0) { throw 'Publishing failed.' }
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination $publish
$archive = Join-Path $artifacts 'CursorLock-Portable-win-x64.zip'
Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $archive -Force
if (-not $SkipInstaller) {
    if (-not $InnoCompiler) {
        $compilerCommand = Get-Command ISCC.exe -ErrorAction SilentlyContinue
        if ($compilerCommand) { $InnoCompiler = $compilerCommand.Source }
        else {
            $programFiles32 = [Environment]::GetEnvironmentVariable('ProgramFiles(x86)')
            $candidates = @(
                (Join-Path $projectRoot '.tools\inno\ISCC.exe'),
                "$programFiles32\Inno Setup 6\ISCC.exe",
                "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
                "$env:ProgramFiles\Inno Setup 7\ISCC.exe"
            )
            $InnoCompiler = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
        }
    }
    if (-not $InnoCompiler) { throw 'Install Inno Setup 6.7 or newer, or pass -InnoCompiler with the path to ISCC.exe.' }
    & $InnoCompiler (Join-Path $projectRoot 'installer\CursorLock.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
}
Get-ChildItem -LiteralPath $artifacts -File | Where-Object { $_.Extension -in '.exe', '.zip' } | ForEach-Object {
    $hash = Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256
    "$($hash.Hash.ToLowerInvariant())  $($_.Name)"
} | Set-Content -LiteralPath (Join-Path $artifacts 'SHA256SUMS.txt') -Encoding ascii
Write-Host "Build completed. Artifacts: $artifacts"

