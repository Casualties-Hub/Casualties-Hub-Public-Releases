#!/bin/sh
# Optional: adds Casualties Hub to your application menu.
# Nothing else depends on this - you can always just run ./casualties-hub directly.
#
# Installs per-user only (~/.local/share), so it never needs root and never
# touches anything outside your home directory.
set -eu

DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
BIN="$DIR/casualties-hub"

if [ ! -f "$BIN" ]; then
    echo "error: casualties-hub not found next to this script." >&2
    exit 1
fi

chmod +x "$BIN"

APPS="${XDG_DATA_HOME:-$HOME/.local/share}/applications"
ICONS="${XDG_DATA_HOME:-$HOME/.local/share}/icons/hicolor/256x256/apps"
mkdir -p "$APPS" "$ICONS"

if [ -f "$DIR/casualties-hub.png" ]; then
    cp "$DIR/casualties-hub.png" "$ICONS/casualties-hub.png"
fi

# Exec must be absolute: the launcher runs from an arbitrary working directory.
# The path is double-quoted for the Desktop Entry spec, which also requires
# backslash, double quote, backtick and dollar to be escaped inside the quotes
# and any percent sign doubled, so a folder like "Casualties Hub" still launches.
EXEC_PATH=$(printf '%s' "$BIN" | sed 's/[\\"`$]/\\&/g; s/%/%%/g')
{
    grep -v '^Exec=' "$DIR/casualties-hub.desktop"
    printf 'Exec="%s"\n' "$EXEC_PATH"
} > "$APPS/casualties-hub.desktop"
chmod 644 "$APPS/casualties-hub.desktop"

if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database "$APPS" 2>/dev/null || true
fi

echo "Installed. 'Casualties Hub' should appear in your application menu."
echo "To remove it:  rm '$APPS/casualties-hub.desktop'"
