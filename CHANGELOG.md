# Changelog

## 1.10.1
- Quick Menu pages now sit on the Quick Menu's own background. The native page underneath is hidden while a VividFrame page is open and comes back when it closes, instead of a dark panel drawn over it.
- Groups and section headers are cloned from the same native page as the page itself, so spacing and alignment match VRChat's pages. Rows keep their native height and button labels are centred.
- Pages scroll when their content is taller than the window, with a native scrollbar and clipping.
- Toggles look like VRChat's settings toggles (on and off icons) instead of buttons with On/Off text. Without a native toggle to copy, they use the Quick Menu's check and cross icons.
- Sliders: the value text updates live, the handle no longer jumps back while you drag it, and visual settings apply at most 20 times a second during a drag instead of every frame. Preferences still save once after the last change.
- Pages fade and slide in when opened and fade out when closed. All pages are built once; switching between them does not rebuild anything.
- Back goes from a section to the VividFrame page, and from there back to the native page. The VividFrame page closes right away when you pick a native tab, VRChat changes page, the Quick Menu closes, or another mod hides it.
- The VividFrame tab keeps working if the Quick Menu appears late, and native tabs are only hooked once per session.
- The "No PostProcessLayer found" warning is logged once per scene instead of every 30 seconds, with a note when a layer shows up later.

## 1.10.0
- New VividFrame tab in VRChat's Quick Menu, in desktop and VR. It opens a VividFrame page with a master toggle, a Glass menu button and buttons for eight sections: Visuals, Colour, Effects, Camera, Presets, Configs, Settings and Misc.
- The sections use the Quick Menu's own sliders, toggles and buttons. Every slider and toggle is bound to the same setting as the glass menu and the VR panel, so a change in one shows in the others right away.
- Options such as Tonemapper, AO mode, Focus and Bokeh size are buttons that step through their values and show the current one.
- Presets apply with one press. Configs saves and loads Profile 1 to 3 and lists your other saved profiles.
- The Quick Menu pages are built from stripped copies of native Quick Menu parts and are shown on top of the current page. VRChat's own page navigation is not changed, and choosing any native tab leaves the VividFrame page.
- If the Quick Menu layout is not recognised, VividFrame logs one warning and skips the tab. The glass menu and VR panel keep working.
- New Settings switch: Quick Menu tab (on by default).

## 1.9.0
- The VR panel is now the desktop glass menu. The same menu is drawn into a texture on one world-space quad, so every tab, control and setting from the desktop is there in VR, with the same animations.
- VR pointing: a laser from the right controller, the knob shows where you point, trigger clicks and drags sliders, double trigger resets a slider, the right stick scrolls smoothly, and dragging the title bar moves the panel.
- The VR panel opens in front of you slightly below eye level, faces you, and (with Keep panel in view) drifts back when you turn or walk away. New VR section in Settings: Panel size and Keep panel in view.
- VR overlays (FPS, stats, crosshair, toasts) use the desktop pills on a head-locked layer. Cinema bars stay desktop only.
- Fixed the VR panel and HUD showing as dark quads with tiny dots, also visible on the PC mirror: their TextMesh text had no font, so nothing but the pointer dot and glyph specks rendered.
- Fixed VR clicks firing twice: controller input was polled every 20 ms, so a trigger press stayed "down" for two frames. Input is now read once per frame with trigger hysteresis.
- Removed the VR panel's physics colliders and the rebuild of every object on each click or scroll.
- Fixed sliders and switches with the same label (several "Intensity" sliders) sharing drag and animation state. Control ids now come from the setting id.
- Fixed per-world profiles not loading when the Settings tab was open while joining a world.
- Quality override only writes values that changed. Re-setting MSAA and shadow settings every 3 seconds rebuilt render targets.
- Turning a post effect off no longer leaks its settings object; it is deactivated instead of removed.
- FOV and zoom crash-log entries are throttled so they no longer push useful entries out of the log.
- The menu key chip in the title bar now opens Settings instead of doing nothing.
- Fixed a possible error every frame on the menu key if the menu failed to start.
- Denser glass, brighter hint text and a 0.45 glass opacity floor keep text readable over bright worlds.
- Mod author shows as mcperr in the MelonLoader log.

## 1.8.3
- Fixed the VR settings panel showing only tiny dots: the panel and HUD were rotated 180 degrees (quads faced away and text was mirrored) and TextMesh text was about 10 times smaller than intended. The panel now faces the headset and the text is about 2 cm tall.
- The VR HUD sat behind the camera because of the same rotation; it is now in front of you.
- VR panel materials pick the first supported unlit shader and are drawn double-sided; the chosen shader is logged once.

## 1.8.2
- Added a headset-visible world-space settings panel with right-controller pointing, trigger clicks, thumbstick scrolling, and a B+Y menu toggle.
- Mirrored settings, HUD overlays, and post-processing through the stereo camera so they render to both eyes.
- Added an in-VR screenshot action and stereo post-processing diagnostics.
- FOV override and zoom now stay off in VR, where the headset owns lens projection.

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
