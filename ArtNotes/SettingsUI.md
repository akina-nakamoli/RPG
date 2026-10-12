# Settings UI

## Install

Return to Unity and let scripts finish compiling. An empty `SettingCanvas/SettingPanel`
in SampleScene is populated automatically. Alternatively, use
`Tools > RPG > Install Settings UI`, then save the scene.
Existing custom panel content is preserved rather than replaced.

The editable prefab is `Assets/Prefabs/UI/SettingCanvas.prefab`.
Keep SettingCanvas active. SettingPanel starts closed in Play mode; the gear opens it.
The panel is visible in edit mode so its layout can be customized.

The scene's customized layout may omit CloseButton: the reference is optional,
and Resume / Esc still close the window. Moving controls under new parents does
not require reinstalling the prefab; keep their existing SettingsUI references.

## Included

- English pixel-art settings window using the project's parchment and blue ribbons.
- Master, music and sound-effects sliders with percentages and saved preferences.
- Fullscreen toggle (effective in a standalone build, not the Unity Game view).
- Controls reference, restore-settings confirmation and quit confirmation.
- Resume / X / Esc to close, with gameplay paused while the window is open.
- Restoring settings does not erase game progress.

## Audio integration

Master volume controls AudioListener directly. Music and SFX settings are saved,
but independent volume control requires an AudioMixer assigned to SettingsUI.
Expose parameters `MusicVolume` and `SFXVolume`, and route music and effects
AudioSources to their respective mixer groups. No audio assets are created here.

## Verification

Runtime and installer scripts were compiled against the project's Unity references;
prefab internal references were checked for missing or duplicate IDs.
In-editor rendering and Play mode interaction still require Unity to import the files.
After installation, test opening/closing, slider persistence, both confirmation
dialogs and returning to normal movement. Export a rendering with
`Tools > RPG > Export Settings UI Preview` if desired.
