# Fif-Man VR

A native Unity VR maze game for standalone headsets (Meta Quest and any OpenXR device). Run through a
glowing neon maze at human scale, **grab pellets with your hands**, and avoid the four ghosts. Eat a
power pellet to turn the ghosts blue and chase them down.

Everything — maze, ghosts, HUD, sound effects — is generated in code at runtime, so there are no art
or audio assets to import.

## Controls

| Input | Action |
| --- | --- |
| Left thumbstick | Move (relative to where you look) |
| Right thumbstick | Snap turn 45° |
| Hands / head | Touch pellets to collect them |
| Any trigger | Next level / restart after the end screen |

Editor testing without a headset: `WASD` move, `Q`/`E` turn, `Space` confirm.

## Rules

- Pellet 10 pts, power pellet 50 pts, ghosts 200/400/800/1600 pts in a combo.
- 3 lives; touching a ghost costs one and resets positions (3 s of grace).
- Each level the ghosts get a little faster.

## Setup

1. Open the folder with **Unity 6 (6000.0 LTS)** with the *Android Build Support* module (for Quest).
   Accept the prompt to enable the new Input System backend.
2. The `Fif-Man > Setup Project` menu runs automatically on first open: it sets IL2CPP/ARM64/Vulkan for
   Android, creates `Assets/Scenes/Main.unity` and adds it to the build.
3. **Project Settings > XR Plug-in Management**: tick **OpenXR** on the Android tab (and PC tab for
   Link/desktop testing), then under OpenXR add the **Oculus Touch Controller Profile**.
4. Press Play with a headset connected, or use **Fif-Man > Build Android (Quest) APK** and sideload
   `Builds/FifMan.apk`.

## Layout

- `Assets/Scripts/Runtime/` — `GameBootstrap` (entry point), `GameManager` (rules + world building),
  `VrRig` (head/hands/locomotion), `Ghost`, `Maze`, `Hud`, `Sfx`, `PoseFollower`.
- `Assets/Scripts/Editor/ProjectSetup.cs` — one-click project config and APK build.
- Edit the maze in `Maze.cs` (`#` wall, `.` pellet, `o` power pellet, `G` ghost start, `P` player start).
