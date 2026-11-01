$ErrorActionPreference = 'Stop'
$mod = Split-Path $PSScriptRoot -Parent
$managed = Join-Path (Split-Path $mod -Parent) 'Mortal_Data\Managed'
$core = Join-Path $mod 'tools\runtime\BepInEx\core'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$refs = @('mscorlib.dll','System.dll','System.Core.dll','netstandard.dll','UnityEngine.dll','UnityEngine.CoreModule.dll','UnityEngine.JSONSerializeModule.dll','UnityEngine.UI.dll','UnityEngine.UIModule.dll','Mortal.Core.dll','Mortal.Combat.dll') | ForEach-Object { '/reference:' + (Join-Path $managed $_) }
$refs += ( '/reference:' + (Join-Path $core 'BepInEx.dll'))
$refs += ( '/reference:' + (Join-Path $core '0Harmony.dll'))
$refs += ( '/reference:' + (Join-Path $mod 'dist\MortalFastCombat.dll'))
$sources=Get-ChildItem -LiteralPath $PSScriptRoot -Filter 'Validation*.cs' | ForEach-Object FullName
& $compiler /nologo /noconfig /nostdlib+ /target:library "/out:$(Join-Path $PSScriptRoot 'ValidationPlugin.dll')" @refs @sources
if($LASTEXITCODE -ne 0) {throw 'Validation plugin compilation failed'}
