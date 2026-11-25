# VividFrame

A graphics and quality-of-life mod for VRChat, loaded by [MelonLoader](https://melonwiki.xyz). It adds a glass in-game menu (default key **Home**) with colour grading, post-processing, camera, quality and overlay tools.

Made by **mcperr**. MIT licensed and fully open source.

> Client-side mods can break VRChat's terms of service and can get an account banned. VividFrame only talks to Unity's post-processing stack, camera and quality settings. It does not touch Easy Anti-Cheat or the MelonLoader install. Use it at your own risk.

## Features

**Visuals**
- Colour grading: saturation, vibrance, contrast, exposure, gamma, temperature, tint, hue shift, sharpen and tonemapper.
- Bloom, motion blur, vignette, film grain, chromatic aberration, ambient occlusion, depth of field (auto-focus on avatar faces) and lens distortion.
- Presets (Vivid, Natural+, Cinematic, Night, Performance, Off), named profiles stored as JSON, and a master switch that restores the world's own look.
- Render scale, and a Reset colour button.

**Misc tab**
- **Overlays:** FPS counter, players in the instance, clock, session timer, camera position, memory use, crosshair and cinema bars.
- **Camera:** free camera (Shift+B; WASD, mouse look, Space/Ctrl, Shift speed), FOV override, hold-to-zoom with scroll wheel (default Left Alt), near and far clip, clean screenshots (1x to 4x).
- **Quality:** FPS cap, VSync, LOD bias, shadows, texture quality, MSAA, pixel lights, skin weights, soft particles and reflections.
- **System:** run in background, mute when unfocused, master volume, loading priority, fog and ambient light, free memory, and real hardware info read from Windows.

**Menu**
- Resizable by dragging any corner, accent colours, glass opacity, optional frosted blur, tooltips, favourites tab (Shift-click to pin), double-click a slider to reset, right-click to type a value.
- Hotkeys for hold-to-compare, master toggle, next profile and screenshots.
- World profiles: pin a saved profile to a world and it loads when you join.

**Safety**
- Every override restores the game's own value when switched off.
- Crash logging: if the game dies, the next launch writes a report of what VividFrame was doing to `UserData\VividFrame\crash`.
- Auto update (below).

## Install

1. Install MelonLoader (0.7.x, net6, IL2CPP) for VRChat.
2. Download `VividFrame.dll` from the [latest release](https://github.com/bliper2/vividframe/releases/latest).
3. Put it in `VRChat\Mods` and start the game. Press **Home**.

### VR controls

When VRChat is rendering in stereo, the same glass menu as on desktop opens on a panel in front of you, with every tab and setting. Press both controllers' secondary buttons together (usually **B + Y**) to open or close it. Point the **right controller** at a control and pull the trigger to click or drag; pull twice on a slider to reset it. Push the right thumbstick up or down to scroll, and drag the title bar to move the panel. Settings, VR has the panel size and **Keep panel in view**, which brings the panel back in front of you when you turn away. FPS, stats, crosshair and messages show on a head-locked layer. The shared post-processing profile is applied through VRChat's stereo camera layers, so both eyes receive the same settings. FOV override and zoom do not change headset lens projection.

## Auto update

On startup (and every six hours) VividFrame asks GitHub for the latest release. If it is newer and **Auto update** is on (Settings tab), it downloads the DLL, verifies it, and installs it when the game closes.

What it checks and does:
- It only downloads from this repository's release assets over HTTPS.
- The file must match the SHA-256 published with the release, and must be a `VividFrame` assembly of the expected version. Otherwise it is discarded.
- A running DLL cannot be overwritten, so the swap is done by renaming it, or by a small hidden PowerShell helper that waits for the game to exit and then moves the new file into place.
- The only request made is a plain HTTPS GET with the user agent `VividFrame-Updater`. Nothing about you or your machine is sent.

Turn it off in Settings, or by setting `AutoUpdate = false` in `UserData\MelonPreferences.cfg`. You can also check manually from the same tab.

## Build

You need the .NET 6 SDK (or newer) and a VRChat install with MelonLoader, because the project references the game's generated Il2Cpp assemblies. These are not redistributable and are not in this repository.

```bash
dotnet build -c Release
```

The DLL is written to `bin\Release\VividFrame.dll`. If VRChat is not in the default Steam folder, pass its location:

```bash
dotnet build -c Release -p:VRChatPath="D:\Games\VRChat"
```

Releases are built locally and uploaded with a `VividFrame.dll.sha256` file next to the DLL, which the auto updater uses.

## Rules for contributors

The game is IL2CPP, and several managed APIs are stripped from it. Please keep to these:

1. Never use `GUILayout`. Its overloads are stripped and calling them crashes the game. Use `GUI.*` with explicit `Rect`s.
2. Use only `GUI.*` overloads that exist in the game's `IMGUIModule`. Check new APIs against `MelonLoader\Il2CppAssemblies` first.
3. Do not call `Mathf.Min` or `Mathf.Max` with three or more arguments (the `params` overloads are stripped). Use the two-argument form.
4. Draw chrome with `GUI.DrawTexture` from cached textures. Do not rely on `GUI.skin` visuals.
5. Build textures and styles once, never retry every frame, and never allocate per frame in `OnGUI`.
6. Reach game-internal (obfuscated) types by reflection inside a try/catch that logs once, so a game update disables a feature instead of breaking the mod.
7. Never touch Easy Anti-Cheat, the loader files or other mods.

Pull requests are welcome. In-game behaviour can only be checked in VRChat, so please say how you tested.

## Settings

Everything is stored in MelonPreferences (`UserData\MelonPreferences.cfg`, category `VividFrame`). Profiles are JSON files in `UserData\VividFrame\profiles`. Screenshots go to `UserData\VividFrame\screenshots`, world pins to `UserData\VividFrame\worlds.txt`.

### Vibrance and sharpen

Post Processing Stack v2 has neither effect and a custom shader cannot be compiled inside the player. Vibrance is approximated through colour grading (mostly saturation with a small contrast lift), and sharpen is a midtone-contrast stand-in. Neither is a true per-pixel effect.

## Licence

[MIT](LICENSE) (c) 2026 mcperr.
