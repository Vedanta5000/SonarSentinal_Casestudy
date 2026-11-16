# Sonar Sentinel mobile HUD

## Applying the landscape reference layout

Open the Unity project and choose **Sonar Sentinel > Apply Reference Mobile Style**. The command updates `GreyboxRoom.unity` in place, keeps existing gameplay GameObjects and colliders, and saves a landscape HUD layout. It reuses the generated `GameHUD`, minimap camera, Render Texture and gameplay marker objects. Generated circle sprites and palette materials are stored in `Assets/UI/HUD`.

The resulting layout is authored for a 1920 x 1080 landscape canvas and scales through the Canvas Scaler. It uses a Screen Space - Overlay canvas, safe-area handling, rectangular segmented alert meter at upper left, circular relative-noise gauge at upper right, objective chip above the lower-left touch controls, circular extraction minimap at lower right, and round touch controls at lower left. The `THROW` control accepts a drag gesture to aim and throw. While aiming, a world-space preview traces the orb's ballistic path and stops at the first collision; after release, a short cyan trail follows the actual orb. Tapping `OBJ` toggles the objective panel. Player movement remains tap-to-move on the walkable floor.

The scene camera uses a slightly closer orthographic isometric view aimed toward the room center from the starting position, then follows the player. The palette gives the existing room a pale tiled floor, cool light-grey walls, and a translucent amber vision cone. The builder does not add walls, doors, a Data Core, extraction geometry or new gameplay rules; it preserves the current room topology, colliders and NavMesh. The extraction marker stays hidden until Part 1 supplies its actual Transform.

## HUDController interface for Part 1

`HUDController` is presentation code and can be referenced by the mission manager:

- `SetDetectionProgress(float)` accepts a normalized 0–1 value. Existing `DroneVision.DetectionProgress` is read; its detection algorithm is not duplicated.
- `SetNoiseLevel(float)` accepts a normalized 0–1 relative sound-reach intensity. It is not a physical decibel reading. Existing `SoundSystem.OnSoundEmitted` supplies the event radius, which is normalized against the `maximumSoundRadius` inspector value (12 in this scene).
- `SetObjectiveStatus(bool)` sets whether the Data Core is secured.
- `SetExtractionPoint(Transform)` assigns the real extraction target and enables its minimap marker.
- `ShowMissionComplete()` and `ShowCaughtScreen()` display the respective mission overlay.
- `RestartMission()` is the restart-button callback placeholder; it reloads the active scene and therefore requires that the scene be included in Build Settings for a player build.
- `SetDroneVision(DroneVision)` allows the mission setup to replace the initially assigned vision component.

The HUD subscribes to `SoundSystem.OnSoundEmitted`. The event radius is normalized against `maximumSoundRadius` (12, matching the current DistractionOrb). On orb impact the gauge fills, shows a relative percentage, pulses, holds briefly, then fades. The current orb is the sound source wired to this event; other sound sources must emit through the existing event to appear. This is game-space reach feedback, not a physical decibel reading. `SoundRipple` renders three expanding, fading rings on impact.

Example mission-side calls:

```csharp
hud.SetObjectiveStatus(dataCoreCollected);
hud.SetExtractionPoint(extractionTransform);
if (missionState == MissionState.Complete) hud.ShowMissionComplete();
```

The HUD does not infer mission completion from the objective bool. The GameManager should call these methods at the correct mission transitions. No Part 1 GameManager, Data Core or extraction Transform exists in the current scene, so those references are intentionally left unassigned.

## Inspector references

The builder assigns the HUD panel text and fill images, mission screen roots, `DroneVision` on `SecurityDrone`, player and drone transforms, map markers, minimap camera and Render Texture. It assigns `ThrowButton` to `ThrowController.mobileThrowButton`. The main camera excludes marker layer 8; `MinimapCamera` renders that layer to `MinimapRT.renderTexture`. The scene EventSystem uses the Input System UI module.

The earlier **Sonar Sentinel > Build Greybox HUD** menu item remains for creating the baseline HUD hierarchy in a scene without one. Use **Apply Reference Mobile Style** after that baseline builder, or when updating the existing generated HUD.

## Title screen

`MainMenuPanel` is generated on the existing overlay canvas and shows the SONAR SENTINEL title with a centered PLAY button. Choose **Sonar Sentinel > Build Title Screen** to add or refresh it. The existing HUD builder and reference-style builder also add it when they refresh the canvas. At runtime, `MainMenuController` pauses gameplay until PLAY is tapped, then hides the overlay and resumes the mission. Restarting the active scene returns to the title screen.
