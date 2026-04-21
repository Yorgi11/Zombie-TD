# Zombie TD

Zombie TD is a multiplayer co-op FPS survival prototype built in Unity. Players join a lobby, launch into a shared game scene, fight off waves of zombies, earn points for kills, and respawn after death while the server remains authoritative over core gameplay systems.

The project combines:
- first-person shooter combat
- wave-based zombie survival
- light tower-defense style structure
- online co-op session flow for up to 4 players

At its current stage, the repo already contains the core loop foundation:
- multiplayer session hosting and joining
- lobby-to-game scene flow
- server-authoritative player movement and combat
- pooled zombie spawning
- wave progression
- points, HP, death, and respawn
- in-game UI for wave, ammo, HP, and score

---

## Overview

Zombie TD is designed as a co-op wave survival game where players defend against escalating zombie rounds. The codebase currently focuses on building the full gameplay backbone rather than only menu or networking scaffolding.

The current implementation includes:
- Unity Services session hosting and joining
- Relay-backed multiplayer sessions
- lobby management and scene transitions
- server-authoritative player spawning and movement
- server-authoritative bullet simulation and damage
- zombie AI target selection and navigation
- zombie object pooling
- wave spawning with support for multiple enemy types
- player scoring and kill rewards
- player death and respawn flow
- local player HUD and death screen

---

## Current Gameplay State

A typical gameplay flow currently looks like this:

1. A host creates a session.
2. Other players join by session code or public session browser entry.
3. The server loads the lobby.
4. The host starts the match.
5. All players are moved into the game scene and assigned spawn positions.
6. The server begins spawning waves of zombies.
7. Players shoot zombies, earn points, take damage, die, and respawn.
8. The wave system advances until all configured waves are completed.

This means the project already goes beyond a raw networking prototype and into a real playable combat testbed.

---

## Core Systems

## Multiplayer Session Bootstrap

`NetBootstrap` is the main multiplayer entry point for the project.

It is responsible for:
- initializing Unity Services and anonymous authentication
- creating and joining multiplayer sessions
- tracking the current session code
- handling host/server startup flow
- moving connected players through lobby and game scenes
- reserving spawn positions for connected players
- spawning or relocating player objects during scene transitions

The project currently uses:
- Unity Netcode for GameObjects
- Unity Transport
- Unity Multiplayer Services sessions / relay flow

---

## Lobby and Match Flow

The project supports a basic lobby pipeline before gameplay begins.

Current flow:
- host creates a session
- clients join by code or public session entry
- server loads the lobby scene
- host selects or confirms game settings
- host starts the match
- all players are spawned into the game scene
- `GameManager` initializes gameplay systems when all clients have loaded

`LobbyState` handles the visible lobby state and exposes the current join code to the UI.

---

## Server-Authoritative Player Controller

`NetworkPlayerController` is the main networked FPS player controller.

Implemented features include:
- owner-controlled local input
- first-person camera setup
- walking, running, and crouch movement states
- jumping with buffered input
- server-authoritative movement
- server-authoritative yaw/input application
- local gun handling for the owning player
- death-state handling
- respawn requests
- point synchronization
- HP and ammo event hooks for UI

The local client reads input and sends authoritative movement data to the server, while the server applies final movement and health state.

---

## Weapons and Shooting

`Gun` handles the local weapon state and presentation.

Current functionality includes:
- fire rate handling
- semi-auto support
- ammo per magazine and reserve ammo
- reload timing
- aim and hip positions
- recoil animation
- aim rotation toward target point
- shot request events
- ammo change events for UI

The weapon requests shots, but the actual combat resolution is handled server-side.

---

## Server Bullet Simulation

`ServerBulletPool` handles active bullets on the server.

Current responsibilities:
- pooled bullet allocation
- bullet flight updates
- gravity application
- hit detection using raycasts
- penetration tracking
- per-bullet hit tracking to prevent duplicate hits
- damage application through `DamageableObject`

This keeps combat authoritative and ensures that hit resolution and damage are decided by the server rather than the client.

---

## Health, Damage, Death, and Respawn

`DamageableObject` is the shared health component used by players and enemies.

It currently supports:
- max HP and current HP
- server-authoritative damage checks
- death event callbacks
- HP change callbacks
- full HP restoration

Player death flow:
- player HP reaches zero
- local death UI is shown
- local movement/combat interaction is disabled
- player can request a respawn
- the server restores position, velocity, and HP
- the death menu is hidden again on the owning client

Enemy death flow:
- the zombie reports the kill to `GameManager`
- the killer receives points
- the zombie is returned to its pool instead of destroyed

---

## Wave System

`GameManager` controls the main PvE flow.

Implemented wave features include:
- per-wave enemy counts
- support for four enemy categories
- configurable spawn interval
- safe phase timing between waves
- current wave tracking
- kill reward values by enemy type
- server-side spawning only
- wave completion checks
- end-of-waves shutdown of further spawning

Enemy categories currently defined:
- `Regular`
- `Special1`
- `Special2`
- `Special3`

The system is designed so different zombie prefabs can be assigned and prewarmed independently.

---

## Zombie AI and Pooling

### Zombie AI
`Zombie` currently includes:
- NavMeshAgent-based movement
- automatic target selection
- prioritization of nearby alive players
- fallback targeting of the tower objective
- destination refresh and retarget timing
- server-only AI updates
- death handling through `DamageableObject`

