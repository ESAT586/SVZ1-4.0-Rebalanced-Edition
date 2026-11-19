# Animations Library (SVZ1 Blender 5.1.2 + Mixamo -> Unity 2017.4.40f1)

Reusable FBX animation library for SVZ1 characters and weapons, compatible with **Blender 5.1.2**, **Adobe Mixamo**, and **Unity 2017.4.40f1** (converted to native Legacy `AnimationClip` assets via `Unity2017McpBridge` `build_legacy_anim`).

## Current Clips
- `Shogun_Spear_Thrust.fbx` — Zombie Shogun (`PFUndeadshogun`) piercing lunge thrust (`attack04`, 1.70s / 51 frames @ 30fps, `attackActive` @ 0.90s).
- `Shogun_Spear_Sweep.fbx` — Zombie Shogun (`PFUndeadshogun`) high overhead-to-diagonal Yari cleave slam (`attack05`, 1.90s / 57 frames @ 30fps, `attackActive` @ 1.033s, includes `ShakeCamera` & `Slam`).

## Pipeline Workflow
1. Dump any SVZ1 character's exact Unity skeleton & rest mesh (`dump_skeleton` on `127.0.0.1:20174/action`).
2. Author or retarget `.fbx` animations on the reconstructed armature inside **Blender 5.1.2**.
3. Save `.fbx` clip into `Animations_Library/` for future reuse.
4. Export frame-sampled Unity coordinate curves (`build_legacy_anim`) and register non-destructively onto the character prefab (`register_character_clips`) without removing existing stock clips (`attack01`–`attack03`).
