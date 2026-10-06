# Legt die Google-Cloud-Ressourcen für den Betrieb an: statische IP, Firewall-Regel, VM (Debian 12, Docker per
# Startup-Script), optional ein Budget mit Warnungen. Jeder Schritt zeigt den genauen Befehl und fragt vor der
# Ausführung (-Yes: ohne Rückfrage). Kosten entstehen durch VM, Disk und statische IP – siehe doku/betrieb.md.
#
#   $env:BPSIM_DEPLOY_CONFIG = "C:\...\bpsim-deploy.env"   # außerhalb des Repos
#   powershell -ExecutionPolicy Bypass -File deploy\gcp-setup.ps1 [-Budget 10] [-Yes]
param([int]$Budget = 0, [switch]$Yes)
. "$PSScriptRoot\lib.ps1"
$cfg = Get-BpsimConfig
$p = $cfg.GCP_PROJECT

Write-Host "Projekt $p, Zone $($cfg.GCP_ZONE), VM $($cfg.VM_NAME) ($($cfg.MACHINE_TYPE), $($cfg.DISK_GB) GB pd-standard)"

# 1) Compute-API (kostenlos)
Invoke-Step "Compute Engine API aktivieren (kostenlos)" "gcloud services enable compute.googleapis.com --project $p" {
    gcloud services enable compute.googleapis.com --project $p
} -Yes:$Yes | Out-Null

# 2) SSH-Schlüssel für Windows-OpenSSH (lokal)
if (-not (Test-Path $cfg.SSH_KEY)) {
    Invoke-Step "SSH-Schlüssel erzeugen (lokal)" "ssh-keygen -t ed25519 -f $($cfg.SSH_KEY) -N """" -C bpsim" {
        ssh-keygen -q -t ed25519 -f $cfg.SSH_KEY -N '""' -C bpsim
    } -Yes:$Yes | Out-Null
}
$publicKey = (Get-Content "$($cfg.SSH_KEY).pub" -Raw).Trim()

# 3) Statische externe IP (Kosten: auch bei gestoppter VM)
$ipExists = gcloud compute addresses list --filter "name=$($cfg.IP_NAME)" --project $p --format "value(name)" 2>$null
if (-not $ipExists) {
    Invoke-Step "Statische IP reservieren (KOSTENPFLICHTIG)" `
        "gcloud compute addresses create $($cfg.IP_NAME) --region $($cfg.GCP_REGION) --project $p" {
        gcloud compute addresses create $cfg.IP_NAME --region $cfg.GCP_REGION --project $p
    } -Yes:$Yes | Out-Null
}
$ip = Get-VmIp $cfg
Write-Host "    Statische IP: $ip"

# 4) Firewall: HTTP/HTTPS (TCP 80/443) und HTTP/3 (UDP 443) zur VM; SSH erlaubt die Standardregel default-allow-ssh
$fwExists = gcloud compute firewall-rules list --filter "name=$($cfg.FIREWALL_RULE)" --project $p --format "value(name)" 2>$null
if (-not $fwExists) {
    Invoke-Step "Firewall-Regel anlegen (kostenlos)" `
        "gcloud compute firewall-rules create $($cfg.FIREWALL_RULE) --network default --direction INGRESS --allow tcp:80,tcp:443,udp:443 --target-tags $($cfg.NETWORK_TAG) --source-ranges 0.0.0.0/0 --project $p" {
        gcloud compute firewall-rules create $cfg.FIREWALL_RULE --network default --direction INGRESS `
            --allow "tcp:80,tcp:443,udp:443" --target-tags $cfg.NETWORK_TAG --source-ranges "0.0.0.0/0" --project $p
    } -Yes:$Yes | Out-Null
}

# 5) VM mit Startup-Script (Docker, Swap) und SSH-Schlüssel
$vmExists = gcloud compute instances list --filter "name=$($cfg.VM_NAME)" --project $p --format "value(name)" 2>$null
if (-not $vmExists) {
    # Startup-Script mit LF-Zeilenenden übergeben (git checkout unter Windows kann CRLF liefern)
    $startup = Join-Path $env:TEMP "bpsim-vm-startup.sh"
    Write-LfFile $startup ((Get-Content "$PSScriptRoot\vm-startup.sh") -replace "`r", "")
    $sshKeys = Join-Path $env:TEMP "bpsim-ssh-keys.txt"
    Write-LfFile $sshKeys @("$($cfg.VM_USER):$publicKey")
    Invoke-Step "VM anlegen (KOSTENPFLICHTIG)" `
        "gcloud compute instances create $($cfg.VM_NAME) --zone $($cfg.GCP_ZONE) --machine-type $($cfg.MACHINE_TYPE) --image-family debian-12 --image-project debian-cloud --boot-disk-size $($cfg.DISK_GB)GB --boot-disk-type pd-standard --address $ip --tags $($cfg.NETWORK_TAG) --metadata-from-file startup-script=vm-startup.sh,ssh-keys=<öffentlicher Schlüssel> --project $p" {
        gcloud compute instances create $cfg.VM_NAME --zone $cfg.GCP_ZONE --machine-type $cfg.MACHINE_TYPE `
            --image-family debian-12 --image-project debian-cloud `
            --boot-disk-size "$($cfg.DISK_GB)GB" --boot-disk-type pd-standard `
            --address $ip --tags $cfg.NETWORK_TAG `
            --metadata-from-file "startup-script=$startup,ssh-keys=$sshKeys" --project $p
    } -Yes:$Yes | Out-Null
}

# 6) Warten, bis Docker auf der VM installiert ist
Write-Host ""; Write-Host "Warte auf die Einrichtung der VM (Docker-Installation, bis zu 5 Minuten) ..."
$deadline = (Get-Date).AddMinutes(8)
do {
    Start-Sleep -Seconds 15
    $ready = $false
    try { Invoke-VmSsh $cfg "test -f /var/lib/bpsim/ready && sudo docker version --format '{{.Server.Version}}'"; $ready = $true } catch { }
} while (-not $ready -and (Get-Date) -lt $deadline)
if (-not $ready) { throw "VM nach 8 Minuten nicht bereit – Log: gcloud compute instances get-serial-port-output $($cfg.VM_NAME) --zone $($cfg.GCP_ZONE)" }
Write-Host "VM bereit: $ip" -ForegroundColor Green

# 7) Optional: Budget mit Warnungen bei 50/90/100 % (nur Benachrichtigung, stoppt nichts)
if ($Budget -gt 0) {
    if (-not $cfg.BILLING_ACCOUNT) { throw "BILLING_ACCOUNT fehlt für das Budget." }
    Invoke-Step "Budget $Budget EUR mit Warnungen 50/90/100 % (kostenlos)" `
        "gcloud billing budgets create --billing-account <Rechnungskonto> --display-name bpsim --budget-amount ${Budget}EUR --threshold-rule percent=0.5 --threshold-rule percent=0.9 --threshold-rule percent=1.0 --filter-projects projects/$p" {
        gcloud services enable billingbudgets.googleapis.com --project $p
        gcloud billing budgets create --billing-account $cfg.BILLING_ACCOUNT --display-name bpsim `
            --budget-amount "${Budget}EUR" --threshold-rule percent=0.5 --threshold-rule percent=0.9 `
            --threshold-rule percent=1.0 --filter-projects "projects/$p"
    } -Yes:$Yes | Out-Null
}

Write-Host ""
Write-Host "Nächste Schritte: DNS-Einträge mit dns.ps1 add (bpsimulation und *.bpsimulation → $ip), dann deploy.ps1."
