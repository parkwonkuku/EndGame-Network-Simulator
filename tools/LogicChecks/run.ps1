$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$resultDir = Join-Path $PSScriptRoot 'results'
New-Item -ItemType Directory -Path $resultDir -Force | Out-Null
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.dotnet-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_CLI_USE_MSBUILD_SERVER = '0'
$project = Join-Path $PSScriptRoot 'LogicChecks.csproj'
$config = Join-Path $PSScriptRoot 'NuGet.Config'
$report = Join-Path $resultDir 'latest.txt'
[xml]$definition = Get-Content -LiteralPath $project -Raw
$manifest = foreach ($source in $definition.Project.ItemGroup.Compile) {
    $sourcePath = (Resolve-Path (Join-Path $PSScriptRoot $source.Include)).Path
    [pscustomobject]@{
        Path = [IO.Path]::GetRelativePath($projectRoot, $sourcePath).Replace('\', '/')
        SHA256 = (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash
    }
}
$manifest | ConvertTo-Json | Set-Content (Join-Path $resultDir 'sources.json') -Encoding utf8
@(
    'Production-source logic validation'
    "SDK: $(& dotnet --version)"
    'Configuration: Release; no external package sources'
    'Commands: dotnet restore --configfile NuGet.Config; dotnet run -c Release --no-restore -p:UseSharedCompilation=false'
) | Set-Content -LiteralPath $report -Encoding utf8
& dotnet restore $project --configfile $config --verbosity quiet 2>&1 | Tee-Object -FilePath $report -Append
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& dotnet run --project $project --configuration Release --no-restore --verbosity quiet -p:UseSharedCompilation=false 2>&1 |
    Tee-Object -FilePath $report -Append
$testExitCode = $LASTEXITCODE
foreach ($source in $manifest) {
    $currentHash = (Get-FileHash -LiteralPath (Join-Path $projectRoot $source.Path) -Algorithm SHA256).Hash
    if ($currentHash -ne $source.SHA256) { throw "Source changed during validation: $($source.Path)" }
}
"Exit code: $testExitCode" | Tee-Object -FilePath $report -Append
exit $testExitCode
