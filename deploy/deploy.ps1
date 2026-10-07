# Baut die Images lokal (linux/amd64), überträgt sie per "docker save" auf die VM und startet die Container.
# Kein Build auf der VM, keine Registry. Beim ersten Aufruf entstehen außerhalb des Repositorys die .env für den
# Betrieb (Zufallswerte) und die OpenIddict-Zertifikate (ENV_FILE, SECRETS_DIR aus der Konfiguration).
# Caddy (HTTPS) startet erst, wenn die DNS-Einträge öffentlich auf die VM zeigen (sonst scheitert Let's Encrypt).
#
#   deploy.ps1 [-Tag <tag>] [-SkipBuild] [-Yes]
param([string]$Tag = "", [switch]$SkipBuild, [switch]$Yes)
. "$PSScriptRoot\lib.ps1"
$cfg = Get-BpsimConfig
$repo = Split-Path $PSScriptRoot -Parent
if (-not $Tag) { $Tag = (git -C $repo rev-parse --short HEAD).Trim() }
if (-not $cfg.ENV_FILE -or -not $cfg.SECRETS_DIR) { throw "ENV_FILE und SECRETS_DIR in der Konfiguration angeben (Pfade außerhalb des Repos)." }
if (-not $cfg.ACME_EMAIL) { throw "ACME_EMAIL fehlt (Kontakt für Let's Encrypt)." }

$images = [ordered]@{
    "bpsim-auth:$Tag"     = "Dockerfile.auth"
    "bpsim-api:$Tag"      = "Dockerfile.api"
    "bpsim-web:$Tag"      = "Dockerfile.portal"
    "bpsim-postkorb:$Tag" = "Dockerfile.postkorb"
}
$simImage = "bpsim-bundid-simulator:$Tag"
$simSource = "https://github.com/DrFisch/bundid-simulator.git#bpsim/standardkonform"

