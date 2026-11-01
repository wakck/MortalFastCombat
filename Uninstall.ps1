[CmdletBinding(SupportsShouldProcess)]
param()
$ErrorActionPreference = 'Stop'
$game = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent)).TrimEnd('\') + '\'
if (Get-Process Mortal -ErrorAction SilentlyContinue) { throw '请先退出游戏再卸载。' }
$manifest = Join-Path $PSScriptRoot 'installed-files.json'
if (!(Test-Path -LiteralPath $manifest)) { throw '找不到本次安装记录，未删除任何文件。' }
$entries = Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json
$ownPluginDir = [IO.Path]::GetFullPath((Join-Path $game 'BepInEx\plugins\MortalFastCombat')).TrimEnd('\') + '\'
$others = @(Get-ChildItem -LiteralPath (Join-Path $game 'BepInEx\plugins') -Filter '*.dll' -Recurse -File -ErrorAction SilentlyContinue |
    Where-Object { !$_.FullName.StartsWith($ownPluginDir,[StringComparison]::OrdinalIgnoreCase) })
$shared = $others.Count -gt 0
if ($shared) { Write-Warning '检测到其他插件，保留共享 BepInEx 加载器和运行库。' }
foreach ($entry in $entries) {
    if ($shared -and $entry.Kind -eq 'Runtime') { continue }
    $dest = [IO.Path]::GetFullPath((Join-Path $game $entry.Path))
    if (!$dest.StartsWith($game, [StringComparison]::OrdinalIgnoreCase)) { throw "安装清单路径越界：$dest" }
    if (!(Test-Path -LiteralPath $dest)) { continue }
    if ((Get-FileHash -LiteralPath $dest -Algorithm SHA256).Hash -ne $entry.Hash) { Write-Warning "保留安装后发生变化的文件：$dest"; continue }
    if ($PSCmdlet.ShouldProcess($dest, '移除本次安装的文件')) { Remove-Item -LiteralPath $dest }
}
$cfg = Join-Path $game 'BepInEx\config\local.legendofmortal.fastcombat.cfg'
if ((Test-Path -LiteralPath $cfg) -and $PSCmdlet.ShouldProcess($cfg, '移除本插件配置')) { Remove-Item -LiteralPath $cfg }
# Keep source, backups, logs and unrelated/new mod files. No recursive deletion.
if (!$WhatIfPreference) { Move-Item -LiteralPath $manifest -Destination (Join-Path $PSScriptRoot ('uninstalled-' + (Get-Date -Format yyyyMMddHHmmss) + '.json')) }
'卸载完成。源码、备份和独立胜利记录保留在 MortalFastCombat。'
