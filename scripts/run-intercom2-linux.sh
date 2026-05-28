#!/usr/bin/env bash
# Start NDI Intercom2 (2-channel) on Linux with PipeWire/Pulse.
# Same prerequisites as run-intercom-linux.sh; default web UI port 5017.

set -euo pipefail

export PATH="/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin:${PATH:-}"

for _cmd in parec pacat pactl; do
  if ! command -v "${_cmd}" >/dev/null 2>&1; then
    echo "ERROR: '${_cmd}' not found (needed for audio)." >&2
    echo "  Install: sudo apt install pulseaudio-utils" >&2
    exit 1
  fi
done

UID_NUM="$(id -u)"
export XDG_RUNTIME_DIR="${XDG_RUNTIME_DIR:-/run/user/${UID_NUM}}"
export DBUS_SESSION_BUS_ADDRESS="${DBUS_SESSION_BUS_ADDRESS:-unix:path=${XDG_RUNTIME_DIR}/bus}"

if [[ ! -d "$XDG_RUNTIME_DIR" ]]; then
  echo "ERROR: XDG_RUNTIME_DIR ($XDG_RUNTIME_DIR) missing." >&2
  exit 1
fi

if [[ ! -S "$XDG_RUNTIME_DIR/bus" ]]; then
  echo "ERROR: User D-Bus socket missing at $XDG_RUNTIME_DIR/bus" >&2
  exit 1
fi

if command -v systemctl >/dev/null 2>&1; then
  systemctl --user start pipewire.socket pipewire.service pipewire-pulse.socket wireplumber.service 2>/dev/null || true
fi

PULSE_NATIVE="${XDG_RUNTIME_DIR}/pulse/native"
for _wait in $(seq 1 60); do
  if [[ -e "$PULSE_NATIVE" ]]; then
    export PULSE_SERVER="unix:${PULSE_NATIVE}"
    break
  fi
  sleep 0.25
done

if [[ ! -e "$PULSE_NATIVE" ]]; then
  echo "WARNING: $PULSE_NATIVE not found after starting user PipeWire units." >&2
fi

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
APP_DIR="${REPO_ROOT}/bin/NDIIntercom2/Release/net8.0"
DLL="${APP_DIR}/NDI Intercom2.dll"
APPHOST="${APP_DIR}/NDI Intercom2"

if [[ ! -f "$DLL" ]]; then
  echo "ERROR: Build not found at $DLL" >&2
  echo "  Run: cd \"$REPO_ROOT\" && dotnet build \"NDI Intercom2.csproj\" -c Release" >&2
  exit 1
fi

cd "$APP_DIR"

if [[ -z "${DOTNET_ROOT:-}" || ! -d "${DOTNET_ROOT}/host/fxr" ]]; then
  if [[ -d /usr/lib/dotnet/host/fxr ]]; then
    export DOTNET_ROOT="/usr/lib/dotnet"
  elif [[ -d "${HOME}/.dotnet/host/fxr" ]]; then
    export DOTNET_ROOT="${HOME}/.dotnet"
  fi
fi
if [[ -n "${DOTNET_ROOT:-}" ]]; then
  export PATH="${DOTNET_ROOT}:${PATH}"
fi

if command -v dotnet >/dev/null 2>&1 && dotnet --version >/dev/null 2>&1; then
  exec dotnet "NDI Intercom2.dll" "$@"
fi

if [[ ! -f "$APPHOST" ]]; then
  echo "ERROR: apphost missing at $APPHOST and 'dotnet' not usable." >&2
  exit 1
fi

exec "./NDI Intercom2" "$@"
