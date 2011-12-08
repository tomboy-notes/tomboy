#!/bin/sh

mkdir -p bin/Debug/tomboy/icons/hicolor/16x16/apps
mkdir -p bin/Debug/tomboy/icons/hicolor/22x22/apps
mkdir -p bin/Debug/tomboy/icons/hicolor/24x24/apps
mkdir -p bin/Debug/tomboy/icons/hicolor/32x32/apps
mkdir -p bin/Debug/tomboy/icons/hicolor/48x48/apps
mkdir -p bin/Debug/tomboy/icons/hicolor/256x256/apps
mkdir -p bin/Debug/tomboy/icons/hicolor/scalable/apps

mkdir -p bin/Debug/tomboy/icons/hicolor/16x16/actions
mkdir -p bin/Debug/tomboy/icons/hicolor/22x22/actions
mkdir -p bin/Debug/tomboy/icons/hicolor/24x24/actions
mkdir -p bin/Debug/tomboy/icons/hicolor/32x32/actions
mkdir -p bin/Debug/tomboy/icons/hicolor/48x48/actions
mkdir -p bin/Debug/tomboy/icons/hicolor/scalable/actions

mkdir -p bin/Debug/tomboy/icons/hicolor/16x16/places
mkdir -p bin/Debug/tomboy/icons/hicolor/22x22/places
mkdir -p bin/Debug/tomboy/icons/hicolor/24x24/places
mkdir -p bin/Debug/tomboy/icons/hicolor/32x32/places
mkdir -p bin/Debug/tomboy/icons/hicolor/48x48/places
mkdir -p bin/Debug/tomboy/icons/hicolor/scalable/places

mkdir -p bin/Debug/tomboy/icons/hicolor/16x16/status
mkdir -p bin/Debug/tomboy/icons/hicolor/22x22/status
mkdir -p bin/Debug/tomboy/icons/hicolor/24x24/status
mkdir -p bin/Debug/tomboy/icons/hicolor/32x32/status
mkdir -p bin/Debug/tomboy/icons/hicolor/48x48/status
mkdir -p bin/Debug/tomboy/icons/hicolor/scalable/status

cp data/icons/hicolor_actions_16x16_note-new.png bin/Debug/tomboy/icons/hicolor/16x16/actions/note-new.png
cp data/icons/hicolor_actions_48x48_notebook-new.png bin/Debug/tomboy/icons/hicolor/48x48/actions/notebook-new.png
cp data/icons/hicolor_actions_22x22_filter-note-all.png bin/Debug/tomboy/icons/hicolor/22x22/actions/filter-note-all.png
cp data/icons/hicolor_actions_22x22_filter-note-unfiled.png bin/Debug/tomboy/icons/hicolor/22x22/actions/filter-note-unfiled.png
cp data/icons/hicolor_apps_16x16_tomboy.png bin/Debug/tomboy/icons/hicolor/16x16/apps/tomboy.png
cp data/icons/hicolor_apps_22x22_tomboy.png bin/Debug/tomboy/icons/hicolor/22x22/apps/tomboy.png
cp data/icons/hicolor_apps_24x24_tomboy.png bin/Debug/tomboy/icons/hicolor/24x24/apps/tomboy.png
cp data/icons/hicolor_apps_32x32_tomboy.png bin/Debug/tomboy/icons/hicolor/32x32/apps/tomboy.png
cp data/icons/hicolor_apps_48x48_tomboy.png bin/Debug/tomboy/icons/hicolor/48x48/apps/tomboy.png
cp data/icons/hicolor_apps_256x256_tomboy.png bin/Debug/tomboy/icons/hicolor/256x256/apps/tomboy.png
cp data/icons/hicolor_apps_scalable_tomboy.svg bin/Debug/tomboy/icons/hicolor/scalable/apps/tomboy.svg
cp data/icons/hicolor_places_22x22_note.png bin/Debug/tomboy/icons/hicolor/22x22/places/note.png
cp data/icons/hicolor_places_22x22_notebook.png bin/Debug/tomboy/icons/hicolor/22x22/places/notebook.png
cp data/icons/hicolor_status_16x16_pin-down.png bin/Debug/tomboy/icons/hicolor/16x16/status/pin-down.png
cp data/icons/hicolor_status_16x16_pin-up.png bin/Debug/tomboy/icons/hicolor/16x16/status/pin-up.png
cp data/icons/hicolor_status_16x16_pin-active.png bin/Debug/tomboy/icons/hicolor/16x16/status/pin-active.png