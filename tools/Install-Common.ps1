$ErrorActionPreference='Stop'
function Get-GamePath([string]$GameDirectory){
    $game=[IO.Path]::GetFullPath($GameDirectory).TrimEnd('\')
    if(!(Test-Path -LiteralPath (Join-Path $game 'Mortal.exe'))){throw '请选择含 Mortal.exe 的游戏目录。'}
    if(Get-Process Mortal -ErrorAction SilentlyContinue){throw '请先退出游戏。'}
    return $game
}
function Get-SafeTarget([string]$Game,[string]$Relative){
    $path=[IO.Path]::GetFullPath((Join-Path $Game $Relative))
    if(!$path.StartsWith($Game+'\',[StringComparison]::OrdinalIgnoreCase)){throw '安装清单路径越界。'}
    return $path
}
function Get-IniValue([string]$Text,[string]$Section,[string]$Key){
    $active=$false;$found=@()
    foreach($line in ($Text -split '\r?\n')){
        if($line -match '^\s*\[([^\]]+)\]\s*$'){$active=$Matches[1] -eq $Section}
        elseif($active -and $line -match ('^\s*'+[regex]::Escape($Key)+'\s*=\s*(.*?)\s*$')){$found+=$Matches[1]}
    }
    if($found.Count -gt 1){throw ('配置项重复：'+$Section+'/'+$Key)}
    if($found.Count){return $found[0]}
    return $null
}
function Set-IniValue([string]$Text,[string]$Section,[string]$Key,[string]$Value){
    $lines=[Collections.Generic.List[string]]::new()
    foreach($line in ($Text -split '\r?\n')){$lines.Add($line)}
    $starts=@();$keys=@();$active=$false;$end=$lines.Count
    for($i=0;$i -lt $lines.Count;$i++){
        if($lines[$i] -match '^\s*\[([^\]]+)\]\s*$'){
            if($active){$end=$i;$active=$false}
            if($Matches[1] -eq $Section){$starts+=$i;$active=$true}
        }elseif($active -and $lines[$i] -match ('^\s*'+[regex]::Escape($Key)+'\s*=')){$keys+=$i}
    }
    if($starts.Count -gt 1 -or $keys.Count -gt 1){throw ('配置节或项目重复：'+$Section+'/'+$Key)}
    if($keys.Count){$lines[$keys[0]]=$Key+' = '+$Value}
    elseif($starts.Count){$lines.Insert($end,$Key+' = '+$Value)}
    else{$lines.Add('');$lines.Add('['+$Section+']');$lines.Add($Key+' = '+$Value)}
    return ($lines -join "`r`n")
}
function Read-TextOrEmpty([string]$Path){if(Test-Path -LiteralPath $Path){return [IO.File]::ReadAllText($Path)};return ''}
function Read-JsonArray([string]$Path){
    # Windows PowerShell 5.1 emits JSON arrays as one pipeline object; enumerate explicitly.
    $value=Get-Content -LiteralPath $Path -Raw|ConvertFrom-Json
    foreach($item in $value){$item}
}
function Write-Utf8([string]$Path,[string]$Text){
    New-Item -ItemType Directory -Path (Split-Path $Path -Parent) -Force|Out-Null
    [IO.File]::WriteAllText($Path,$Text,[Text.UTF8Encoding]::new($true))
}
