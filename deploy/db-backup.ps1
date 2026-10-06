# Sichert die drei Datenbanken (AuthServer, BuergerPortalDB, PostkorbDB) auf der VM und lädt die .bak-Dateien
# in BACKUP_DIR (außerhalb des Repos). Das SA-Passwort bleibt im Container (Umgebungsvariable MSSQL_SA_PASSWORD). Express-Edition: Sicherung ohne Kompression.
#   db-backup.ps1
. "$PSScriptRoot\lib.ps1"
$cfg = Get-BpsimConfig
if (-not $cfg.BACKUP_DIR) { throw "BACKUP_DIR fehlt in der Konfiguration." }
$stamp = Get-Date -Format "yyyy-MM-dd-HHmmss"
$compose = Get-ComposeCommand $cfg
$rd = $cfg.REMOTE_DIR
$lines = @("$compose exec -T sqlserver mkdir -p /var/opt/mssql/backup")
foreach ($db in "AuthServer", "BuergerPortalDB", "PostkorbDB") {
    # Das Passwort setzt die Shell IM Container ein (\$ bleibt bis dorthin unaufgelöst).
    $lines += "echo 'Sichere $db ...'"
    $lines += "$compose exec -T sqlserver sh -c `"/opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P \`"\`$MSSQL_SA_PASSWORD\`" -b -Q \`"BACKUP DATABASE [$db] TO DISK = N'/var/opt/mssql/backup/$db-$stamp.bak' WITH INIT\`"`""
}
$lines += "mkdir -p $rd/backup"
$lines += "$compose cp sqlserver:/var/opt/mssql/backup/. $rd/backup/"
$lines += "sudo chown -R `$(id -u) $rd/backup"
Invoke-VmScript $cfg $lines
$target = Join-Path $cfg.BACKUP_DIR $stamp
New-Item -ItemType Directory -Force $target | Out-Null
$ip = Get-VmIp $cfg
$scpArgs = (Get-SshArgs $cfg) + @("$($cfg.VM_USER)@${ip}:$rd/backup/*-$stamp.bak", $target)
& scp @scpArgs
if ($LASTEXITCODE -ne 0) { throw "Download fehlgeschlagen" }
Get-ChildItem $target | Format-Table Name, @{ n = "MB"; e = { "{0:N1}" -f ($_.Length / 1MB) } } -AutoSize
