param(
    [ValidateSet("win-x64", "win-arm64")]
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$projectRoot = $PSScriptRoot
$localDotnet = Join-Path $projectRoot "work\dotnet-sdk\dotnet.exe"
$dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
if (Test-Path -LiteralPath $localDotnet) {
    $dotnetExecutable = $localDotnet
} elseif ($null -ne $dotnetCommand) {
    $dotnetExecutable = $dotnetCommand.Source
} else {
    throw "Install the .NET 8 SDK or open this solution in Visual Studio with the .NET desktop workload."
}

$verificationProject = Join-Path $projectRoot "tests\DuctSizer.Verification\DuctSizer.Verification.csproj"
$uiVerificationProject = Join-Path $projectRoot "tests\DuctSizer.UiVerification\DuctSizer.UiVerification.csproj"
$applicationProject = Join-Path $projectRoot "src\DuctSizer.WinForms\DuctSizer.WinForms.csproj"
[xml]$applicationProjectXml = Get-Content -LiteralPath $applicationProject
$applicationVersion = ($applicationProjectXml.Project.PropertyGroup.Version | Select-Object -First 1).Trim()
$outputDirectory = Join-Path $projectRoot "outputs\FanWorks-$Runtime-v$applicationVersion"

& $dotnetExecutable run --project $verificationProject -c Release
if ($LASTEXITCODE -ne 0) { throw "Engineering verification failed." }

& $dotnetExecutable build $uiVerificationProject -c Release -p:UseAppHost=false -m:1
if ($LASTEXITCODE -ne 0) { throw "Interface verification build failed." }
$uiVerificationAssembly = Join-Path (Split-Path $uiVerificationProject -Parent) "bin\Release\net8.0-windows\DuctSizer.UiVerification.dll"
& $dotnetExecutable $uiVerificationAssembly
if ($LASTEXITCODE -ne 0) { throw "Interface verification failed." }

& $dotnetExecutable publish $applicationProject `
    -c Release `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $outputDirectory
if ($LASTEXITCODE -ne 0) { throw "Publishing failed." }

$executablePath = Join-Path $outputDirectory "FanWorks.exe"
$checksumPath = Join-Path $outputDirectory "SHA256.txt"
$checksumSidecarPath = Join-Path $outputDirectory "FanWorks.exe.sha256"
$checksum = (Get-FileHash -LiteralPath $executablePath -Algorithm SHA256).Hash
Set-Content -LiteralPath $checksumPath -Value "$checksum  FanWorks.exe" -Encoding ascii
Set-Content -LiteralPath $checksumSidecarPath -Value "$checksum  FanWorks.exe" -Encoding ascii
Copy-Item -LiteralPath (Join-Path $projectRoot "README.md") -Destination (Join-Path $outputDirectory "README.md") -Force
Copy-Item -LiteralPath (Join-Path $projectRoot "README.md") -Destination (Join-Path $outputDirectory "README.txt") -Force

Write-Host "Created $outputDirectory\FanWorks.exe"
