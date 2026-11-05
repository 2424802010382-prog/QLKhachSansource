param([switch]$SkipBuild)
$ErrorActionPreference = 'Stop'
$repoPath = Split-Path $PSScriptRoot -Parent
$msbuildPath = 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe'
if (!$SkipBuild) {
    & $msbuildPath (Join-Path $repoPath 'QLKhachSan.sln') /t:Build /p:Configuration=Debug /nologo /verbosity:minimal
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
}
$binPath = Join-Path $repoPath 'QLKhachSan\bin'
$outputPath = Join-Path $repoPath 'QLKhachSan\obj\HotelRegression'
New-Item -ItemType Directory -Force -Path $outputPath | Out-Null
Copy-Item -Path (Join-Path $binPath '*.dll') -Destination $outputPath -Force
$compilerPath = Join-Path $binPath 'roslyn\csc.exe'
if (!(Test-Path -LiteralPath $compilerPath)) { $compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe' }
$executablePath = Join-Path $outputPath 'HotelRegressionTests.exe'
$references = @('System.dll','System.Core.dll','System.Data.dll','System.Configuration.dll','System.Web.dll','System.Web.Extensions.dll',
    (Join-Path $binPath 'QLKhachSan.dll'),(Join-Path $binPath 'EntityFramework.dll'),(Join-Path $binPath 'System.Web.Mvc.dll'),(Join-Path $binPath 'System.Web.Helpers.dll'))
$arguments = @('/nologo','/target:exe',('/out:' + $executablePath)) + ($references | ForEach-Object { '/reference:' + $_ }) + (Join-Path $PSScriptRoot 'HotelRegressionTests.cs')
& $compilerPath @arguments
if ($LASTEXITCODE -ne 0) { throw 'Regression test compilation failed' }
# Copy existing settings unchanged. Tests refuse any database other than LocalDB QLKhachSanDB.
Copy-Item -LiteralPath (Join-Path $repoPath 'QLKhachSan\Web.config') -Destination ($executablePath + '.config') -Force
& $executablePath
if ($LASTEXITCODE -ne 0) { throw 'Regression tests failed' }
