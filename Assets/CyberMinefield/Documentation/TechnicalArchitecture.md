# Technical Architecture

## Unity Baseline

- Engine: Unity 6000.4.7f1.
- Render pipeline: Universal Render Pipeline.
- Input: Unity Input System is already installed.
- Target platform: PC/laptop.
- MVP scene type: one gameplay scene with optional menu panels.

## Recommended Folder Layout

- `Assets/CyberMinefield/Scenes`: project-specific scenes.
- `Assets/CyberMinefield/Scripts/Core`: game state, restart, win, lose, pause.
- `Assets/CyberMinefield/Scripts/Grid`: grid generation, tile data, neighbor counts.
- `Assets/CyberMinefield/Scripts/Player`: grid-based movement or selector control.
- `Assets/CyberMinefield/Scripts/Levels`: level configuration and progression.
- `Assets/CyberMinefield/Scripts/UI`: HUD, panels, messages, timer, counters.
- `Assets/CyberMinefield/Scripts/Audio`: sound effect routing.
- `Assets/CyberMinefield/Prefabs`: tile, marker, player/drone, exit prefabs.
- `Assets/CyberMinefield/Materials`: cyber tile and marker materials.
- `Assets/CyberMinefield/Documentation`: design and production notes.

## Runtime Systems

### GameManager

Owns global state: boot level, start, pause, restart, win, lose, scene transitions, and mission result. It should not know low-level tile placement details.

### LevelManager

Provides level settings: grid size, danger count, defuser limit, timer, objective mode, exit position, and progression order. Use ScriptableObjects once multiple levels are needed.

### GridManager

Creates the board, stores tile references, places danger tiles, calculates hint numbers, resolves scan/reveal requests, and exposes tile lookup by grid coordinate.

### TileNode

Stores per-tile state: closed, revealed, danger, neutralized, marked, exit, hint number, and world/grid position. Handles visual state changes through a small presentation method rather than embedding game rules in materials.

### PlayerController

Moves the player or selector one tile per input. It asks GridManager whether a move/scan is valid and keeps the player snapped to tile centers.

### InputManager

Maps keyboard and mouse actions to gameplay requests. For MVP, support WASD/arrow movement, defuser toggle, restart, and pause.

### UIManager

Displays timer, current level, defuser count, win/lose text, restart, pause, and optional how-to-play panels.

### AudioManager

Routes one-shot feedback for scan, defuser deploy, error, breach, and mission complete.

## Data Model

Recommended tile data:

- `Vector2Int GridPosition`
- `bool HasDanger`
- `bool IsRevealed`
- `bool HasDefuser`
- `bool IsExit`
- `int NeighborDangerCount`

Recommended level data:

- `string LevelName`
- `int Width`
- `int Height`
- `int DangerCount`
- `int DefuserLimit`
- `float TimeLimit`
- `Vector2Int StartPosition`
- `Vector2Int ExitPosition`
- `WinConditionType WinCondition`

## Scene Composition

MVP gameplay scene should include:

- Main Camera set to top-down or isometric view.
- Directional or area light.
- GameManager object.
- GridManager object.
- LevelManager object.
- Player/selector prefab.
- Canvas with HUD and win/lose/pause panels.
- AudioManager object.

## Risk Controls

- Use grid-based movement only; avoid physics-driven free movement for tile logic.
- Keep camera top-down/isometric by default; 360 camera should be optional because it can make numbers hard to read.
- Keep tile numbers large and readable before adding decoration.
- Implement 2D/top-down fallback without changing the data model.
- Keep level generation deterministic or seedable for easier debugging.
