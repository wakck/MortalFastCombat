[CmdletBinding(SupportsShouldProcess)]
param([string]$GameDirectory,[switch]$RemoveConfiguration)
$ErrorActionPreference='Stop'
if(!$GameDirectory){$GameDirectory=Split-Path $PSScriptRoot -Parent}
. (Join-Path $PSScriptRoot 'tools\Install-Common.ps1')
$game=Get-GamePath $GameDirectory
$manifest=Join-Path $game 'MortalFastCombat\installed-files.json'
$entries=@(Read-JsonArray $manifest)
foreach($entry in $entries){Get-SafeTarget $game $entry.Path|Out-Null}
$plugin=@($entries|Where-Object {$_.Path -eq 'BepInEx\plugins\MortalFastCombat\MortalFastCombat.dll' -and $_.Kind -eq 'Plugin'})
if($plugin.Count -ne 1){throw '缺少唯一插件清单，未删除任何文件。'}
$path=Get-SafeTarget $game $plugin[0].Path
if(Test-Path -LiteralPath $path){
    if((Get-FileHash -LiteralPath $path).Hash -ne $plugin[0].Hash){throw '插件发生变化，未删除。'}
    if($PSCmdlet.ShouldProcess($path,'移除 MortalFastCombat 插件')){Remove-Item -LiteralPath $path}
}
if($RemoveConfiguration){$cfg=Join-Path $game 'BepInEx\config\local.legendofmortal.fastcombat.cfg';if((Test-Path -LiteralPath $cfg) -and $PSCmdlet.ShouldProcess($cfg,'移除本插件配置')){Remove-Item -LiteralPath $cfg}}
if($PSCmdlet.ShouldProcess($manifest,'保留卸载清单备份')){Move-Item -LiteralPath $manifest -Destination ($manifest+'.uninstalled-'+[Guid]::NewGuid().ToString('N'))}
'卸载完成。助手、BepInEx、全局倍速设置、备份与胜利记录保留。此后战斗速度由游戏及助手处理。'
