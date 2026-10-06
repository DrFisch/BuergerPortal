#!/bin/bash
# Startup-Script der VM (Debian 12): läuft bei JEDEM Start als root (Google-Guest-Agent), deshalb idempotent.
# Legt 2 GB Swap an und installiert Docker mit Compose-Plugin aus dem offiziellen Docker-Repository.
# Die Container startet Docker selbst wieder (restart: unless-stopped).
set -euo pipefail

# Swap als Puffer für Speicherspitzen (SQL Server, .NET, Java)
if [ ! -f /swapfile ]; then
  fallocate -l 2G /swapfile
  chmod 600 /swapfile
  mkswap /swapfile
  echo '/swapfile none swap sw 0 0' >>/etc/fstab
fi
swapon --show | grep -q /swapfile || swapon /swapfile

if ! command -v docker >/dev/null; then
  # Log-Rotation, damit Container-Logs die Disk nicht füllen
  mkdir -p /etc/docker
  echo '{ "log-driver": "json-file", "log-opts": { "max-size": "10m", "max-file": "3" } }' >/etc/docker/daemon.json

  apt-get update
  apt-get install -y ca-certificates curl
  install -m 0755 -d /etc/apt/keyrings
  curl -fsSL https://download.docker.com/linux/debian/gpg -o /etc/apt/keyrings/docker.asc
  chmod a+r /etc/apt/keyrings/docker.asc
  . /etc/os-release
  echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/debian $VERSION_CODENAME stable" \
    >/etc/apt/sources.list.d/docker.list
  apt-get update
  apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
  systemctl enable --now docker
fi

# Betriebsordner und Markierung für die Skripte (Einrichtung abgeschlossen)
mkdir -p /opt/bpsim /var/lib/bpsim
touch /var/lib/bpsim/ready
