#!/bin/sh
# StickMateOverlayPlugin.bundle 재빌드 스크립트. Xcode 프로젝트 없이 clang을 직접 호출해 .bundle을
# 만든다(Unity가 요구하는 것은 올바른 디렉터리 구조 + Info.plist뿐, Xcode 프로젝트가 아니다).
#
# 사용법: 이 스크립트가 있는 디렉터리(Assets/Plugins/macOS)에서 실행:
#   sh build.sh
#
# 유니버설 바이너리(arm64+x86_64)로 빌드해 Apple Silicon/Intel Mac 양쪽에서 동작하게 한다.
set -e
cd "$(dirname "$0")"

BUNDLE_DIR="StickMateOverlayPlugin.bundle"
MACOS_DIR="$BUNDLE_DIR/Contents/MacOS"
mkdir -p "$MACOS_DIR"

clang -dynamiclib -arch arm64 -arch x86_64 -mmacosx-version-min=11.0 -framework Cocoa \
  -o "$MACOS_DIR/StickMateOverlayPlugin" \
  StickMateOverlayPlugin.m

echo "빌드 완료: $MACOS_DIR/StickMateOverlayPlugin"
lipo -info "$MACOS_DIR/StickMateOverlayPlugin"
