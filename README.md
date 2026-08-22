# LAST LINE

## Overview

LAST LINE is a compact defense survival shooter. Move along the bottom of the battlefield, aim with the mouse, and hold the left mouse button to fire.

## Objective

Stop enemies before they cross the defense line. Each lane has a single-use defensive mine. After the mines are gone, three enemy breaches end the run.

## Controls

- Move: `A` / `D` or Left / Right Arrow
- Aim: Mouse
- Fire: Hold Left Mouse Button
- Start or restart: `Space`
- Choose an upgrade: `1`, `2`, `3`, or click a card
- Return to title: `Esc`
- Optional auto-fire: in-game toggle

## Core Mechanics

Enemies award score and experience when defeated. Experience levels pause the action and offer three upgrades. Enemy health, movement speed, spawn rate, and wave budget increase over time. Brute, runner, weaver, shield, elite, and giant variants join the normal enemies in later waves.

## Upgrade List

- Reinforced Rounds: increases bullet damage
- Rapid Fire: reduces the fire interval, down to 0.12 seconds
- High-Velocity Rounds: increases projectile speed
- Multishot: adds projectiles, up to five per volley
- Piercing Rounds: adds penetration, up to three extra enemies
- Critical Rounds: adds a chance to deal double damage
- Burst Module: adds rapid volleys to each attack
- Auto Lightning: periodically strikes the enemy closest to the defense line

Upgrade cards can appear at R, SR, SSR, or UR rarity. Higher rarity gives a larger numerical increase where applicable. Upgrades that have reached their limit are removed from the selection pool.

## How to Run the Build

Open `WindowsBuild/LastLine.exe`. The build targets 64-bit Windows and starts in a 600 x 800 portrait window. Unity Editor is not required.

## How to Open the Project

1. Open Unity Hub.
2. Add the `UnityProject` folder from the submission package, or this repository root.
3. Open it with Unity `6000.3.15f1`.
4. Open `Assets/Scenes/SampleScene.unity` and enter Play Mode.

## Unity Version

Unity `6000.3.15f1`.

## Third-Party Assets

Visual and audio assets are credited in `THIRD_PARTY_NOTICES.md`. The included Kenney assets and the OpenGameArt music track are distributed under CC0 1.0. No third-party gameplay code is used.

## Code Structure

- `StageLoop` coordinates the run lifecycle and transitions between title, play, level-up, and game-over states.
- `StageSession` owns score, survival time, progression, and state notifications without depending on scene UI.
- `DefenseController` contains breach and defensive-mine rules, while `StageHudPresenter` updates the HUD from session events.
- `GameBalanceConfig` is a `ScriptableObject` used as the single runtime source for weapon, enemy, wave, rarity, and progression values.
- `WeaponRuntimeState` applies upgrades and enforces caps separately from player input and projectile spawning.
- `ComponentPool<T>` provides reusable enemies, bullets, and short-lived effects through a small `IPoolable` contract.

## Engineering Notes

- State and presentation are separated so restarting a run resets gameplay data and scene objects without rebuilding the UI.
- Upgrade selection pauses gameplay and consumes queued level-ups one at a time, including cases where one reward crosses several levels.
- Projectile collision uses non-allocating sphere casts and overlap queries. Each projectile tracks enemies already hit so penetration cannot damage one target twice.
- Enemy spawning supports a fixed debug seed for reproducible runs, while normal play creates a new seed for each session.
- Camera shake is treated as a visual offset. Aiming and viewport calculations use the stable camera position, then transient feedback restores the camera after interruption or restart.
- Runtime-created objects and event subscriptions are explicitly released when returning to the title or starting another run.