function New-Secret([int]$Length) {
    $chars = [char[]]"ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789"
    $bytes = New-Object byte[] $Length
    [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    return -join ($bytes | ForEach-Object { $chars[$_ % $chars.Length] })
}

function Read-EnvFile([string]$Path) {
    $values = [ordered]@{}
    foreach ($line in Get-Content $Path) { if ($line -match '^([A-Z_]+)=(.*)$') { $values[$Matches[1]] = $Matches[2] } }
    return $values
}

Write-Host "Deploy Tag $Tag nach $($cfg.VM_NAME) (https://$($cfg.BPSIM_DOMAIN))"
if (-not $Yes -and (Read-Host "Fortfahren? (j/n)") -ne "j") { return }

# 1) .env für den Betrieb (außerhalb des Repos): beim ersten Mal mit Zufallswerten anlegen, sonst nur Tag setzen
if (-not (Test-Path $cfg.ENV_FILE)) {
    $env0 = [ordered]@{
        BPSIM_DOMAIN = $cfg.BPSIM_DOMAIN; ACME_EMAIL = $cfg.ACME_EMAIL
        BPSIM_TAG = $Tag; BUNDID_SIM_IMAGE = $simImage
        SQL_SA_PASSWORD = "Aa1" + (New-Secret 24); SQL_MEMORY_LIMIT_MB = "1024"; SQL_MEM_LIMIT = "2g"
        BUNDID_SIM_KEYSTORE_PASSWORD = New-Secret 24; OIDC_CERT_PASSWORD = New-Secret 24
        WEB_CLIENT_SECRET = New-Secret 40; POSTKORB_API_KEY = New-Secret 48
    }
    Write-LfFile $cfg.ENV_FILE ($env0.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" })
    Write-Host "Neue .env für den Betrieb angelegt: $($cfg.ENV_FILE)"
}
$envValues = Read-EnvFile $cfg.ENV_FILE
$envValues["BPSIM_TAG"] = $Tag
$envValues["BUNDID_SIM_IMAGE"] = $simImage
Write-LfFile $cfg.ENV_FILE ($envValues.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" })

# 2) OpenIddict-Zertifikate (Signatur, Verschlüsselung) – einmalig, selbstsigniert, 5 Jahre
$oidcDir = Join-Path $cfg.SECRETS_DIR "oidc"
New-Item -ItemType Directory -Force $oidcDir | Out-Null
$pfxPassword = ConvertTo-SecureString $envValues["OIDC_CERT_PASSWORD"] -AsPlainText -Force
foreach ($c in @(@("signing", "DigitalSignature"), @("encryption", "KeyEncipherment"))) {
    $pfx = Join-Path $oidcDir "$($c[0]).pfx"
    if (-not (Test-Path $pfx)) {
        $cert = New-SelfSignedCertificate -Subject "CN=bpsim OpenIddict $($c[0])" -KeyUsage $c[1] -KeyAlgorithm RSA `
            -KeyLength 2048 -KeyExportPolicy Exportable -CertStoreLocation Cert:\CurrentUser\My `
            -NotAfter (Get-Date).AddYears(5) -Type Custom -ErrorAction Stop
        Export-PfxCertificate -Cert $cert -FilePath $pfx -Password $pfxPassword -ErrorAction Stop | Out-Null
        Remove-Item "Cert:\CurrentUser\My\$($cert.Thumbprint)"
        Write-Host "Zertifikat erzeugt: $pfx"
    }
}

# 3) Images bauen (linux/amd64 für die VM)
if (-not $SkipBuild) {
    foreach ($i in $images.GetEnumerator()) {
        Write-Host "Baue $($i.Key) ..."
        docker build --platform linux/amd64 -q -f (Join-Path $repo $i.Value) -t $i.Key $repo | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Build fehlgeschlagen: $($i.Key)" }
    }
    Write-Host "Baue $simImage aus $simSource ..."
    docker build --platform linux/amd64 -q -t $simImage $simSource | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Build fehlgeschlagen: $simImage" }
}

# 4) Images packen (docker save + gzip über tar.exe von Windows)
$work = Join-Path $env:TEMP "bpsim-deploy-$Tag"
New-Item -ItemType Directory -Force $work | Out-Null
$tar = Join-Path $work "images.tar"
$tgz = Join-Path $work "images-$Tag.tar.gz"
Write-Host "Packe Images ..."
docker save -o $tar @($images.Keys) $simImage
if ($LASTEXITCODE -ne 0) { throw "docker save fehlgeschlagen" }
& "$env:SystemRoot\System32\tar.exe" -czf $tgz -C $work images.tar
Remove-Item $tar
Write-Host ("Archiv: {0:N0} MB" -f ((Get-Item $tgz).Length / 1MB))

# 5) Dateien übertragen (Caddyfile und Compose-Datei mit LF-Zeilenenden)
$rd = $cfg.REMOTE_DIR
Invoke-VmSsh $cfg "sudo mkdir -p $rd/secrets/oidc && sudo chown -R `$(id -u):`$(id -g) $rd"
foreach ($f in "compose.prod.yml", "Caddyfile") {
    Write-LfFile (Join-Path $work $f) ((Get-Content (Join-Path $PSScriptRoot $f) -Encoding UTF8) -replace "`r", "")
}
Copy-ToVm $cfg @((Join-Path $work "compose.prod.yml"), (Join-Path $work "Caddyfile")) "$rd/"
Copy-ToVm $cfg @($cfg.ENV_FILE) "$rd/.env"
Copy-ToVm $cfg @((Join-Path $oidcDir "signing.pfx"), (Join-Path $oidcDir "encryption.pfx")) "$rd/secrets/oidc/"
# .env nur für den Betriebsbenutzer; Zertifikate für den Container-Benutzer (UID 1654) lesbar
Invoke-VmSsh $cfg "chmod 600 $rd/.env && sudo chown 1654 $rd/secrets/oidc/*.pfx && sudo chmod 600 $rd/secrets/oidc/*.pfx"
Write-Host "Übertrage Images ..."
Copy-ToVm $cfg @($tgz) "$rd/"
# Das Archiv enthält die Datei images.tar (Ausgabe von docker save) – direkt an docker load streamen.
Invoke-VmSsh $cfg "tar -xzOf $rd/images-$Tag.tar.gz images.tar | sudo docker load && rm $rd/images-$Tag.tar.gz"
Remove-Item -Recurse -Force $work

# 6) Starten – Caddy nur, wenn alle Namen öffentlich auf die VM zeigen
$ip = Get-VmIp $cfg
$hosts = @($cfg.BPSIM_DOMAIN, "auth.$($cfg.BPSIM_DOMAIN)", "bundid.$($cfg.BPSIM_DOMAIN)", "postkorb.$($cfg.BPSIM_DOMAIN)")
$dnsOk = $true
foreach ($h in $hosts) {
    $a = (Resolve-DnsName -Name $h -Type A -Server 8.8.8.8 -DnsOnly -ErrorAction SilentlyContinue | Where-Object { $_.Type -eq "A" }).IPAddress
    if ($a -ne $ip) { $dnsOk = $false; Write-Host "DNS: $h -> $(if ($a) { $a } else { '(nicht gefunden)' }) (erwartet $ip)" -ForegroundColor Yellow }
}
$compose = Get-ComposeCommand $cfg
if ($dnsOk) {
    Invoke-VmSsh $cfg "$compose up -d --remove-orphans"
    # Eine geänderte Caddyfile übernimmt der laufende Caddy-Container nicht von selbst (Compose startet ihn nur bei
    # geänderter Compose-Konfiguration neu) – Konfiguration ohne Unterbrechung neu laden.
    Invoke-VmSsh $cfg "$compose exec -T caddy caddy reload --config /etc/caddy/Caddyfile --adapter caddyfile"
} else {
    Write-Host "Caddy wird noch nicht gestartet (DNS fehlt). Nach dem Anlegen der Einträge: deploy.ps1 -SkipBuild" -ForegroundColor Yellow
    Invoke-VmSsh $cfg "$compose up -d --remove-orphans sqlserver bundid-simulator auth api web postkorb"
}
Invoke-VmSsh $cfg "$compose ps --format 'table {{.Service}}\t{{.Status}}'"
# Ältere Versionen der eigenen Images entfernen (in Benutzung befindliche lehnt docker rmi ab)
Invoke-VmSsh $cfg "sudo docker images --format '{{.Repository}}:{{.Tag}}' | grep '^bpsim-' | grep -v ':$Tag`$' | xargs -r sudo docker rmi >/dev/null 2>&1; true"
Write-Host "Fertig: https://$($cfg.BPSIM_DOMAIN)" -ForegroundColor Green
