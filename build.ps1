param([string]$EnginePath = "$PSScriptRoot/engine/codex-app-server-x86_64-pc-windows-msvc.exe")
$ErrorActionPreference = "Stop"
if (!(Test-Path -LiteralPath $EnginePath -PathType Leaf)) { throw "Place the engine folder from the complete release beside build.ps1 first." }
$hashSource = Get-Content "$PSScriptRoot/source/EngineHash.cs" -Raw
$expected = [regex]::Match($hashSource, 'Value="([a-fA-F0-9]{64})"').Groups[1].Value
if (!$expected -or (Get-FileHash -LiteralPath $EnginePath -Algorithm SHA256).Hash -ne $expected) { throw "Official engine SHA256 mismatch." }
$version = ([xml](Get-Content "$PSScriptRoot/source/Quota.csproj")).Project.PropertyGroup.Version
$dist = Join-Path $PSScriptRoot "dist"
New-Item -ItemType Directory -Force $dist | Out-Null
$stage = Join-Path $dist ("Quota-" + $version + "-" + [guid]::NewGuid().ToString("N"))
dotnet publish "$PSScriptRoot/source/Quota.csproj" -c Release -r win-x64 --self-contained true -o $stage
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }
New-Item -ItemType Directory -Force "$stage/engine" | Out-Null
Copy-Item -LiteralPath $EnginePath -Destination "$stage/engine/"
Copy-Item "$PSScriptRoot/licenses" $stage -Recurse
Copy-Item "$PSScriptRoot/使用说明.txt" $stage
$zip = Join-Path $dist "Quota-v$version-Windows.zip"
Compress-Archive -Path "$stage/*" -DestinationPath $zip -Force
Write-Output "Created $zip"
