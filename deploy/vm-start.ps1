# Startet die VM. Die Container starten von selbst (restart: unless-stopped); danach wird der Zustand angezeigt.
#   vm-start.ps1
. "$PSScriptRoot\lib.ps1"
$cfg = Get-BpsimConfig
Write-Host "Starte $($cfg.VM_NAME) ..."
gcloud compute instances start $cfg.VM_NAME --zone $cfg.GCP_ZONE --project $cfg.GCP_PROJECT
if ($LASTEXITCODE -ne 0) { throw "Start fehlgeschlagen" }
$deadline = (Get-Date).AddMinutes(4)
do {
    Start-Sleep -Seconds 10
    $ok = $false
    try { Invoke-VmSsh $cfg "true"; $ok = $true } catch { }
} while (-not $ok -and (Get-Date) -lt $deadline)
if (-not $ok) { throw "VM per SSH nicht erreichbar" }
Start-Sleep -Seconds 20
Invoke-VmSsh $cfg "$(Get-ComposeCommand $cfg) ps --format 'table {{.Service}}\t{{.Status}}'"
Write-Host "Läuft: https://$($cfg.BPSIM_DOMAIN)" -ForegroundColor Green
