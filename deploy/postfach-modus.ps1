# Schaltet die Postfach-Variante im Betrieb um, ohne neu zu bauen:
#   simulator = Postfach im simulierten BundID-Konto (BundID-Simulator, Standard)
#   dienst    = eigener Postfach-Dienst (zweiter SAML-Service-Provider mit Single Sign-on)
# Setzt BPSIM_POSTFACH in der .env für den Betrieb (lokal und auf der VM) und lässt Compose die betroffenen Container
# neu erstellen (api, web, caddy). Beide Postfächer laufen weiter; Nachrichten bleiben in dem Postfach, in das sie
# eingeliefert wurden. Die Adresse für Bürgerinnen und Bürger bleibt https://bundid.<domain>/postfach/.
#
#   postfach-modus.ps1 -Modus simulator|dienst [-Yes]
param([Parameter(Mandatory = $true)][ValidateSet("simulator", "dienst")][string]$Modus, [switch]$Yes)
. "$PSScriptRoot\lib.ps1"
$cfg = Get-BpsimConfig
if (-not $cfg.ENV_FILE -or -not (Test-Path $cfg.ENV_FILE)) { throw "ENV_FILE fehlt – zuerst deploy.ps1 ausführen." }

$envValues = Read-EnvFile $cfg.ENV_FILE
$alt = if ($envValues.Contains("BPSIM_POSTFACH")) { $envValues["BPSIM_POSTFACH"] } else { "simulator" }
Write-Host "Postfach-Variante auf $($cfg.VM_NAME): $alt -> $Modus"
if (-not $Yes -and (Read-Host "Fortfahren? (j/n)") -ne "j") { return }

$envValues["BPSIM_POSTFACH"] = $Modus
Write-LfFile $cfg.ENV_FILE ($envValues.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" })
$rd = $cfg.REMOTE_DIR
Copy-ToVm $cfg @($cfg.ENV_FILE) "$rd/.env"
Invoke-VmSsh $cfg "chmod 600 $rd/.env"
$compose = Get-ComposeCommand $cfg
Invoke-VmSsh $cfg "$compose up -d"
Invoke-VmSsh $cfg "$compose exec -T caddy caddy reload --config /etc/caddy/Caddyfile --adapter caddyfile"
Invoke-VmSsh $cfg "$compose ps --format 'table {{.Service}}\t{{.Status}}'"
Write-Host "Fertig: https://bundid.$($cfg.BPSIM_DOMAIN)/postfach/ (Variante $Modus)" -ForegroundColor Green
