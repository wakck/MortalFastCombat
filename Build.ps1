param([string]$GameDirectory)
$ErrorActionPreference='Stop'
if(!$GameDirectory){$GameDirectory=Split-Path $PSScriptRoot -Parent}
$game=[IO.Path]::GetFullPath($GameDirectory)
$managed=Join-Path $game 'Mortal_Data\Managed'
$core=Join-Path $game 'BepInEx\core'
if(!(Test-Path -LiteralPath (Join-Path $core 'BepInEx.Unity.Mono.dll'))){throw '需要本机 BepInEx 6 Unity Mono 依赖；不能使用 BepInEx 5。'}
$refs=@('mscorlib.dll','System.dll','System.Core.dll','System.Runtime.Serialization.dll','System.Xml.dll','netstandard.dll','UnityEngine.dll','UnityEngine.CoreModule.dll','UnityEngine.IMGUIModule.dll','UnityEngine.TextRenderingModule.dll','UnityEngine.JSONSerializeModule.dll','UnityEngine.AudioModule.dll','UnityEngine.UIModule.dll','Unity.InputSystem.dll','UnityEngine.UI.dll','Mortal.Combat.dll','Mortal.Core.dll')|ForEach-Object {'/reference:'+(Join-Path $managed $_)}
$refs+=@('BepInEx.Core.dll','BepInEx.Unity.Mono.dll','0Harmony.dll')|ForEach-Object {'/reference:'+(Join-Path $core $_)}
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
New-Item -ItemType Directory -Path (Join-Path $PSScriptRoot 'dist') -Force|Out-Null
$out=Join-Path $PSScriptRoot 'dist\MortalFastCombat.dll'
$sources=Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs'|ForEach-Object FullName
& $compiler /nologo /noconfig /nostdlib+ /target:library /optimize+ /warn:4 "/out:$out" @refs @sources
if($LASTEXITCODE -ne 0){throw '构建失败。'}
$hash=(Get-FileHash -LiteralPath $out).Hash
[IO.File]::WriteAllText($out+'.sha256',$hash+'  MortalFastCombat.dll'+"`n")
"Built $out SHA256=$hash (未安装)"
