# Zeigt die Container-Logs auf der VM.
#   logs.ps1 [-Service auth|api|web|postkorb|bundid-simulator|sqlserver|caddy] [-Tail 100] [-Follow]
param([string]$Service = "", [int]$Tail = 100, [switch]$Follow)
. "$PSScriptRoot\lib.ps1"
$cfg = Get-BpsimConfig
$f = if ($Follow) { " -f" } else { "" }
Invoke-VmSsh $cfg "$(Get-ComposeCommand $cfg) logs --no-color --tail $Tail$f $Service"
