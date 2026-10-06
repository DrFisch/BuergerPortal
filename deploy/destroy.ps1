# Baut alles in Google Cloud ab: VM (mit Disk und allen Daten!), statische IP und Firewall-Regel.
# DNS-Einträge bleiben – entfernen mit dns.ps1 remove. Ein Budget bleibt ebenfalls bestehen (kostenlos).
# Vorher ggf. db-backup.ps1 ausführen.
#   destroy.ps1
. "$PSScriptRoot\lib.ps1"
$cfg = Get-BpsimConfig
$p = $cfg.GCP_PROJECT
Write-Host "Löscht in Projekt ${p}: VM $($cfg.VM_NAME) samt Disk und Daten, IP $($cfg.IP_NAME), Firewall-Regel $($cfg.FIREWALL_RULE)." -ForegroundColor Red
if ((Read-Host "Zum Bestätigen den VM-Namen eingeben") -ne $cfg.VM_NAME) { Write-Host "Abgebrochen."; return }

gcloud compute instances delete $cfg.VM_NAME --zone $cfg.GCP_ZONE --project $p --delete-disks all --quiet
gcloud compute addresses delete $cfg.IP_NAME --region $cfg.GCP_REGION --project $p --quiet
gcloud compute firewall-rules delete $cfg.FIREWALL_RULE --project $p --quiet

$known = Join-Path (Split-Path $cfg.SSH_KEY -Parent) "bpsim_known_hosts"
if (Test-Path $known) { Remove-Item $known }
Write-Host "Übrig: " -NoNewline
gcloud compute instances list --project $p --format "value(name)"
gcloud compute addresses list --project $p --format "value(name)"
Write-Host "(leer = nichts mehr vorhanden). DNS: dns.ps1 remove" -ForegroundColor Green
