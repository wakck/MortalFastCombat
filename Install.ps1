$ErrorActionPreference = 'Stop'
$game = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
if (Get-Process Mortal -ErrorAction SilentlyContinue) { throw '请先退出游戏再安装。' }
if (Test-Path (Join-Path $PSScriptRoot 'installed-files.json')) { throw '已有安装记录；请退出游戏后运行同目录的 Update.ps1。' }

if (!(Test-Path -LiteralPath (Join-Path $game 'Mortal.exe'))) { throw '请先把压缩包中的 MortalFastCombat 文件夹解压到游戏目录（与 Mortal.exe 同级）。' }
$expectedAssemblies=@{
    'Mortal.Core.dll'='093EF965BBCCF3C9EF543C51C1D9AF61B8B9CAE93BB8EBE8D8F58ACDAB12D922'
    'Mortal.Combat.dll'='D35179271EC13841E1EF6A9A2341846F8C484CA67916FC332B44F1853B480ABE'
}
foreach($name in $expectedAssemblies.Keys){
    $assembly=Join-Path $game ('Mortal_Data\Managed\'+$name)
    if(!(Test-Path -LiteralPath $assembly) -or (Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash -ne $expectedAssemblies[$name]){throw '游戏版本不匹配，停止安装／更新。本包适配 Steam Build 26237817。'}
}

$source = Join-Path $PSScriptRoot 'tools\runtime'
if(!(Test-Path -LiteralPath (Join-Path $source 'BepInEx\core\BepInEx.dll'))){& (Join-Path $PSScriptRoot 'tools\Prepare-Runtime.ps1')}
$files = @(Get-ChildItem -LiteralPath $source -File -Recurse -Force)
$entries = [Collections.Generic.List[object]]::new()
foreach ($file in $files) {
    $rel = $file.FullName.Substring($source.Length + 1)
    if (Test-Path -LiteralPath (Join-Path $game $rel)) { throw "目标文件已存在，拒绝覆盖：$rel" }
}
$pluginRel = 'BepInEx\plugins\MortalFastCombat\MortalFastCombat.dll'
if (Test-Path (Join-Path $game $pluginRel)) { throw '插件已存在。' }
foreach ($file in $files) {
    $rel = $file.FullName.Substring($source.Length + 1)
    $dest = Join-Path $game $rel
    New-Item -ItemType Directory (Split-Path $dest -Parent) -Force | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $dest
    $entries.Add([pscustomobject]@{ Path=$rel; Hash=(Get-FileHash -LiteralPath $dest -Algorithm SHA256).Hash; Kind='Runtime' })
}
$pluginDest = Join-Path $game $pluginRel
New-Item -ItemType Directory (Split-Path $pluginDest -Parent) -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'dist\MortalFastCombat.dll') -Destination $pluginDest
$entries.Add([pscustomobject]@{ Path=$pluginRel; Hash=(Get-FileHash $pluginDest -Algorithm SHA256).Hash; Kind='Plugin' })
$entries | ConvertTo-Json | Set-Content (Join-Path $PSScriptRoot 'installed-files.json') -Encoding utf8
'Installed. Launch from Steam normally.'
