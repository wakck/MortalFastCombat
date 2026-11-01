$ErrorActionPreference='Stop'
$version='1.1.3'
$stage=Join-Path $PSScriptRoot ('package-checks\stage-'+[Guid]::NewGuid().ToString('N'))
$mod=Join-Path $stage 'MortalFastCombat'
$files=@('Install.ps1','Update.ps1','Uninstall.ps1','Rollback.ps1','Clear-History.ps1','README.md','THIRD_PARTY.md','dist\MortalFastCombat.dll','dist\MortalFastCombat.dll.sha256','tools\Install-Common.ps1','tools\compatibility.json')
$files+=@(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'docs') -File -Filter '*.md'|ForEach-Object {'docs\'+$_.Name})
$files+=@(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'licenses') -File|ForEach-Object {'licenses\'+$_.Name})
foreach($relative in $files){
    $dest=Join-Path $mod $relative
    New-Item -ItemType Directory -Path (Split-Path $dest -Parent) -Force|Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $relative) -Destination $dest
}
$expected=(Get-Content -LiteralPath (Join-Path $mod 'dist\MortalFastCombat.dll.sha256') -Raw).Trim().Split(' ')[0]
if((Get-FileHash -LiteralPath (Join-Path $mod 'dist\MortalFastCombat.dll')).Hash -ne $expected){throw 'DLL checksum mismatch'}
$release=Join-Path $PSScriptRoot 'release'
New-Item -ItemType Directory -Path $release -Force|Out-Null
$zip=Join-Path $release ('MortalFastCombat-'+$version+'-Windows-x64-LOMAssistant.zip')
Compress-Archive -LiteralPath $mod -DestinationPath $zip -Force
$hash=(Get-FileHash -LiteralPath $zip).Hash
[IO.File]::WriteAllText($zip+'.sha256',$hash+'  '+[IO.Path]::GetFileName($zip)+"`n",[Text.UTF8Encoding]::new($false))
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::OpenRead($zip)
try{
    $actual=@($archive.Entries|Where-Object {!$_.FullName.EndsWith('/')}|ForEach-Object {$_.FullName.Replace('\','/')})
    $allowed=@($files|ForEach-Object {'MortalFastCombat/'+$_.Replace('\','/')})
    if(@(Compare-Object ($allowed|Sort-Object) ($actual|Sort-Object)).Count){throw 'Unexpected package entries'}
}finally{$archive.Dispose()}
"PACKAGED $zip"
"SHA256 $hash"
"Verified $($files.Count) allowlisted runtime/documentation files; no saves, records, configs, backups or test output."
