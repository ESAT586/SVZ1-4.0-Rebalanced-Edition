# SVZ1 Mod & Feature Master Design Document (`GAME_DESIGN.md`)

> Living multi-feature GDD for **Samurai vs Zombies Defense (`Unity 2017.4.40f1` LTS / `.NET 3.5` / C# 4)**. New user ideas are logged here in order so code, Blender assets, SFX, and QA stay aligned across turns and subagents.

---

## Idea #1 — Zombie Shogun Phase 2: "Terasu: Rise of the Blood Moon"

- **Status**: Implemented & Verified in Unity Play Mode (V2 Upgrade: **Azir + Zed 6-Ring Shurima Deity Hybrid**, Title: `TERASU: RISE OF THE BLOOD MOON`)
- **Inspiration**: *League of Legends — Shurima: Rise of the Ascended (Azir)* + *Zed Living Shadow / Death Mark* + *Sun Wukong telekinetic weapon recall* + Japanese Blood Moon / Eclipse Oni mythology.

### Core Gameplay Loop & Phase Transition
1. **Phase 1 — Zombie Shogun (Grounded Sword Boss)**:
   - Bright daylight battlefield using the original Zombie Shogun (`PFUndeadshogun`) wielding the rusty katana (`Props/PFUndeadShogunSword`).
2. **Defeat Interception -> 6.7s Ascension Sequence ([BloodMoonTerasuController.cs](Assets/Scripts/Assembly-CSharp/BloodMoonTerasuController.cs))**:
   - Triggered when Phase 1 Shogun reaches fatal HP (`BloodMoonTerasuController`), blocking premature victory until Ascension finishes.
   - `0.0s – 1.2s`: Normal collapse; sword removed.
   - `1.2s – 2.6s` (**Eclipse Lightning & Quake**): Scene darkens into crimson Blood Moon Eclipse with rising crimson moon, stroboscopic lightning flashes, expanding ground shockwaves, and earthquake camera shake (`cameraShakeOffset` + `CameraShaker.RequestShake`).
   - `2.6s – 5.8s` (**Extended Sphere Levitation**): Longer levitation inside an expanding Blood Moon sphere (`kSphereDuration = 5.8f`) bombarded by `12` jagged lightning bolts, continuous AoE pulse damage (`0.50s` interval), apocalyptic roar SFX (`Assets/Resources/Audio/terasu_apocalypse_roar.wav`, `4.80s`), and an inner blazing `BloodMoonCoreCocoon` veil at the climax apex.
   - `5.8s – 6.7s` (**RebirthBurst — Darkness Shatter, Husk & V2 Prefab Swap**): Crimson-gold rebirth explosion completely shatters the eclipse darkness back to bright daylight (`RestoreAtmosphereIfNeeded`), disables all original green Zombie Shogun body renderers (`DisableOriginalShogunRenderers`), leaves the shattered old Zombie Shogun corpse/armor crater on the bridge (`SpawnShatteredShogunHusk`), and spawns the V2 20-part deity model ([PFTerasuBloodMoon.prefab](Assets/Resources/characters/PFTerasuBloodMoon.prefab)) with heroic de-squished bone tracking in `LateUpdate()`.
3. **Phase 2 — Terasu: Rise of the Blood Moon (Shurima-Zed Deity Emperor)**:
   - **Sculpted V2 3D Asset & UV-Baked Atlas (`Unlit/Texture`)**: [terasu_blood_moon_fullbody.fbx](Assets/Resources/props/terasu_blood_moon_fullbody.fbx) (`4,362` tris, `20` articulated mesh parts: obsidian/antique-bronze armor, diagonal crimson chest scars, 7-fin Azir flaring collar, falcon beak mask, forearm wrist blades, tattered crimson back cape, asymmetric taloned war greaves, upright 6-ring horseshoe halo `Terasu_EclipseHaloDisc` + `Terasu_HaloOrb_0..5`, and left-hand `Terasu_LeftVortexOrb`) textured with pre-shaded `1024x1024` 4-quadrant [terasu_shurima_zed_atlas.png](Assets/Resources/characters/Materials/terasu_shurima_zed_atlas.png) and persisted into [PFTerasuBloodMoon.prefab](Assets/Resources/characters/PFTerasuBloodMoon.prefab) (`4` materials).
   - **6-Ring Halo Counter & Lightning Arcs (`UpdateSixRingDeityHalo`)**: Orbs ignite `1/6 .. 6/6` as `mUltCooldownTimer` recharges (all `6` blazing during Ult), bridged by jagged crimson-to-violet lightning arcs (`SixRingDeityArcs`).
   - **Sun Wukong Backline Spear + Vortex Vacuum + Zed Blood Shadow Clone (`SpearUltState`)**:
     - `Throwing`: Hurls `PFZombieShogunSpear` to the farthest backline opponent.
     - `Impaled`: Plunges into the backline with lightning pillar + AoE shockwave; left arm raises & swells `Terasu_LeftVortexOrb` while `LeftHandVortexVacuumBeam` pulls nearby opponents within `240f` toward `mSpearTargetPos`; spawns spinning `TerasuBloodShadowClone` + `BloodShadowSlashRing` beside the impaled spear (`SpawnBloodShadowClone`).
     - `Recalling`: Shadow Clone detonates with `Shadow Slash` AoE (`DamageAndKnockbackArea`) & shockwave ring, while Terasu stretches his right gauntlet out to recall the spear through clustered enemies.

### Verified Checks (2026-11-14, V2 Final)
- Blender 5.1.2 V2 sculpt & `qa_check.py --profile game`: `pass: true` (`TOTAL_TRIS=4362`, `0` errors/z-fighting/floating/enclosed).
- `unity2017_cs_check.ps1 -Target All`: exit `0` (`Assembly-CSharp.csproj` 314 files in `2.04s` + `Assembly-CSharp-Editor.csproj` 9 files in `1.02s`).
- `unity_refresh_assets` + `Tools/SVZ/Terasu/0. Build Standalone PFTerasuBloodMoon Prefab`: `0` Unity console errors; built `PFTerasuBloodMoon.prefab` (`materials=4`), verified `Unlit/Texture` + `terasu_shurima_zed_atlas.png` GUID `277580b1a07956746afa5ef4be913885`.
- `Tools/SVZ/Terasu/1. Spawn Phase 1 Shogun & Backline Targets` in `Assets/Scenes/InGame.unity`: `0` runtime console errors; all 6 unobstructed, undimmed live screenshots (`terasu_live_1` – `terasu_live_6`) visually verified.

---

## Pending Ideas Backlog
- *Idea #2+*: To be appended as the user shares additional ideas.
