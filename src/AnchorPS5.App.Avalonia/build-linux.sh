#!/usr/bin/env bash
# Builds the self-contained Linux package of AnchorPS5 (Avalonia UI):
#   dist/AnchorPS5-<version>-linux-x64/      ready-to-run folder
#   dist/AnchorPS5-<version>-linux-x64.zip   + .sha256
# Usage: src/AnchorPS5.App.Avalonia/build-linux.sh   (from anywhere)
set -euo pipefail

project_dir="$(cd "$(dirname "$0")" && pwd)"
repo_root="$(cd "$project_dir/../.." && pwd)"
dist="$repo_root/dist"
sevenzip="$project_dir/tools/7zip/7zz"

version="$(dotnet msbuild "$project_dir/AnchorPS5.App.Avalonia.csproj" -getProperty:Version)"
name="AnchorPS5-$version-linux-x64"
publish="$project_dir/bin/publish/linux-x64"
stage="$dist/$name"

echo "==> Publishing $name"
rm -rf "$publish"
dotnet publish "$project_dir/AnchorPS5.App.Avalonia.csproj" -c Release -r linux-x64 --self-contained true -o "$publish"

echo "==> Staging $stage"
rm -rf "$stage" "$dist/$name.zip" "$dist/$name.zip.sha256"
mkdir -p "$stage"
cp -a "$publish/." "$stage/"
cp "$project_dir/anchorps5" "$project_dir/AnchorPS5.desktop" "$stage/"
cp "$project_dir/Assets/AnchorPS5.png" "$stage/anchorps5.png"
chmod +x "$stage/anchorps5" "$stage/AnchorPS5" "$stage/tools/7zip/7zz"
find "$stage" -name '*.pdb' -delete
# The user's config is created on first launch; never ship one.
rm -rf "$stage/config/logs" "$stage/config/cache" "$stage/config/config.json" "$stage/config/state.json"

echo "==> Zipping"
(cd "$dist" && "$sevenzip" a -tzip -mx=9 "$name.zip" "$name" >/dev/null && sha256sum "$name.zip" > "$name.zip.sha256")
cat "$dist/$name.zip.sha256"
echo "==> Done: $stage"
