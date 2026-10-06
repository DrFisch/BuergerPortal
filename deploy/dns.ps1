# DNS-Einträge für das Projekt bei IONOS (DNS-API: https://developer.hosting.ionos.de/docs/dns).
# Darf AUSSCHLIESSLICH die A-Records "bpsimulation" und "*.bpsimulation" der Zone anlegen bzw. löschen –
# alle anderen Einträge werden nie verändert. Vor jeder Änderung wird die Zone gesichert (BACKUP_DIR).
#
#   dns.ps1 show                 Einträge der Zone anzeigen (nur lesen)
#   dns.ps1 backup               Zone als JSON sichern
#   dns.ps1 add [-Ip <ip>]       beide A-Records anlegen (TTL 300); ohne -Ip die statische IP der VM
#   dns.ps1 remove               beide A-Records löschen
param([Parameter(Mandatory)][ValidateSet("show", "backup", "add", "remove")][string]$Action, [string]$Ip = "")
. "$PSScriptRoot\lib.ps1"
$cfg = Get-BpsimConfig
if (-not $cfg.IONOS_API_KEY) { throw "IONOS_API_KEY fehlt in der Konfiguration." }
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$api = "https://api.hosting.ionos.com/dns/v1"
$headers = @{ "X-API-Key" = $cfg.IONOS_API_KEY; "Accept" = "application/json" }

# Die einzigen Namen, die dieses Skript anfassen darf
$base = $cfg.BPSIM_DOMAIN
if ($base -notmatch "^bpsimulation\.") { throw "BPSIM_DOMAIN muss mit 'bpsimulation.' beginnen (ist: $base)." }
$names = @($base, "*.$base")

$zone = Invoke-RestMethod -Uri "$api/zones" -Headers $headers | Where-Object { $_.name -eq $cfg.DNS_ZONE }
if (-not $zone) { throw "Zone $($cfg.DNS_ZONE) nicht gefunden." }
$full = Invoke-RestMethod -Uri "$api/zones/$($zone.id)" -Headers $headers

function Save-Zone {
    if (-not $cfg.BACKUP_DIR) { throw "BACKUP_DIR fehlt in der Konfiguration (Sicherung vor Änderungen ist Pflicht)." }
    New-Item -ItemType Directory -Force $cfg.BACKUP_DIR | Out-Null
    $file = Join-Path $cfg.BACKUP_DIR ("dns-backup-" + (Get-Date -Format "yyyy-MM-dd-HHmmss") + ".json")
    [IO.File]::WriteAllText($file, ($full | ConvertTo-Json -Depth 6))
    Write-Host "Zone gesichert: $file"
}

$own = @($full.records | Where-Object { $_.name -in $names })

switch ($Action) {
    "show" {
        $full.records | Sort-Object name, type | Format-Table name, type, content, ttl, disabled -AutoSize | Out-String -Width 200
    }
    "backup" { Save-Zone }
    "add" {
        if (-not $Ip) { $Ip = Get-VmIp $cfg }
        $missing = @()
        foreach ($n in $names) {
            $existing = @($own | Where-Object { $_.name -eq $n })
            if ($existing.Count -eq 0) { $missing += $n; continue }
            if ($existing | Where-Object { $_.type -ne "A" -or $_.content -ne $Ip }) {
                throw "Für $n gibt es bereits einen anderen Eintrag ($(($existing | ForEach-Object { "$($_.type) $($_.content)" }) -join ', ')) – abgebrochen, bitte prüfen."
            }
            Write-Host "$n zeigt bereits auf $Ip – nichts zu tun."
        }
        if ($missing.Count -eq 0) { return }
        Save-Zone
        $body = @($missing | ForEach-Object { @{ name = $_; type = "A"; content = $Ip; ttl = 300; prio = 0; disabled = $false } })
        Invoke-RestMethod -Method Post -Uri "$api/zones/$($zone.id)/records" -Headers $headers `
            -ContentType "application/json" -Body (ConvertTo-Json -InputObject $body -Depth 3) | Out-Null
        Write-Host "Angelegt: $($missing -join ', ') -> $Ip (TTL 300)" -ForegroundColor Green
    }
    "remove" {
        if ($own.Count -eq 0) { Write-Host "Keine Einträge für $($names -join ', ') vorhanden."; return }
        Save-Zone
        foreach ($r in $own) {
            Invoke-RestMethod -Method Delete -Uri "$api/zones/$($zone.id)/records/$($r.id)" -Headers $headers | Out-Null
            Write-Host "Gelöscht: $($r.name) $($r.type) $($r.content)"
        }
    }
}
