$ErrorActionPreference = "Stop"

$sourceRoot = Split-Path -Parent $PSScriptRoot
$zipOutputE = Join-Path (Split-Path -Parent $sourceRoot) "Antihub.zip"
$downloads = Join-Path $env:USERPROFILE "Downloads"
$zipOutputDl = [System.IO.Path]::Combine($downloads, "Antihub.zip")

$tempStage = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), "Antihub_staging_" + [System.Guid]::NewGuid().ToString("N"))
$stageRoot = [System.IO.Path]::Combine($tempStage, "Antihub")

Write-Host "Creating clean staging directory: $stageRoot" -ForegroundColor Cyan
New-Item -ItemType Directory -Path $stageRoot -Force | Out-Null

$itemsToCopy = @(
    "assets",
    "dist",
    "release",
    "scripts",
    "src",
    "src-native",
    "src-tauri",
    "package.json",
    "package-lock.json",
    "tsconfig.json",
    "vite.config.ts",
    "postcss.config.js",
    "tailwind.config.js",
    "index.html",
    "build.bat",
    "Run-AntigravityHub.bat",
    "Setup-Shortcuts.bat",
    ".gitignore",
    "LICENSE",
    "README.md"
)

foreach ($item in $itemsToCopy) {
    $srcPath = Join-Path $sourceRoot $item
    if (Test-Path $srcPath) {
        $destPath = Join-Path $stageRoot $item
        Write-Host "Staging: $item"
        Copy-Item -Path $srcPath -Destination $destPath -Recurse -Force
    }
}

# Clean unwanted intermediate artifacts from staging
$cleanTargets = @(
    (Join-Path $stageRoot "src-native\bin"),
    (Join-Path $stageRoot "src-native\obj"),
    (Join-Path $stageRoot "src-tauri\target"),
    (Join-Path $stageRoot "release\hub-startup.log"),
    (Join-Path $stageRoot "release\HubWebView2Cache"),
    (Join-Path $stageRoot "release\AntigravityHub.pdb"),
    (Join-Path $stageRoot "release\*.lnk")
)

foreach ($target in $cleanTargets) {
    if (Test-Path $target) {
        Write-Host "Removing intermediate artifact: $target"
        Remove-Item -Path $target -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# Remove existing zip files if they exist
if (Test-Path $zipOutputE) { Remove-Item $zipOutputE -Force }
if (Test-Path $zipOutputDl) { Remove-Item $zipOutputDl -Force }

Write-Host "Compressing archive to: $zipOutputE" -ForegroundColor Green
Compress-Archive -Path $stageRoot -DestinationPath $zipOutputE -CompressionLevel Optimal

if (Test-Path $downloads) {
    Write-Host "Copying to Downloads folder: $zipOutputDl" -ForegroundColor Green
    Copy-Item $zipOutputE -Destination $zipOutputDl -Force
}

# Clean up staging directory
Remove-Item -Path $tempStage -Recurse -Force -ErrorAction SilentlyContinue

$sizeE = (Get-Item $zipOutputE).Length / 1MB
Write-Host "Done! Archive size: $([math]::Round($sizeE, 2)) MB" -ForegroundColor Green
