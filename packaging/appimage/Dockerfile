FROM debian:bookworm-slim
ARG APPIMAGETOOL_RELEASE=continuous
RUN apt-get update && apt-get install -y --no-install-recommends \
    ca-certificates curl file appstream desktop-file-utils squashfs-tools python3 \
    && rm -rf /var/lib/apt/lists/*
# The packaging tool runs on the container's native architecture. The runtime
# embedded in each output is selected separately, so no target emulation is needed.
RUN arch="$(uname -m)" && case "$arch" in x86_64|aarch64) ;; *) exit 1 ;; esac \
    && curl -fL --retry 3 "https://github.com/AppImage/appimagetool/releases/download/${APPIMAGETOOL_RELEASE}/appimagetool-${arch}.AppImage" \
        -o /usr/local/bin/appimagetool \
    && chmod +x /usr/local/bin/appimagetool
