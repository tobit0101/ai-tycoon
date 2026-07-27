#!/bin/bash
# Unity Git Setup Script
# Einmalig ausführen: ./setup-git.sh
# Richtet UnityYAMLMerge und Git LFS für dieses Projekt ein.

set -e

echo "=== Unity Git Setup ==="

# 1. Git LFS sicherstellen
if ! command -v git-lfs &> /dev/null; then
    echo "❌ git-lfs ist nicht installiert. Bitte installieren: https://git-lfs.com"
    exit 1
fi
git lfs install
echo "✅ Git LFS aktiviert"

# 2. UnityYAMLMerge finden
# Typische Pfade pro Betriebssystem
if [[ "$OSTYPE" == "darwin"* ]]; then
    # macOS: Suche in Unity Hub Installationen
    UNITY_MERGE=$(find /Applications/Unity/Hub/Editor -name "UnityYAMLMerge" -type f 2>/dev/null | head -1)
    if [ -z "$UNITY_MERGE" ]; then
        UNITY_MERGE="/Applications/Unity/Unity.app/Contents/Helpers/UnityYAMLMerge"
    fi
elif [[ "$OSTYPE" == "msys" || "$OSTYPE" == "cygwin" ]]; then
    # Windows
    UNITY_MERGE=$(find "C:/Program Files/Unity/Hub/Editor" -name "UnityYAMLMerge.exe" -type f 2>/dev/null | head -1)
    if [ -z "$UNITY_MERGE" ]; then
        UNITY_MERGE="C:/Program Files/Unity/Editor/Data/Tools/UnityYAMLMerge.exe"
    fi
else
    # Linux
    UNITY_MERGE=$(find ~/Unity/Hub/Editor -name "UnityYAMLMerge" -type f 2>/dev/null | head -1)
fi

if [ ! -f "$UNITY_MERGE" ]; then
    echo "⚠️  UnityYAMLMerge nicht gefunden. Bitte manuell konfigurieren."
    echo "   Pfad: <Unity-Installation>/Helpers/UnityYAMLMerge"
else
    echo "✅ UnityYAMLMerge gefunden: $UNITY_MERGE"
    git config --local merge.tool unityyamlmerge
    git config --local mergetool.unityyamlmerge.trustExitCode false
    git config --local "mergetool.unityyamlmerge.cmd" "$UNITY_MERGE merge -p \"\$BASE\" \"\$REMOTE\" \"\$LOCAL\" \"\$MERGED\""
    echo "✅ UnityYAMLMerge konfiguriert"
fi

# 3. Zeilenumbrüche (OS-spezifisch)
if [[ "$OSTYPE" == "darwin"* || "$OSTYPE" == "linux"* ]]; then
    git config --local core.autocrlf input
else
    git config --local core.autocrlf true
fi
echo "✅ Zeilenumbruch-Einstellungen gesetzt"

echo ""
echo "=== Fertig! ==="
echo "Aktuelle Git-Konfiguration:"
git config --list --local | grep -E "merge|autocrlf|lfs"