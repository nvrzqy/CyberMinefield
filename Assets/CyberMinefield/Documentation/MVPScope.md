# MVP Scope

## MVP Goal

Deliver one playable Cyber Minefield experience that proves the core deduction loop: read hint numbers, mark danger with defusers, move through a grid, and reach the objective without triggering malware.

## Must Have

- One Unity gameplay scene.
- Grid-based board generation.
- At least three level configurations:
  - Tutorial: 5x5, 3 dangers.
  - Level 1: 6x6, 5 dangers.
  - Level 2: 8x8, 8 dangers with exit objective.
- Hidden danger tiles.
- Safe tile reveal with neighbor danger number.
- Defuser marker toggle.
- Correctly defused danger tiles are neutralized.
- Game over when the player opens or steps on unneutralized danger.
- Win when the player reaches the exit or completes the chosen safe-tile objective.
- Grid-based movement or selector movement using WASD/arrow keys.
- Restart level with `R`.
- Basic HUD: level name, timer or elapsed time, defuser count, restart hint, result message.
- Simple cyber visuals with readable tile numbers.

## Should Have

- Pause with `Esc`.
- Camera locked to top-down/isometric grid view.
- Simple player representation: capsule, drone, or selector.
- Audio feedback for scan, defuser, error, and success.
- Basic menu or start panel.
- Deterministic level seed for repeatable testing.

## Could Have

- 360 camera rotation with camera lock toggle.
- Level select.
- Rating/star score.
- Save progress.
- Particle effects for malware breach and mission complete.
- More levels beyond the first three.

## Out Of MVP

- Full 3D third-person movement.
- Complex character animation.
- Procedural campaign generation.
- Advanced enemy AI.
- Multiplayer.
- Heavy custom modeling.
- Large narrative cutscenes.

## MVP Definition Of Done

- A new player can start the scene, understand the grid, reveal numbers, place defusers, avoid danger, and reach an exit.
- Losing and restarting are quick.
- The board state is readable at all times.
- The implementation can be expanded into Mission, Classic, or Escape Mode without replacing the grid data model.
