param(
    [string]$GameDirectory,
    [ValidateRange(0.1,10)][single]$GlobalSpeed=2,
    [switch]$Update
)
$ErrorActionPreference='Stop'
if(!$GameDirectory){$GameDirectory=Split-Path $PSScriptRoot -Parent}
. (Join-Path $PSScriptRoot 'tools\Install-Common.ps1')
$game=Get-GamePath $GameDirectory
$compat=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'tools\compatibility.json') -Raw|ConvertFrom-Json
foreach($entry in $compat.GameAssemblies.PSObject.Properties){
    $path=Join-Path $game ('Mortal_Data\Managed\'+$entry.Name)
    if(!(Test-Path -LiteralPath $path) -or (Get-FileHash -LiteralPath $path).Hash -ne $entry.Value){throw '游戏版本不匹配；本版适配 Steam Build 26237817。'}
}
foreach($entry in $compat.Runtime.PSObject.Properties){
    $path=Get-SafeTarget $game $entry.Name
    if(!(Test-Path -LiteralPath $path) -or (Get-FileHash -LiteralPath $path).Hash -ne $entry.Value){throw '需要已安装的 LOMAssistant 3.3.2 配套 BepInEx 6.0.0-be.789；本包不安装或替换加载器。'}
}
$assistant=Join-Path $game 'BepInEx\plugins\LOMAssistant\LOMAssistant.dll'
if(!(Test-Path -LiteralPath $assistant) -or (Get-FileHash -LiteralPath $assistant).Hash -ne $compat.AssistantSHA256){throw 'LOMAssistant 版本不匹配。仅验证过文档所列 3.3.2 指纹。'}
$built=Join-Path $PSScriptRoot 'dist\MortalFastCombat.dll'
$expected=(Get-Content -LiteralPath ($built+'.sha256') -Raw).Trim().Split(' ')[0]
if($expected -notmatch '^[A-Fa-f0-9]{64}$' -or (Get-FileHash -LiteralPath $built).Hash -ne $expected){throw '插件校验失败。'}
$pluginRel='BepInEx\plugins\MortalFastCombat\MortalFastCombat.dll'
$assistantCfg='BepInEx\config\LOMAssistant.cfg'
$loaderCfg='BepInEx\config\BepInEx.cfg'
$manifestRel='MortalFastCombat\installed-files.json'
$dest=Join-Path $game $pluginRel
$manifest=Join-Path $game $manifestRel
if($Update){
    if(!(Test-Path -LiteralPath $dest)){throw '尚未安装，请运行 Install.ps1。'}
    $current=(Get-FileHash -LiteralPath $dest).Hash
    if(Test-Path -LiteralPath $manifest){
        $entries=@(Read-JsonArray $manifest)
        foreach($entry in $entries){Get-SafeTarget $game $entry.Path|Out-Null}
        $own=@($entries|Where-Object {$_.Path -eq $pluginRel -and $_.Kind -eq 'Plugin'})
        if($own.Count -ne 1 -or $own[0].Hash -ne $current){throw '现有插件与安装清单不一致，未覆盖。'}
    }elseif($current -notin $compat.KnownPluginHashes){throw '手动安装的插件指纹未知，未覆盖。请先自行保留旧插件。'}
}elseif(Test-Path -LiteralPath $dest){throw '已安装，请运行 Update.ps1。'}
$assistantText=Read-TextOrEmpty (Join-Path $game $assistantCfg)
$choice=$GlobalSpeed.ToString([Globalization.CultureInfo]::InvariantCulture)
if($Update -and !$PSBoundParameters.ContainsKey('GlobalSpeed')){
    $old=Get-IniValue $assistantText 'LOMAssistant' 'GameSpeed'
    if($null -ne $old){$choice=$old}
}
$assistantText=Set-IniValue $assistantText 'LOMAssistant' 'GameSpeed' $choice
$assistantText=Set-IniValue $assistantText 'LOMAssistant' 'EnableFlowSpeedup' 'false'
$assistantText=Set-IniValue $assistantText 'LOMAssistant' 'EnableCombatAssist' 'false'
$loaderText=Set-IniValue (Read-TextOrEmpty (Join-Path $game $loaderCfg)) 'Chainloader' 'HideManagerGameObject' 'true'
$backup=Join-Path $game ('MortalFastCombat\backup\install-1.1.3-'+(Get-Date -Format yyyyMMddHHmmss)+'-'+[Guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Path $backup -Force|Out-Null
$state=@()
foreach($relative in @($pluginRel,$assistantCfg,$loaderCfg,$manifestRel)){
    $path=Get-SafeTarget $game $relative;$existed=Test-Path -LiteralPath $path
    if($existed){$saved=Join-Path $backup $relative;New-Item -ItemType Directory -Path (Split-Path $saved -Parent) -Force|Out-Null;Copy-Item -LiteralPath $path -Destination $saved}
    $state+=[pscustomobject]@{Path=$relative;Existed=$existed;BeforeHash=$(if($existed){(Get-FileHash -LiteralPath $path).Hash}else{$null});AfterHash=$null}
}
try{
    New-Item -ItemType Directory -Path (Split-Path $dest -Parent) -Force|Out-Null
    Copy-Item -LiteralPath $built -Destination ($dest+'.installing') -Force
    if(Test-Path -LiteralPath $dest){[IO.File]::Replace($dest+'.installing',$dest,[NullString]::Value)}else{[IO.File]::Move($dest+'.installing',$dest)}
    Write-Utf8 (Join-Path $game $assistantCfg) $assistantText
    Write-Utf8 (Join-Path $game $loaderCfg) $loaderText
    # This package owns only our plugin. The assistant and its loader remain externally owned.
    @([pscustomobject]@{Path=$pluginRel;Hash=$expected;Kind='Plugin'})|ConvertTo-Json|Set-Content -LiteralPath $manifest -Encoding utf8
    foreach($entry in $state){$entry.AfterHash=(Get-FileHash -LiteralPath (Join-Path $game $entry.Path)).Hash}
    $state|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $backup 'restore.json') -Encoding utf8
    Write-Utf8 (Join-Path $game 'MortalFastCombat\last-install.json') ([pscustomobject]@{Backup=$backup.Substring($game.Length+1);Version='1.1.3'}|ConvertTo-Json)
}catch{
    foreach($entry in $state){$path=Get-SafeTarget $game $entry.Path;if($entry.Existed){Copy-Item -LiteralPath (Join-Path $backup $entry.Path) -Destination $path -Force}elseif(Test-Path -LiteralPath $path){Remove-Item -LiteralPath $path}}
    throw
}
"已安装 1.1.3。剧情/探索全局 $choice 倍；战斗独立 2/4/8 倍。首次默认战斗 4 倍，已有本插件配置与胜利记录保留。"
"旧插件和相关配置备份：$backup"
