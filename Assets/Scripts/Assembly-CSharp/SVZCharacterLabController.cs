using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class SVZCharacterLabController : MonoBehaviour
{
    public enum LabCameraMode
    {
        Orbit360 = 0,
        SideGameView = 1,
        CloseRightHandSpear = 2,
        CloseLeftHandGauntlet = 3,
        CloseBackHalo = 4,
        WideUltArena = 5,
        TurntableAutoSpin = 6
    }

    public enum LabTab
    {
        ActionsAndChars = 0,
        AnimScrubber = 1,
        RigTuner = 2,
        OpponentSpawner = 3
    }

    public enum DummyBehaviorMode
    {
        ImmortalStationary = 0,
        ImmortalActiveFighter = 1,
        NormalMortal = 2
    }

    public static bool IsLabActive { get; private set; }
    public static bool IsLabCameraActive { get; private set; }
    public static bool IsLabWavePaused { get; private set; }

    private static readonly string[] kTerasuClips = new string[]
    {
        "Combat_Stance",
        "Hover_Fly",
        "Double_Combo_Attack",
        "Spear_Sky_Slam",
        "Over_Shoulder_Throw",
        "Charged_Spell_Cast",
        "ThanosShockwave",
        "Dead"
    };

    private static readonly string[] kEnemyCatalogIds = new string[]
    {
        "Iron_Butcher",
        "Shogun",
        "H_Zombie_Sword",
        "H_Zombie_Bow",
        "H_Zombie_Spear",
        "Oni",
        "Oni_boss",
        "Jurogumo",
        "Chochinobake",
        "Tengu",
        "Horseman",
        "Taiko_Drummer",
        "Evil_Ninja",
        "Evil_Nobunaga",
        "Shuten_Doji",
        "Amanojaku",
        "Akaname",
        "Harionago",
        "Onmoraki",
        "Nio_Guardian",
        "Panzer_Enemy"
    };

    private static readonly string[] kHelperCatalogIds = new string[]
    {
        "IronButcher",
        "Samurai",
        "SamuraiArcher",
        "SpearSamurai",
        "HeavySamurai",
        "Priest",
        "Farmer",
        "Nobunaga",
        "Takeda",
        "Zato",
        "HorseArcher",
        "RocketFarmer",
        "Panzer",
        "NioGuardian"
    };

    private class TrackedDummy
    {
        public Character character;
        public DummyBehaviorMode mode;
        public string label;
        public float baseSpeed;
        public float baseMeleeRange;
        public float baseBowRange;
    }

    // Core Lab State
    private bool mInitialized = false;
    private bool mSetupAttempted = false;
    private float mInitDelay = 0.25f;
    private bool mHudVisible = true;
    private LabTab mActiveTab = LabTab.ActionsAndChars;
    private LabCameraMode mCamMode = LabCameraMode.Orbit360;
    private DummyBehaviorMode mDummySpawnBehavior = DummyBehaviorMode.ImmortalStationary;
    private float mDummySpawnDistance = 175f;
    private float mLabTimeScale = 1.0f;
    private string mStatusBanner = "Hazir - 360 derece kamera icin Sag Tik surukle, Zoom icin Tekerlek kullan";
    private float mStatusTimer = 0f;

    // Active Focus & Characters
    private BloodMoonTerasuController mTerasuCtrl;
    private IronButcherController mButcherCtrl;
    private Character mActiveSubject;
    private string mActiveSubjectLabel = "Blood Moon Terasu (Faz 2)";
    private List<TrackedDummy> mSpawnedUnits = new List<TrackedDummy>();
    private bool mHeroParked = true;
    private int mWalkToggleDir = 0; // -1 left, 0 stop, +1 right
    private Vector2 mCharScroll = Vector2.zero;
    private Vector2 mOpponentScroll = Vector2.zero;

    // 360 Orbit Camera Parameters
    private float mCamYaw = 90f;      // 90 = SVZ side view (+X looking toward -X)
    private float mCamPitch = 14f;
    private float mCamDistance = 420f;
    private float mCamHeightOffset = 74f;
    private float mCamPanZ = 0f;
    private Vector3 mSmoothedFocusPos = new Vector3(0f, 0f, 780f);
    private bool mFocusPosValid = false;
    private Vector3 mLastMousePos;
    private bool mIsOrbitDragging = false;
    private bool mIsPanDragging = false;

    // UI Styling Resources
    private Texture2D mBgDark;
    private Texture2D mBgHeader;
    private Texture2D mBgTabActive;
    private Texture2D mBgTabIdle;
    private Texture2D mBgButton;
    private Texture2D mBgButtonActive;
    private Texture2D mBgAccentRed;
    private GUIStyle mPanelStyle;
    private GUIStyle mHeaderStyle;
    private GUIStyle mTitleStyle;
    private GUIStyle mLabelStyle;
    private GUIStyle mSmallLabelStyle;
    private GUIStyle mButtonStyle;
    private GUIStyle mActiveButtonStyle;
    private GUIStyle mAccentButtonStyle;

    private float mArenaBridgeZ = 780f;
    private float mArenaMinZ = 120f;
    private float mArenaMaxZ = 1480f;

    private void Awake()
    {
        IsLabActive = true;
        IsLabCameraActive = true;
        IsLabWavePaused = true;
        Application.runInBackground = true;
        EnsureGameSingletonsSafe();
    }

    private void OnEnable()
    {
        IsLabActive = true;
        IsLabCameraActive = true;
        IsLabWavePaused = true;
    }

    private void OnDestroy()
    {
        IsLabActive = false;
        IsLabCameraActive = false;
        IsLabWavePaused = false;
        Time.timeScale = 1f;
    }

    private static void EnsureGameSingletonsSafe()
    {
        if (Singleton<PlayModesManager>.instance != null && string.IsNullOrEmpty(Singleton<PlayModesManager>.instance.selectedMode))
        {
            Singleton<PlayModesManager>.instance.selectedMode = "classic";
        }
        if (Singleton<Profile>.instance != null && Singleton<Profile>.instance.waveToBeat <= 0)
        {
            Singleton<Profile>.instance.waveToBeat = 10;
        }
    }

    private void InitializeArenaIfReady()
    {
        InGameImpl inGame = WeakGlobalSceneBehavior<InGameImpl>.instance;
        WaveManager wm = WeakGlobalInstance<WaveManager>.instance;
        CharactersManager cm = WeakGlobalInstance<CharactersManager>.instance;
        if (inGame == null || wm == null || cm == null)
        {
            return;
        }

        mSetupAttempted = true;
        ClearBlockingUIAndDialogs();

        float leftZ = (inGame.heroWalkLeftEdge != null) ? inGame.heroWalkLeftEdge.position.z : 100f;
        float rightZ = (inGame.heroWalkRightEdge != null) ? inGame.heroWalkRightEdge.position.z : 1400f;
        mArenaMinZ = Mathf.Min(leftZ, rightZ);
        mArenaMaxZ = Mathf.Max(leftZ, rightZ);
        float openMinZ = Mathf.Max(mArenaMinZ + 120f, 380f);
        mArenaBridgeZ = (mArenaMaxZ > 2500f) ? 780f : Mathf.Lerp(openMinZ, mArenaMaxZ - 150f, 0.48f);

        // Immortalize Hero and park cleanly offstage by default so Terasu has a distraction-free test studio
        inGame.allAlliesInvincibleTimer = 99999f;
        SetHeroParked(true);

        // Destroy any leftover wave enemies
        DestroyAllNonBaseCharacters(true);

        // Spawn instant Phase 2 Terasu + 2 initial target dummies (close + backline)
        SpawnTerasuPhase2(false);
        SpawnPresetPair();

        mSmoothedFocusPos = new Vector3(0f, 0f, mArenaBridgeZ);
        mFocusPosValid = true;
        mInitialized = true;
        SetStatus("Terasu Test Laboratuvari hazir! Sag Tik = 360 Kamera | 1..8 = Hareket/Ulti | Space = Dondur");
    }

    private void ClearBlockingUIAndDialogs()
    {
        if (WeakGlobalInstance<DialogHandler>.instance != null)
        {
            WeakGlobalInstance<DialogHandler>.instance.Destroy();
        }
        for (int guard = 0; guard < 12; guard++)
        {
            GameObject bell = GameObject.Find("BellShockwave(Clone)");
            if (bell == null) break;
            Destroy(bell);
        }
        Time.timeScale = mLabTimeScale;
    }

    private void SetHeroParked(bool parked)
    {
        mHeroParked = parked;
        InGameImpl inGame = WeakGlobalSceneBehavior<InGameImpl>.instance;
        if (inGame == null || inGame.hero == null || inGame.hero.controller == null)
        {
            return;
        }
        inGame.hero.maxHealth = 999999f;
        inGame.hero.health = 999999f;
        if (parked)
        {
            inGame.hero.controller.StopWalking();
            inGame.hero.controller.position = new Vector3(0f, 0f, mArenaMinZ - 380f);
            if (inGame.hero.controlledObject != null)
            {
                inGame.hero.controlledObject.SetActive(false);
            }
        }
        else
        {
            if (inGame.hero.controlledObject != null)
            {
                inGame.hero.controlledObject.SetActive(true);
            }
            inGame.hero.controller.position = new Vector3(0f, 0f, mArenaBridgeZ - 165f);
            inGame.hero.controller.facing = FacingType.Right;
        }
    }

    private void DestroyAllNonBaseCharacters(bool destroySubjectAlso)
    {
        CharactersManager cm = WeakGlobalInstance<CharactersManager>.instance;
        if (cm == null) return;
        List<Character> alive = cm.allAlive;
        for (int i = alive.Count - 1; i >= 0; i--)
        {
            Character c = alive[i];
            if (c == null || c.isBase || c.isPlayer) continue;
            if (!destroySubjectAlso && c == mActiveSubject) continue;
            if (!destroySubjectAlso && mTerasuCtrl != null && c == mTerasuCtrl.ownerCharacter) continue;
            cm.DestroyCharacter(c);
        }
        if (destroySubjectAlso)
        {
            mActiveSubject = null;
            mTerasuCtrl = null;
            mSpawnedUnits.Clear();
        }
        else
        {
            for (int i = mSpawnedUnits.Count - 1; i >= 0; i--)
            {
                if (mSpawnedUnits[i].character != mActiveSubject)
                {
                    mSpawnedUnits.RemoveAt(i);
                }
            }
        }
    }

    private void ClearOpponentsOnly()
    {
        CharactersManager cm = WeakGlobalInstance<CharactersManager>.instance;
        if (cm == null) return;
        for (int i = mSpawnedUnits.Count - 1; i >= 0; i--)
        {
            TrackedDummy td = mSpawnedUnits[i];
            if (td != null && td.character != null && td.character != mActiveSubject)
            {
                cm.DestroyCharacter(td.character);
            }
            if (td == null || td.character != mActiveSubject)
            {
                mSpawnedUnits.RemoveAt(i);
            }
        }
        List<Character> alive = cm.allAlive;
        for (int i = alive.Count - 1; i >= 0; i--)
        {
            Character c = alive[i];
            if (c == null || c.isBase || c.isPlayer) continue;
            if (c == mActiveSubject) continue;
            if (mTerasuCtrl != null && c == mTerasuCtrl.ownerCharacter) continue;
            cm.DestroyCharacter(c);
        }
        SetStatus("Tum rakipler ve kuklalar temizlendi.");
    }

    private void SpawnTerasuPhase2(bool cinematicPhase1)
    {
        WaveManager wm = WeakGlobalInstance<WaveManager>.instance;
        CharactersManager cm = WeakGlobalInstance<CharactersManager>.instance;
        if (wm == null || cm == null) return;

        // Destroy previous subject cleanly
        if (mTerasuCtrl != null && mTerasuCtrl.ownerCharacter != null)
        {
            cm.DestroyCharacter(mTerasuCtrl.ownerCharacter);
        }
        else if (mActiveSubject != null)
        {
            cm.DestroyCharacter(mActiveSubject);
        }

        Vector3 spawnPos = new Vector3(0f, 0f, mArenaBridgeZ);
        Enemy shogun = wm.ConstructEnemyAt("Shogun", spawnPos, false);
        if (shogun == null)
        {
            SetStatus("HATA: Shogun spawn edilemedi!");
            return;
        }

        shogun.maxHealth = 999999f;
        shogun.health = 999999f;
        shogun.meleeDamage = 250f;
        if (shogun.controller != null)
        {
            shogun.controller.constraintLeft = mArenaMinZ + 40f;
            shogun.controller.constraintRight = mArenaMaxZ - 40f;
            shogun.controller.StopWalking();
            shogun.controller.facing = FacingType.Left;
        }

        mTerasuCtrl = BloodMoonTerasuController.GetOrAttach(shogun);
        mButcherCtrl = null;
        mActiveSubject = shogun;
        mWalkToggleDir = 0;

        if (cinematicPhase1)
        {
            mActiveSubjectLabel = "Shogun -> Faz 2 Dirilis";
            mTerasuCtrl.labManualMode = false;
            shogun.health = 10f;
            shogun.RecievedAttack(EAttackType.BladeCritical, 500f);
            SetStatus("Faz 1 -> Faz 2 Tam Sinematik Dirilis baslatildi!");
        }
        else
        {
            mActiveSubjectLabel = "Blood Moon Terasu (Faz 2)";
            mTerasuCtrl.ForceInstantPhase2();
            mTerasuCtrl.labManualMode = true;
            SetStatus("Blood Moon Terasu (Faz 2) hazir - Manuel Kontrol aktif.");
        }
    }

    private void SpawnSubjectToInspect(string unitId, bool isHelperUnit)
    {
        CharactersManager cm = WeakGlobalInstance<CharactersManager>.instance;
        WaveManager wm = WeakGlobalInstance<WaveManager>.instance;
        Smithy smithy = WeakGlobalInstance<Smithy>.instance;
        if (cm == null) return;

        // Remove previous main subject
        if (mActiveSubject != null)
        {
            cm.DestroyCharacter(mActiveSubject);
            mActiveSubject = null;
        }
        mTerasuCtrl = null;
        mButcherCtrl = null;
        mWalkToggleDir = 0;

        Vector3 spawnPos = new Vector3(0f, 0f, mArenaBridgeZ);
        Character c = null;
        if (isHelperUnit)
        {
            if (smithy != null)
            {
                c = smithy.ForceSpawnAt(unitId, spawnPos, false);
            }
        }
        else
        {
            if (wm != null)
            {
                Enemy e = wm.ConstructEnemyAt(unitId, spawnPos, false);
                if (e != null && string.Equals(unitId, "Shogun", StringComparison.OrdinalIgnoreCase))
                {
                    BloodMoonTerasuController ctrl = e.controlledObject != null ? e.controlledObject.GetComponent<BloodMoonTerasuController>() : null;
                    if (ctrl != null)
                    {
                        Destroy(ctrl); // Keep raw Phase 1 Shogun intact when user explicitly chooses Phase 1 Shogun!
                    }
                }
                c = e;
            }
        }

        if (c == null)
        {
            SetStatus("Spawn basarisiz: " + unitId);
            return;
        }

        c.maxHealth = 999999f;
        c.health = 999999f;
        if (c.controller != null)
        {
            c.controller.constraintLeft = mArenaMinZ + 40f;
            c.controller.constraintRight = mArenaMaxZ - 40f;
            c.controller.StopWalking();
            c.controller.facing = isHelperUnit ? FacingType.Right : FacingType.Left;
        }

        mActiveSubject = c;
        mButcherCtrl = (c.controlledObject != null) ? c.controlledObject.GetComponent<IronButcherController>() : null;
        if (mButcherCtrl != null)
        {
            mButcherCtrl.Bind(c);
        }
        mActiveSubjectLabel = unitId + (isHelperUnit ? " (Muttefik)" : " (Dusman)");
        if (mButcherCtrl != null)
        {
            SetStatus("Demir Kasap Zombi hazir! A/D: Yuru | 3: Satir Kombo | Space: Dondur | 9: Rakip ekle");
        }
        else
        {
            SetStatus("Incelenen karakter: " + mActiveSubjectLabel + " | A/D: Yurut | 9: Rakip ekle");
        }
    }

    private void SpawnOpponentUnit(string unitId, bool isHelperPrefab, float distanceFromSubject, float xJitter)
    {
        WaveManager wm = WeakGlobalInstance<WaveManager>.instance;
        Smithy smithy = WeakGlobalInstance<Smithy>.instance;
        CharactersManager cm = WeakGlobalInstance<CharactersManager>.instance;
        if (cm == null) return;

        bool subjectIsEnemy = (mActiveSubject == null) || mActiveSubject.isEnemy;
        float facingSign = (mActiveSubject != null && mActiveSubject.controller != null && mActiveSubject.controller.facing == FacingType.Right) ? 1f : -1f;
        Vector3 anchorPos = (mActiveSubject != null) ? mActiveSubject.position : new Vector3(0f, 0f, mArenaBridgeZ);
        float spawnZ = Mathf.Clamp(anchorPos.z + facingSign * distanceFromSubject, mArenaMinZ + 30f, mArenaMaxZ - 30f);
        Vector3 spawnPos = new Vector3(anchorPos.x + xJitter, 0f, spawnZ);

        Character opp = null;
        bool wantOpponentAsAllyTeam = subjectIsEnemy; // Opposing team!
        if (isHelperPrefab && smithy != null)
        {
            opp = smithy.ForceSpawnAt(unitId, spawnPos, !wantOpponentAsAllyTeam);
        }
        else if (wm != null)
        {
            Enemy e = wm.ConstructEnemyAt(unitId, spawnPos, wantOpponentAsAllyTeam);
            if (e != null && string.Equals(unitId, "Shogun", StringComparison.OrdinalIgnoreCase))
            {
                BloodMoonTerasuController extraCtrl = e.controlledObject != null ? e.controlledObject.GetComponent<BloodMoonTerasuController>() : null;
                if (extraCtrl != null) Destroy(extraCtrl);
            }
            opp = e;
        }

        if (opp == null)
        {
            SetStatus("Rakip spawn edilemedi: " + unitId);
            return;
        }

        TrackedDummy td = new TrackedDummy();
        td.character = opp;
        td.mode = mDummySpawnBehavior;
        td.label = unitId;
        td.baseSpeed = (opp.controller != null) ? Mathf.Max(opp.controller.speed, 65f) : 65f;
        td.baseMeleeRange = opp.meleeAttackRange;
        td.baseBowRange = opp.bowAttackRange;

        if (opp.controller != null)
        {
            opp.controller.constraintLeft = mArenaMinZ + 20f;
            opp.controller.constraintRight = mArenaMaxZ - 20f;
            opp.controller.facing = (facingSign < 0f) ? FacingType.Right : FacingType.Left;
        }
        ApplyDummyBehavior(td);
        mSpawnedUnits.Add(td);
        SetStatus("Rakip eklendi: " + unitId + " (" + GetDummyModeTr(mDummySpawnBehavior) + ")");
    }

    private void ApplyDummyBehavior(TrackedDummy td)
    {
        if (td == null || td.character == null) return;
        Character c = td.character;
        if (td.mode == DummyBehaviorMode.ImmortalStationary)
        {
            c.maxHealth = 999999f;
            c.health = 999999f;
            c.meleeAttackRange = 0f;
            c.bowAttackRange = 0f;
            c.knockbackResistance = 1000;
            if (c.controller != null)
            {
                c.controller.speed = 0f;
                c.controller.StopWalking();
                c.controller.Idle();
            }
        }
        else if (td.mode == DummyBehaviorMode.ImmortalActiveFighter)
        {
            c.maxHealth = 999999f;
            c.health = 999999f;
            c.meleeAttackRange = td.baseMeleeRange;
            c.bowAttackRange = td.baseBowRange;
            if (c.controller != null)
            {
                c.controller.speed = td.baseSpeed;
            }
        }
        else
        {
            c.maxHealth = Mathf.Min(c.maxHealth, 1200f);
            c.health = c.maxHealth;
            c.meleeAttackRange = td.baseMeleeRange;
            c.bowAttackRange = td.baseBowRange;
            if (c.controller != null)
            {
                c.controller.speed = td.baseSpeed;
            }
        }
    }

    private void SpawnPresetPair()
    {
        SpawnOpponentUnit("HeavySamurai", true, 130f, -18f);
        SpawnOpponentUnit("SamuraiArcher", true, 265f, 18f);
    }

    private void Update()
    {
        if (!Application.isPlaying) return;

        IsLabActive = true;
        IsLabCameraActive = true;
        IsLabWavePaused = true;

        if (!mInitialized)
        {
            mInitDelay -= Time.unscaledDeltaTime;
            if (mInitDelay <= 0f)
            {
                InitializeArenaIfReady();
            }
            return;
        }

        if (mStatusTimer > 0f)
        {
            mStatusTimer -= Time.unscaledDeltaTime;
        }

        // Prevent accidental pause/bell popups in Lab
        if (WeakGlobalInstance<DialogHandler>.instance != null)
        {
            WeakGlobalInstance<DialogHandler>.instance.Destroy();
        }
        Time.timeScale = Mathf.Clamp(mLabTimeScale, 0.01f, 2.5f);

        UpdateActiveSubjectAndDummies();
        HandleCameraInput();
        HandleLabHotkeys();
    }

    private void UpdateActiveSubjectAndDummies()
    {
        // Keep active subject healthy & inside arena bounds
        if (mActiveSubject != null)
        {
            if (mActiveSubject.health <= 0f)
            {
                mActiveSubject.health = mActiveSubject.maxHealth;
            }
            else if (mActiveSubject.health < mActiveSubject.maxHealth * 0.25f)
            {
                mActiveSubject.health = mActiveSubject.maxHealth;
            }
        }

        if (mTerasuCtrl != null && mActiveSubject != null && mActiveSubject.controller != null && mTerasuCtrl.labManualMode)
        {
            // Manual horizontal locomotion via A/D or toggle button '2'
            int keyDir = 0;
            if (Input.GetKey(KeyCode.A)) keyDir = -1;
            else if (Input.GetKey(KeyCode.D)) keyDir = 1;
            else if (mWalkToggleDir != 0) keyDir = mWalkToggleDir;

            if (keyDir != 0 && !mTerasuCtrl.labFreezeClip && string.IsNullOrEmpty(mTerasuCtrl.labForcedClip))
            {
                if (keyDir < 0)
                {
                    if (mActiveSubject.controller.facing != FacingType.Left)
                    {
                        mActiveSubject.controller.facing = FacingType.Left;
                    }
                    mActiveSubject.controller.StartWalkLeft();
                }
                else
                {
                    if (mActiveSubject.controller.facing != FacingType.Right)
                    {
                        mActiveSubject.controller.facing = FacingType.Right;
                    }
                    mActiveSubject.controller.StartWalkRight();
                }
            }
            else if (mActiveSubject.controller.isMoving)
            {
                mActiveSubject.controller.StopWalking();
            }
        }
        else if (mActiveSubject != null && mTerasuCtrl == null && mActiveSubject.controller != null)
        {
            // Non-Terasu inspected character manual walk (A / D or toggle 2)
            int keyDir = 0;
            if (Input.GetKey(KeyCode.A)) keyDir = -1;
            else if (Input.GetKey(KeyCode.D)) keyDir = 1;
            else if (mWalkToggleDir != 0) keyDir = mWalkToggleDir;

            if (keyDir < 0)
            {
                mActiveSubject.controller.facing = FacingType.Left;
                mActiveSubject.controller.StartWalkLeft();
            }
            else if (keyDir > 0)
            {
                mActiveSubject.controller.facing = FacingType.Right;
                mActiveSubject.controller.StartWalkRight();
            }
        }

        // Maintain dummies
        for (int i = mSpawnedUnits.Count - 1; i >= 0; i--)
        {
            TrackedDummy td = mSpawnedUnits[i];
            if (td == null || td.character == null)
            {
                mSpawnedUnits.RemoveAt(i);
                continue;
            }
            if (td.mode == DummyBehaviorMode.ImmortalStationary)
            {
                td.character.health = td.character.maxHealth;
                if (td.character.controller != null)
                {
                    td.character.controller.StopWalking();
                    td.character.controller.speed = 0f;
                }
            }
            else if (td.mode == DummyBehaviorMode.ImmortalActiveFighter)
            {
                if (td.character.health < td.character.maxHealth * 0.5f)
                {
                    td.character.health = td.character.maxHealth;
                }
            }
        }
    }

    private void HandleCameraInput()
    {
        float dt = Time.unscaledDeltaTime;

        // Scroll zoom
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.001f)
        {
            mCamDistance = Mathf.Clamp(mCamDistance - scroll * 240f, 85f, 1200f);
        }
        if (Input.GetKey(KeyCode.Equals) || Input.GetKey(KeyCode.KeypadPlus))
        {
            mCamDistance = Mathf.Clamp(mCamDistance - 320f * dt, 85f, 1200f);
        }
        if (Input.GetKey(KeyCode.Minus) || Input.GetKey(KeyCode.KeypadMinus))
        {
            mCamDistance = Mathf.Clamp(mCamDistance + 320f * dt, 85f, 1200f);
        }

        // Keyboard orbit Q/E (Yaw) and R/F (Pitch)
        if (Input.GetKey(KeyCode.Q))
        {
            mCamMode = LabCameraMode.Orbit360;
            mCamYaw -= 95f * dt;
        }
        if (Input.GetKey(KeyCode.E))
        {
            mCamMode = LabCameraMode.Orbit360;
            mCamYaw += 95f * dt;
        }
        if (Input.GetKey(KeyCode.R))
        {
            mCamMode = LabCameraMode.Orbit360;
            mCamPitch = Mathf.Clamp(mCamPitch + 55f * dt, -25f, 75f);
        }
        if (Input.GetKey(KeyCode.F))
        {
            mCamMode = LabCameraMode.Orbit360;
            mCamPitch = Mathf.Clamp(mCamPitch - 55f * dt, -25f, 75f);
        }

        // Right-Mouse Drag: 360 Orbit!
        if (Input.GetMouseButtonDown(1))
        {
            mIsOrbitDragging = true;
            mLastMousePos = Input.mousePosition;
        }
        if (Input.GetMouseButtonUp(1))
        {
            mIsOrbitDragging = false;
        }
        if (mIsOrbitDragging && Input.GetMouseButton(1))
        {
            Vector3 delta = Input.mousePosition - mLastMousePos;
            mLastMousePos = Input.mousePosition;
            if (delta.sqrMagnitude > 0.01f)
            {
                mCamMode = LabCameraMode.Orbit360;
                mCamYaw += delta.x * 0.42f;
                mCamPitch = Mathf.Clamp(mCamPitch - delta.y * 0.34f, -25f, 75f);
            }
        }

        // Middle-Mouse Drag or Up/Down/Left/Right arrows: camera target height / Z pan
        if (Input.GetMouseButtonDown(2))
        {
            mIsPanDragging = true;
            mLastMousePos = Input.mousePosition;
        }
        if (Input.GetMouseButtonUp(2))
        {
            mIsPanDragging = false;
        }
        if (mIsPanDragging && Input.GetMouseButton(2))
        {
            Vector3 delta = Input.mousePosition - mLastMousePos;
            mLastMousePos = Input.mousePosition;
            mCamHeightOffset = Mathf.Clamp(mCamHeightOffset - delta.y * 0.45f, 10f, 220f);
            mCamPanZ = Mathf.Clamp(mCamPanZ - delta.x * 0.65f, -350f, 350f);
        }

        if (mCamMode == LabCameraMode.TurntableAutoSpin)
        {
            mCamYaw += 36f * dt;
        }
    }

    private void HandleLabHotkeys()
    {
        if (Input.GetKeyDown(KeyCode.F1) || Input.GetKeyDown(KeyCode.H))
        {
            mHudVisible = !mHudVisible;
        }
        if (Input.GetKeyDown(KeyCode.F12) || Input.GetKeyDown(KeyCode.P))
        {
            CaptureLabScreenshot();
        }
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            ActionIdleStance();
        }
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            ActionToggleWalk();
        }
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            ActionMeleeAttack(false);
        }
        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            ActionMeleeAttack(true);
        }
        if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            ActionSpearThrowUlt();
        }
        if (Input.GetKeyDown(KeyCode.Alpha6))
        {
            ActionSixthWheelThanosUlt();
        }
        if (Input.GetKeyDown(KeyCode.Alpha7))
        {
            ActionStepWheel();
        }
        if (Input.GetKeyDown(KeyCode.Alpha8))
        {
            SpawnTerasuPhase2(true);
        }
        if (Input.GetKeyDown(KeyCode.Alpha9))
        {
            SpawnPresetPair();
        }
        if (Input.GetKeyDown(KeyCode.Alpha0))
        {
            ClearOpponentsOnly();
        }
        if (Input.GetKeyDown(KeyCode.Space))
        {
            ToggleFreezeFrame();
        }
        if (Input.GetKeyDown(KeyCode.Comma) && mTerasuCtrl != null)
        {
            mTerasuCtrl.labFreezeClip = true;
            mTerasuCtrl.labForcedNormalizedTime = Mathf.Repeat(mTerasuCtrl.labForcedNormalizedTime - 0.025f, 1.0f);
        }
        else if (Input.GetKeyDown(KeyCode.Comma) && mButcherCtrl != null)
        {
            mButcherCtrl.labManualMode = true;
            mButcherCtrl.labFreezeClip = true;
            mButcherCtrl.labForcedNormalizedTime = Mathf.Repeat(mButcherCtrl.labForcedNormalizedTime - 0.025f, 1.0f);
        }
        if (Input.GetKeyDown(KeyCode.Period) && mTerasuCtrl != null)
        {
            mTerasuCtrl.labFreezeClip = true;
            mTerasuCtrl.labForcedNormalizedTime = Mathf.Repeat(mTerasuCtrl.labForcedNormalizedTime + 0.025f, 1.0f);
        }
        else if (Input.GetKeyDown(KeyCode.Period) && mButcherCtrl != null)
        {
            mButcherCtrl.labManualMode = true;
            mButcherCtrl.labFreezeClip = true;
            mButcherCtrl.labForcedNormalizedTime = Mathf.Repeat(mButcherCtrl.labForcedNormalizedTime + 0.025f, 1.0f);
        }
    }

    private void ActionIdleStance()
    {
        mWalkToggleDir = 0;
        if (mActiveSubject != null && mActiveSubject.controller != null)
        {
            mActiveSubject.controller.StopWalking();
            mActiveSubject.controller.Idle();
        }
        if (mTerasuCtrl != null)
        {
            mTerasuCtrl.ForceInstantPhase2();
            mTerasuCtrl.labManualMode = true;
            mTerasuCtrl.labFreezeClip = false;
            mTerasuCtrl.labForcedClip = string.Empty;
        }
        if (mButcherCtrl != null)
        {
            mButcherCtrl.PlayManualAction("Combat_Stance", 0f, false);
        }
        SetStatus("[1] Durus / Suzulme (Combat_Stance) aktif.");
    }

    private void ActionToggleWalk()
    {
        if (mTerasuCtrl != null)
        {
            mTerasuCtrl.ForceInstantPhase2();
            mTerasuCtrl.labManualMode = true;
            mTerasuCtrl.labFreezeClip = false;
            mTerasuCtrl.labForcedClip = string.Empty;
        }
        if (mButcherCtrl != null)
        {
            mButcherCtrl.labManualMode = false;
            mButcherCtrl.labFreezeClip = false;
            mButcherCtrl.labForcedClip = string.Empty;
        }
        if (mWalkToggleDir == 0)
        {
            bool faceRight = (mActiveSubject != null && mActiveSubject.controller != null && mActiveSubject.controller.facing == FacingType.Right);
            mWalkToggleDir = faceRight ? 1 : -1;
            SetStatus("[2] Ucus / Yuru basladi (" + (mWalkToggleDir < 0 ? "Sol" : "Sag") + "). Durmak icin tekrar 2'ye bas.");
        }
        else
        {
            mWalkToggleDir = 0;
            if (mActiveSubject != null && mActiveSubject.controller != null)
            {
                mActiveSubject.controller.StopWalking();
            }
            SetStatus("[2] Yuru / Ucus durduruldu.");
        }
    }

    private void ActionMeleeAttack(bool sweep)
    {
        mWalkToggleDir = 0;
        if (mTerasuCtrl != null)
        {
            mTerasuCtrl.ForceInstantPhase2();
            mTerasuCtrl.labManualMode = true;
            mTerasuCtrl.TriggerManualMeleeAttack(sweep);
            SetStatus(sweep ? "[4] Vurus 2: Genis Mizrak Savurma (Spear_Sky_Slam)" : "[3] Vurus 1: Mizrak Saplama (Double_Combo_Attack)");
        }
        else if (mButcherCtrl != null)
        {
            mButcherCtrl.TriggerManualAttack(sweep ? 2 : -1);
            if (mActiveSubject != null && mActiveSubject.controller != null)
            {
                mActiveSubject.controller.Attack(1.1f);
            }
            SetStatus(sweep ? "[4] Demir Kasap Satir Kombo (Double_Combo_Attack)" : "[3] Demir Kasap Agir Satir Vurusu");
        }
        else if (mActiveSubject != null && mActiveSubject.controller != null)
        {
            mActiveSubject.controller.Attack(1.2f);
            SetStatus("Standart karakter saldirisi tetiklendi.");
        }
    }

    private void ActionSpearThrowUlt()
    {
        mWalkToggleDir = 0;
        if (mTerasuCtrl == null)
        {
            SetStatus("Bu yetenek Terasu (Faz 2) icindir.");
            return;
        }
        mTerasuCtrl.ForceInstantPhase2();
        mTerasuCtrl.labManualMode = true;
        mTerasuCtrl.labFreezeClip = false;
        mTerasuCtrl.labForcedClip = string.Empty;

        // Auto-spawn a target if none exists so spear impale & magnet pull always demonstrate clearly!
        if (CountAliveOpponents() == 0)
        {
            SpawnOpponentUnit("SamuraiArcher", true, 240f, 0f);
        }
        mTerasuCtrl.TriggerSpearUltimateNow();
        SetStatus("[5] Mizrak Firlatma -> Saplama -> El Kaldirip Miknatisla Geri Cekme tetiklendi!");
    }

    private void ActionSixthWheelThanosUlt()
    {
        mWalkToggleDir = 0;
        if (mTerasuCtrl == null)
        {
            SetStatus("Bu yetenek Terasu (Faz 2) icindir.");
            return;
        }
        mTerasuCtrl.ForceInstantPhase2();
        mTerasuCtrl.labManualMode = true;
        mTerasuCtrl.labFreezeClip = false;
        mTerasuCtrl.labForcedClip = string.Empty;
        if (CountAliveOpponents() == 0)
        {
            SpawnOpponentUnit("HeavySamurai", true, 160f, 0f);
        }
        mTerasuCtrl.TriggerSixthWheelUltNow();
        SetStatus("[6] 6. Halka Donmesi Parlamasi + Thanos Yumruk Parcacik Sok Dalgasi Ultisi!");
    }

    private void ActionStepWheel()
    {
        if (mTerasuCtrl == null) return;
        mTerasuCtrl.ForceInstantPhase2();
        mTerasuCtrl.StepHaloWheelOnce();
        SetStatus("[7] Arka halka +60 derece donduruldu (" + mTerasuCtrl.mahoragaStacks + "/6).");
    }

    public IronButcherController InspectIronButcherPreset(string optionalClip)
    {
        InitializeArenaIfReady();
        ClearOpponentsOnly();
        SpawnSubjectToInspect("Iron_Butcher", false);
        SpawnOpponentUnit("HeavySamurai", true, 128f, -10f);
        if (mButcherCtrl != null && !string.IsNullOrEmpty(optionalClip))
        {
            mButcherCtrl.PlayManualAction(optionalClip, 0.35f, false);
        }
        return mButcherCtrl;
    }

    private void ToggleFreezeFrame()
    {
        if (mTerasuCtrl != null)
        {
            mTerasuCtrl.labFreezeClip = !mTerasuCtrl.labFreezeClip;
            if (mTerasuCtrl.labFreezeClip && mTerasuCtrl.terasuAnimator != null)
            {
                AnimatorStateInfo st = mTerasuCtrl.terasuAnimator.GetCurrentAnimatorStateInfo(0);
                mTerasuCtrl.labForcedNormalizedTime = Mathf.Repeat(st.normalizedTime, 1.0f);
            }
            SetStatus(mTerasuCtrl.labFreezeClip ? "[Space] Kare donduruldu! ',' ve '.' ile kare ilerlet/gerilet." : "[Space] Animasyon tekrar akisa devam ediyor.");
            return;
        }
        if (mButcherCtrl != null)
        {
            mButcherCtrl.labManualMode = true;
            if (string.IsNullOrEmpty(mButcherCtrl.labForcedClip))
            {
                mButcherCtrl.labForcedClip = string.IsNullOrEmpty(mButcherCtrl.currentVisualState) ? "Combat_Stance" : mButcherCtrl.currentVisualState;
            }
            mButcherCtrl.labFreezeClip = !mButcherCtrl.labFreezeClip;
            if (mButcherCtrl.labFreezeClip && mButcherCtrl.butcherAnimator != null)
            {
                AnimatorStateInfo st = mButcherCtrl.butcherAnimator.GetCurrentAnimatorStateInfo(0);
                mButcherCtrl.labForcedNormalizedTime = Mathf.Repeat(st.normalizedTime, 1.0f);
            }
            SetStatus(mButcherCtrl.labFreezeClip ? "[Space] Kasap karesi donduruldu! ',' ve '.' ile ilerlet." : "[Space] Kasap animasyonu tekrar akisa devam ediyor.");
        }
    }

    private int CountAliveOpponents()
    {
        CharactersManager cm = WeakGlobalInstance<CharactersManager>.instance;
        if (cm == null || mActiveSubject == null) return 0;
        int cnt = 0;
        List<Character> alive = cm.allAlive;
        for (int i = 0; i < alive.Count; i++)
        {
            Character c = alive[i];
            if (c != null && !c.isBase && !c.isPlayer && c.health > 0f && c.isEnemy != mActiveSubject.isEnemy)
            {
                cnt++;
            }
        }
        return cnt;
    }

    private void CaptureLabScreenshot()
    {
        try
        {
            string dir = Path.Combine(Directory.GetCurrentDirectory(), "Screenshots");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, "SVZ_Lab_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
            ScreenCapture.CaptureScreenshot(file);
            SetStatus("Ekran goruntusu kaydedildi: " + file);
            Debug.Log("[CharacterLab] Captured screenshot -> " + file);
        }
        catch (Exception ex)
        {
            SetStatus("Ekran goruntusu hatasi: " + ex.Message);
        }
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying || Camera.main == null || !mInitialized)
        {
            return;
        }

        Camera cam = Camera.main;
        Vector3 rawFocus = (mActiveSubject != null) ? mActiveSubject.position : new Vector3(0f, 0f, mArenaBridgeZ);
        if (!mFocusPosValid)
        {
            mSmoothedFocusPos = rawFocus;
            mFocusPosValid = true;
        }
        else
        {
            mSmoothedFocusPos = Vector3.Lerp(mSmoothedFocusPos, rawFocus, Time.unscaledDeltaTime * 9f);
        }

        float effYaw = mCamYaw;
        float effPitch = mCamPitch;
        float effDist = mCamDistance;
        float effHeight = mCamHeightOffset;
        float effPanZ = mCamPanZ;
        Vector3 customTarget = mSmoothedFocusPos;

        float facingSign = (mActiveSubject != null && mActiveSubject.controller != null && mActiveSubject.controller.facing == FacingType.Right) ? 1f : -1f;

        switch (mCamMode)
        {
            case LabCameraMode.SideGameView:
                effYaw = 90f;
                effPitch = 13f;
                effDist = 560f;
                effHeight = 72f;
                effPanZ = facingSign * 35f;
                break;

            case LabCameraMode.CloseRightHandSpear:
                effYaw = (facingSign < 0f) ? 62f : 118f;
                effPitch = 10f;
                effDist = 205f;
                effHeight = 84f;
                effPanZ = facingSign * 22f;
                break;

            case LabCameraMode.CloseLeftHandGauntlet:
                effYaw = (facingSign < 0f) ? 116f : 64f;
                effPitch = 8f;
                effDist = 195f;
                effHeight = 82f;
                effPanZ = facingSign * 14f;
                break;

            case LabCameraMode.CloseBackHalo:
                effYaw = (facingSign < 0f) ? 20f : 160f;
                effPitch = 14f;
                effDist = 250f;
                effHeight = 96f;
                effPanZ = -facingSign * 18f;
                break;

            case LabCameraMode.WideUltArena:
                effYaw = 90f;
                effPitch = 15f;
                effDist = 720f;
                effHeight = 76f;
                effPanZ = facingSign * 125f;
                break;
        }

        Vector3 lookTarget = new Vector3(customTarget.x, customTarget.y + effHeight, customTarget.z + effPanZ);
        float yawRad = effYaw * Mathf.Deg2Rad;
        float pitchRad = effPitch * Mathf.Deg2Rad;
        float horizDist = Mathf.Cos(pitchRad) * effDist;
        float camX = lookTarget.x + Mathf.Sin(yawRad) * horizDist;
        float camZ = lookTarget.z + Mathf.Cos(yawRad) * horizDist;
        float camY = lookTarget.y + Mathf.Sin(pitchRad) * effDist;

        Vector3 shake = (mTerasuCtrl != null) ? mTerasuCtrl.cameraShakeOffset : Vector3.zero;
        cam.transform.position = new Vector3(camX, camY, camZ) + shake;
        cam.transform.LookAt(lookTarget);

        if (shake.sqrMagnitude > 0.1f)
        {
            float roll = Mathf.Clamp(shake.x * 0.22f + shake.y * 0.18f, -4.5f, 4.5f);
            cam.transform.Rotate(0f, 0f, roll, Space.Self);
        }
    }

    private void SetStatus(string msg)
    {
        mStatusBanner = msg;
        mStatusTimer = 5.0f;
    }

    private static string GetDummyModeTr(DummyBehaviorMode mode)
    {
        if (mode == DummyBehaviorMode.ImmortalStationary) return "Olumsuz Sabit Kukla";
        if (mode == DummyBehaviorMode.ImmortalActiveFighter) return "Olumsuz Saldiran";
        return "Normal Olumlu";
    }

    private void EnsureStyles()
    {
        if (mBgDark != null) return;
        mBgDark = MakeSolidTex(new Color(0.07f, 0.06f, 0.09f, 0.88f));
        mBgHeader = MakeSolidTex(new Color(0.14f, 0.04f, 0.07f, 0.94f));
        mBgTabActive = MakeSolidTex(new Color(0.68f, 0.12f, 0.14f, 0.96f));
        mBgTabIdle = MakeSolidTex(new Color(0.16f, 0.14f, 0.19f, 0.92f));
        mBgButton = MakeSolidTex(new Color(0.20f, 0.18f, 0.24f, 0.94f));
        mBgButtonActive = MakeSolidTex(new Color(0.58f, 0.24f, 0.10f, 0.98f));
        mBgAccentRed = MakeSolidTex(new Color(0.72f, 0.11f, 0.13f, 0.96f));

        mPanelStyle = new GUIStyle(GUI.skin.box);
        mPanelStyle.normal.background = mBgDark;
        mPanelStyle.padding = new RectOffset(8, 8, 8, 8);

        mHeaderStyle = new GUIStyle(GUI.skin.box);
        mHeaderStyle.normal.background = mBgHeader;

        mTitleStyle = new GUIStyle(GUI.skin.label);
        mTitleStyle.fontSize = 13;
        mTitleStyle.fontStyle = FontStyle.Bold;
        mTitleStyle.normal.textColor = new Color(1.0f, 0.86f, 0.62f, 1f);

        mLabelStyle = new GUIStyle(GUI.skin.label);
        mLabelStyle.fontSize = 11;
        mLabelStyle.normal.textColor = new Color(0.93f, 0.93f, 0.96f, 1f);
        mLabelStyle.wordWrap = true;

        mSmallLabelStyle = new GUIStyle(GUI.skin.label);
        mSmallLabelStyle.fontSize = 10;
        mSmallLabelStyle.normal.textColor = new Color(0.78f, 0.80f, 0.86f, 1f);
        mSmallLabelStyle.wordWrap = true;

        mButtonStyle = new GUIStyle(GUI.skin.button);
        mButtonStyle.fontSize = 11;
        mButtonStyle.normal.background = mBgButton;
        mButtonStyle.hover.background = mBgTabActive;
        mButtonStyle.normal.textColor = Color.white;
        mButtonStyle.alignment = TextAnchor.MiddleCenter;

        mActiveButtonStyle = new GUIStyle(mButtonStyle);
        mActiveButtonStyle.fontStyle = FontStyle.Bold;
        mActiveButtonStyle.normal.background = mBgButtonActive;
        mActiveButtonStyle.normal.textColor = new Color(1.0f, 0.95f, 0.78f, 1f);

        mAccentButtonStyle = new GUIStyle(mButtonStyle);
        mAccentButtonStyle.fontStyle = FontStyle.Bold;
        mAccentButtonStyle.normal.background = mBgAccentRed;
        mAccentButtonStyle.normal.textColor = Color.white;
    }

    private static Texture2D MakeSolidTex(Color c)
    {
        Texture2D t = new Texture2D(1, 1);
        t.SetPixel(0, 0, c);
        t.Apply();
        return t;
    }

    private void OnGUI()
    {
        if (!Application.isPlaying) return;
        EnsureStyles();
        GUI.depth = -30;

        if (!mHudVisible)
        {
            if (GUI.Button(new Rect(12f, 12f, 185f, 30f), "[F1 / H] Laboratuvar Menusu Ac", mAccentButtonStyle))
            {
                mHudVisible = true;
            }
            return;
        }

        // Top Studio Toolbar
        float topW = Mathf.Min(Screen.width - 24f, 1180f);
        GUI.Box(new Rect(10f, 8f, topW, 46f), GUIContent.none, mHeaderStyle);
        GUILayout.BeginArea(new Rect(16f, 12f, topW - 12f, 40f));
        GUILayout.BeginHorizontal();

        GUILayout.Label("SVZ TEST LAB", mTitleStyle, GUILayout.Width(102f));

        string clipInfo = (mTerasuCtrl != null)
            ? (mTerasuCtrl.lastPlayedClip + " | Halka: " + mTerasuCtrl.mahoragaStacks + "/6")
            : mActiveSubjectLabel;
        GUILayout.Label(clipInfo, mSmallLabelStyle, GUILayout.Width(210f));

        // Quick Camera Preset Bar
        if (GUILayout.Button("360", mCamMode == LabCameraMode.Orbit360 ? mActiveButtonStyle : mButtonStyle, GUILayout.Width(40f), GUILayout.Height(28f))) mCamMode = LabCameraMode.Orbit360;
        if (GUILayout.Button("Yan", mCamMode == LabCameraMode.SideGameView ? mActiveButtonStyle : mButtonStyle, GUILayout.Width(38f), GUILayout.Height(28f))) mCamMode = LabCameraMode.SideGameView;
        if (GUILayout.Button("SagEl", mCamMode == LabCameraMode.CloseRightHandSpear ? mActiveButtonStyle : mButtonStyle, GUILayout.Width(48f), GUILayout.Height(28f))) mCamMode = LabCameraMode.CloseRightHandSpear;
        if (GUILayout.Button("SolEl", mCamMode == LabCameraMode.CloseLeftHandGauntlet ? mActiveButtonStyle : mButtonStyle, GUILayout.Width(46f), GUILayout.Height(28f))) mCamMode = LabCameraMode.CloseLeftHandGauntlet;
        if (GUILayout.Button("Halka", mCamMode == LabCameraMode.CloseBackHalo ? mActiveButtonStyle : mButtonStyle, GUILayout.Width(48f), GUILayout.Height(28f))) mCamMode = LabCameraMode.CloseBackHalo;
        if (GUILayout.Button("Genis", mCamMode == LabCameraMode.WideUltArena ? mActiveButtonStyle : mButtonStyle, GUILayout.Width(48f), GUILayout.Height(28f))) mCamMode = LabCameraMode.WideUltArena;
        if (GUILayout.Button("Don", mCamMode == LabCameraMode.TurntableAutoSpin ? mActiveButtonStyle : mButtonStyle, GUILayout.Width(38f), GUILayout.Height(28f))) mCamMode = LabCameraMode.TurntableAutoSpin;

        GUILayout.Space(8f);
        GUILayout.Label("Hiz:" + mLabTimeScale.ToString("F2") + "x", mSmallLabelStyle, GUILayout.Width(64f));
        mLabTimeScale = GUILayout.HorizontalSlider(mLabTimeScale, 0.05f, 2.0f, GUILayout.Width(80f));
        if (GUILayout.Button("1x", mButtonStyle, GUILayout.Width(28f), GUILayout.Height(24f))) mLabTimeScale = 1.0f;

        GUILayout.FlexibleSpace();
        if (GUILayout.Button("[F12] SS", mButtonStyle, GUILayout.Width(68f), GUILayout.Height(28f))) CaptureLabScreenshot();
        if (GUILayout.Button("[F1] Gizle", mAccentButtonStyle, GUILayout.Width(72f), GUILayout.Height(28f))) mHudVisible = false;

        GUILayout.EndHorizontal();
        GUILayout.EndArea();

        // Bottom Status Toast
        GUI.Box(new Rect(10f, Screen.height - 32f, Mathf.Min(Screen.width - 20f, 920f), 26f), GUIContent.none, mHeaderStyle);
        GUI.Label(new Rect(18f, Screen.height - 29f, 900f, 22f), mStatusBanner, mSmallLabelStyle);

        // Left Control Panel (cleanly sized so center/right of arena stays unobstructed)
        float panelW = Mathf.Clamp(Screen.width * 0.27f, 305f, 365f);
        float panelH = Mathf.Min(Screen.height - 98f, 640f);
        GUI.Box(new Rect(10f, 60f, panelW, panelH), GUIContent.none, mPanelStyle);
        GUILayout.BeginArea(new Rect(16f, 66f, panelW - 12f, panelH - 12f));

        // 4 Navigation Tabs
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Kontrol", mActiveTab == LabTab.ActionsAndChars ? mActiveButtonStyle : mButtonStyle, GUILayout.Height(26f))) mActiveTab = LabTab.ActionsAndChars;
        if (GUILayout.Button("Kare/Klip", mActiveTab == LabTab.AnimScrubber ? mActiveButtonStyle : mButtonStyle, GUILayout.Height(26f))) mActiveTab = LabTab.AnimScrubber;
        if (GUILayout.Button("Rig Ayar", mActiveTab == LabTab.RigTuner ? mActiveButtonStyle : mButtonStyle, GUILayout.Height(26f))) mActiveTab = LabTab.RigTuner;
        if (GUILayout.Button("Rakipler", mActiveTab == LabTab.OpponentSpawner ? mActiveButtonStyle : mButtonStyle, GUILayout.Height(26f))) mActiveTab = LabTab.OpponentSpawner;
        GUILayout.EndHorizontal();
        GUILayout.Space(6f);

        if (mActiveTab == LabTab.ActionsAndChars)
        {
            DrawTabActionsAndCharacters();
        }
        else if (mActiveTab == LabTab.AnimScrubber)
        {
            DrawTabAnimScrubber();
        }
        else if (mActiveTab == LabTab.RigTuner)
        {
            DrawTabRigTuner();
        }
        else
        {
            DrawTabOpponentSpawner();
        }

        GUILayout.EndArea();
    }

    private void DrawTabActionsAndCharacters()
    {
        mCharScroll = GUILayout.BeginScrollView(mCharScroll);

        // Control Mode + Facing + Hero Switch
        if (mTerasuCtrl != null)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(mTerasuCtrl.labManualMode ? "Mod: MANUEL (Aktif)" : "Mod: Yapay Zeka (AI)",
                mTerasuCtrl.labManualMode ? mActiveButtonStyle : mButtonStyle, GUILayout.Height(26f)))
            {
                mTerasuCtrl.labManualMode = !mTerasuCtrl.labManualMode;
                mWalkToggleDir = 0;
            }
            if (GUILayout.Button(mTerasuCtrl.labAutoCycleUlts ? "Oto Ulti: ACIK" : "Oto Ulti: KAPALI",
                mTerasuCtrl.labAutoCycleUlts ? mActiveButtonStyle : mButtonStyle, GUILayout.Height(26f)))
            {
                mTerasuCtrl.labAutoCycleUlts = !mTerasuCtrl.labAutoCycleUlts;
            }
            GUILayout.EndHorizontal();
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Sola Bak (-Z)", mButtonStyle, GUILayout.Height(24f)) && mActiveSubject != null && mActiveSubject.controller != null)
        {
            mActiveSubject.controller.facing = FacingType.Left;
        }
        if (GUILayout.Button("Saga Bak (+Z)", mButtonStyle, GUILayout.Height(24f)) && mActiveSubject != null && mActiveSubject.controller != null)
        {
            mActiveSubject.controller.facing = FacingType.Right;
        }
        if (GUILayout.Button(mHeroParked ? "Kahramani Getir" : "Kahramani Gizle", mButtonStyle, GUILayout.Height(24f)))
        {
            SetHeroParked(!mHeroParked);
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(6f);
        GUILayout.Label("Terasu Hareket & Yetenek Tuslari (Klavye 1-8):", mTitleStyle);
        GUILayout.Label("Ayrica A / D ile sola-saga ucurabilirsin.", mSmallLabelStyle);

        if (GUILayout.Button("[1] Dur / Suzul (Combat_Stance)", mButtonStyle, GUILayout.Height(26f))) ActionIdleStance();
        if (GUILayout.Button("[2] Ucarak Ilerle / Dur (Hover_Fly) " + (mWalkToggleDir != 0 ? "[AKTIF]" : ""), mWalkToggleDir != 0 ? mActiveButtonStyle : mButtonStyle, GUILayout.Height(26f))) ActionToggleWalk();
        if (GUILayout.Button("[3] Vurus 1: Mizrak Saplama (Double_Combo)", mButtonStyle, GUILayout.Height(26f))) ActionMeleeAttack(false);
        if (GUILayout.Button("[4] Vurus 2: Genis Mizrak Savurma (Sky_Slam)", mButtonStyle, GUILayout.Height(26f))) ActionMeleeAttack(true);
        if (GUILayout.Button("[5] Mizrak Firlat + Miknatisla Geri Cek", mAccentButtonStyle, GUILayout.Height(28f))) ActionSpearThrowUlt();
        if (GUILayout.Button("[6] 6. Halka Parla + Thanos Sok Dalgasi Ulti", mAccentButtonStyle, GUILayout.Height(28f))) ActionSixthWheelThanosUlt();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("[7] Halka +60°", mButtonStyle, GUILayout.Height(26f))) ActionStepWheel();
        if (GUILayout.Button("[8] Faz 1->2 Dirilis", mButtonStyle, GUILayout.Height(26f))) SpawnTerasuPhase2(true);
        GUILayout.EndHorizontal();

        GUILayout.Space(8f);
        GUILayout.Label("Incelenecek Karakteri Degistir:", mTitleStyle);
        if (GUILayout.Button("DEMIR KASAP ZOMBI (60) YUKLE", mAccentButtonStyle, GUILayout.Height(28f)))
        {
            SpawnSubjectToInspect("Iron_Butcher", false);
        }
        if (mButcherCtrl != null)
        {
            GUILayout.Space(4f);
            GUILayout.Label("Demir Kasap Animasyonlari:", mSmallLabelStyle);
            string[] ibClips = mButcherCtrl.availableClips;
            for (int cIdx = 0; cIdx < ibClips.Length; cIdx += 2)
            {
                GUILayout.BeginHorizontal();
                string c1 = ibClips[cIdx];
                bool a1 = string.Equals(mButcherCtrl.labForcedClip, c1, StringComparison.Ordinal);
                if (GUILayout.Button(c1, a1 ? mActiveButtonStyle : mButtonStyle, GUILayout.Height(22f)))
                {
                    mButcherCtrl.PlayManualAction(c1, 0f, false);
                }
                if (cIdx + 1 < ibClips.Length)
                {
                    string c2 = ibClips[cIdx + 1];
                    bool a2 = string.Equals(mButcherCtrl.labForcedClip, c2, StringComparison.Ordinal);
                    if (GUILayout.Button(c2, a2 ? mActiveButtonStyle : mButtonStyle, GUILayout.Height(22f)))
                    {
                        mButcherCtrl.PlayManualAction(c2, 0f, false);
                    }
                }
                GUILayout.EndHorizontal();
            }
            if (GUILayout.Button("Kasap AI / Otomatik Klip Moduna Don", mButtonStyle, GUILayout.Height(22f)))
            {
                mButcherCtrl.labManualMode = false;
                mButcherCtrl.labFreezeClip = false;
                mButcherCtrl.labForcedClip = string.Empty;
            }
        }
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Terasu (Faz 2)", mAccentButtonStyle, GUILayout.Height(26f))) SpawnTerasuPhase2(false);
        if (GUILayout.Button("Shogun (Faz 1)", mButtonStyle, GUILayout.Height(26f))) SpawnSubjectToInspect("Shogun", false);
        GUILayout.EndHorizontal();

        // Compact 2-column grid for inspecting other characters
        for (int i = 0; i < kEnemyCatalogIds.Length; i += 2)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(kEnemyCatalogIds[i], mButtonStyle, GUILayout.Height(22f)))
            {
                SpawnSubjectToInspect(kEnemyCatalogIds[i], false);
            }
            if (i + 1 < kEnemyCatalogIds.Length && GUILayout.Button(kEnemyCatalogIds[i + 1], mButtonStyle, GUILayout.Height(22f)))
            {
                SpawnSubjectToInspect(kEnemyCatalogIds[i + 1], false);
            }
            GUILayout.EndHorizontal();
        }
        GUILayout.Space(4f);
        GUILayout.Label("Muttefik Birimler:", mSmallLabelStyle);
        for (int i = 0; i < kHelperCatalogIds.Length; i += 2)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(kHelperCatalogIds[i], mButtonStyle, GUILayout.Height(22f)))
            {
                SpawnSubjectToInspect(kHelperCatalogIds[i], true);
            }
            if (i + 1 < kHelperCatalogIds.Length && GUILayout.Button(kHelperCatalogIds[i + 1], mButtonStyle, GUILayout.Height(22f)))
            {
                SpawnSubjectToInspect(kHelperCatalogIds[i + 1], true);
            }
            GUILayout.EndHorizontal();
        }

        GUILayout.EndScrollView();
    }

    private void DrawTabAnimScrubber()
    {
        if (mTerasuCtrl == null && mButcherCtrl == null)
        {
            GUILayout.Label("Animasyon kare inceleme icin Terasu veya Demir Kasap Zombi sec.", mLabelStyle);
            if (GUILayout.Button("DEMIR KASAP ZOMBI (60) YUKLE", mAccentButtonStyle, GUILayout.Height(28f))) SpawnSubjectToInspect("Iron_Butcher", false);
            if (GUILayout.Button("Terasu (Faz 2) Getir", mButtonStyle, GUILayout.Height(26f))) SpawnTerasuPhase2(false);
            return;
        }

        if (mButcherCtrl != null && mTerasuCtrl == null)
        {
            GUILayout.Label("Demir Kasap Kare Dondurma & Klip Inceleme:", mTitleStyle);
            GUILayout.Label("Space: Dondur/Coz | ',' ve '.' : Kare Adimla", mSmallLabelStyle);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(mButcherCtrl.labFreezeClip ? "Kare DONDURULDU (Coz)" : "Canli Oynuyor (Dondur)",
                mButcherCtrl.labFreezeClip ? mAccentButtonStyle : mButtonStyle, GUILayout.Height(28f)))
            {
                ToggleFreezeFrame();
            }
            if (GUILayout.Button("Klip Kilidini Kaldir", mButtonStyle, GUILayout.Height(28f)))
            {
                mButcherCtrl.labManualMode = false;
                mButcherCtrl.labForcedClip = string.Empty;
                mButcherCtrl.labFreezeClip = false;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.Label("Kare Ilerleme (0.00 -> 1.00): " + mButcherCtrl.labForcedNormalizedTime.ToString("F3"), mLabelStyle);
            float bNorm = GUILayout.HorizontalSlider(mButcherCtrl.labForcedNormalizedTime, 0f, 1f);
            if (Mathf.Abs(bNorm - mButcherCtrl.labForcedNormalizedTime) > 0.0005f)
            {
                mButcherCtrl.labManualMode = true;
                mButcherCtrl.labFreezeClip = true;
                mButcherCtrl.labForcedNormalizedTime = bNorm;
            }

            GUILayout.Label("Animasyon Hizi: " + mButcherCtrl.labAnimSpeedMultiplier.ToString("F2") + "x", mLabelStyle);
            mButcherCtrl.labAnimSpeedMultiplier = GUILayout.HorizontalSlider(mButcherCtrl.labAnimSpeedMultiplier, 0.1f, 2.5f);

            GUILayout.Space(8f);
            GUILayout.Label("Demir Kasap Klip Sec:", mTitleStyle);
            string[] clips = mButcherCtrl.availableClips;
            for (int i = 0; i < clips.Length; i++)
            {
                string c = clips[i];
                bool active = string.Equals(mButcherCtrl.labForcedClip, c, StringComparison.Ordinal);
                if (GUILayout.Button(c, active ? mActiveButtonStyle : mButtonStyle, GUILayout.Height(24f)))
                {
                    mButcherCtrl.PlayManualAction(c, 0f, mButcherCtrl.labFreezeClip);
                }
            }
            return;
        }

        GUILayout.Label("Kare Dondurma & Klip Inceleme:", mTitleStyle);
        GUILayout.Label("Space: Dondur/Coz | ',' ve '.' : Kare Adimla", mSmallLabelStyle);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button(mTerasuCtrl.labFreezeClip ? "Kare DONDURULDU (Coz)" : "Canli Oynuyor (Dondur)",
            mTerasuCtrl.labFreezeClip ? mAccentButtonStyle : mButtonStyle, GUILayout.Height(28f)))
        {
            ToggleFreezeFrame();
        }
        if (GUILayout.Button("Klip Kilidini Kaldir", mButtonStyle, GUILayout.Height(28f)))
        {
            mTerasuCtrl.labForcedClip = string.Empty;
            mTerasuCtrl.labFreezeClip = false;
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(6f);
        GUILayout.Label("Kare Ilerleme (0.00 -> 1.00): " + mTerasuCtrl.labForcedNormalizedTime.ToString("F3"), mLabelStyle);
        float newNorm = GUILayout.HorizontalSlider(mTerasuCtrl.labForcedNormalizedTime, 0f, 1f);
        if (Mathf.Abs(newNorm - mTerasuCtrl.labForcedNormalizedTime) > 0.0005f)
        {
            mTerasuCtrl.labFreezeClip = true;
            mTerasuCtrl.labForcedNormalizedTime = newNorm;
        }

        GUILayout.Label("Animasyon Hizi: " + mTerasuCtrl.labAnimSpeedMultiplier.ToString("F2") + "x", mLabelStyle);
        mTerasuCtrl.labAnimSpeedMultiplier = GUILayout.HorizontalSlider(mTerasuCtrl.labAnimSpeedMultiplier, 0.1f, 2.5f);

        GUILayout.Space(8f);
        GUILayout.Label("Klip Sec (Donguye Al veya Dondur):", mTitleStyle);
        for (int i = 0; i < kTerasuClips.Length; i++)
        {
            string c = kTerasuClips[i];
            bool active = string.Equals(mTerasuCtrl.labForcedClip, c, StringComparison.Ordinal);
            if (GUILayout.Button(c, active ? mActiveButtonStyle : mButtonStyle, GUILayout.Height(25f)))
            {
                mTerasuCtrl.labForcedClip = c;
            }
        }
    }

    private void DrawTabRigTuner()
    {
        if (mTerasuCtrl == null && mButcherCtrl != null)
        {
            GUILayout.Label("Demir Kasap Canli Boyut Ayari:", mTitleStyle);
            GUILayout.Label("Karakter Scale Carpani: " + mButcherCtrl.visualScaleMultiplier.ToString("F2") + "x", mSmallLabelStyle);
            mButcherCtrl.visualScaleMultiplier = GUILayout.HorizontalSlider(mButcherCtrl.visualScaleMultiplier, 0.55f, 1.85f);
            GUILayout.Space(8f);
            if (GUILayout.Button("Varsayilana Sifirla (1.00x)", mButtonStyle, GUILayout.Height(26f)))
            {
                mButcherCtrl.visualScaleMultiplier = 1.0f;
            }
            return;
        }
        if (mTerasuCtrl == null)
        {
            GUILayout.Label("Canli Rig / Halka / Mizrak ayari icin Terasu (Faz 2) veya Demir Kasap sec.", mLabelStyle);
            if (GUILayout.Button("DEMIR KASAP ZOMBI (60) YUKLE", mAccentButtonStyle, GUILayout.Height(28f))) SpawnSubjectToInspect("Iron_Butcher", false);
            if (GUILayout.Button("Terasu (Faz 2) Getir", mButtonStyle, GUILayout.Height(26f))) SpawnTerasuPhase2(false);
            return;
        }

        GUILayout.Label("Canli Boyut, Halka & Mizrak Ayarlari:", mTitleStyle);

        GUILayout.Label("Karakter Scale Carpani: " + mTerasuCtrl.labScaleMultiplier.ToString("F2") + "x", mSmallLabelStyle);
        mTerasuCtrl.labScaleMultiplier = GUILayout.HorizontalSlider(mTerasuCtrl.labScaleMultiplier, 0.55f, 1.85f);

        GUILayout.Label("Havada Suzulme Yuksekligi: " + mTerasuCtrl.labHoverOffsetY.ToString("F1"), mSmallLabelStyle);
        mTerasuCtrl.labHoverOffsetY = GUILayout.HorizontalSlider(mTerasuCtrl.labHoverOffsetY, -25f, 65f);

        GUILayout.Space(6f);
        GUILayout.Label("Sirt Halkasi (Halo) Konum & Boyut:", mTitleStyle);
        Vector3 hp = mTerasuCtrl.labHaloPosOffset;
        GUILayout.Label("Halo Y (Yukari/Asagi): " + hp.y.ToString("F2") + " | Z (Ileri/Geri): " + hp.z.ToString("F2"), mSmallLabelStyle);
        hp.y = GUILayout.HorizontalSlider(hp.y, -0.60f, 0.80f);
        hp.z = GUILayout.HorizontalSlider(hp.z, -0.60f, 0.60f);
        mTerasuCtrl.labHaloPosOffset = hp;

        GUILayout.Label("Halo Boyut: " + mTerasuCtrl.labHaloScaleMultiplier.ToString("F2") + "x", mSmallLabelStyle);
        mTerasuCtrl.labHaloScaleMultiplier = GUILayout.HorizontalSlider(mTerasuCtrl.labHaloScaleMultiplier, 0.5f, 1.8f);

        GUILayout.Space(6f);
        GUILayout.Label("Mizrak Pozisyon / Aci / Boyut:", mTitleStyle);
        Vector3 sp = mTerasuCtrl.labSpearPosOffset;
        GUILayout.Label("Mizrak Ofset X:" + sp.x.ToString("F2") + " Y:" + sp.y.ToString("F2") + " Z:" + sp.z.ToString("F2"), mSmallLabelStyle);
        sp.x = GUILayout.HorizontalSlider(sp.x, -0.50f, 0.50f);
        sp.y = GUILayout.HorizontalSlider(sp.y, -0.80f, 0.80f);
        sp.z = GUILayout.HorizontalSlider(sp.z, -0.50f, 0.50f);
        mTerasuCtrl.labSpearPosOffset = sp;

        Vector3 se = mTerasuCtrl.labSpearEulerOffset;
        GUILayout.Label("Mizrak Aci (" + se.x.ToString("F0") + ", " + se.y.ToString("F0") + ", " + se.z.ToString("F0") + ")", mSmallLabelStyle);
        se.x = GUILayout.HorizontalSlider(se.x, -90f, 90f);
        se.y = GUILayout.HorizontalSlider(se.y, -90f, 90f);
        mTerasuCtrl.labSpearEulerOffset = se;

        GUILayout.Label("Mizrak Boyut: " + mTerasuCtrl.labSpearScaleMultiplier.ToString("F2") + "x", mSmallLabelStyle);
        mTerasuCtrl.labSpearScaleMultiplier = GUILayout.HorizontalSlider(mTerasuCtrl.labSpearScaleMultiplier, 0.5f, 1.8f);

        GUILayout.Space(8f);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Varsayilana Sifirla", mButtonStyle, GUILayout.Height(26f)))
        {
            mTerasuCtrl.ResetLabOffsets();
            SetStatus("Tum canli rig ayarlari sifirlandi.");
        }
        if (GUILayout.Button("Konsola Yazdir", mAccentButtonStyle, GUILayout.Height(26f)))
        {
            string report = string.Format(
                "[LabRigTuner] Scale={0:F2} HoverY={1:F1} HaloPos={2} HaloScale={3:F2} SpearPos={4} SpearEuler={5} SpearScale={6:F2}",
                mTerasuCtrl.labScaleMultiplier, mTerasuCtrl.labHoverOffsetY,
                mTerasuCtrl.labHaloPosOffset, mTerasuCtrl.labHaloScaleMultiplier,
                mTerasuCtrl.labSpearPosOffset, mTerasuCtrl.labSpearEulerOffset, mTerasuCtrl.labSpearScaleMultiplier
            );
            Debug.Log(report);
            SetStatus("Degerler Unity Console'a yazdirildi!");
        }
        GUILayout.EndHorizontal();
    }

    private void DrawTabOpponentSpawner()
    {
        mOpponentScroll = GUILayout.BeginScrollView(mOpponentScroll);

        GUILayout.Label("Rakip Davranis Modu:", mTitleStyle);
        if (GUILayout.Button("1) Olumsuz Sabit Kukla (Kacmaz/Vurmaz)", mDummySpawnBehavior == DummyBehaviorMode.ImmortalStationary ? mActiveButtonStyle : mButtonStyle, GUILayout.Height(24f)))
        {
            mDummySpawnBehavior = DummyBehaviorMode.ImmortalStationary;
        }
        if (GUILayout.Button("2) Olumsuz Saldiran Rakip (Test Vuruslari)", mDummySpawnBehavior == DummyBehaviorMode.ImmortalActiveFighter ? mActiveButtonStyle : mButtonStyle, GUILayout.Height(24f)))
        {
            mDummySpawnBehavior = DummyBehaviorMode.ImmortalActiveFighter;
        }
        if (GUILayout.Button("3) Normal Olumlu Rakip (Gercek AI)", mDummySpawnBehavior == DummyBehaviorMode.NormalMortal ? mActiveButtonStyle : mButtonStyle, GUILayout.Height(24f)))
        {
            mDummySpawnBehavior = DummyBehaviorMode.NormalMortal;
        }

        GUILayout.Space(4f);
        GUILayout.Label("Dogma Mesafesi: " + mDummySpawnDistance.ToString("F0") + " birim", mSmallLabelStyle);
        mDummySpawnDistance = GUILayout.HorizontalSlider(mDummySpawnDistance, 85f, 340f);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("[9] Hizli Ikili Ekle", mAccentButtonStyle, GUILayout.Height(26f))) SpawnPresetPair();
        if (GUILayout.Button("[0] Tumunu Temizle", mButtonStyle, GUILayout.Height(26f))) ClearOpponentsOnly();
        GUILayout.EndHorizontal();

        if (GUILayout.Button("5'li Kalabalik Kalabalik Grup Ekle (AoE Test)", mButtonStyle, GUILayout.Height(25f)))
        {
            SpawnOpponentUnit("Samurai", true, 105f, -24f);
            SpawnOpponentUnit("HeavySamurai", true, 125f, 22f);
            SpawnOpponentUnit("SpearSamurai", true, 165f, 0f);
            SpawnOpponentUnit("SamuraiArcher", true, 245f, -18f);
            SpawnOpponentUnit("Priest", true, 285f, 18f);
        }

        GUILayout.Space(6f);
        GUILayout.Label("Tekil Rakip Ekle (Muttefik / Samuraylar):", mTitleStyle);
        for (int i = 0; i < kHelperCatalogIds.Length; i += 2)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(kHelperCatalogIds[i], mButtonStyle, GUILayout.Height(22f)))
            {
                SpawnOpponentUnit(kHelperCatalogIds[i], true, mDummySpawnDistance, 0f);
            }
            if (i + 1 < kHelperCatalogIds.Length && GUILayout.Button(kHelperCatalogIds[i + 1], mButtonStyle, GUILayout.Height(22f)))
            {
                SpawnOpponentUnit(kHelperCatalogIds[i + 1], true, mDummySpawnDistance, 0f);
            }
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(4f);
        GUILayout.Label("Tekil Rakip Ekle (Zombi / Yokai):", mTitleStyle);
        for (int i = 0; i < kEnemyCatalogIds.Length; i += 2)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(kEnemyCatalogIds[i], mButtonStyle, GUILayout.Height(22f)))
            {
                SpawnOpponentUnit(kEnemyCatalogIds[i], false, mDummySpawnDistance, 0f);
            }
            if (i + 1 < kEnemyCatalogIds.Length && GUILayout.Button(kEnemyCatalogIds[i + 1], mButtonStyle, GUILayout.Height(22f)))
            {
                SpawnOpponentUnit(kEnemyCatalogIds[i + 1], false, mDummySpawnDistance, 0f);
            }
            GUILayout.EndHorizontal();
        }

        GUILayout.EndScrollView();
    }

    public void ToggleHud()
    {
        mHudVisible = !mHudVisible;
        SetStatus(mHudVisible ? "HUD acildi (F1/H)." : "HUD gizlendi (F1/H ile geri ac).");
    }
}
