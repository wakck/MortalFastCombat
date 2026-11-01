$ErrorActionPreference='Stop'
$mod=Split-Path $PSScriptRoot -Parent
$base=Join-Path $mod ('validation\uninstall-smoke-'+(Get-Date -Format yyyyMMddHHmmss))
$results=[Collections.Generic.List[string]]::new()
function Assert($ok,$message){if(!$ok){throw $message};$results.Add('PASS '+$message)}
function Fixture([string]$name){
    $game=Join-Path $base $name
    New-Item -ItemType Directory (Join-Path $game 'MortalFastCombat'),(Join-Path $game 'BepInEx\plugins\MortalFastCombat'),(Join-Path $game 'BepInEx\config') -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $mod 'Uninstall.ps1') -Destination (Join-Path $game 'MortalFastCombat\Uninstall.ps1')
    'runtime' | Set-Content (Join-Path $game 'winhttp.dll')
    'plugin' | Set-Content (Join-Path $game 'BepInEx\plugins\MortalFastCombat\MortalFastCombat.dll')
    'user file' | Set-Content (Join-Path $game 'keep.txt')
    New-Item -ItemType Directory (Join-Path $game 'MortalFastCombat\data\test') -Force | Out-Null
    'history' | Set-Content (Join-Path $game 'MortalFastCombat\data\test\history-0123456789ABCDEF.json')
    'config' | Set-Content (Join-Path $game 'BepInEx\config\local.legendofmortal.fastcombat.cfg')
    $entries=foreach($pair in @(@('winhttp.dll','Runtime'),@('BepInEx\plugins\MortalFastCombat\MortalFastCombat.dll','Plugin'))){
        [pscustomobject]@{Path=$pair[0];Kind=$pair[1];Hash=(Get-FileHash (Join-Path $game $pair[0])).Hash}
    }
    $entries | ConvertTo-Json | Set-Content (Join-Path $game 'MortalFastCombat\installed-files.json')
    return $game
}
# The fixtures are independent fake installations; the live game is irrelevant here.
function Get-Process {param($Name,$ErrorAction);return @()}
$normal=Fixture 'normal'
& (Join-Path $normal 'MortalFastCombat\Uninstall.ps1') -WhatIf | Out-Null
Assert (Test-Path (Join-Path $normal 'winhttp.dll')) 'WhatIf preserves runtime'
Assert (Test-Path (Join-Path $normal 'MortalFastCombat\installed-files.json')) 'WhatIf preserves installation manifest'
& (Join-Path $normal 'MortalFastCombat\Uninstall.ps1') | Out-Null
Assert (!(Test-Path (Join-Path $normal 'winhttp.dll'))) 'Removes installed loader'
Assert (!(Test-Path (Join-Path $normal 'BepInEx\plugins\MortalFastCombat\MortalFastCombat.dll'))) 'Removes owned plugin'
Assert (Test-Path (Join-Path $normal 'keep.txt')) 'Preserves unrelated files'
Assert (Test-Path (Join-Path $normal 'MortalFastCombat\data\test\history-0123456789ABCDEF.json')) 'Uninstall preserves victory history'
Copy-Item -LiteralPath (Join-Path $mod 'Clear-History.ps1') -Destination (Join-Path $normal 'MortalFastCombat\Clear-History.ps1')
'unrelated' | Set-Content (Join-Path $normal 'MortalFastCombat\data\test\keep.json')
& (Join-Path $normal 'MortalFastCombat\Clear-History.ps1') -Confirm:$false | Out-Null
Assert (!(Test-Path (Join-Path $normal 'MortalFastCombat\data\test\history-0123456789ABCDEF.json'))) 'Explicit cleanup removes owned history'
Assert (Test-Path (Join-Path $normal 'MortalFastCombat\data\test\keep.json')) 'History cleanup preserves unrelated data'
$shared=Fixture 'shared'
'other plugin' | Set-Content (Join-Path $shared 'BepInEx\plugins\Other.dll')
& (Join-Path $shared 'MortalFastCombat\Uninstall.ps1') -WarningAction SilentlyContinue | Out-Null
Assert (Test-Path (Join-Path $shared 'winhttp.dll')) 'Preserves loader used by another plugin'
Assert (Test-Path (Join-Path $shared 'BepInEx\plugins\Other.dll')) 'Preserves another plugin'
$modified=Fixture 'modified'
'modified runtime' | Set-Content (Join-Path $modified 'winhttp.dll')
& (Join-Path $modified 'MortalFastCombat\Uninstall.ps1') -WarningAction SilentlyContinue | Out-Null
Assert (Test-Path (Join-Path $modified 'winhttp.dll')) 'Preserves modified runtime'
$traversal=Fixture 'traversal'
$manifest=Join-Path $traversal 'MortalFastCombat\installed-files.json'
@{Path='..\escape.txt';Hash='bad';Kind='Plugin'} | ConvertTo-Json | Set-Content $manifest
$blocked=$false
try {& (Join-Path $traversal 'MortalFastCombat\Uninstall.ps1') | Out-Null}catch{$blocked=$true}
Assert $blocked 'Rejects manifest paths outside game directory'
$results | Tee-Object (Join-Path $mod 'reports\uninstall-tests.txt')
