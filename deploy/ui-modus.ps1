# Schaltet die Oberfläche des Portals im Betrieb um, ohne neu zu bauen – in beide Richtungen:
#   -Modern true  = Portal 2 (neue, moderne Oberfläche)
#   -Modern false = bisherige Oberfläche (Standard)
# Setzt BPSIM_UI_MODERN in der .env für den Betrieb, kopiert sie auf die VM und lässt Compose nur den Dienst web neu
# erstellen (Ui__Modern überschreibt Ui:Modern aus appsettings.json). Angemeldete Personen bleiben angemeldet: Sitzungen
# und Data-Protection-Schlüssel liegen im Volume. Unabhängig davon wählt ?ui=modern|klassisch|standard die Oberfläche
# für den eigenen Browser, solange BPSIM_UI_URL=true (Standard).
#
#   ui-modus.ps1 -Modern true|false [-Yes]
param([Parameter(Mandatory = $true)][ValidateSet("true", "false")][string]$Modern, [switch]$Yes)
. "$PSScriptRoot\lib.ps1"
$cfg = Get-BpsimConfig
if (-not $cfg.ENV_FILE -or -not (Test-Path $cfg.ENV_FILE)) { throw "ENV_FILE fehlt – zuerst deploy.ps1 ausführen." }

$name = @{ "true" = "Portal 2"; "false" = "bisherige Oberfläche" }
$envValues = Read-EnvFile $cfg.ENV_FILE
$alt = if ($envValues.Contains("BPSIM_UI_MODERN")) { $envValues["BPSIM_UI_MODERN"] } else { "false" }
Write-Host "Oberfläche auf $($cfg.VM_NAME): $alt ($($name[$alt])) -> $Modern ($($name[$Modern]))"
if (-not $Yes -and (Read-Host "Fortfahren? (j/n)") -ne "j") { return }

$envValues["BPSIM_UI_MODERN"] = $Modern
Write-LfFile $cfg.ENV_FILE ($envValues.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" })
$rd = $cfg.REMOTE_DIR
Copy-ToVm $cfg @($cfg.ENV_FILE) "$rd/.env"
Invoke-VmSsh $cfg "chmod 600 $rd/.env"
$compose = Get-ComposeCommand $cfg
Invoke-VmSsh $cfg "$compose up -d web"
Invoke-VmSsh $cfg "$compose exec -T web printenv Ui__Modern Ui__UrlUmschalter"
Invoke-VmSsh $cfg "$compose ps web --format 'table {{.Service}}\t{{.Status}}'"
$zurueck = if ($Modern -eq "true") { "false" } else { "true" }
Write-Host "Fertig: https://$($cfg.BPSIM_DOMAIN)/ zeigt jetzt: $($name[$Modern])." -ForegroundColor Green
Write-Host "Zurück: deploy\ui-modus.ps1 -Modern $zurueck  ·  nur im eigenen Browser: ?ui=modern | ?ui=klassisch | ?ui=standard"
