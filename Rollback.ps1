param([string]$GameDirectory,[string]$BackupDirectory)
$ErrorActionPreference='Stop'
if(!$GameDirectory){$GameDirectory=Split-Path $PSScriptRoot -Parent}
. (Join-Path $PSScriptRoot 'tools\Install-Common.ps1')
$game=Get-GamePath $GameDirectory
if(!$BackupDirectory){$last=Get-Content -LiteralPath (Join-Path $game 'MortalFastCombat\last-install.json') -Raw|ConvertFrom-Json;$BackupDirectory=Get-SafeTarget $game $last.Backup}
$backup=[IO.Path]::GetFullPath($BackupDirectory).TrimEnd('\')
if(!$backup.StartsWith((Join-Path $game 'MortalFastCombat\backup')+'\',[StringComparison]::OrdinalIgnoreCase)){throw '备份必须位于本游戏的 MortalFastCombat/backup 内。'}
$allowed=@('BepInEx\plugins\MortalFastCombat\MortalFastCombat.dll','BepInEx\config\LOMAssistant.cfg','BepInEx\config\BepInEx.cfg','MortalFastCombat\installed-files.json')
$entries=@(Read-JsonArray (Join-Path $backup 'restore.json'))
if($entries.Count -ne 4 -or @($entries.Path|Select-Object -Unique).Count -ne 4){throw '备份清单不完整。'}
foreach($entry in $entries){
    if($entry.Path -notin $allowed){throw '备份路径不属于允许的插件/配置文件。'}
    $path=Get-SafeTarget $game $entry.Path
    if(!(Test-Path -LiteralPath $path) -or (Get-FileHash -LiteralPath $path).Hash -ne $entry.AfterHash){throw '安装后文件已变化，停止回退以保留后续设置；可从备份手动恢复。'}
    if($entry.Existed -and (Get-FileHash -LiteralPath (Join-Path $backup $entry.Path)).Hash -ne $entry.BeforeHash){throw '备份文件校验失败。'}
}
foreach($entry in $entries){$path=Get-SafeTarget $game $entry.Path;if($entry.Existed){Copy-Item -LiteralPath (Join-Path $backup $entry.Path) -Destination $path -Force}else{Remove-Item -LiteralPath $path}}
'已恢复安装前的插件和相关配置；存档及胜利记录未改动。'
