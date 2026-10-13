#!/usr/bin/env bash
# Install the MVP toolchain if it is missing.
# Linux / WSL: apt + official Clojure installer + pinned Terraform.
# macOS: Homebrew for Java, Clojure, and jq; pinned Terraform zip; Docker Desktop.
# Safe to re-run. Cloud Agent install calls this script.
set -euo pipefail
cd "$(dirname "$0")/.."

TERRAFORM_VERSION=1.16.5

log() { printf '==> %s\n' "$*"; }

have() { command -v "$1" >/dev/null 2>&1; }

java_major() {
  local line major
  line=$(java -version 2>&1 | head -1)
  major=$(sed -E 's/.* version "([0-9]+).*/\1/' <<<"$line")
  if [[ "$major" == "1" ]]; then
    major=$(sed -E 's/.* version "1\.([0-9]+).*/\1/' <<<"$line")
  fi
  printf '%s' "$major"
}

java_ok() {
  have java || return 1
  local major
  major=$(java_major)
  [[ "$major" =~ ^[0-9]+$ ]] && (( major >= 17 ))
}

clojure_ok() {
  local path
  path=$(command -v clojure || true)
  [[ -n "$path" && "$path" != /mnt/* ]]
}

terraform_ok() { have terraform; }

require_sudo() {
  if [[ "$(id -u)" -eq 0 ]]; then
    return 0
  fi
  if ! sudo -n true; then
    echo "sudo is required to install missing tools." >&2
    exit 1
  fi
}

terraform_asset() {
  case "$(uname -s)-$(uname -m)" in
    Linux-x86_64) printf '%s %s\n' linux_amd64 2bc2fcfff033265c9e02ca0351f01794eb122f62a9b2a49a3294b9e49eaab5e4 ;;
    Linux-aarch64|Linux-arm64) printf '%s %s\n' linux_arm64 61a50b00485ee4810cf20581ef080fc54d34d666e175c58d9a10501c65c1ccde ;;
    Darwin-x86_64) printf '%s %s\n' darwin_amd64 9809158e481cf2d3a8433e59179f397b8edb5172397711bd98f6bb32edcaf53b ;;
    Darwin-arm64) printf '%s %s\n' darwin_arm64 ecdef65e24193d627f27c39baeda31295f08c938d8a3d4764f442fb916d4b7dc ;;
    *)
      echo "Unsupported OS/arch for Terraform: $(uname -s) $(uname -m)" >&2
      exit 1
      ;;
  esac
}

install_terraform() {
  if terraform_ok; then
    log "Terraform already present: $(terraform version | head -1)"
    return 0
  fi
  require_sudo
  local asset sum url tmp
  read -r asset sum < <(terraform_asset)
  url="https://releases.hashicorp.com/terraform/${TERRAFORM_VERSION}/terraform_${TERRAFORM_VERSION}_${asset}.zip"
  tmp=$(mktemp -d)
  log "Installing Terraform ${TERRAFORM_VERSION} (${asset})"
  curl -fsSL -o "${tmp}/terraform.zip" "$url"
  if have sha256sum; then
    echo "${sum}  ${tmp}/terraform.zip" | sha256sum -c -
  else
    echo "${sum}  ${tmp}/terraform.zip" | shasum -a 256 -c -
  fi
  sudo unzip -o "${tmp}/terraform.zip" -d /usr/local/bin
  sudo chmod 755 /usr/local/bin/terraform
  rm -rf "$tmp"
  hash -r || true
}

install_clojure_linux() {
  if clojure_ok; then
    log "Clojure CLI already present: $(clojure --version 2>&1)"
    return 0
  fi
  require_sudo
  local tmp
  tmp=$(mktemp)
  log "Installing Clojure CLI"
  curl -fsSL -o "$tmp" https://github.com/clojure/brew-install/releases/latest/download/linux-install.sh
  sudo bash "$tmp"
  rm -f "$tmp"
  hash -r || true
}

link_brew_openjdk() {
  local prefix cmd
  prefix=$(brew --prefix openjdk@21)
  for cmd in java javac jar keytool; do
    if [[ -x "${prefix}/bin/${cmd}" ]]; then
      sudo ln -sfn "${prefix}/bin/${cmd}" "/usr/local/bin/${cmd}"
    fi
  done
  hash -r || true
}

install_macos() {
  if ! have brew; then
    if java_ok && clojure_ok && terraform_ok && have jq && have docker; then
      log "Toolchain already present."
    else
      echo "Homebrew is required for missing tools. Install it from https://brew.sh and re-run." >&2
      exit 1
    fi
  else
    if ! java_ok; then
      log "Installing Temurin-compatible OpenJDK 21"
      brew install openjdk@21
      require_sudo
      link_brew_openjdk
    fi
    if ! clojure_ok; then
      log "Installing Clojure CLI"
      brew trust clojure/tools
      brew install clojure/tools/clojure
    fi
    if ! have jq; then
      log "Installing jq"
      brew install jq
    fi
    if ! have docker; then
      echo "Install Docker Desktop from https://docs.docker.com/desktop/setup/install/mac-install/, then re-run ./scripts/start-dev.sh" >&2
      exit 1
    fi
  fi
  install_terraform
}

member_of_docker() {
  [[ "$(id -u)" -eq 0 ]] && return 0
  getent group docker 2>/dev/null | grep -Eq "(^|[,:])${USER}(,|$)"
}

install_linux() {
  local packages=()
  if ! java_ok; then
    packages+=(openjdk-21-jdk-headless)
  fi
  if ! have curl; then packages+=(curl ca-certificates); fi
  if ! have unzip; then packages+=(unzip); fi
  if ! have rlwrap; then packages+=(rlwrap); fi
  if ! have jq; then packages+=(jq); fi
  if ! have iptables; then packages+=(iptables); fi
  if ! have docker; then packages+=(docker.io); fi
  if (( ${#packages[@]} > 0 )); then
    require_sudo
    log "Installing packages: ${packages[*]}"
    sudo DEBIAN_FRONTEND=noninteractive apt-get update -qq
    sudo DEBIAN_FRONTEND=noninteractive apt-get install -y -qq "${packages[@]}"
  else
    log "Base packages already present"
  fi
  install_clojure_linux
  install_terraform
  if ! getent group docker >/dev/null 2>&1; then
    require_sudo
    sudo groupadd docker
  fi
  if ! member_of_docker; then
    require_sudo
    sudo usermod -aG docker "${USER}"
  fi
  # usermod does not change the current process. Re-enter so docker pull works.
  if [[ "$(id -u)" -ne 0 ]] && ! id -nG | grep -qw docker; then
    if [[ "${SETUP_DEV_SG:-}" == "1" ]]; then
      echo "This shell is still outside the docker group." >&2
      exit 1
    fi
    script=$(readlink -f "$0")
    exec sg docker -c "SETUP_DEV_SG=1 bash $(printf '%q' "$script")"
  fi
}

case "$(uname -s)" in
  Linux) install_linux ;;
  Darwin) install_macos ;;
  *)
    echo "Unsupported OS: $(uname -s). Use Linux, WSL, or macOS." >&2
    exit 1
    ;;
esac

export PATH="/usr/local/bin:${PATH}"

log "Starting Docker"
bash "$(dirname "$0")/start-dev.sh"

if docker info >/dev/null 2>&1; then
  log "Pulling nginx:alpine"
  docker pull nginx:alpine
fi

if [[ -f deps.edn ]]; then
  log "Downloading Clojure dependencies"
  clojure -P -M:test
fi

log "Ready: java $(java -version 2>&1 | head -1)"
log "Ready: $(clojure --version 2>&1)"
log "Ready: $(terraform version | head -1)"
log "Ready: $(docker --version)"
log "Next: ./scripts/demo.sh"
