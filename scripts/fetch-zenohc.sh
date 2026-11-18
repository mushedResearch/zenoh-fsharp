#!/usr/bin/env bash
# Download a prebuilt zenoh-c release into native/<rid>/ (library) and native/include/ (headers).
#
# Usage: scripts/fetch-zenohc.sh [version] [rid]
#   version  zenoh-c release tag (default: the version in ZenohCVersion of Directory.Build.props)
#   rid      .NET runtime identifier: linux-x64 (default), linux-arm64, osx-x64, osx-arm64, win-x64,
#            or all (every one of them, as the NuGet package needs)
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
version="${1:-$(sed -n 's:.*<ZenohCVersion>\(.*\)</ZenohCVersion>.*:\1:p' "$root/Directory.Build.props")}"
rid="${2:-linux-x64}"

if [ "$rid" = all ]; then
  for r in linux-x64 linux-arm64 osx-x64 osx-arm64 win-x64; do
    bash "$0" "$version" "$r"
  done
  exit 0
fi

case "$rid" in
  linux-x64)   triple=x86_64-unknown-linux-gnu;  lib=libzenohc.so ;;
  linux-arm64) triple=aarch64-unknown-linux-gnu; lib=libzenohc.so ;;
  osx-x64)     triple=x86_64-apple-darwin;       lib=libzenohc.dylib ;;
  osx-arm64)   triple=aarch64-apple-darwin;      lib=libzenohc.dylib ;;
  win-x64)     triple=x86_64-pc-windows-msvc;    lib=zenohc.dll ;;
  *) echo "unsupported rid: $rid" >&2; exit 1 ;;
esac

url="https://github.com/eclipse-zenoh/zenoh-c/releases/download/${version}/zenoh-c-${version}-${triple}-standalone.zip"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

echo "Downloading $url"
curl -fsSL -o "$work/zenohc.zip" "$url"
unzip -q "$work/zenohc.zip" -d "$work/x"

mkdir -p "$root/native/$rid" "$root/native/include"
found="$(find "$work/x" -name "$lib" -print -quit)"
[ -n "$found" ] || { echo "$lib not found in archive" >&2; exit 1; }
cp "$found" "$root/native/$rid/"
cp "$work"/x/include/*.h "$root/native/include/"
echo "$version" > "$root/native/VERSION"
echo "Installed zenoh-c $version: native/$rid/$lib, headers in native/include/"
