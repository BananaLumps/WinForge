# Copy-WinForgeCore.ps1

param (
    [string]$Configuration = "Debug"
)

# Get the current directory (assumes it's the project root)
$projectRoot = Get-Location

# Find the output directory
$buildDirs = Get-ChildItem "$projectRoot\bin\$Configuration" -Directory | Where-Object {
    Test-Path "$($_.FullName)\WinForge.Core.dll"
}

if ($buildDirs.Count -eq 0) {
    Write-Host "No build output found with WinForge.Core.dll in $Configuration configuration." -ForegroundColor Red
    exit 1
}

# Use the first valid directory found
$outputDir = $buildDirs[0].FullName
$dllPath = Join-Path $outputDir "WinForge.Core.dll"

# Prepare destination folder
$destDir = "C:\Users\micha\source\repos\WinForge\WinForge.Base\bin\Debug\net8.0\modules"
if (-not (Test-Path $destDir)) {
    New-Item -ItemType Directory -Path $destDir | Out-Null
}

# Copy the DLL
Copy-Item -Path $dllPath -Destination $destDir -Force

Write-Host "Copied WinForge.Core.dll from '$outputDir' to '$destDir'" -ForegroundColor Green
