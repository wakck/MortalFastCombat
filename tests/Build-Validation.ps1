param([Parameter(Mandatory)][string]$GameDirectory)
$ErrorActionPreference='Stop'
$game=[IO.Path]::GetFullPath($GameDirectory)
if(!(Test-Path -LiteralPath (Join-Path $game 'isolation-root.txt')) -or (Get-Content -LiteralPath (Join-Path $game 'isolation-root.txt') -Raw).Trim() -ne 'COEXISTENCE_TEST_ONLY'){throw '需要独立测试副本及隔离标记，不得用于正式游戏。'}
$managed=Join-Path $game 'Mortal_Data\Managed';$core=Join-Path $game 'BepInEx\core'
$mod=Split-Path $PSScriptRoot -Parent
$out=Join-Path $PSScriptRoot 'artifacts'
New-Item -ItemType Directory -Path $out -Force|Out-Null
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$refs=@('mscorlib.dll','System.dll','System.Core.dll','netstandard.dll','UnityEngine.dll','UnityEngine.CoreModule.dll','UnityEngine.JSONSerializeModule.dll','UnityEngine.UI.dll','UnityEngine.UIModule.dll','UnityEngine.IMGUIModule.dll','Unity.InputSystem.dll','Mortal.Core.dll','Mortal.Combat.dll')|ForEach-Object {'/reference:'+(Join-Path $managed $_)}
$refs+='/reference:'+(Join-Path $mod 'dist\MortalFastCombat.dll')
$refs+=@('BepInEx.Core.dll','BepInEx.Unity.Mono.dll','0Harmony.dll')|ForEach-Object {'/reference:'+(Join-Path $core $_)}
$sources=Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'integration') -Filter 'Validation*.cs'|ForEach-Object FullName
& $compiler /nologo /noconfig /nostdlib+ /target:library /define:BEPINEX6 "/out:$(Join-Path $out 'ValidationPlugin.dll')" @refs @sources
if($LASTEXITCODE -ne 0){throw 'Test host build failed'}
$patchRefs=@('mscorlib.dll','System.dll','System.Core.dll','netstandard.dll')|ForEach-Object {'/reference:'+(Join-Path $managed $_)}
$patchRefs+=@('BepInEx.Core.dll','BepInEx.Preloader.Core.dll','Mono.Cecil.dll')|ForEach-Object {'/reference:'+(Join-Path $core $_)}
& $compiler /nologo /noconfig /nostdlib+ /target:library /define:BEPINEX6 "/out:$(Join-Path $out 'IsolationPatcher.dll')" @patchRefs (Join-Path $PSScriptRoot 'integration\IsolationPatcher.cs')
if($LASTEXITCODE -ne 0){throw 'Isolation patcher build failed'}
'仅构建测试工具，没有安装或启动游戏。使用前阅读 tests/README.md。'
