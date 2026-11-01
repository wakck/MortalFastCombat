$ErrorActionPreference='Stop'
$game=Split-Path $PSScriptRoot -Parent
if(Get-Process Mortal -ErrorAction SilentlyContinue){throw '请先退出游戏再更新插件。'}

if (!(Test-Path -LiteralPath (Join-Path $game 'Mortal.exe'))) { throw '请先把压缩包中的 MortalFastCombat 文件夹解压到游戏目录（与 Mortal.exe 同级）。' }
$expectedAssemblies=@{
    'Mortal.Core.dll'='093EF965BBCCF3C9EF543C51C1D9AF61B8B9CAE93BB8EBE8D8F58ACDAB12D922'
    'Mortal.Combat.dll'='D35179271EC13841E1EF6A9A2341846F8C484CA67916FC332B44F1853B480ABE'
}
foreach($name in $expectedAssemblies.Keys){
    $assembly=Join-Path $game ('Mortal_Data\Managed\'+$name)
    if(!(Test-Path -LiteralPath $assembly) -or (Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash -ne $expectedAssemblies[$name]){throw '游戏版本不匹配，停止安装／更新。本包适配 Steam Build 26237817。'}
}

$manifest=Join-Path $PSScriptRoot 'installed-files.json'
$entries=@(Get-Content -LiteralPath $manifest -Raw|ConvertFrom-Json)
$plugin=$entries|Where-Object {$_.Kind -eq 'Plugin' -and $_.Path -eq 'BepInEx\plugins\MortalFastCombat\MortalFastCombat.dll'}
if(@($plugin).Count -ne 1){throw '安装清单中缺少唯一插件条目'}
$dest=Join-Path $game $plugin.Path
if((Get-FileHash -LiteralPath $dest).Hash -ne $plugin.Hash){throw '当前插件与安装清单不符，未覆盖'}
$built=Join-Path $PSScriptRoot 'dist\MortalFastCombat.dll'
if(!(Test-Path -LiteralPath $built)){throw '请先执行 Build.ps1'}
New-Item -ItemType Directory -Path (Join-Path $PSScriptRoot 'backup') -Force | Out-Null
Copy-Item -LiteralPath $built -Destination ($dest+'.updating') -Force
[IO.File]::Replace(($dest+'.updating'),$dest,(Join-Path $PSScriptRoot 'backup\plugin-before-update.dll'))
$plugin.Hash=(Get-FileHash -LiteralPath $dest).Hash
$entries|ConvertTo-Json|Set-Content -LiteralPath $manifest -Encoding utf8
'插件已更新。已有倍率、配置和胜利记录保留；新选项在下次启动时加入。'
