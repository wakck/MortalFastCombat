param([Parameter(Mandatory)][string]$Command,[string]$RunDirectory='validation\v110')
$ErrorActionPreference='Stop'
$path=Join-Path (Join-Path (Split-Path $PSScriptRoot -Parent) $RunDirectory) 'command.txt'
if(Test-Path -LiteralPath $path){throw 'A test command is still pending.'}
$temp=$path+'.pending'
[IO.File]::WriteAllText($temp,$Command)
[IO.File]::Move($temp,$path)
$deadline=(Get-Date).AddSeconds(8)
while((Test-Path -LiteralPath $path) -and (Get-Date) -lt $deadline){Start-Sleep -Milliseconds 100}
if(Test-Path -LiteralPath $path){throw "Command not consumed: $Command"}
