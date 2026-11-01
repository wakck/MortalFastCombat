param([Parameter(Mandatory)][string]$GameDirectory,[string]$PackageDirectory)
$ErrorActionPreference='Stop'
if(!$PackageDirectory){$PackageDirectory=Split-Path $PSScriptRoot -Parent}
$repo=[IO.Path]::GetFullPath($PackageDirectory)
. (Join-Path $repo 'tools\Install-Common.ps1')
$source=[IO.Path]::GetFullPath($GameDirectory)
$run=Join-Path ([IO.Path]::GetTempPath()) ('MortalFastCombat-package-'+[Guid]::NewGuid().ToString('N').Substring(0,8))
$game=Join-Path $run 'Game';$count=0
New-Item -ItemType Directory -Path $game -Force|Out-Null
$compat=Get-Content -LiteralPath (Join-Path $repo 'tools\compatibility.json') -Raw|ConvertFrom-Json
$copies=@('BepInEx\plugins\LOMAssistant\LOMAssistant.dll')
$copies+=@($compat.Runtime.PSObject.Properties.Name)
$copies+=@($compat.GameAssemblies.PSObject.Properties.Name|ForEach-Object {'Mortal_Data\Managed\'+$_})
foreach($relative in $copies){$dest=Join-Path $game $relative;New-Item -ItemType Directory -Path (Split-Path $dest -Parent) -Force|Out-Null;Copy-Item -LiteralPath (Join-Path $source $relative) -Destination $dest}
[IO.File]::WriteAllText((Join-Path $game 'Mortal.exe'),'TEST PLACEHOLDER: never execute')
$originalAssistant="[LOMAssistant]`r`nGameSpeed = 3`r`nEnableFlowSpeedup = true`r`nEnableCombatAssist = true`r`nKeepOther = preserve`r`n[Other]`r`nGameSpeed = 9`r`n"
$originalLoader="[Chainloader]`r`nHideManagerGameObject = false`r`n[Logging]`r`nKeep = yes`r`n"
Write-Utf8 (Join-Path $game 'BepInEx\config\LOMAssistant.cfg') $originalAssistant
Write-Utf8 (Join-Path $game 'BepInEx\config\BepInEx.cfg') $originalLoader
Write-Utf8 (Join-Path $game 'BepInEx\config\local.legendofmortal.fastcombat.cfg') "[General]`r`nMultiplier = 8`r`n"
Write-Utf8 (Join-Path $game 'MortalFastCombat\data\dummy-history.json') '{"testSentinel":true}'
Write-Utf8 (Join-Path $game 'mock-formal-saves\dummy.dat') 'never change this dummy save'
$sentinels=@('MortalFastCombat\data\dummy-history.json','mock-formal-saves\dummy.dat','BepInEx\config\local.legendofmortal.fastcombat.cfg')+$copies
$fingerprints=@{};foreach($relative in $sentinels){$fingerprints[$relative]=(Get-FileHash -LiteralPath (Join-Path $game $relative)).Hash}
function Check([bool]$OK,[string]$Label){if(!$OK){throw ('Package assertion failed: '+$Label)};$script:count++;'PASS '+$Label}
function Reject([scriptblock]$Action,[string]$Label){$failed=$false;try{& $Action}catch{$failed=$true};Check $failed $Label}
& (Join-Path $repo 'Install.ps1') -GameDirectory $game
$cfg=Read-TextOrEmpty (Join-Path $game 'BepInEx\config\LOMAssistant.cfg')
Check ((Get-IniValue $cfg 'LOMAssistant' 'GameSpeed') -eq '2') 'fresh global default 2x'
Check ((Get-IniValue $cfg 'LOMAssistant' 'EnableFlowSpeedup') -eq 'false' -and (Get-IniValue $cfg 'LOMAssistant' 'EnableCombatAssist') -eq 'false') 'overlapping features disabled'
Check ((Get-IniValue $cfg 'Other' 'GameSpeed') -eq '9' -and (Get-IniValue $cfg 'LOMAssistant' 'KeepOther') -eq 'preserve') 'unrelated config preserved'
$manifest=Join-Path $game 'MortalFastCombat\installed-files.json'
$entries=@(Read-JsonArray $manifest)
Check ($entries.Count -eq 1 -and $entries[0].Kind -eq 'Plugin') 'manifest owns only own plugin'
$plugin=Join-Path $game 'BepInEx\plugins\MortalFastCombat\MortalFastCombat.dll'
& (Join-Path $repo 'Uninstall.ps1') -GameDirectory $game -WhatIf
Check ((Test-Path -LiteralPath $plugin) -and (Test-Path -LiteralPath $manifest)) 'uninstall WhatIf leaves files'
Reject {& (Join-Path $repo 'Install.ps1') -GameDirectory $game} 'duplicate installation refused'
& (Join-Path $repo 'Rollback.ps1') -GameDirectory $game
Check (!(Test-Path -LiteralPath $plugin) -and !(Test-Path -LiteralPath $manifest)) 'fresh rollback removes only added files'
Check ((Read-TextOrEmpty (Join-Path $game 'BepInEx\config\LOMAssistant.cfg')) -ceq $originalAssistant -and (Read-TextOrEmpty (Join-Path $game 'BepInEx\config\BepInEx.cfg')) -ceq $originalLoader) 'rollback restores original config bytes'
& (Join-Path $repo 'Install.ps1') -GameDirectory $game
$cfg=Set-IniValue (Read-TextOrEmpty (Join-Path $game 'BepInEx\config\LOMAssistant.cfg')) 'LOMAssistant' 'GameSpeed' '4'
Write-Utf8 (Join-Path $game 'BepInEx\config\LOMAssistant.cfg') $cfg
$legacy=@(Read-JsonArray $manifest)
$legacy+=[pscustomobject]@{Path='BepInEx\core\BepInEx.Core.dll';Hash=$fingerprints['BepInEx\core\BepInEx.Core.dll'];Kind='Runtime'}
$legacy|ConvertTo-Json|Set-Content -LiteralPath $manifest -Encoding utf8
& (Join-Path $repo 'Update.ps1') -GameDirectory $game
Check ((Get-IniValue (Read-TextOrEmpty (Join-Path $game 'BepInEx\config\LOMAssistant.cfg')) 'LOMAssistant' 'GameSpeed') -eq '4') 'update preserves current global speed'
Check (@(Read-JsonArray $manifest).Count -eq 1) 'upgrade stops owning shared runtime'
& (Join-Path $repo 'Rollback.ps1') -GameDirectory $game
Check ((Get-IniValue (Read-TextOrEmpty (Join-Path $game 'BepInEx\config\LOMAssistant.cfg')) 'LOMAssistant' 'GameSpeed') -eq '4') 'update rollback preserves pre-update config'
& (Join-Path $repo 'Update.ps1') -GameDirectory $game -GlobalSpeed 2
Check ((Get-IniValue (Read-TextOrEmpty (Join-Path $game 'BepInEx\config\LOMAssistant.cfg')) 'LOMAssistant' 'GameSpeed') -eq '2') 'explicit update global choice applied'
& (Join-Path $repo 'Rollback.ps1') -GameDirectory $game
$dll=Join-Path $game 'Mortal_Data\Managed\Mortal.Core.dll'
[IO.File]::WriteAllText($dll,'wrong game')
Reject {& (Join-Path $repo 'Update.ps1') -GameDirectory $game} 'wrong game refused'
Copy-Item -LiteralPath (Join-Path $source 'Mortal_Data\Managed\Mortal.Core.dll') -Destination $dll -Force
$dll=Join-Path $game 'BepInEx\plugins\LOMAssistant\LOMAssistant.dll'
[IO.File]::WriteAllText($dll,'wrong assistant')
Reject {& (Join-Path $repo 'Update.ps1') -GameDirectory $game} 'wrong assistant refused'
Copy-Item -LiteralPath (Join-Path $source 'BepInEx\plugins\LOMAssistant\LOMAssistant.dll') -Destination $dll -Force
$dll=Join-Path $game 'BepInEx\core\0Harmony.dll'
[IO.File]::WriteAllText($dll,'wrong runtime')
Reject {& (Join-Path $repo 'Update.ps1') -GameDirectory $game} 'wrong loader refused'
Copy-Item -LiteralPath (Join-Path $source 'BepInEx\core\0Harmony.dll') -Destination $dll -Force
[IO.File]::WriteAllText($plugin,'changed plugin')
Reject {& (Join-Path $repo 'Update.ps1') -GameDirectory $game} 'modified plugin refused'
Copy-Item -LiteralPath (Join-Path $repo 'dist\MortalFastCombat.dll') -Destination $plugin -Force
$savedManifest=[IO.File]::ReadAllText($manifest)
@([pscustomobject]@{Path='..\outside.dat';Hash='irrelevant';Kind='Plugin'})|ConvertTo-Json|Set-Content -LiteralPath $manifest -Encoding utf8
Reject {& (Join-Path $repo 'Uninstall.ps1') -GameDirectory $game} 'uninstall traversal refused'
Write-Utf8 $manifest $savedManifest
& (Join-Path $repo 'Uninstall.ps1') -GameDirectory $game
Check (!(Test-Path -LiteralPath $plugin)) 'uninstall removes own plugin'
Check ((Get-IniValue (Read-TextOrEmpty (Join-Path $game 'BepInEx\config\LOMAssistant.cfg')) 'LOMAssistant' 'GameSpeed') -eq '4') 'uninstall leaves assistant settings'
foreach($relative in $sentinels){Check ((Get-FileHash -LiteralPath (Join-Path $game $relative)).Hash -eq $fingerprints[$relative]) ('unchanged sentinel/dependency '+$relative)}
$local=Join-Path $game 'MortalFastCombat'
foreach($relative in @('Install.ps1','Update.ps1','Uninstall.ps1','Rollback.ps1','tools\Install-Common.ps1','tools\compatibility.json','dist\MortalFastCombat.dll','dist\MortalFastCombat.dll.sha256')){
    $target=Join-Path $local $relative;New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force|Out-Null
    Copy-Item -LiteralPath (Join-Path $repo $relative) -Destination $target -Force
}
& (Join-Path $local 'Install.ps1')
Check (Test-Path -LiteralPath $plugin) 'default game directory installation'
& (Join-Path $local 'Update.ps1')
Check (Test-Path -LiteralPath $plugin) 'default game directory update'
& (Join-Path $local 'Uninstall.ps1')
Check (!(Test-Path -LiteralPath $plugin) -and (Get-FileHash -LiteralPath (Join-Path $game 'MortalFastCombat\data\dummy-history.json')).Hash -eq $fingerprints['MortalFastCombat\data\dummy-history.json']) 'default directory uninstall preserves records'
"PACKAGE TESTS COMPLETE assertions=$count"
