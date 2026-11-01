param([string]$GameDirectory,[ValidateRange(0.1,10)][single]$GlobalSpeed=2)
$ErrorActionPreference='Stop'
if(!$GameDirectory){$GameDirectory=Split-Path $PSScriptRoot -Parent}
& (Join-Path $PSScriptRoot 'Install.ps1') @PSBoundParameters -Update
