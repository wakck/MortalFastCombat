param([string]$GameDirectory = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
$game = [IO.Path]::GetFullPath($GameDirectory)
$managed = Join-Path $game 'Mortal_Data\Managed'
$core = Join-Path $PSScriptRoot 'tools\runtime\BepInEx\core'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw '需要 Windows .NET Framework 4 C# 编译器。' }
if(!(Test-Path -LiteralPath $managed)){throw 'Use -GameDirectory to select your installed game directory.'}
& (Join-Path $PSScriptRoot 'tools\Prepare-Runtime.ps1')
# Normal rebuilds keep the reviewed fingerprints and whitelist. Never silently rebase them after a game update.
New-Item -ItemType Directory (Join-Path $PSScriptRoot 'dist') -Force | Out-Null
$references = @('mscorlib.dll','System.dll','System.Core.dll','System.Runtime.Serialization.dll','System.Xml.dll','netstandard.dll','UnityEngine.dll',
    'UnityEngine.CoreModule.dll','UnityEngine.IMGUIModule.dll','UnityEngine.TextRenderingModule.dll','UnityEngine.JSONSerializeModule.dll','UnityEngine.AudioModule.dll','UnityEngine.UIModule.dll',
    'Unity.InputSystem.dll','UnityEngine.UI.dll','Mortal.Combat.dll','Mortal.Core.dll') | ForEach-Object { '/reference:' + (Join-Path $managed $_) }
$references += @(( '/reference:' + (Join-Path $core 'BepInEx.dll')), ( '/reference:' + (Join-Path $core '0Harmony.dll')))
$sources = Get-ChildItem (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object FullName
& $compiler /nologo /noconfig /nostdlib+ /target:library /optimize+ /warn:4 "/out:$(Join-Path $PSScriptRoot 'dist\MortalFastCombat.dll')" @references @sources
if ($LASTEXITCODE -ne 0) { throw 'Plugin compilation failed.' }
Get-FileHash (Join-Path $PSScriptRoot 'dist\MortalFastCombat.dll') -Algorithm SHA256 | Format-List
