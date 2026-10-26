#!/usr/bin/env bash
# Extracts Unity's engine and editor assemblies for the project's Unity version into DevTools/UnityCompileCheck/.unity,
# so the Unity-side scripts can be compiled without installing the editor. Streams the Linux editor archive (about
# 4 GB) and keeps only the managed DLLs (about 70 MB). Does nothing if they are already there.
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
root="$(cd "$here/../.." && pwd)"
version_line="$(grep m_EditorVersionWithRevision "$root/ProjectSettings/ProjectVersion.txt")"
version="$(echo "$version_line" | sed -E 's/.*: ([^ ]+) \(([0-9a-f]+)\).*/\1/')"
revision="$(echo "$version_line" | sed -E 's/.*: ([^ ]+) \(([0-9a-f]+)\).*/\2/')"
target="$here/.unity"

if [ -f "$target/version" ] && [ "$(cat "$target/version")" = "$version" ]; then
    echo "Unity $version assemblies already in $target"
    exit 0
fi

rm -rf "$target"
mkdir -p "$target"
url="https://download.unity3d.com/download_unity/$revision/LinuxEditorInstaller/Unity-$version.tar.xz"
echo "Fetching Unity $version assemblies from $url"
curl -fsSL "$url" | xz -dc -T0 | tar -x -C "$target" --strip-components=2 --wildcards 'Editor/Data/Managed/UnityEngine/*.dll'
echo "$version" > "$target/version"
echo "Done: $(ls "$target/Managed/UnityEngine" | wc -l) assemblies"
