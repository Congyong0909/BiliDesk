param(
    [string]$OutputDirectory = $PSScriptRoot,
    [string]$Dotnet = 'dotnet'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$app = Join-Path $root 'BiliDesk\App.xaml.cs'
$versionMatch = Select-String -LiteralPath $app -Pattern 'const string TvVersion = "([0-9.]+-tv\.[0-9]+)";' | Select-Object -First 1
if (-not $versionMatch) { throw 'TvVersion not found in App.xaml.cs' }
$version = $versionMatch.Matches[0].Groups[1].Value
$folderName = "BiliDeskTV-$version-win-x64"
$stagingRoot = Join-Path $PSScriptRoot ('tv-stage-' + [Guid]::NewGuid().ToString('N'))
$packageDirectory = Join-Path $stagingRoot $folderName
$archive = Join-Path $OutputDirectory "$folderName.zip"

New-Item -Path $packageDirectory -ItemType Directory -Force | Out-Null
New-Item -Path $OutputDirectory -ItemType Directory -Force | Out-Null
try {
    & $Dotnet publish (Join-Path $root 'BiliDesk\BiliDesk.csproj') -c Release -r win-x64 `
        --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true `
        -o $packageDirectory
    if ($LASTEXITCODE -ne 0) { throw "TV publish failed ($LASTEXITCODE)" }

    Rename-Item -LiteralPath (Join-Path $packageDirectory 'BiliDesk.exe') -NewName 'BiliDeskTV.exe'
    foreach ($dependency in @('libvlc\win-x64\libvlc.dll', 'libvlc\win-x64\libvlccore.dll')) {
        if (-not (Test-Path -LiteralPath (Join-Path $packageDirectory $dependency))) {
            throw "Publish is missing $dependency"
        }
    }
    Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination (Join-Path $packageDirectory 'LICENSE.txt')
    Copy-Item -LiteralPath (Join-Path $root 'docs\tv-mode.md') -Destination (Join-Path $packageDirectory 'README-TV.md')
    Set-Content -LiteralPath (Join-Path $packageDirectory 'Start-TV.cmd') -Encoding ascii -Value @(
        '@echo off',
        'start "BiliDesk TV" "%~dp0BiliDeskTV.exe" --tv'
    )

    [System.IO.Compression.ZipFile]::CreateFromDirectory($packageDirectory,
        (Join-Path $stagingRoot "$folderName.zip"), [System.IO.Compression.CompressionLevel]::Optimal, $true)
    Move-Item -LiteralPath (Join-Path $stagingRoot "$folderName.zip") -Destination $archive -Force
    Get-FileHash -LiteralPath $archive -Algorithm SHA256
}
finally {
    $stage = (Resolve-Path -LiteralPath $stagingRoot).ProviderPath
    $publish = (Resolve-Path -LiteralPath $PSScriptRoot).ProviderPath
    if ((Split-Path -Parent $stage) -eq $publish -and (Split-Path -Leaf $stage) -match '^tv-stage-[0-9a-f]{32}$') {
        Remove-Item -LiteralPath $stage -Recurse -Force
    }
}