### Zombie Pooling
`ZombiePoolManager` and `ZombiePool` provide:
- per-enemy-type pools
- configurable prewarm counts
- reuse of spawned zombie objects
- pooled activation/deactivation
- return-to-pool on death

This avoids repeated instantiate/destroy churn during large wave spawning.

---

## Map and Objective

`Map` exposes scene references used by gameplay systems:
- zombie spawn points
- the tower transform

Zombies can target either:
- the nearest valid player within range
- or the tower as the default objective

This gives the project its tower-defense style pressure structure even in its current prototype state.

---

## UI Systems

### Main Menu / Session UI
`MenuUI` currently supports:
- entering a server/session name
- entering a join code
- toggling public/private visibility
- hosting a session
- joining a session by code
- refreshing the browser
- copying the session code
- shutting down the active session

### Session Browser
`SessionBrowser` and `ServerEntryUI` support:
- querying public sessions
- rebuilding browser UI entries
- displaying session name and player counts
- refresh loops while in the bootstrap scene
- joining a selected public session
- visible session status feedback

### In-Game UI
`GameUI` currently displays:
- death screen
- respawn button
- current wave
- points
- ammo
- HP slider

The in-game HUD binds itself to the local owning player and updates dynamically from ammo, HP, and points events.

---

## Included Core Scripts

The uploaded project currently centers around these scripts:

- `NetBootstrap.cs` — multiplayer bootstrap, session flow, scene transitions
- `LobbyState.cs` — lobby UI state and start-game flow
- `MenuUI.cs` — main menu controls for hosting and joining
- `SessionBrowser.cs` — public session query and UI rebuilding
- `ServerEntryUI.cs` — browser entry display and join hooks
- `MainMenu.cs` — simple scene entry / quit controls
- `GameManager.cs` — wave control, enemy spawning, bullet pool setup, scoring
- `Map.cs` — scene references for spawn points and tower
- `NetworkPlayerController.cs` — server-authoritative networked FPS controller
- `Gun.cs` — weapon logic, ammo, aiming, recoil, shot requests
- `ServerBulletPool.cs` — pooled server bullet simulation
- `DamageableObject.cs` — health, damage, death events
- `Zombie.cs` — AI target logic, NavMesh movement, pool return on death
- `ZombiePoolManager.cs` — zombie pools by enemy type
- `GameUI.cs` — local player HUD and death/respawn UI

---

## Scene Setup

The code currently references these scenes:
- `Bootstrap`
- `Lobby`
- `Game`

These should all exist in Build Settings.

Recommended flow:
- **Bootstrap**: main menu, session browser, host/join UI, bootstrap object
- **Lobby**: waiting room / match start screen
- **Game**: playable wave survival map

---

## Unity Setup Requirements

The project expects a Unity scene setup roughly like this:

### Bootstrap Object
A persistent bootstrap object should include:
- `NetworkManager`
- `UnityTransport`
- `NetBootstrap`

### Player Prefab
The player prefab should include at minimum:
- `NetworkObject`
- `Rigidbody`
- `DamageableObject`
- `NetworkPlayerController`
- a valid camera target transform
- a valid aim target transform
- collider setup appropriate for the controller

### Game Scene
The game scene should include:
- `GameManager`
- `Map`
- `ZombiePoolManager`
- UI canvas with `GameUI`
- configured spawn points
- configured tower transform
- valid zombie prefabs for each enemy type
- any required NavMesh data for zombie navigation

### Enemy Prefabs
Zombie prefabs should include:
- `NetworkObject`
- `DamageableObject`
- `NavMeshAgent`
- `Zombie`
- colliders/renderers that can be enabled/disabled on pool take/return

---

## How to Run

## Host a Match
1. Open the project in Unity.
2. Load the `Bootstrap` scene.
3. Enter a session/server name if desired.
4. Choose whether the session is public or private.
5. Click **Host**.
6. After the lobby loads, start the game from the lobby UI.

## Join a Match
1. Open the `Bootstrap` scene.
2. Enter a join code and click **Join**  
   or
3. Use the public session browser and join from an available entry.

---

## Design Direction

The long-term target for Zombie TD is a fuller hybrid of:
- co-op FPS combat
- defendable objective pressure
- between-wave preparation
- buildable defenses
- stronger weapon and progression systems
- more enemy archetypes
- multiple maps and match variations

Next expansions:
- more weapons
- barriers, traps, turrets, and repair systems
- clearer tower damage / loss state
- enemy variety with unique behaviors
- buy stations or upgrade systems
- better game state transitions and win/lose flow
- dedicated server support and persistence improvements

---

## Current Strengths of the Project

What this repo already does well:
- separates local feel from authoritative server logic
- uses object pooling for both bullets and zombies
- supports real multiplayer session flow instead of local-only testing
- has a working wave/combat/points/respawn gameplay backbone
- is organized around extensible gameplay systems rather than a one-off prototype script pile

---

## Notes

This project is still in active prototype / foundation development. Some systems are intentionally simple or placeholder-level, but the repo already contains the main architecture needed to grow into a larger co-op zombie survival game.

The best way to describe the current state is:

**a functional multiplayer gameplay prototype with server-authoritative combat, wave spawning, pooled enemies, and lobby-to-match flow already in place.**
