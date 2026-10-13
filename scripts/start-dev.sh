#!/usr/bin/env bash
# Start the Docker daemon and wait until the current user can talk to it.
# Idempotent. Used by Cloud Agent `start` and by local macOS / Linux / WSL.
set -euo pipefail

if docker info >/dev/null 2>&1; then
  echo "Docker is ready."
  docker version --format 'Client {{.Client.Version}} / Server {{.Server.Version}}'
  exit 0
fi

if [[ "$(uname -s)" == "Darwin" ]]; then
  echo "Docker Desktop is not running. Start it, then re-run ./scripts/start-dev.sh" >&2
  exit 1
fi

if ! command -v docker >/dev/null 2>&1 || ! command -v dockerd >/dev/null 2>&1; then
  echo "Docker is not installed. Run ./scripts/setup-dev.sh" >&2
  exit 1
fi

if ! getent group docker >/dev/null 2>&1; then
  sudo groupadd docker
fi

if [[ "$(id -u)" -ne 0 ]] && ! id -nG | grep -qw docker; then
  sudo usermod -aG docker "${USER}"
  if [[ "${START_DEV_SG:-}" == "1" ]]; then
    echo "Could not join the docker group in this shell." >&2
    exit 1
  fi
  script=$(readlink -f "$0")
  exec sg docker -c "START_DEV_SG=1 bash $(printf '%q' "$script")"
fi

if [[ -S /var/run/docker.sock ]] && ! docker info >/dev/null 2>&1; then
  sudo rm -f /var/run/docker.sock
fi
if [[ -f /var/run/docker.pid ]]; then
  pid=$(cat /var/run/docker.pid || true)
  if [[ -z "${pid}" ]] || ! kill -0 "${pid}" 2>/dev/null; then
    sudo rm -f /var/run/docker.pid
  fi
fi

if ! pgrep -x dockerd >/dev/null 2>&1; then
  if [[ "$(ps -p 1 -o comm=)" == "systemd" ]]; then
    sudo systemctl start docker
  else
    sudo mkdir -p /var/run /var/lib/docker
    sudo dockerd --host=unix:///var/run/docker.sock --group docker >>/tmp/dockerd.log 2>&1 &
  fi
fi

for _ in $(seq 1 60); do
  if docker info >/dev/null 2>&1; then
    echo "Docker is ready."
    docker version --format 'Client {{.Client.Version}} / Server {{.Server.Version}}'
    exit 0
  fi
  sleep 1
done

echo "Docker daemon did not become ready." >&2
sudo tail -n 40 /tmp/dockerd.log >&2 || true
exit 1
