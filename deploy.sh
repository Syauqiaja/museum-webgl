#!/usr/bin/env bash
# Deploy the Unity WebGL client to museumethnofun.com.
#
#   ./deploy.sh            upload Builds/WebGL as it stands
#   ./deploy.sh --build    headless production build first (Unity must be closed)
#
# The game server deploys separately, from its own repo: deploy/deploy.sh.
#
# Upload rules — each one is a trap that has already been sprung, see
# Assets/Docs/build-and-deploy.md:
#   - chmod a+rX after EVERY build (Unity writes the .br files 600 -> nginx 403s them)
#   - never --delete (a dropped transfer leaves index.html pointing at 404s)
#   - Build/ goes up before index.html, and --delay-updates swaps every file into place
#     only after the whole transfer has arrived, so a live file is never half-written
#   - verify by hashing what nginx serves with Accept-Encoding: br, never --compressed
#
# Env overrides: DEPLOY_USER (default root), DEPLOY_HOST (default 212.85.25.177),
# UNITY (editor binary; default is the Unity Hub install of the project's version on macOS).
set -euo pipefail

DEPLOY_USER=${DEPLOY_USER:-root}
DEPLOY_HOST=${DEPLOY_HOST:-212.85.25.177}
TARGET="$DEPLOY_USER@$DEPLOY_HOST"
SITE_DIR=/docker/museum/site
SITE_URL=https://museumethnofun.com

cd "$(dirname "$0")"
PROJECT=$(pwd)
BUILD_DIR=Builds/WebGL
UNITY=${UNITY:-/Applications/Unity/Hub/Editor/$(sed -n 's/^m_EditorVersion: //p' ProjectSettings/ProjectVersion.txt)/Unity.app/Contents/MacOS/Unity}

# One shared connection for every ssh/rsync below: authenticate (key passphrase or
# password) once, not once per command.
CONTROL_DIR=$(mktemp -d /tmp/museum-deploy.XXXXXX)   # short: unix socket paths cap at 104 bytes
SSH_OPTS=(-o ConnectTimeout=10 -o ControlMaster=auto -o "ControlPath=$CONTROL_DIR/%C" -o ControlPersist=5m)
cleanup() { ssh "${SSH_OPTS[@]}" -O exit "$TARGET" 2>/dev/null || true; rm -rf "$CONTROL_DIR"; }
trap cleanup EXIT
remote() { ssh "${SSH_OPTS[@]}" "$TARGET" "$@"; }
upload() { rsync -e "ssh ${SSH_OPTS[*]}" "$@"; }

say() { printf '\033[1m==> %s\033[0m\n' "$*"; }
die() { printf '\033[31mERROR: %s\033[0m\n' "$*" >&2; exit 1; }

build=0
for arg in "$@"; do
  case "$arg" in
    --build) build=1 ;;
    *) sed -n '2,7p' "$0" | sed 's/^# \{0,1\}//'; exit 1 ;;
  esac
done

if [ "$build" -eq 1 ]; then
  # Batch mode cannot open a project the editor already has open.
  if pgrep -f "Unity -projectpath $PROJECT( |$)" >/dev/null; then
    die "Unity has this project open — close it, or build via Museum/Build/WebGL (Production) and rerun without --build"
  fi
  [ -x "$UNITY" ] || die "Unity not found at $UNITY"
  say "Building WebGL (Production) — takes several minutes"
  mkdir -p Logs
  "$UNITY" -quit -batchmode -projectPath "$PROJECT" \
    -executeMethod Museum.Build.Editor.BuildWebGL.Production \
    -logFile Logs/deploy-build.log \
    || { grep -E '\[BuildWebGL\]|error' Logs/deploy-build.log | tail -20; die "build failed — full log in Logs/deploy-build.log"; }
  grep '\[BuildWebGL\]' Logs/deploy-build.log | tail -5 || true
fi

files=(WebGL.data.br WebGL.wasm.br WebGL.framework.js.br WebGL.loader.js)
for f in "${files[@]}"; do
  [ -f "$BUILD_DIR/Build/$f" ] || die "$BUILD_DIR/Build/$f missing — build with Museum/Build/WebGL (Production)"
done
if grep -q 'BUILDSTAMP' "$BUILD_DIR/index.html"; then
  die "index.html still has the BUILDSTAMP placeholder — not a BuildWebGL build"
fi
say "Build from $(stat -f '%Sm' "$BUILD_DIR/Build/WebGL.data.br"), $(du -sh "$BUILD_DIR" | cut -f1)"

say "Connecting to $TARGET"
remote true || die "cannot ssh to $TARGET"
chmod -R a+rX "$BUILD_DIR"

say "Uploading Build/ to $TARGET:$SITE_DIR"
upload -rtz --progress --delay-updates --partial-dir=.rsync-partial --exclude='.DS_Store' \
  "$BUILD_DIR/Build/" "$TARGET:$SITE_DIR/Build/"
say "Uploading index.html and style.css"
upload -rtz --delay-updates --exclude='.DS_Store' --exclude='Build/' \
  "$BUILD_DIR/" "$TARGET:$SITE_DIR/"
remote "chmod -R a+rX $SITE_DIR"

say "Verifying what $SITE_URL serves"
bad=0
for f in "${files[@]}"; do
  l=$(shasum -a256 "$BUILD_DIR/Build/$f" | awk '{print $1}')
  r=$(curl -fsS -H 'Accept-Encoding: br' "$SITE_URL/Build/$f" -o - | shasum -a256 | awk '{print $1}')
  if [ "$l" = "$r" ]; then echo "  $f MATCH"; else echo "  $f MISMATCH"; bad=1; fi
done
if curl -fsSI "$SITE_URL/Build/WebGL.wasm.br" | grep -qi '^content-encoding: br'; then
  echo "  Content-Encoding: br OK"
else
  echo "  Content-Encoding: br MISSING"; bad=1
fi
[ "$bad" -eq 0 ] || die "verification failed"
say "Client live at $SITE_URL"
