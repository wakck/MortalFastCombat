param([Parameter(Mandatory)][string]$GameDirectory,[Parameter(Mandatory)][string]$Command)
$ErrorActionPreference='Stop'
$game=[IO.Path]::GetFullPath($GameDirectory)
if(!(Test-Path -LiteralPath (Join-Path $game 'isolation-root.txt')) -or (Get-Content -LiteralPath (Join-Path $game 'isolation-root.txt') -Raw).Trim() -ne 'COEXISTENCE_TEST_ONLY'){throw '拒绝向非隔离游戏发送测试指令。'}
$path=Join-Path $game 'validation\command.txt'
if(Test-Path -LiteralPath $path){throw 'A command is still pending'}
[IO.File]::WriteAllText($path+'.pending',$Command)
[IO.File]::Move($path+'.pending',$path)
$until=(Get-Date).AddSeconds(8)
while((Test-Path -LiteralPath $path) -and (Get-Date) -lt $until){Start-Sleep -Milliseconds 100}
if(Test-Path -LiteralPath $path){throw 'Command not consumed; check startup fixtures and isolation logs'}
