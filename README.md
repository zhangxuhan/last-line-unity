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

Enemies award score and experience when defeated. Experience levels pause the action and offer three upgrades. Enemy health, movement speed, and spawn rate increase continuously over time. Brute, runner, and elite variants join the normal enemies as pressure rises.

## Upgrade List

- Reinforced Rounds: increases bullet damage
- Rapid Fire: reduces the fire interval, down to 0.12 seconds
- High-Velocity Rounds: increases projectile speed
- Multishot: adds projectiles, up to five per volley
- Piercing Rounds: adds penetration, up to three extra enemies
- Critical Rounds: adds a chance to deal double damage
- Burst Module: adds rapid volleys to each attack
- Auto Lightning: periodically strikes the enemy closest to the defense line

Upgrade cards can appear at R, SR, or SSR rarity. Higher rarity gives a larger numerical increase where applicable. Upgrades that have reached their limit are removed from the selection pool.

## How to Run the Build

Open `WindowsBuild/LastLine.exe`. The build targets 64-bit Windows and starts in a 1024 x 768 window. Unity Editor is not required.

## How to Open the Project

1. Open Unity Hub.
2. Add the `Project` folder from the submission package, or this repository root.
3. Open it with Unity `6000.3.15f1`.
4. Open `Assets/Scenes/SampleScene.unity` and enter Play Mode.

## Unity Version

Unity `6000.3.15f1`.

## Third-Party Assets

Visual and audio assets are credited in `THIRD_PARTY_NOTICES.md`. The included Kenney assets and the OpenGameArt music track are distributed under CC0 1.0. No third-party gameplay code is used.

## Balance Reference

- Weapon: 10 damage, 0.35-second fire interval, 10 projectile speed, one projectile, no penetration, 8-degree spread step
- Enemy: 30 HP, 0.9 movement speed, 100 score, 1 EXP
- Spawn interval: 1.4 seconds initially, with a 0.5-second floor
- Difficulty: every 30 seconds of survival corresponds to approximately 1.15x HP, 1.05x speed, and 0.92x spawn interval
- Level requirements: 5, 8, 12, 17, then the previous requirement multiplied by 1.3 and rounded up

## Design Notes

- The three-breach rule makes failure readable while allowing recovery from individual mistakes.
- Level-up choices pause the game so the player can compare exact value changes without losing ground.
- Damage, speed, spread, penetration, burst, critical, and automatic attacks support different firing patterns rather than a single fixed build.
- Continuous difficulty scaling avoids abrupt jumps at 30-second boundaries while preserving the intended pressure curve.
- Hits, kills, defensive mine detonations, breaches, and game over use distinct visual and audio feedback.
