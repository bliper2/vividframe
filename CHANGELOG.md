# Changelog

## 1.8.1
- Keep replaced frosted-blur textures alive for 90 frames before destroying them, matching the menu's other cached textures.
- Reset the FOV while VividFrame's own menu is visible, even if cursor lock state does not change.

## 1.8.0
- Added a "Players in instance" overlay (Misc, Overlays).
- Added auto update from GitHub releases (checksum verified, installs when the game closes). Settings, Updates.
- Fixed world profiles and the player count not finding the game's data: Il2CppInterop exposes these as properties, not fields.
- Evicted menu textures are now destroyed a while after their last use instead of immediately, which avoids a possible crash from destroying a texture that is still queued for drawing.
- MIT licence, rewritten README, open-sourced.

## 1.7.3
- System info now reads GPU, CPU, RAM, display and driver from Windows instead of the game's reported values.

## 1.7.2
- Crash logging: a crash report is written on the next launch if the game did not exit cleanly.

## 1.7.1
- Hold Left Alt to zoom and scroll to adjust, with smooth easing and a remembered zoom level.
- FOV override eases back to normal while the cursor is free so menu buttons line up with the mouse.

## 1.7.0
- Misc tab split into Overlays, Camera, Quality and System.
- Stats overlays, cinema bars, zoom, clip planes, screenshot scale, shadow, texture, skin weight and other quality overrides, audio and loading options, fog and ambient light, memory tools.

## 1.6.0
- Misc tab: FPS counter, crosshair, screenshots, FOV override, quality overrides.
- Resize the menu by dragging any corner.

## 1.5.0
- Favourites tab, world profiles, hotkeys, double-click reset and right-click value entry, tooltips, toasts.
- Appearance settings (glass opacity, accent colour) and optional frosted blur.
- Settings now save when the menu closes instead of on every slider tick.
- New installs start on Natural+, plus a Reset colour button.

## 1.4.4
- Restyled the menu: exact-size anti-aliased glass textures, hover and press animation, sliding switches.
- New logo and credits.

## 1.4.3 and earlier
- Dropdown fixes, IMGUI performance work and the original two-column glass menu.
