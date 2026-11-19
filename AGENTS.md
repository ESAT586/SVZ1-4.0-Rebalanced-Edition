# SVZ1 Project & Unity 2017.4.40f1 Workspace Rules

## Target Engine & C# Runtime Constraints (Mandatory)
- **Unity Version**: `2017.4.40f1` LTS ONLY.
- **Scripting Runtime / C#**: `.NET 3.5 Equivalent` (C# 4 / early C# 6 syntax).
  - **NEVER** use C# 7+ features (`out var`, pattern matching `is Type x`, tuple literals `(a, b)`, `Span<T>`, `async/await` Tasks, switch expressions, `nameof` check carefully). Use traditional `Type x; if (TryGet(..., out x))` and classic `.NET 3.5` collections.
  - **NEVER** use Unity 2018.1+ / 2019+ / Unity 6 APIs:
    - No `UnityEngine.InputSystem`, `UnityEngine.Addressables`, `Unity.Entities`, `UnityEditor.PackageManager`, `TMPro` (unless verified present), `PrefabUtility.SaveAsPrefabAsset` (use `PrefabUtility.CreatePrefab` / `ReplacePrefab`), or `UnityWebRequest` modern overloads not in 2017.4.
    - Prefer `ScreenCapture.CaptureScreenshot` or `Camera.Render()` + `Texture2D.ReadPixels` + `Texture2D.EncodeToPNG()`.

## SVZ1 Project Conventions
- **Registries**: `Assets/Resources/registry/` (`*.json` & `*.txt` such as `characters`, `waves*`, `weapons`, `abilities`, `Enemies.txt`). Verify syntax/format after any edit.
- **Codebase Graph (`codebase-memory` MCP)**: Prefer `search_graph` / `trace_path` / `get_code_snippet` over broad filesystem greps when tracing C# symbols across the 300+ `Assembly-CSharp` scripts.
- **Offline C# Pre-Check & Unity Bridge**: Run `unity2017-dev` (`scripts/unity2017_cs_check.ps1`) to validate `.NET 3.5` / `smcs.bat` compilation (~2s) before `unity_refresh_assets` (`127.0.0.1:20174`, `Assets/Editor/Unity2017McpBridge.cs`).
- **3D & Audio Pipeline (`game-asset-pipeline` skill)**: Keep new Blender meshes `< 5,000` tris (characters) / `< 1,500` tris (props/weapons) using `Decimate`/`make_lods.py`, validate with `qa_check.py` + `look`, expose Unity-driven animations via Custom Properties / Shape Keys, and export a single clean `.fbx` (`export_asset.py --preset unity`). Use `sfxbrew` MCP + `ffmpeg` for SFX and `3d-assets` MCP for CC0 models/textures.
- **Design Alignment (`GAME_DESIGN.md`)**: When building a brand-new game mode or multi-system feature, document loops, stats, and acceptance checks in `GAME_DESIGN.md` first (template in `unity2017-dev/references/game_design_template.md`).
- **Build & Android**: Android builds target Gradle + IL2CPP via `BuildWindow.ApplyUniversalAndroid64BitSettings()`. Always check `unity_read_console` for `0` compile errors after C# changes.
- **Local Media/Audio CLI**: `ffmpeg` is available in PATH (`WinGet Links`) for local `.wav`/`.mp3`/audio trimming, mixing, pitch/reverb effects directly into `Assets/Resources/` or `Assets/Audio/`.
