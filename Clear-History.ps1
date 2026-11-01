[CmdletBinding(SupportsShouldProcess,ConfirmImpact='High')]
param([string]$User)
$ErrorActionPreference='Stop'
if(Get-Process Mortal -ErrorAction SilentlyContinue){throw '请先退出游戏再清理胜利记录。'}
$data=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'data')).TrimEnd('\')+'\'
if(!(Test-Path -LiteralPath $data)){return}
if($User -and ($User -in @('.','..') -or $User.IndexOfAny([IO.Path]::GetInvalidFileNameChars()) -ge 0)){throw '用户目录名无效'}
$users=if($User){@(Get-Item -LiteralPath (Join-Path $data $User))}else{@(Get-ChildItem -LiteralPath $data -Directory)}
foreach($folder in $users){
    if(($folder.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw '拒绝访问重解析目录'}
    $target=[IO.Path]::GetFullPath($folder.FullName).TrimEnd('\')+'\'
    if(!$target.StartsWith($data,[StringComparison]::OrdinalIgnoreCase)){throw '记录路径越界'}
    foreach($file in Get-ChildItem -LiteralPath $target -File){
        if($file.Name -notmatch '^history-[0-9A-F]{16}\.json(\.bak|\.tmp)?$'){continue}
        if($PSCmdlet.ShouldProcess($file.FullName,'删除模组周目关联和胜利记录')){Remove-Item -LiteralPath $file.FullName}
    }
}
