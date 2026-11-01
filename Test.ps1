$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Path (Join-Path $PSScriptRoot 'reports\v110') -Force | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$out = Join-Path $PSScriptRoot 'tests\PumpTests.exe'
& $compiler /nologo /target:exe /optimize+ "/out:$out" (Join-Path $PSScriptRoot 'src\CoroutinePump.cs') (Join-Path $PSScriptRoot 'tests\PumpTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
& $out | Tee-Object (Join-Path $PSScriptRoot 'reports\scheduler-tests.txt')
if ($LASTEXITCODE -ne 0) { throw 'Scheduler tests failed.' }
$historyOut=Join-Path $PSScriptRoot 'tests\HistoryTests.exe'
& $compiler /nologo /target:exe /reference:System.Runtime.Serialization.dll "/out:$historyOut" (Join-Path $PSScriptRoot 'src\VictoryHistory.cs') (Join-Path $PSScriptRoot 'tests\HistoryTests.cs')
if($LASTEXITCODE -ne 0){throw 'History test compilation failed'}
$fixture=Join-Path $PSScriptRoot ('validation\history-'+[Guid]::NewGuid().ToString('N'))
& $historyOut $fixture | Tee-Object (Join-Path $PSScriptRoot 'reports\v110\history-tests.txt')
if($LASTEXITCODE -ne 0){throw 'History tests failed'}
