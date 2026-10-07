# Zeigt den Zustand: VM, Container, Speicher, Disk und Erreichbarkeit der öffentlichen Adressen.
#   vm-status.ps1
. "$PSScriptRoot\lib.ps1"
$cfg = Get-BpsimConfig
$status = gcloud compute instances describe $cfg.VM_NAME --zone $cfg.GCP_ZONE --project $cfg.GCP_PROJECT `
    --format "value(status,machineType.basename())" 2>$null
Write-Host "VM $($cfg.VM_NAME): $status"
if ($status -notmatch "RUNNING") { return }
Invoke-VmSsh $cfg "$(Get-ComposeCommand $cfg) ps --format 'table {{.Service}}\t{{.Status}}'; echo; free -m; echo; df -h / | tail -1; echo; sudo docker stats --no-stream --format 'table {{.Name}}\t{{.MemUsage}}\t{{.CPUPerc}}'"
Write-Host ""
foreach ($u in "https://$($cfg.BPSIM_DOMAIN)/", "https://auth.$($cfg.BPSIM_DOMAIN)/.well-known/openid-configuration",
               "https://bundid.$($cfg.BPSIM_DOMAIN)/saml/metadata", "https://bundid.$($cfg.BPSIM_DOMAIN)/postfach/") {
    # Postfach: 302 = Weiterleitung zur Anmeldung (Simulator) bzw. ins Postfach (eigener Dienst) – beides in Ordnung
    $code = curl.exe -s -o NUL -w "%{http_code}" --max-time 10 $u
    Write-Host ("{0,-75} {1}" -f $u, $code)
}
