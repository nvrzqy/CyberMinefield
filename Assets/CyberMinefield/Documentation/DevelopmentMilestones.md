# Development Milestones

## Milestone 1: Preproduction And Grid Prototype

Target: Week 1.

Deliverables:
- Confirm MVP mode: Mission Mode with exit objective.
- Create one gameplay scene.
- Create grid generation from width and height.
- Create tile data model.
- Reveal safe tiles and show placeholder hint numbers.
- Establish cyber visual placeholders: closed, revealed, danger, defuser, exit.

Acceptance:
- A 5x5 board appears in Unity.
- Tile states can be inspected or toggled during play.
- At least one safe tile can reveal a hint number.

## Milestone 2: Core Gameplay

Target: Week 2.

Deliverables:
- Random or seeded danger placement.
- Neighbor danger count calculation.
- Defuser placement and removal.
- Grid-based player or selector movement.
- Lose condition for unneutralized danger.
- Win condition for reaching exit or clearing safe tiles.
- Restart level.

Acceptance:
- The game can be played from start to win or lose.
- Defused danger tiles do not trigger failure.
- Restart resets the board cleanly.

## Milestone 3: UI, Levels, And Presentation

Target: Week 3.

Deliverables:
- HUD for timer, defuser count, level name, and restart.
- Win, lose, and pause panels.
- Level configuration for at least three boards.
- Tutorial level plus two progression levels.
- Basic cyber materials, glow accents, and readable tile labels.

Acceptance:
- Players can complete a short sequence of levels.
- UI communicates objective, remaining defusers, and result.
- Tile numbers remain readable from the chosen camera angle.

## Milestone 4: Polish, Testing, And Final Build

Target: Week 4.

Deliverables:
- Balance danger counts.
- Add sound effects for scan, defuser, breach, and success.
- Add simple particle or color feedback for danger and completion.
- Fix edge cases in reveal, movement, restart, and win/lose state.
- Prepare final build, screenshots, and report.

Acceptance:
- MVP build runs without blocking gameplay bugs.
- Tutorial explains scan, numbers, defuser, danger, and exit.
- Final scene is presentation-ready.

## Optional Stretch

- Level select.
- Star/rating system.
- Save progress.
- Time attack mode.
- Additional special nodes such as switch, broken node, or firewall gate.
