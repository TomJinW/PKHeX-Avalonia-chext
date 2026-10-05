#!/usr/bin/env bash
# Local release builds. Run from any directory; outputs stay in this repository.
set -euo pipefail
ROOT=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
cd "$ROOT"
usage() {
    cat <<'HELP'
Usage: ./build.sh [all | osx-arm64 win-x64 linux-x64 linux-arm64 ...]
Default: all four targets, Release, self-contained. Outputs: build/<RID>/
Requires .NET 10 SDK; macOS packaging requires macOS; AppImages require Docker.
DOTNET=/path/to/dotnet selects an SDK. DOCKER=/path/to/docker selects Docker.
APPIMAGETOOL_RELEASE and APPIMAGE_RUNTIME_RELEASE default to continuous.
HELP
}
fail() { echo "Error: $*" >&2; exit 1; }
if [[ ${1:-} == --help || ${1:-} == -h ]]; then usage; exit 0; fi
if [[ $# == 0 || ( $# == 1 && $1 == all ) ]]; then
    set -- osx-arm64 win-x64 linux-x64 linux-arm64
fi
need_linux=false
for target in "$@"; do
    case "$target" in
        osx-arm64) [[ $(uname -s) == Darwin ]] || fail 'osx-arm64 packaging requires macOS for signing.' ;;
        win-x64) ;;
        linux-x64|linux-arm64) need_linux=true ;;
        *) usage; fail "Unknown target: $target" ;;
    esac
done
DOTNET=${DOTNET:-dotnet}
DOCKER=${DOCKER:-docker}
command -v "$DOTNET" >/dev/null || fail 'Install .NET 10 SDK or set DOTNET.'
[[ $("$DOTNET" --version) == 10.* ]] || fail 'Select .NET 10 SDK with DOTNET=/path/to/dotnet.'
command -v python3 >/dev/null || fail 'python3 is required to create ZIP archives.'
if $need_linux; then
    command -v "$DOCKER" >/dev/null || fail 'Install/start Docker to package Linux AppImages (or select osx-arm64 win-x64 only).'
    "$DOCKER" info >/dev/null 2>&1 || fail 'Docker daemon is unavailable. Start Docker and retry.'
fi
VERSION=$(sed -n 's/.*<UIVersion>\([^<]*\)<\/UIVersion>.*/\1/p' Directory.Build.props)
REVISION=$(git rev-parse HEAD)
RELEASE_DATE=$(date +%F)
mkdir -p build
# Prevent concurrent publishes sharing project obj files or output directories.
mkdir build/.build-lock 2>/dev/null || fail 'Another build is running (remove build/.build-lock only if a previous build was interrupted).'
trap 'rmdir "$ROOT/build/.build-lock"' EXIT
if $need_linux; then
    "$DOCKER" build -t pkhex-local-appimage -f packaging/appimage/Dockerfile \
        --build-arg "APPIMAGETOOL_RELEASE=${APPIMAGETOOL_RELEASE:-continuous}" packaging/appimage
fi
for target in "$@"; do
    echo "Building $target ..."
    # Fresh staging prevents files from an older publish entering the new package.
    stage=$(mktemp -d "$ROOT/build/.${target}.XXXXXX")
    "$DOTNET" publish PKHeX.Avalonia/PKHeX.Avalonia.csproj -c Release -r "$target" \
        --self-contained true -p:PublishSingleFile=true -o "$stage/publish"
    case "$target" in
        osx-arm64)
            app="$stage/publish/PKHeX.Avalonia.app"
            codesign --force --deep --sign - "$app"
            codesign --verify --deep --strict "$app"
            ditto -c -k --keepParent "$app" "$stage/PKHeX-Avalonia-osx-arm64.zip"
            ;;
        win-x64)
            python3 - "$stage" <<'PY'
import pathlib, sys, zipfile
stage = pathlib.Path(sys.argv[1])
with zipfile.ZipFile(stage / 'PKHeX-Avalonia-win-x64.zip', 'w', zipfile.ZIP_DEFLATED) as archive:
    for path in sorted((stage / 'publish').rglob('*')):
        if path.is_file():
            archive.write(path, path.relative_to(stage / 'publish'))
PY
            ;;
        linux-*)
            arch=x86_64; [[ $target != linux-arm64 ]] || arch=aarch64
            "$DOCKER" run --rm --user "$(id -u):$(id -g)" \
                -v "$ROOT:/repo:ro" -v "$stage:/output" -w /repo \
                -e "APPIMAGE_RUNTIME_RELEASE=${APPIMAGE_RUNTIME_RELEASE:-continuous}" \
                pkhex-local-appimage bash packaging/appimage/package.sh "$arch" "$VERSION" "$REVISION" "$RELEASE_DATE"
            ;;
    esac
    # Only replace this target's previous results after the entire package succeeded.
    destination="$ROOT/build/$target"
    if [[ -e $destination ]]; then mv "$destination" "$stage/previous"; fi
    mv "$stage" "$destination"
    rm -rf -- "$destination/previous"
    echo "Completed: $destination"
done
