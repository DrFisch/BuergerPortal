# Gemeinsame Funktionen der Betriebsskripte (per ". $PSScriptRoot\lib.ps1" eingebunden).
#
# Einstellungen: Standardwerte unten; überschreibbar durch eine Datei AUSSERHALB des Repositorys, deren Pfad in der
# Umgebungsvariable BPSIM_DEPLOY_CONFIG steht (Zeilen KEY=Wert), und durch gleichnamige Umgebungsvariablen.
# Geheimnisse (IONOS_API_KEY, Passwörter) stehen nur dort bzw. in der .env für den Betrieb – nie im Repository.
#
# Fehlerbehandlung: Native Programme (gcloud, ssh, scp, docker) schreiben Status- und Fortschrittsmeldungen auf
# stderr; PowerShell 5.1 macht daraus mit "Stop" einen Abbruch, auch bei Erfolg. Deshalb "Continue" – über Erfolg
# entscheidet ausschließlich der Exit-Code ($LASTEXITCODE), der nach jedem wichtigen Aufruf geprüft wird.
$ErrorActionPreference = "Continue"

function Get-BpsimConfig {
    $cfg = [ordered]@{
        GCP_PROJECT   = ""
        GCP_REGION    = "europe-west1"
        GCP_ZONE      = "europe-west1-b"
        VM_NAME       = "bpsim-vm"
        MACHINE_TYPE  = "e2-medium"
        DISK_GB       = "20"
        IP_NAME       = "bpsim-ip"
        FIREWALL_RULE = "bpsim-allow-web"
        NETWORK_TAG   = "bpsim-web"
        VM_USER       = "bpsim"
        SSH_KEY       = (Join-Path $HOME ".ssh\bpsim_gcp")
        REMOTE_DIR    = "/opt/bpsim"
        BPSIM_DOMAIN  = "bpsimulation.gortisbuergerportal.de"
        DNS_ZONE      = "gortisbuergerportal.de"
        ACME_EMAIL    = ""
        ENV_FILE      = ""   # .env für den Betrieb (außerhalb des Repos); deploy.ps1 legt sie bei Bedarf an
        SECRETS_DIR   = ""   # Ordner für secrets/oidc/*.pfx (außerhalb des Repos)
        BACKUP_DIR    = ""   # Ziel für Datenbank-Sicherungen
        IONOS_API_KEY = ""
        BILLING_ACCOUNT = ""
    }
    $file = $env:BPSIM_DEPLOY_CONFIG
    if ($file) {
        if (-not (Test-Path $file)) { throw "BPSIM_DEPLOY_CONFIG zeigt auf eine fehlende Datei: $file" }
        foreach ($line in Get-Content $file) {
            if ($line -match '^\s*([A-Z_]+)\s*=(.*)$') { $cfg[$Matches[1]] = $Matches[2].Trim().Trim('"') }
        }
    }
    foreach ($k in @($cfg.Keys)) {
        $e = [Environment]::GetEnvironmentVariable($k)
        if ($e) { $cfg[$k] = $e }
    }
    # Kompatibel zur Datei des Vorgängerprojekts (PROJECT_ID statt GCP_PROJECT)
    if (-not $cfg.GCP_PROJECT -and $cfg.Contains("PROJECT_ID")) { $cfg.GCP_PROJECT = $cfg["PROJECT_ID"] }
    if (-not $cfg.GCP_PROJECT) { throw "GCP_PROJECT fehlt (Datei aus BPSIM_DEPLOY_CONFIG oder Umgebungsvariable)." }
    return $cfg
}

# Zeigt einen Schritt mit genauem Befehl an und führt ihn erst nach Bestätigung aus (-Yes: ohne Rückfrage).
function Invoke-Step {
    param([string]$Title, [string]$Display, [scriptblock]$Action, [switch]$Yes)
    Write-Host ""
    Write-Host "==> $Title" -ForegroundColor Cyan
    Write-Host "    $Display"
    if (-not $Yes) {
        $answer = Read-Host "    Ausführen? (j/n)"
        if ($answer -ne "j") { Write-Host "    übersprungen" -ForegroundColor Yellow; return $false }
    }
    $ErrorActionPreference = "Continue"   # lokal: stderr der Programme ist kein Fehler (siehe Dateikopf)
    $global:LASTEXITCODE = 0
    & $Action
    if ($LASTEXITCODE -ne 0) { throw "Fehlgeschlagen ($LASTEXITCODE): $Display" }
    return $true
}

