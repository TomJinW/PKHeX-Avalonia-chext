#!/usr/bin/env bash
set -euo pipefail
arch=${1:?Missing architecture}
case "$arch" in x86_64|aarch64) ;; *) echo 'Unsupported architecture' >&2; exit 1 ;; esac
chmod +x /output/publish/PKHeX.Avalonia
bash .github/scripts/prepare-appdir.sh /output/publish /output/AppDir "$2" "$3" "$4"
curl -fL --retry 3 \
    "https://github.com/AppImage/type2-runtime/releases/download/${APPIMAGE_RUNTIME_RELEASE:-continuous}/runtime-${arch}" \
    -o /output/runtime
artifact="/output/PKHeX-Avalonia-linux-${arch}.AppImage"
ARCH="$arch" appimagetool --appimage-extract-and-run --runtime-file /output/runtime /output/AppDir "$artifact"
chmod +x "$artifact"
# Verify both runtime and payload architecture without executing foreign binaries.
python3 - "$arch" "$artifact" /output/publish/PKHeX.Avalonia <<'PY'
import pathlib, struct, sys
expected = {'x86_64': 62, 'aarch64': 183}[sys.argv[1]]
for name in sys.argv[2:]:
    header = pathlib.Path(name).open('rb').read(20)
    if header[:4] != b'\x7fELF' or header[4:6] != b'\x02\x01' or struct.unpack_from('<H', header, 18)[0] != expected:
        raise SystemExit(f'Wrong ELF architecture: {name}')
PY
rm /output/runtime
