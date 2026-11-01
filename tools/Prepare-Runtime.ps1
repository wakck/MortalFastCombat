$ErrorActionPreference='Stop'
$mod=Split-Path $PSScriptRoot -Parent
$archive=Join-Path $mod 'release\MortalFastCombat-1.1.0-Windows-x64-Runtime.zip'
$expected='D0C448884FE6D5908D878A64150BADEA85CA9D630B30E1070EE4AA883B0629AA'
if((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $expected){throw 'Bundled runtime ZIP fingerprint mismatch.'}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$runtime=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'runtime')).TrimEnd('\')+'\'
$zip=[IO.Compression.ZipFile]::OpenRead($archive)
try {
    $prefix='MortalFastCombat/tools/runtime/'
    foreach($entry in $zip.Entries){
        if(!$entry.FullName.StartsWith($prefix,[StringComparison]::Ordinal) -or !$entry.Name){continue}
        $rel=$entry.FullName.Substring($prefix.Length)
        $dest=[IO.Path]::GetFullPath((Join-Path $runtime $rel))
        if(!$dest.StartsWith($runtime,[StringComparison]::OrdinalIgnoreCase)){throw 'Runtime archive path escapes destination.'}
        New-Item -ItemType Directory -Path (Split-Path $dest -Parent) -Force | Out-Null
        [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,$dest,$true)
    }
}finally{$zip.Dispose()}
'Prepared official runtime dependencies in tools/runtime.'
