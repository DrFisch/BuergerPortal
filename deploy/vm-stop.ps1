# Hält die VM an (Daten auf der Disk bleiben erhalten). Gestoppt kosten weiterhin die Disk und die reservierte
# statische IP (eine nicht genutzte statische IP ist teurer als eine genutzte) – Beträge in doku/betrieb.md.
#   vm-stop.ps1 [-Yes]
param([switch]$Yes)
. "$PSScriptRoot\lib.ps1"
$cfg = Get-BpsimConfig
Invoke-Step "VM anhalten" "gcloud compute instances stop $($cfg.VM_NAME) --zone $($cfg.GCP_ZONE) --project $($cfg.GCP_PROJECT)" {
    gcloud compute instances stop $cfg.VM_NAME --zone $cfg.GCP_ZONE --project $cfg.GCP_PROJECT
} -Yes:$Yes | Out-Null
