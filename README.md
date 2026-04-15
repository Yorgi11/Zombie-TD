# Zombie TD

Zombie TD is a co-op first-person survival prototype that combines wave-based zombie combat with defensive preparation and server-based multiplayer flow.

Built in Unity, the project is currently focused on the core online session flow, player spawning, first-person movement, and a lightweight server browser system that supports saved direct-connect entries with live status polling.

The long-term design direction is a hybrid of:
- FPS combat
- tower defense style preparation
- wave survival
- 1–4 player co-op

The design foundation for the project is a wave-based survival game where players survive escalating zombie rounds by combining gunplay, teamwork, and defensive placement. The current target structure is 20 waves, with players earning resources between rounds to improve weapons, upgrades, and defenses. :contentReference[oaicite:0]{index=0}

---

## Current Project Status

This repository is currently centered on the multiplayer and bootstrap foundation rather than the full zombie gameplay loop.

Implemented or partially implemented systems include:

- Unity Netcode bootstrap and transport setup
- host, client, and server startup flow
- automatic transition into the game scene
- authoritative player spawning on the server
- first-person networked movement and jumping
- server browser with saved direct-connect entries
- periodic UDP server status pinging
- runtime server display name propagation to clients
- menu hooks for hosting, joining, refreshing, loading, and shutdown

The current menu flow allows a player to:
- host a session with a custom server name
- connect to an entered IP
- refresh the server browser
- load into the game scene
- shut down any active session state :contentReference[oaicite:1]{index=1}

---

## Vision

The intended full game is a 1–4 player co-op FPS / tower defense survival experience for PC. Players will prepare during safe phases, spend resources, build defenses, and survive increasingly difficult zombie waves. Planned systems include weapons, traps, enemy variety, progression, and multiple maps. :contentReference[oaicite:2]{index=2}

The current repo should be viewed as the networking and session-management foundation for that larger game.

---

## Core Features in This Repo

### 1. Network Bootstrap
`NetBootstrap` is the central networking entry point for the project.

It currently handles:
- singleton setup
- `NetworkManager` and `UnityTransport` initialization
- host, client, and dedicated server startup
- connection approval
- shutdown and cleanup
- server scene loading
- player spawning for connected clients
- UDP status responder for the server browser

It also supports setting a custom server display name, which is included in the status response used by the browser. :contentReference[oaicite:3]{index=3}

### 2. Server Browser
The server browser system lets users save direct-connect servers locally and periodically ping them for availability.

Current behavior:
- stores saved servers in JSON under `Application.persistentDataPath`
- rebuilds UI from saved entries on startup
- refreshes every 5 seconds while in the `Bootstrap` scene
- supports manual refresh
- sends a UDP status request to `port + 1`
- updates server name, ping, status, and player count in the UI
- disables joining when a server is offline or unknown :contentReference[oaicite:4]{index=4} :contentReference[oaicite:5]{index=5}

### 3. Saved Server Data Model
Saved server entries store:
- server name
- IP
- port

The status request / response format is also defined in the shared saved server data model. :contentReference[oaicite:6]{index=6}

### 4. Menu Integration
The menu UI currently exposes buttons for:
- host
- join
- load game
- shutdown
- refresh servers

Hosting can optionally use a custom server name entered through the UI. :contentReference[oaicite:7]{index=7}

### 5. Networked First-Person Player Controller
`NetworkPlayerController` is a server-authoritative `NetworkBehaviour` with local-owner input and camera handling.

Current movement/controller features include:
- walking
- sprinting
- crouch state selection
- mouse-look
- jump buffering
- grounding checks
- server-side movement application
- server-side jump handling
- local camera setup for the owning player
- clamped client look input submission for validation

The server remains authoritative over movement and jump behavior, while the owning client handles local input and camera updates. :contentReference[oaicite:8]{index=8}

---

## Repository Structure

The exact folder structure may vary, but the uploaded core scripts currently include:

- `NetBootstrap.cs`  
  Main network/session bootstrap and server status responder. :contentReference[oaicite:9]{index=9}

- `MenuUI.cs`  
  Menu button handlers for hosting, joining, loading, shutdown, and refreshing. :contentReference[oaicite:10]{index=10}

- `ServerBrowser.cs`  
  Saved server list management, UI rebuilding, periodic refresh loop, and UDP query logic. :contentReference[oaicite:11]{index=11}

- `ServerEntryUI.cs`  
  UI binding and visual state updates for each saved server entry. :contentReference[oaicite:12]{index=12}

- `SavedServerData.cs`  
  Serializable models for saved server entries and status messages. :contentReference[oaicite:13]{index=13}

- `NetworkPlayerController.cs`  
  Networked server-authoritative player controller with local-owner input and camera logic. :contentReference[oaicite:14]{index=14}

