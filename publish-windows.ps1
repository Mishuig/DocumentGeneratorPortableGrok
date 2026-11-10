$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'DocumentGenerator.csproj'
$output  = Join-Path $PSScriptRoot 'publish\win-x64'
$version = ([xml](Get-Content $project)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw '.NET 10 SDK is required. Install it from https://dotnet.microsoft.com/download/dotnet/10.0' }
if (Test-Path $output) { Remove-Item $output -Recurse -Force }
dotnet restore $project
dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o $output
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
if (-not (Test-Path (Join-Path $output 'DocumentGenerator.exe'))) { throw 'DocumentGenerator.exe was not produced.' }

# Portable package extras
Copy-Item (Join-Path $PSScriptRoot 'App.ico') $output -Force
Copy-Item (Join-Path $PSScriptRoot 'create-shortcut.ps1') $output -Force
Copy-Item (Join-Path $PSScriptRoot 'Create-Shortcut.cmd') $output -Force
if (Test-Path (Join-Path $PSScriptRoot 'samples')) { Copy-Item (Join-Path $PSScriptRoot 'samples') (Join-Path $output 'samples') -Recurse -Force }

$zip = Join-Path $PSScriptRoot "publish\DocumentGenerator-$version-portable-win-x64.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $output '*') -DestinationPath $zip -Force
Write-Host "Portable folder: $output" -ForegroundColor Green
Write-Host "Portable ZIP:    $zip" -ForegroundColor Green
