# Cyber Minefield

Cyber Minefield is a 3D logic puzzle game inspired by Minesweeper. Instead of clicking a flat board, the player moves directly on top of a 3D grid, reads clue numbers, places defusers on dangerous tiles, and clears every safe tile without stepping on a virus.

This project was developed for a Computer Graphics and Visualization course. The main focus is combining 3D player interaction, procedural grid generation, puzzle logic, pixel-art UI, audio feedback, and multiple game modes into one playable Unity project.

### Game can be downloaded at: https://nvrzqy.itch.io/cyber-minefield

## Gameplay Overview

- Move the player with `WASD`.
- Press `Space` to jump.
- Use `left click` to place or remove a defuser/flag on a closed tile.
- Hold `right click` and move the mouse to rotate the camera in 3D.
- Use mouse scroll to zoom in and out.
- Camera Lock can be enabled from Settings to switch into a top-down view.
- The number on each opened tile shows how many viruses are touching that tile.
- A defused tile is safe to step on, but it will not be manually revealed while the defuser is still placed.
- The player wins when all safe tiles are revealed.
- The player loses when stepping on an undefused virus tile.

## Game Modes

- **New Game**  
  Starts from the story intro, continues to the tutorial, then automatically enters the first campaign level.

- **Tutorial**  
  A step-by-step guided mode that teaches clue numbers, defusers, safe tiles, UI stats, restart, home, and camera controls.

- **Level Mode**  
  A campaign mode with several main levels. The next level is unlocked after the previous level is completed.

- **Classic Mode**  
  A replayable mode with a larger board. The game records win count, attempt count, and best time locally.

- **Time Mode**  
  A timed mode where the player must clear all safe tiles before the timer runs out.

## Technologies Used

- **Unity 6**
  - Project version: `6000.4.7f1`
- **C#**
  - Used for gameplay logic, input handling, UI management, audio, level management, and procedural grid generation.
- **Universal Render Pipeline (URP)**
  - Used for the 3D rendering pipeline.
- **Unity UI**
  - Used for the home menu, level select, pause/settings menu, audio sliders, tutorial overlay, HUD, loading screen, win screen, and game over screen.
- **Unity Input System with legacy fallback**
  - Supports keyboard and mouse input in both the Unity Editor and built executable.
- **3D Assets**
  - Used for the player character, flag/virus markers, and 3D board presentation.
- **Pixel-Art UI Assets**
  - Used for the font, buttons, animated story character, and decorative stars.

## Main Project Structure

```text
Assets/
  CyberMinefield/
    Scripts/
      Audio/
      Core/
      Grid/
      Levels/
      Player/
      UI/
      Editor/
    Resources/
      Audio/
      Fonts/
      Materials/
      Models/
      UI/
    Materials/
    Models/
    Prefabs/
  Scenes/
    scene.unity
  Settings/
Packages/
ProjectSettings/
```

## Important Scripts

- `Assets/CyberMinefield/Scripts/Core/GameManager.cs`  
  Controls the overall game flow, game states, game modes, level start/restart, win/lose conditions, story intro, ending story, loading screen, and campaign progression.

- `Assets/CyberMinefield/Scripts/Grid/GridManager.cs`  
  Generates the grid, places viruses, calculates clue numbers, reveals tiles, handles flood reveal, validates defusers, and checks whether all safe tiles have been cleared.

- `Assets/CyberMinefield/Scripts/Grid/TileNode.cs`  
  Stores the state of each tile, including whether it is closed, revealed, dangerous, defused, occupied by the player, and what visual marker or number should be shown.

- `Assets/CyberMinefield/Scripts/Player/PlayerController.cs`  
  Handles 3D movement, jumping, player animation, tile detection, and tile reveal triggers when the player moves across the board.

- `Assets/CyberMinefield/Scripts/Core/InputManager.cs`  
  Handles keyboard and mouse input, including movement, jump, left-click defuser placement, right-click camera rotation, zoom, and camera lock.

- `Assets/CyberMinefield/Scripts/UI/UIManager.cs`  
  Builds and manages the runtime UI, including the home screen, level select screen, pause/settings menu, audio menu, tutorial focus overlay, HUD, loading screen, win panel, and game over panel.

- `Assets/CyberMinefield/Scripts/Levels/LevelManager.cs`  
  Stores the tutorial and campaign level configurations.

- `Assets/CyberMinefield/Scripts/Audio/AudioManager.cs`  
  Manages sound effects, background music, SFX/music volume, footsteps loop, win/game over sounds, jump sound, button click sound, and defuser placement sound.

## Team Workflow

The development workflow was divided based on script responsibility:

1. **Core Gameplay**
   - Managed game state, mode transitions, restart, win condition, lose condition, and campaign progression.

2. **Grid System**
   - Implemented board generation, virus placement, clue number calculation, reveal logic, defuser rules, and solvability-focused board generation.

3. **Player and Input**
   - Implemented 3D movement, jump, camera controls, click interaction, tile detection, and player animation behavior.

4. **UI/UX**
   - Built the home menu, tutorial flow, settings menu, audio menu, HUD, level select, loading screen, and win/lose feedback.

5. **Visual and Audio**
   - Integrated pixel font, button sprites, character assets, marker visuals, sound effects, and background music.

6. **Testing**
   - Tested the game through Unity Editor by checking New Game, Story, Tutorial, Level Mode, Classic Mode, Time Mode, Restart, Home, Settings, Camera Lock, and Win/Lose flows.

## How to Run in Unity Editor

1. Clone the repository:

```bash
git clone https://github.com/nvrzqy/CyberMinefield.git
```

2. Open Unity Hub.

3. Click **Add** or **Open**, then select the cloned `CyberMinefield` folder.

4. Use this Unity version:

```text
Unity 6000.4.7f1
```

5. Open the main scene:

```text
Assets/Scenes/scene.unity
```

6. Click the **Play** button in the Unity Editor.

7. From the home screen, select:
   - **New Game** to start from the story intro,
   - **Tutorial** to test the tutorial directly,
   - **Classic** or **Time** to test additional modes when available or unlocked.

## Gameplay Testing Checklist

- Press `WASD` to move.
- Press `Space` to jump.
- Left click a closed tile to place a defuser.
- Left click the same tile again to remove the defuser.
- Hold right click and move the mouse to rotate the camera.
- Scroll the mouse wheel to zoom in or out.
- Open Settings and enable Camera Lock to test the top-down camera.
- Press `R` or click the Restart button to restart the current level.
- Click Home to return to the home screen.
- Reveal all safe tiles to trigger the win condition.
- Step on an undefused virus tile to trigger the game over sequence.

## Build Instructions

To create an executable build:

1. Open the project in Unity.
2. Make sure the main scene is included in Build Settings:

```text
Assets/Scenes/scene.unity
```

3. Open:

```text
File > Build Profiles / Build Settings
```

4. Select the target platform, for example Windows.
5. Click **Build**.
6. Choose an output folder outside the Unity project folder, for example:

```text
Builds/CyberMinefield
```

## Repository Notes

The following folders/files should not be uploaded to GitHub:

- `Library/`
- `Temp/`
- `Logs/`
- `UserSettings/`

- executable build files such as `.exe`

These files are ignored through `.gitignore` so the repository stays lightweight and can be opened properly by Unity after cloning.