- `LobbyState.cs`  
  Placeholder script for future lobby state management. :contentReference[oaicite:15]{index=15}

---

## Scene Flow

Based on the current scripts, the intended flow is:

1. Launch into the `Bootstrap` scene
2. Host starts a server or player joins a server by IP
3. Host/server starts listening
4. Browser status polling becomes available on UDP `port + 1`
5. Once connected, the server loads the `Game` scene
6. Connected clients are spawned into the game scene automatically :contentReference[oaicite:16]{index=16}

### Current scene names referenced in code
- `Bootstrap`
- `Game` :contentReference[oaicite:17]{index=17} :contentReference[oaicite:18]{index=18}

These scenes must exist and be added to Unity build settings for the current flow to work correctly.

---

## Networking Notes

### Transport
The project uses:
- Unity Netcode for GameObjects
- Unity Transport (`UnityTransport`) :contentReference[oaicite:19]{index=19}

### Default Port
The current default gameplay/network port is:

- `7777`

The server browser status responder listens on:

- `7778` by default (`port + statusPortOffset`) :contentReference[oaicite:20]{index=20}

### Hosting / Joining
- Host binds to `0.0.0.0`
- Client connects to the entered or default IP
- Dedicated server startup is also supported in code :contentReference[oaicite:21]{index=21}

---

## Server Browser Behavior

The browser stores saved servers in:

`Application.persistentDataPath/saved_servers.json` :contentReference[oaicite:22]{index=22}

Each saved entry contains:
- `Name`
- `IP`
- `Port` :contentReference[oaicite:23]{index=23}

When refreshing:
- the client sends a UDP JSON request
- the server responds with name, current players, and max players
- the UI updates each entry accordingly :contentReference[oaicite:24]{index=24} :contentReference[oaicite:25]{index=25}

---

## Requirements

This repo currently assumes:

- Unity project setup with Netcode for GameObjects
- Unity Transport package
- TextMeshPro
- scenes named `Bootstrap` and `Game`
- a `NetworkManager` and `UnityTransport` attached to the bootstrap object
- a valid player `NetworkObject` prefab assigned in `NetBootstrap`
- menu and browser UI wired in the Unity inspector :contentReference[oaicite:26]{index=26} :contentReference[oaicite:27]{index=27} :contentReference[oaicite:28]{index=28}

---

## Setup

### Basic Setup
1. Open the project in Unity.
2. Make sure the required multiplayer packages are installed.
3. Add both `Bootstrap` and `Game` scenes to build settings.
4. Create a bootstrap object with:
   - `NetworkManager`
   - `UnityTransport`
   - `NetBootstrap`
5. Assign the player prefab in `NetBootstrap`.
6. Wire the menu input fields and buttons to `MenuUI`.
7. Wire the server browser fields, entry parent, and entry prefab to `ServerBrowser`.
8. Make sure the server entry prefab is configured with `ServerEntryUI`.

### Player Setup
The player prefab should include:
- `NetworkObject`
- `Rigidbody`
- a `CapsuleCollider` on self or child
- `NetworkPlayerController`
- a valid camera target transform for first-person camera placement :contentReference[oaicite:29]{index=29}

---

## Running the Project

### Host
- Enter a server name in the host field if desired
- Click **Host**
- The host starts listening and can transition into the game scene
- The host’s custom server name becomes the browser-visible display name for that server :contentReference[oaicite:30]{index=30} :contentReference[oaicite:31]{index=31}

### Join
- Enter an IP and click **Join**
- Or add a server to the browser and join from its entry button
- Clients connect to the selected IP and port :contentReference[oaicite:32]{index=32} :contentReference[oaicite:33]{index=33}

### Refresh Browser
- Click **Refresh**
- Or allow the browser to refresh automatically every 5 seconds while in the bootstrap scene :contentReference[oaicite:34]{index=34} :contentReference[oaicite:35]{index=35}

---

## Planned Gameplay Systems

The design target for the full game includes:

- 20-wave survival mode
- zombie enemy scaling by wave
- multiple enemy archetypes such as runner, tank, spitter, and mutant
- defensive buildables such as barriers, turrets, spike traps, and explosive mines
- player and weapon upgrades
- multiple maps
- co-op revive systems
- replayability through upgrade paths and map variation :contentReference[oaicite:36]{index=36}

The recommended initial gameplay scope remains:
- 1 map
- 1–2 enemy types
- 2–3 weapons
- a basic wave system :contentReference[oaicite:37]{index=37}

---

## Credits

Project concept and design direction by the repository owner @Yorgi11 and collaborator @Baldi7327

Initial design basis taken from the Zombie TD concept document, including:
- FPS / tower defense / survival hybrid
- 1–4 player co-op
- wave-based survival structure
- enemy escalation and map concepts
- progression and replayability goals :contentReference[oaicite:39]{index=39}
