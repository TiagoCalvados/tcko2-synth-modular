[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compilerPath)) {
    throw 'The 64-bit .NET Framework compiler is required. Enable or install .NET Framework 4.x on Windows.'
}
$binaryDirectory = Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Force -Path $binaryDirectory | Out-Null
$sourcePath = Join-Path $PSScriptRoot 'src\TCKO2Synth.cs'
$targetPath = Join-Path $binaryDirectory 'TCKO2Synth.exe'
& $compilerPath /nologo /target:winexe /platform:x64 /optimize+ "/out:$targetPath" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll $sourcePath (Join-Path $PSScriptRoot 'src\PcmRecorder.cs') (Join-Path $PSScriptRoot 'src\SoundFile.cs') (Join-Path $PSScriptRoot 'src\SoundFileUI.cs') (Join-Path $PSScriptRoot 'src\SoundDecoder.cs') (Join-Path $PSScriptRoot 'src\SoundFileChecks.cs') (Join-Path $PSScriptRoot 'src\SoundTechnologies.cs') (Join-Path $PSScriptRoot 'src\SoundTechnologiesUI.cs') (Join-Path $PSScriptRoot 'src\TechnologyChecks.cs') (Join-Path $PSScriptRoot 'src\LayeredSynth.cs') (Join-Path $PSScriptRoot 'src\LayerMixUI.cs') (Join-Path $PSScriptRoot 'src\LayerMixChecks.cs')
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
Write-Output "Built $targetPath"
