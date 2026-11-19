using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public class Hero : Character
{
	private const float kChickenSwordDuration = 15f;

	private HeroControls mPlayerControls;

	private bool mPerformingSpecialAttack;

	private string mHealthBarToShow = "Sprites/HUD/life_bar_green";

	private GameObject mKatanaSlashFXPrefab;

	private bool mIsLeftToRightGameplay = true;

	private List<string> mCachedMeleeWeaponPrefab = new List<string>();

	private string[] charmMeleePrefabID = new string[2] { "meleePrefab", "meleePrefab2" };

	private string[] normalMeleePrefabID = new string[2] { "prefab", "prefab2" };

	public string healthBarFileToUse
	{
		get
		{
			return mHealthBarToShow;
		}
	}

	public bool canUseSpecialAttack
	{
		get
		{
			return base.health > 0f && !mPerformingSpecialAttack && !base.controller.isInHurtState;
		}
	}

	public Hero(Transform spawnPoint)
	{
		base.isPlayer = true;
		mIsLeftToRightGameplay = Singleton<PlayModesManager>.instance.gameDirection == PlayModesManager.GameDirection.LeftToRight;
		if (SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive)
		{
			mIsLeftToRightGameplay = (SVZMultiplayerManager.Instance.P1Side == SVZMultiplayerManager.BattlefieldSide.LeftGate);
		}
		SDFTreeNode sDFTreeNode = SingletonMonoBehaviour<ResourcesManager>.instance.Open("Registry/Heroes/" + Singleton<Profile>.instance.heroID);
		string path = GetHeroPrefab(sDFTreeNode);
		if (Singleton<Profile>.instance.inBonusWave)
		{
			string text = sDFTreeNode["ghostPrefab"];
			if (text != string.Empty)
			{
				path = text;
			}
		}
		UnityEngine.Object heroPrefab = Resources.Load(path);
		if (heroPrefab == null && Singleton<Profile>.instance.inBonusWave && Singleton<PlayModesManager>.instance.selectedMode == "zombies")
		{
			Debug.LogError("Hero: missing zombie bonus hero prefab '" + path + "', falling back to Characters/PFZombieHero.");
			heroPrefab = Resources.Load("Characters/PFZombieHero");
		}
		base.controlledObject = (GameObject)UnityEngine.Object.Instantiate(heroPrefab);
		if (base.controller != null)
		{
			base.controller.nativeFacing = CharacterModelController.GetModelNativeFacing(path);
		}
		base.controller.position = spawnPoint.position;
		SetDefaultFacing();
		base.stats = GetHeroStats();
		UpdateHealthThreshold();
		mPlayerControls = new HeroControls();
		mPlayerControls.onMoveLeft = onMoveLeft;
		mPlayerControls.onMoveRight = onMoveRight;
		mPlayerControls.onDontMove = onDontMove;
		if (!Singleton<Profile>.instance.inBonusWave)
		{
			RestoreMeleeWeapon();
		}
		base.rangedWeaponPrefab = Resources.Load(sDFTreeNode["rangedWeaponPrefab"]) as GameObject;
		SetRangeAttackMode(false);
		if (CheckAbilitySelected("KatanaSlash") || CheckAbilitySelected("LifeSteal"))
		{
			mKatanaSlashFXPrefab = Resources.Load(sDFTreeNode["meleeSlashFXPrefab"]) as GameObject;
		}
		if (WeakGlobalSceneBehavior<InGameImpl>.instance.activeCharm == "wealth" || WeakGlobalSceneBehavior<InGameImpl>.instance.activeCharm.Contains("+wealth"))
		{
			GameObject magnetFX = Resources.Load("FX/Magnet") as GameObject;
			if (magnetFX != null)
			{
				base.controller.SpawnEffectAtJoint(magnetFX, "body_effect", true);
			}
		}
	}

	public static float GetHealthThreshold(int numblockLost, float maxHealth)
	{
		numblockLost = Mathf.Clamp(numblockLost, 0, 2);
		float num = maxHealth / 3f * 0.965f;
		return maxHealth - num * (float)numblockLost;
	}

	public static CharacterStats GetHeroStats()
	{
		CharacterStats result = default(CharacterStats);
		result.isPlayer = true;
		result.knockbackResistance = 100;
		float num = 1f;
		if (WeakGlobalSceneBehavior<InGameImpl>.instance.activeCharm == "berserk" || WeakGlobalSceneBehavior<InGameImpl>.instance.activeCharm.Contains("+berserk"))
		{
			num = float.Parse(Singleton<CharmsDatabase>.instance.GetAttribute(WeakGlobalSceneBehavior<InGameImpl>.instance.activeCharm, "heroDamageMultiplier"), CultureInfo.InvariantCulture);
			if (num < 1f)
			{
				num = 1f;
				Debug.Log("WARNING: problem with the stat 'heroDamageMultiplier' for charm: " + WeakGlobalSceneBehavior<InGameImpl>.instance.activeCharm);
			}
		}
		SDFTreeNode root = SingletonMonoBehaviour<ResourcesManager>.instance.Open("Registry/Heroes/" + Singleton<Profile>.instance.heroID);
		result.maxHealth = InfiniteUpgrades.Extrapolate<float>(root, "infiniteUpgradeHealth", "health", Singleton<Profile>.instance.heroLevel);
		result.health = result.maxHealth;
		result.autoHealthRecovery = InfiniteUpgrades.Extrapolate<float>(root, "infiniteUpgradeHealthRecovery", "healthRecovery", Singleton<Profile>.instance.heroLevel);
		result.speed = InfiniteUpgrades.SnapToHighest<float>(root, "speed", Singleton<Profile>.instance.heroLevel);
		bool isMultiplayer = SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive;
		int swordLvl = isMultiplayer ? 8 : Singleton<Profile>.instance.swordLevel;
		int bowLvl = isMultiplayer ? 8 : Singleton<Profile>.instance.bowLevel;
		SDFTreeNode sDFTreeNode = SingletonMonoBehaviour<ResourcesManager>.instance.Open("Registry/Weapons/" + Singleton<Profile>.instance.swordID);
		if (sDFTreeNode.hasAttribute("isBladeWeapon"))
		{
			result.meleeWeaponIsABlade = bool.Parse(sDFTreeNode["isBladeWeapon"]);
		}
		result.meleeAttackRange = InfiniteUpgrades.SnapToHighest<float>(sDFTreeNode, "attackRange", swordLvl);
		result.meleeAttackDamage += InfiniteUpgrades.Extrapolate<float>(sDFTreeNode, "infiniteUpgradeDamage", "damage", swordLvl);
		result.meleeAttackFrequency = InfiniteUpgrades.SnapToHighest<float>(sDFTreeNode, "attackFrequency", swordLvl);
		result.knockbackPower = InfiniteUpgrades.SnapToHighest<int>(sDFTreeNode, "knockbackPower", swordLvl);
		result.meleeAttackDamage *= num;
		bool flag = isMultiplayer || Singleton<PlayModesManager>.instance.CheckFlag("allowBowOnFirstWave1");
		if (!flag)
		{
			flag = Singleton<Profile>.instance.waveToBeat > 1 || Singleton<Profile>.instance.GetWaveLevel(1) > 1;
		}
		if (flag)
		{
			SDFTreeNode root2 = SingletonMonoBehaviour<ResourcesManager>.instance.Open("Registry/Weapons/" + Singleton<Profile>.instance.bowID);
			result.bowAttackRange = InfiniteUpgrades.SnapToHighest<float>(root2, "attackRange", bowLvl);
			result.bowAttackDamage = InfiniteUpgrades.Extrapolate<float>(root2, "infiniteUpgradeDamage", "damage", bowLvl);
			result.bowAttackFrequency = InfiniteUpgrades.SnapToHighest<float>(root2, "attackFrequency", bowLvl);
			result.projectile = (Projectile.EProjectileType)(int)Enum.Parse(typeof(Projectile.EProjectileType), InfiniteUpgrades.SnapToHighest<string>(root2, "projectile", bowLvl));
			result.bowAttackDamage *= num;
		}
		if (WeakGlobalSceneBehavior<InGameImpl>.instance.activeCharm == "power" || WeakGlobalSceneBehavior<InGameImpl>.instance.activeCharm.Contains("+power"))
		{
			result.criticalChance = float.Parse(Singleton<CharmsDatabase>.instance.GetAttribute(WeakGlobalSceneBehavior<InGameImpl>.instance.activeCharm, "criticalChance"), CultureInfo.InvariantCulture);
			result.criticalMultiplier = float.Parse(Singleton<CharmsDatabase>.instance.GetAttribute(WeakGlobalSceneBehavior<InGameImpl>.instance.activeCharm, "criticalMultiplier"), CultureInfo.InvariantCulture);
		}
		else
		{
			SDFTreeNode sDFTreeNode2 = Singleton<Config>.instance.root.to("DefaultCriticals");
			if (sDFTreeNode2 == null)
			{
				Debug.Log("ERROR: Could not find [DefaultCriticals] in Config.txt");
			}
			else
			{
				result.criticalChance = float.Parse(sDFTreeNode2["chance"], CultureInfo.InvariantCulture);
				result.criticalMultiplier = float.Parse(sDFTreeNode2["multiplier"], CultureInfo.InvariantCulture);
			}
		}
		if (isMultiplayer)
		{
			result.maxHealth = 4000f;
			result.health = 4000f;
			result.autoHealthRecovery = 85f;
			result.speed = 300f;
			result.knockbackResistance = 100;
			result.knockbackPower = 100;
			result.meleeAttackDamage = 50f;
			result.meleeAttackRange = 145f;
			result.meleeAttackFrequency = 1.0f;
			result.meleeWeaponIsABlade = true;
			result.bowAttackDamage = 40f;
			result.bowAttackRange = 400f;
			result.bowAttackFrequency = 1.5f;
			result.projectile = (Singleton<Profile>.instance.heroID == "HeroZombie")
				? Projectile.EProjectileType.BoneArrowUpgraded
				: Projectile.EProjectileType.HeroBigExplodingArrow;
			result.criticalChance = 0.05f;
			result.criticalMultiplier = 1.5f;
		}
		return result;
	}

	public override void Update()
	{
		if (SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive && SVZMultiplayerManager.Instance.CurrentGameMode == SVZMultiplayerManager.MultiplayerGameMode.ArmyOnly)
		{
			base.health = 999999f;
			return;
		}
		base.Update();
		UpdateHealthThreshold();
		if (base.health > 0f)
		{
			bool isOnlineClient = (SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive && 
			                       SVZNetworkManager.Instance != null && !SVZNetworkManager.Instance.IsHost && SVZNetworkManager.Instance.IsConnected);
			if (!isOnlineClient)
			{
				mPlayerControls.Update();
			}
		}
		UpdateAttacks();
	}

	public override void Destroy()
	{
		base.Destroy();
	}

	public void UpdateAttacks()
	{
		NotifyTutorials_InRangeOfAttack(false);
		if (!base.isEffectivelyIdle || base.controller.isMoving)
		{
			return;
		}
		if (WeakGlobalSceneBehavior<InGameImpl>.instance.playerWon)
		{
			base.controller.PlayVictoryAnim();
			return;
		}
		if (Singleton<Profile>.instance.inBonusWave)
		{
			return;
		}
		FaceNearestEnemyOrDefault();
		if (IsInMeleeRangeOfOpponent(true))
		{
			NotifyTutorials_InRangeOfAttack(true);
			SetRangeAttackMode(false);
			if (base.canMeleeAttack)
			{
				StartMeleeAttackDelayTimer();
				base.controller.Attack(base.meleeAttackFrequency);
			}
		}
		else if (IsInBowRangeOfOpponent())
		{
			NotifyTutorials_InRangeOfAttack(true);
			SetRangeAttackMode(true);
			if (base.canUseRangedAttack)
			{
				StartRangedAttackDelayTimer();
				base.singleAttackTarget = WeakGlobalInstance<CharactersManager>.instance.GetBestRangedAttackTarget(this, base.bowAttackGameRange);
				if (base.singleAttackTarget != null)
				{
					base.controller.RangeAttack(base.meleeAttackFrequency);
				}
			}
		}
		else if (base.controller.currentAnimation == "rangedattackidle" && base.controller.animPlayer.currAnimTime > 2f)
		{
			SetRangeAttackMode(false);
		}
	}

	private void FaceNearestEnemyOrDefault()
	{
		if (base.controller == null) return;
		bool allowBack = (SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive && SVZMultiplayerManager.Instance.AllowBackAttack);
		if (!allowBack)
		{
			SetDefaultFacing();
			return;
		}
		CharactersManager charMgr = WeakGlobalInstance<CharactersManager>.instance;
		if (charMgr != null && charMgr.allAlive != null)
		{
			float myZ = base.controller.position.z;
			Character nearest = null;
			float bestDist = 450f;
			List<Character> alive = charMgr.allAlive;
			for (int i = 0; i < alive.Count; i++)
			{
				Character c = alive[i];
				if (c == null || c.health <= 0f || c.isBase || !c.isEnemy) continue;
				float d = Mathf.Abs(c.position.z - myZ);
				if (d < bestDist)
				{
					bestDist = d;
					nearest = c;
				}
			}
			if (nearest != null)
			{
				base.controller.facing = (nearest.position.z >= myZ) ? FacingType.Right : FacingType.Left;
				return;
			}
		}
		SetDefaultFacing();
	}

	public override void RecievedAttack(EAttackType attackType, float damage)
	{
		if (DevCheatOverlay.sGodMode || Singleton<Profile>.instance.inBonusWave)
		{
			base.health = base.maxHealth;
			return;
		}
		float num = base.health;
		base.RecievedAttack(attackType, damage);
		if (base.health > 0f && !mPerformingSpecialAttack)
		{
			float healthThreshold = GetHealthThreshold(2, base.maxHealth);
			if (num > healthThreshold && base.health <= healthThreshold)
			{
				SetRangeAttackMode(false);
				ForceKnockback();
			}
		}
	}

	public void Revive()
	{
		mPerformingSpecialAttack = true;
		SetRangeAttackMode(false);
		base.controller.PerformSpecialAction("revive", delegate
		{
			OnReviveAttack(null);
		});
		base.controller.animEndedCallback = OnSpecialAttackDone;
	}

	public void DoKatanaSlash(string abilityID)
	{
		mPerformingSpecialAttack = true;
		SetRangeAttackMode(false);
		FaceNearestEnemyOrDefault();
		base.controller.PerformSpecialAction("katanaslash", delegate
		{
			OnKatanSlashAttack(abilityID);
		});
		base.controller.animEndedCallback = OnSpecialAttackDone;
		base.controller.InstantiateObjectOnJoint(mKatanaSlashFXPrefab, "katana_slash_fx");
	}

	public void DoSummonLightning(string abilityID)
	{
		mPerformingSpecialAttack = true;
		SetRangeAttackMode(false);
		FaceNearestEnemyOrDefault();
		base.controller.PerformSpecialAction("summonlightning", delegate
		{
			OnSummonLightningAttack(abilityID);
		});
		base.controller.animEndedCallback = OnSpecialAttackDone;
		GameObject obj = GameObject.FindGameObjectWithTag("Skybox");
		ProceduralShaderManager.postShaderEvent(new DiffuseFadeInOutShaderEvent(obj, SUILayoutConv.GetColor(Singleton<AbilitiesDatabase>.instance.GetAttribute(abilityID, "skyShaderColor")), float.Parse(Singleton<AbilitiesDatabase>.instance.GetAttribute(abilityID, "skyShaderFadeInTimer"), CultureInfo.InvariantCulture), float.Parse(Singleton<AbilitiesDatabase>.instance.GetAttribute(abilityID, "DOTDuration"), CultureInfo.InvariantCulture), float.Parse(Singleton<AbilitiesDatabase>.instance.GetAttribute(abilityID, "skyShaderFadeOutTimer"), CultureInfo.InvariantCulture)));
		float dirSign = (base.controller.facing == FacingType.Left) ? -1f : 1f;
		float zOff = Mathf.Abs(float.Parse(Singleton<AbilitiesDatabase>.instance.GetAttribute(abilityID, "attackRange"), CultureInfo.InvariantCulture)) * dirSign;
		UnityEngine.Object.Instantiate(Resources.Load(Singleton<AbilitiesDatabase>.instance.GetAttribute(abilityID, "mainFX")), base.controller.position + new Vector3(0f, float.Parse(Singleton<AbilitiesDatabase>.instance.GetAttribute(abilityID, "EffectYOffset"), CultureInfo.InvariantCulture), zOff), Quaternion.identity);
	}

	public void DoGraveHands(string abilityID)
	{
		mPerformingSpecialAttack = true;
		SetRangeAttackMode(false);
		FaceNearestEnemyOrDefault();
		base.controller.PerformSpecialAction("castforward", delegate
		{
		});
		base.controller.animEndedCallback = OnSpecialAttackDone;
		GameObject obj = GameObject.FindGameObjectWithTag("Skybox");
		float range = float.Parse(Singleton<AbilitiesDatabase>.instance.GetAttribute(abilityID, "attackRange"), CultureInfo.InvariantCulture);
		float num = float.Parse(Singleton<AbilitiesDatabase>.instance.GetAttribute(abilityID, "DOTDuration"), CultureInfo.InvariantCulture);
		ProceduralShaderManager.postShaderEvent(new DiffuseFadeInOutShaderEvent(obj, SUILayoutConv.GetColor(Singleton<AbilitiesDatabase>.instance.GetAttribute(abilityID, "skyShaderColor")), float.Parse(Singleton<AbilitiesDatabase>.instance.GetAttribute(abilityID, "skyShaderFadeInTimer"), CultureInfo.InvariantCulture), num, float.Parse(Singleton<AbilitiesDatabase>.instance.GetAttribute(abilityID, "skyShaderFadeOutTimer"), CultureInfo.InvariantCulture)));
		if (WeakGlobalInstance<GraveHandsEffect>.instance != null)
		{
			WeakGlobalInstance<GraveHandsEffect>.instance.Play(range, num);
		}
		OnGraveHandsAttack(abilityID);
	}

	public void DoNightOfTheDead(string abilityID)
	{
		mPerformingSpecialAttack = true;
		SetRangeAttackMode(false);
		FaceNearestEnemyOrDefault();
		base.controller.PerformSpecialAction("castforward", delegate
		{
			OnNightOfTheDeadAttack(abilityID);
		});
		base.controller.animEndedCallback = OnSpecialAttackDone;
	}

	public void DoLethargy(string abilityID)
	{
		if (SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive &&
		    SVZMultiplayerManager.Instance.CurrentGameMode == SVZMultiplayerManager.MultiplayerGameMode.ArmyOnly)
		{
			OnLethargyAttack(abilityID);
			return;
		}
		mPerformingSpecialAttack = true;
		SetRangeAttackMode(false);
		FaceNearestEnemyOrDefault();
		base.controller.PerformSpecialAction("castforward", delegate
		{
			OnLethargyAttack(abilityID);
		});
		base.controller.animEndedCallback = OnSpecialAttackDone;
	}

	public void DoDivineIntervention(string abilityID)
	{
		mPerformingSpecialAttack = true;
		SetRangeAttackMode(false);
		FaceNearestEnemyOrDefault();
		string theAnimName = "castmid";
		if (Singleton<PlayModesManager>.instance.selectedMode == "zombies" || Singleton<Profile>.instance.heroID == "HeroZombie")
		{
			theAnimName = "castforward";
		}
		base.controller.PerformSpecialAction(theAnimName, delegate
		{
			OnDivineInterventionAttack(abilityID);
		});
		base.controller.animEndedCallback = OnSpecialAttackDone;
	}

	public void DoSummonTornado(string abilityID)
	{
		mPerformingSpecialAttack = true;
		SetRangeAttackMode(false);
		FaceNearestEnemyOrDefault();
		base.controller.PerformSpecialAction("castforward", delegate
		{
			OnSummonTornadoAttack(abilityID);
		});
		base.controller.animEndedCallback = OnSpecialAttackDone;
	}

	public void DoGroundShock(string abilityID)
	{
		mPerformingSpecialAttack = true;
		SetRangeAttackMode(false);
		FaceNearestEnemyOrDefault();
		base.controller.PerformSpecialAction("castforward", delegate
		{
			OnGroundShockAttack(abilityID);
		});
		base.controller.animEndedCallback = OnSpecialAttackDone;
	}

	public void DoGiantWave(string abilityID)
	{
		if (SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive &&
		    SVZMultiplayerManager.Instance.CurrentGameMode == SVZMultiplayerManager.MultiplayerGameMode.ArmyOnly)
		{
			OnGiantWaveAttack(abilityID);
			return;
		}
		mPerformingSpecialAttack = true;
		SetRangeAttackMode(false);
		FaceNearestEnemyOrDefault();
		string theAnimName = "castmid";
		if (Singleton<PlayModesManager>.instance.selectedMode == "zombies" || Singleton<Profile>.instance.heroID == "HeroZombie")
		{
			theAnimName = "castforward";
		}
		base.controller.PerformSpecialAction(theAnimName, delegate
		{
			OnGiantWaveAttack(abilityID);
		});
		base.controller.animEndedCallback = OnSpecialAttackDone;
	}

	private void OnKatanSlashAttack(string abilityID)
	{
		float num = Singleton<AbilitiesDatabase>.instance.Extrapolate<float>(abilityID, "infiniteUpgradeDamage", "damage");
		float num2 = float.Parse(Singleton<AbilitiesDatabase>.instance.GetAttribute(abilityID, "damageMultEachTarget"));
		float num3 = num * float.Parse(Singleton<AbilitiesDatabase>.instance.GetAttribute(abilityID, "lifeSteal"));
		List<Character> enemiesAhead = GetEnemiesAhead(float.Parse(Singleton<AbilitiesDatabase>.instance.GetAttribute(abilityID, "attackRange")));
		bool flag = false;
		foreach (Character item in enemiesAhead)
		{
			flag = true;
			item.RecievedAttack(EAttackType.Slice, num);
			num *= num2;
		}
		if (flag)
		{
			base.health += num3;
		}
	}

	private void OnSummonLightningAttack(string abilityID)
	{
		List<Character> enemiesAhead = GetEnemiesAhead(float.Parse(Singleton<AbilitiesDatabase>.instance.GetAttribute(abilityID, "attackRange")));
		float num = float.Parse(Singleton<AbilitiesDatabase>.instance.GetAttribute(abilityID, "DOTDuration"));
		float damage = Singleton<AbilitiesDatabase>.instance.Extrapolate<float>(abilityID, "infiniteUpgradeInitialDamage", "InitalDamage");
		float damagePerTick = float.Parse(Singleton<AbilitiesDatabase>.instance.GetAttribute(abilityID, "DOTDamage"));
		float tickFrequency = float.Parse(Singleton<AbilitiesDatabase>.instance.GetAttribute(abilityID, "DOTFrequency"));
		float fadeInTime = float.Parse(Singleton<AbilitiesDatabase>.instance.GetAttribute(abilityID, "ColorEffectFadeIn"));
		float fadeOutTime = float.Parse(Singleton<AbilitiesDatabase>.instance.GetAttribute(abilityID, "ColorEffectFadeOut"));
		float holdTime = float.Parse(Singleton<AbilitiesDatabase>.instance.GetAttribute(abilityID, "ColorEffectHoldColor"));
		GameObject prefab = Resources.Load(Singleton<AbilitiesDatabase>.instance.GetAttribute(abilityID, "smokeFX")) as GameObject;
		GameObject prefab2 = Resources.Load(Singleton<AbilitiesDatabase>.instance.GetAttribute(abilityID, "chargeFX")) as GameObject;
		foreach (Character item in enemiesAhead)
		{
			UnityEngine.Object.Destroy(item.controller.InstantiateObjectOnJoint(prefab, "head_effect"), num);
			UnityEngine.Object.Destroy(item.controller.InstantiateObjectOnJoint(prefab2, "body_effect"), num);
			item.RecievedAttack(EAttackType.Lightning, damage);
			item.SetDamageOverTime(damagePerTick, tickFrequency, num);
			item.MaterialColorFlicker(Color.yellow, fadeInTime, holdTime, fadeOutTime, num);
		}
	}

	private void OnGraveHandsAttack(string abilityID)
	{
		List<Character> enemiesAhead = GetEnemiesAhead(float.Parse(Singleton<AbilitiesDatabase>.instance.GetAttribute(abilityID, "attackRange")));
		float num = float.Parse(Singleton<AbilitiesDatabase>.instance.GetAttribute(abilityID, "DOTDuration"));
		float damage = Singleton<AbilitiesDatabase>.instance.Extrapolate<float>(abilityID, "infiniteUpgradeInitialDamage", "InitalDamage");
		float damagePerTick = float.Parse(Singleton<AbilitiesDatabase>.instance.GetAttribute(abilityID, "DOTDamage"));
		float tickFrequency = float.Parse(Singleton<AbilitiesDatabase>.instance.GetAttribute(abilityID, "DOTFrequency"));
		float fadeInTime = float.Parse(Singleton<AbilitiesDatabase>.instance.GetAttribute(abilityID, "ColorEffectFadeIn"));
		float fadeOutTime = float.Parse(Singleton<AbilitiesDatabase>.instance.GetAttribute(abilityID, "ColorEffectFadeOut"));
		float holdTime = float.Parse(Singleton<AbilitiesDatabase>.instance.GetAttribute(abilityID, "ColorEffectHoldColor"));
		foreach (Character item in enemiesAhead)
		{
			item.RecievedAttack(EAttackType.Blunt, damage);
			item.SetDamageOverTime(damagePerTick, tickFrequency, num);
			if (!item.isBoss)
			{
				item.controller.StopWalking();
				item.controller.stunnedTimer = num;
			}
			item.MaterialColorFlicker(Color.yellow, fadeInTime, holdTime, fadeOutTime, num);
		}
	}

	private void OnNightOfTheDeadAttack(string abilityID)
	{
		float allAlliesInvincibleTimer = Singleton<AbilitiesDatabase>.instance.Extrapolate<float>(abilityID, "infiniteUpgradeDuration", "Duration");
		WeakGlobalSceneBehavior<InGameImpl>.instance.allAlliesInvincibleTimer = allAlliesInvincibleTimer;
		base.controller.SpawnEffectAtJoint(Resources.Load("FX/NightOfDead") as GameObject, "impact_target", true);
	}

	private void OnReviveAttack(string abilityID)
	{
		base.health = base.maxHealth;
		WeakGlobalSceneBehavior<InGameImpl>.instance.gate.Revive();
		base.controller.startedDieAnim = false;
		List<Character> allEnemies = WeakGlobalInstance<CharactersManager>.instance.allEnemies;
		foreach (Character item in allEnemies)
		{
			if (item.isBoss)
			{
				item.RecievedAttack(EAttackType.Holy, item.maxHealth * 0.5f);
			}
			else
			{
				item.RecievedAttack(EAttackType.Holy, item.maxHealth);
			}
		}
	}

	private void OnLethargyAttack(string abilityID)
	{
		List<Character> allEnemies = WeakGlobalInstance<CharactersManager>.instance.allEnemies;
		float num = Singleton<AbilitiesDatabase>.instance.Extrapolate<float>(abilityID, "infiniteUpgradeDuration", "Duration");
		float speedMod = float.Parse(Singleton<AbilitiesDatabase>.instance.GetAttribute(abilityID, "SpeedSnareMultiplier"));
		float fadeInTime = float.Parse(Singleton<AbilitiesDatabase>.instance.GetAttribute(abilityID, "ColorEffectFadeIn"));
		float fadeOutTime = float.Parse(Singleton<AbilitiesDatabase>.instance.GetAttribute(abilityID, "ColorEffectFadeOut"));
		GameObject prefab = Resources.Load(Singleton<AbilitiesDatabase>.instance.GetAttribute(abilityID, "fx")) as GameObject;
		foreach (Character item in allEnemies)
		{
			UnityEngine.Object.Destroy(item.controller.InstantiateObjectOnJoint(prefab, "head_effect"), num);
			item.SetSpeedModifier(speedMod, num);
			Color color = new Color((float)int.Parse(Singleton<AbilitiesDatabase>.instance.GetAttribute(abilityID, "Red")) / 255f, (float)int.Parse(Singleton<AbilitiesDatabase>.instance.GetAttribute(abilityID, "Green")) / 255f, (float)int.Parse(Singleton<AbilitiesDatabase>.instance.GetAttribute(abilityID, "Blue")) / 255f);
			item.MaterialColorFadeInOut(color, fadeInTime, num, fadeOutTime);
		}
	}

	private void OnDivineInterventionAttack(string abilityID)
	{
		float num = float.Parse(Singleton<AbilitiesDatabase>.instance.GetAttribute(abilityID, "summonAllyChance"));
		if (UnityEngine.Random.Range(0f, 1f) <= num)
		{
			int randomHelperToSpawn = WeakGlobalInstance<Smithy>.instance.GetRandomHelperToSpawn();
			if (randomHelperToSpawn < 0)
			{
				SwitchToCursedWeapon(abilityID);
				return;
			}
			BoxCollider helperSpawnArea = WeakGlobalInstance<Smithy>.instance.helperSpawnArea;
			float num2 = float.Parse(Singleton<AbilitiesDatabase>.instance.GetAttribute(abilityID, "spawnDistance"));
			float num3 = ((!mIsLeftToRightGameplay) ? num2 : (0f - num2));
			Vector3 spawnPos = new Vector3(base.controlledObject.transform.position.x, base.controlledObject.transform.position.y + num2, base.controlledObject.transform.position.z + num3);
			for (int i = 0; i < 2; i++)
			{
				UnityEngine.Object.Instantiate(Resources.Load(Singleton<AbilitiesDatabase>.instance.GetAttribute(abilityID, "divineFX")), spawnPos, Quaternion.identity);
				Color color = new Color((float)int.Parse(Singleton<AbilitiesDatabase>.instance.GetAttribute(abilityID, "allyColorRed")) / 255f, (float)int.Parse(Singleton<AbilitiesDatabase>.instance.GetAttribute(abilityID, "allyColorGreen")) / 255f, (float)int.Parse(Singleton<AbilitiesDatabase>.instance.GetAttribute(abilityID, "allyColorBlue")) / 255f);
				Helper helper = WeakGlobalInstance<Smithy>.instance.SpawnForFree(randomHelperToSpawn, helperSpawnArea.size.x, spawnPos);
				helper.MaterialColorSetPermanentColor(color);
			}
		}
		else if (!Singleton<Profile>.instance.inBonusWave)
		{
			SwitchToCursedWeapon(abilityID);
		}
	}

	private void SwitchToCursedWeapon(string abilityID)
	{
		WeakGlobalSceneBehavior<InGameImpl>.instance.RunAfterDelay(RestoreMeleeWeapon, 15f);
		DestroyMeleeWeaponPrefabs();
		base.meleeWeaponPrefab = Resources.Load(Singleton<AbilitiesDatabase>.instance.GetAttribute(abilityID, "cursedWeaponPrefab")) as GameObject;
	}

	private void OnSummonTornadoAttack(string abilityID)
	{
		float num = Mathf.Abs(float.Parse(Singleton<AbilitiesDatabase>.instance.GetAttribute(abilityID, "spawnOffset")));
		float dirSign = (base.controller.facing == FacingType.Left) ? -1f : 1f;
		Vector3 vector = base.position;
		vector.z += num * dirSign;
		vector.y = WeakGlobalInstance<RailManager>.instance.GetY(vector.z);
		UnityEngine.Object.Instantiate(Resources.Load(Singleton<AbilitiesDatabase>.instance.GetAttribute(abilityID, "fx")) as GameObject, vector, Quaternion.identity);
	}

	private void OnGroundShockAttack(string abilityID)
	{
		float num = Mathf.Abs(float.Parse(Singleton<AbilitiesDatabase>.instance.GetAttribute(abilityID, "spawnOffset")));
		float dirSign = (base.controller.facing == FacingType.Left) ? -1f : 1f;
		Vector3 vector = base.position;
		vector.z += num * dirSign;
		vector.y = WeakGlobalInstance<RailManager>.instance.GetY(vector.z);
		UnityEngine.Object.Instantiate(Resources.Load(Singleton<AbilitiesDatabase>.instance.GetAttribute(abilityID, "fx")) as GameObject, vector, Quaternion.identity);
	}

	private void OnGiantWaveAttack(string abilityID)
	{
		Vector3 vector = WeakGlobalInstance<Smithy>.instance.helperSpawnArea.transform.position;
		float num = float.Parse(Singleton<AbilitiesDatabase>.instance.GetAttribute(abilityID, "spawnDistance"));
		float num2 = ((!mIsLeftToRightGameplay) ? num : (0f - num));
		vector.z = WeakGlobalInstance<Smithy>.instance.helperSpawnArea.transform.position.z + num2;
		if (WeakGlobalInstance<RailManager>.instance != null)
		{
			vector.y = WeakGlobalInstance<RailManager>.instance.GetY(vector.z);
		}
		UnityEngine.Object.Instantiate(Resources.Load(Singleton<AbilitiesDatabase>.instance.GetAttribute(abilityID, "fx")) as GameObject, vector, Quaternion.identity);
	}

	private void OnSpecialAttackDone()
	{
		mPerformingSpecialAttack = false;
	}

	private void RestoreMeleeWeapon()
	{
		DestroyMeleeWeaponPrefabs();
		if (mCachedMeleeWeaponPrefab.Count == 0)
		{
			int swordLvl = (SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive) ? 8 : Singleton<Profile>.instance.swordLevel;
			SDFTreeNode root = SingletonMonoBehaviour<ResourcesManager>.instance.Open("Registry/Weapons/" + Singleton<Profile>.instance.swordID);
			string activeCharm = WeakGlobalSceneBehavior<InGameImpl>.instance.activeCharm;
			for (int i = 0; i < normalMeleePrefabID.Length; i++)
			{
				string text = Singleton<CharmsDatabase>.instance.GetAttribute(activeCharm, charmMeleePrefabID[i]);
				if (text == string.Empty)
				{
					text = InfiniteUpgrades.SnapToHighest<string>(root, normalMeleePrefabID[i], swordLvl);
				}
				if (text != string.Empty)
				{
					mCachedMeleeWeaponPrefab.Add(text);
				}
			}
		}
		List<GameObject> list = new List<GameObject>();
		foreach (string item in mCachedMeleeWeaponPrefab)
		{
			list.Add(Resources.Load(item) as GameObject);
		}
		base.meleeWeaponPrefabAsList = list;
	}

	private void onMoveLeft()
	{
		if (WeakGlobalInstance<TutorialHookup>.instance != null)
		{
			WeakGlobalInstance<TutorialHookup>.instance.playerPressedLeft = true;
		}
		if (!mPerformingSpecialAttack)
		{
			base.controller.facing = FacingType.Left;
			base.controller.StartWalkLeft();
			SetRangeAttackMode(false);
		}
	}

	private void onMoveRight()
	{
		if (WeakGlobalInstance<TutorialHookup>.instance != null)
		{
			WeakGlobalInstance<TutorialHookup>.instance.playerPressedRight = true;
		}
		if (!mPerformingSpecialAttack)
		{
			base.controller.facing = FacingType.Right;
			base.controller.StartWalkRight();
			SetRangeAttackMode(false);
		}
	}

	private void onDontMove()
	{
		if (base.controller.isMoving)
		{
			base.controller.StopWalking();
			FaceNearestEnemyOrDefault();
		}
	}

	private string GetHeroPrefab(SDFTreeNode data)
	{
		if (Singleton<Profile>.instance.inBonusWave)
		{
			string text = data["ghostPrefab"];
			if (text != string.Empty)
			{
				return text;
			}
			if (Singleton<PlayModesManager>.instance.selectedMode == "zombies")
			{
				return "Characters/PFZombieHero";
			}
			return "Characters/PFHeroGhost";
		}
		string text2 = data["prefab"];
		if (WeakGlobalSceneBehavior<InGameImpl>.instance.activeCharm != string.Empty)
		{
			string attribute = Singleton<CharmsDatabase>.instance.GetAttribute(WeakGlobalSceneBehavior<InGameImpl>.instance.activeCharm, "heroPrefabSwap", text2);
			if (attribute != string.Empty)
			{
				return attribute;
			}
		}
		return text2;
	}

	private GameObject FindChildWithTag(GameObject go, string tagName)
	{
		return TraverseHierarchy(go.transform, tagName);
	}

	private GameObject TraverseHierarchy(Transform root, string tagName)
	{
		foreach (Transform item in root)
		{
			if (item.gameObject.tag == tagName)
			{
				return item.gameObject;
			}
			GameObject gameObject = TraverseHierarchy(item, tagName);
			if (gameObject != null)
			{
				return gameObject;
			}
		}
		return null;
	}

	private void UpdateHealthThreshold()
	{
		if (base.health <= GetHealthThreshold(2, base.maxHealth))
		{
			mHealthBarToShow = "Sprites/HUD/life_bar_red";
		}
		else if (base.health <= GetHealthThreshold(1, base.maxHealth))
		{
			mHealthBarToShow = "Sprites/HUD/life_bar_yellow";
		}
		else
		{
			mHealthBarToShow = "Sprites/HUD/life_bar_green";
		}
	}

	private void NotifyTutorials_InRangeOfAttack(bool val)
	{
		if (WeakGlobalInstance<TutorialHookup>.instance != null)
		{
			WeakGlobalInstance<TutorialHookup>.instance.enemyIsInRangeOFAttack = val;
		}
	}

	private void SetDefaultFacing()
	{
		if (SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive)
		{
			mIsLeftToRightGameplay = (SVZMultiplayerManager.Instance.P1Side == SVZMultiplayerManager.BattlefieldSide.LeftGate);
		}
		base.controller.facing = (mIsLeftToRightGameplay ? FacingType.Right : FacingType.Left);
	}

	private List<Character> GetEnemiesAhead(float range)
	{
		float absRange = Mathf.Abs(range);
		bool allowBack = (SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive && SVZMultiplayerManager.Instance.AllowBackAttack);
		float backPad = allowBack ? 40f : 0f;
		if (base.controller.facing == FacingType.Right)
		{
			return WeakGlobalInstance<CharactersManager>.instance.GetCharactersInRange(base.controller.position.z - backPad, base.controller.position.z + absRange, true);
		}
		return WeakGlobalInstance<CharactersManager>.instance.GetCharactersInRange(base.controller.position.z - absRange, base.controller.position.z + backPad, true);
	}

	private bool CheckAbilitySelected(string abilityID)
	{
		List<string> selectedAbilities = Singleton<Profile>.instance.GetSelectedAbilities();
		foreach (string item in selectedAbilities)
		{
			if (abilityID == item)
			{
				return true;
			}
		}
		return false;
	}
}
