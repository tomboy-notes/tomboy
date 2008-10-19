#!/bin/sh

BUNDLE=Tomboy.app/

rm -rf $BUNDLE

CONTENTS=Contents/
MACOS=Contents/MacOS/
RESOURCES=/Contents/Resources/

mkdir -p $BUNDLE/$MACOS
mkdir -p $BUNDLE/$RESOURCES

cp osx/$CONTENTS/Info.plist $BUNDLE/$CONTENTS
cp osx/$MACOS/Tomboy $BUNDLE/$MACOS
cp osx/$RESOURCES/tomboy.icns $BUNDLE/$RESOURCES

cp bin/Debug/*.* $BUNDLE/$MACOS
cp -rf bin/Debug/tomboy/ $BUNDLE/$MACOS