function Get-VmIp($cfg) {
    $ErrorActionPreference = "Continue"
    $ip = gcloud compute addresses describe $cfg.IP_NAME --region $cfg.GCP_REGION --project $cfg.GCP_PROJECT `
        --format "value(address)" 2>$null
    if (-not $ip) { throw "Statische IP $($cfg.IP_NAME) nicht gefunden (gcp-setup.ps1 ausgeführt?)." }
    return $ip.Trim()
}

function Get-SshArgs($cfg) {
    # Eigene known_hosts-Datei: Nach destroy/neu anlegen hat die VM einen neuen Host-Schlüssel.
    $known = Join-Path (Split-Path $cfg.SSH_KEY -Parent) "bpsim_known_hosts"
    return @("-i", $cfg.SSH_KEY, "-o", "StrictHostKeyChecking=accept-new", "-o", "UserKnownHostsFile=$known",
        "-o", "ConnectTimeout=15")
}

# Befehl auf der VM ausführen (Windows-OpenSSH; gcloud compute ssh würde unter Windows PuTTY verwenden).
function Invoke-VmSsh($cfg, [string]$Command) {
    $ErrorActionPreference = "Continue"
    $ip = Get-VmIp $cfg
    $sshArgs = (Get-SshArgs $cfg) + @("$($cfg.VM_USER)@$ip", $Command)
    & ssh @sshArgs
    if ($LASTEXITCODE -ne 0) { throw "SSH-Befehl fehlgeschlagen ($LASTEXITCODE): $Command" }
}

# Mehrzeilige Befehle als Skript übertragen und mit bash ausführen. Vermeidet Quoting-Probleme: PowerShell 5.1
# reicht eingebettete Anführungszeichen nicht korrekt an ssh.exe weiter.
function Invoke-VmScript($cfg, [string[]]$Lines) {
    $local = Join-Path $env:TEMP ("bpsim-" + [guid]::NewGuid().ToString("N") + ".sh")
    Write-LfFile $local (@("set -euo pipefail") + $Lines)
    $remote = "/tmp/$(Split-Path $local -Leaf)"
    try {
        Copy-ToVm $cfg @($local) $remote
        Invoke-VmSsh $cfg "bash $remote; rc=`$?; rm -f $remote; exit `$rc"
    } finally { Remove-Item $local -ErrorAction SilentlyContinue }
}

function Copy-ToVm($cfg, [string[]]$Local, [string]$Remote) {
    $ErrorActionPreference = "Continue"
    $ip = Get-VmIp $cfg
    $scpArgs = (Get-SshArgs $cfg) + $Local + @("$($cfg.VM_USER)@${ip}:$Remote")
    & scp @scpArgs
    if ($LASTEXITCODE -ne 0) { throw "Übertragung fehlgeschlagen: $($Local -join ', ')" }
}

# docker compose auf der VM (im Betriebsordner, mit der .env für den Betrieb)
function Get-ComposeCommand($cfg) {
    return "cd $($cfg.REMOTE_DIR) && sudo docker compose -f compose.prod.yml --env-file .env"
}

# .env-Datei (KEY=Wert je Zeile) in eine geordnete Tabelle lesen
function Read-EnvFile([string]$Path) {
    $values = [ordered]@{}
    foreach ($line in Get-Content $Path) { if ($line -match '^([A-Z_]+)=(.*)$') { $values[$Matches[1]] = $Matches[2] } }
    return $values
}

# Textdatei mit LF-Zeilenenden schreiben (für Linux; Set-Content schriebe CRLF und unter PS 5.1 kein UTF-8)
function Write-LfFile([string]$Path, [string[]]$Lines) {
    [IO.File]::WriteAllText($Path, (($Lines -join "`n") + "`n"), (New-Object Text.UTF8Encoding $false))
}
