using System.Collections.Generic;
using UnityEngine;

public class HUDAbilities
{
	private class CoolDownData
	{
		public float current;

		public float max;

		public CoolDownData()
		{
		}

		public CoolDownData(float c, float m)
		{
			current = c;
			max = m;
		}
	}

	private SUILayout mLayoutRef;

	private List<HUDIconButton> mAbilityButtons = new List<HUDIconButton>();

	private List<CoolDownData> mCooldown = new List<CoolDownData>();

	public void ResetAllCooldowns()
	{
		for (int i = 0; i < mCooldown.Count; i++)
		{
			if (mCooldown[i] != null)
			{
				mCooldown[i].current = mCooldown[i].max;
			}
		}
	}

	private OnSUIStringCallback mOnAbilityTrigger;

	private int? mQueuedAbility;

	public OnSUIStringCallback onAbilityTriggered
	{
		get
		{
			return mOnAbilityTrigger;
		}
		set
		{
			mOnAbilityTrigger = value;
		}
	}

	public HUDAbilities(SUILayout layoutRef)
	{
		mLayoutRef = layoutRef;
	}

	private static Vector2 GetIconPosition(int index)
	{
		return new Vector2(SUIScreen.width - 70f - (float)(index * 110), SUIScreen.height - 70f);
	}

	public void Update()
	{
		if (SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive &&
		    SVZMultiplayerManager.Instance.CurrentGameMode == SVZMultiplayerManager.MultiplayerGameMode.ArmyOnly)
		{
			if (mAbilityButtons.Count > 0)
			{
				DestroyHUD();
			}
			return;
		}
		if (mAbilityButtons.Count > 0 && PCInput.GetButtonDown("Skill1"))
		{
			mAbilityButtons[0].Trigger();
		}
		else if (mAbilityButtons.Count > 1 && PCInput.GetButtonDown("Ability1"))
		{
			mAbilityButtons[1].Trigger();
		}
		else if (mAbilityButtons.Count > 2 && PCInput.GetButtonDown("Ability2"))
		{
			mAbilityButtons[2].Trigger();
		}
		if (mAbilityButtons.Count == 0)
		{
			CreateHUD();
		}
		UpdateSpecialCaseTutorial();
		foreach (CoolDownData item in mCooldown)
		{
			item.current = Mathf.Min(item.current + Time.deltaTime, item.max);
		}
		foreach (HUDIconButton mAbilityButton in mAbilityButtons)
		{
			mAbilityButton.UpdateCoolDown();
		}
		int? num = mQueuedAbility;
		if (num.HasValue)
		{
			onTriggered(mQueuedAbility.Value);
			int? num2 = mQueuedAbility;
			if (num2.HasValue && (WeakGlobalSceneBehavior<InGameImpl>.instance.hero.health == 0f || WeakGlobalSceneBehavior<InGameImpl>.instance.gameOver))
			{
				mQueuedAbility = null;
			}
		}
	}

	public bool IsTouchZone(Vector2 pos)
	{
		foreach (HUDIconButton mAbilityButton in mAbilityButtons)
		{
			if (mAbilityButton.IsTouchZone(pos))
			{
				return true;
			}
		}
		return false;
	}

	private void DestroyHUD()
	{
		foreach (HUDIconButton mAbilityButton in mAbilityButtons)
		{
			mAbilityButton.Destroy();
		}
		mAbilityButtons.Clear();
		mCooldown.Clear();
	}

	private List<string> GetActiveAbilitiesList()
	{
		if (SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive)
		{
			if (SVZMultiplayerManager.Instance.CurrentGameMode == SVZMultiplayerManager.MultiplayerGameMode.ArmyOnly)
			{
				return new List<string>();
			}
			SVZMultiplayerManager.Instance.EnsureValidSpellsForPlayer(1);
			SVZMultiplayerManager.Instance.EnsureValidSpellsForPlayer(2);
			if (SVZMultiplayerManager.Instance.IsOnlineClient)
			{
				List<string> p2 = SVZMultiplayerManager.Instance.P2Spells;
				if (p2 != null && p2.Count > 0)
				{
					return new List<string>(p2);
				}
			}
			else
			{
				List<string> p1 = SVZMultiplayerManager.Instance.P1Spells;
				if (p1 != null && p1.Count > 0)
				{
					return new List<string>(p1);
				}
			}
			if (Singleton<PlayModesManager>.instance != null && Singleton<PlayModesManager>.instance.selectedMode == "zombies")
			{
				return new List<string> { "DragonAttack", "LethargyZM" };
			}
			return new List<string> { "GiantWave", "Lethargy" };
		}
		return Singleton<Profile>.instance.GetSelectedAbilities();
	}

	private void CreateHUD()
	{
		DestroyHUD();
		List<string> selectedAbilities = GetActiveAbilitiesList();
		for (int i = 0; i < selectedAbilities.Count; i++)
		{
			int index = i;
			string text = selectedAbilities[i];
			SDFTreeNode sDFTreeNode = SingletonMonoBehaviour<ResourcesManager>.instance.Open("Registry/Abilities/" + text);
			if (sDFTreeNode == null)
			{
				Debug.Log("ERROR: Unknown special ability: " + text);
				mCooldown.Add(new CoolDownData());
				continue;
			}
			string cdStr = sDFTreeNode.hasAttribute("coolDown") ? sDFTreeNode["coolDown"] : (sDFTreeNode.hasAttribute("cooldown") ? sDFTreeNode["cooldown"] : "15");
			float m = 15f;
			float.TryParse(cdStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out m);
			mCooldown.Add(new CoolDownData(0f, m));
			HUDIconButton hUDIconButton = new HUDIconButton(mLayoutRef, GetIconPosition(i), sDFTreeNode["icon"], "Ability", i, isAvailCallback, getCoolDownCallback);
			hUDIconButton.tag = text;
			hUDIconButton.onTriggered = delegate
			{
				onTriggered(index);
			};
			mAbilityButtons.Add(hUDIconButton);
		}
	}

	private bool isAvailCallback(int index)
	{
		if (SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive)
		{
			if (SVZMultiplayerManager.Instance.CurrentGameMode == SVZMultiplayerManager.MultiplayerGameMode.ArmyOnly)
			{
				return mCooldown[index].current == mCooldown[index].max && !WeakGlobalSceneBehavior<InGameImpl>.instance.gameOver;
			}
			if (SVZMultiplayerManager.Instance.IsOnlineClient)
			{
				OpponentHero oppHero = SVZMultiplayerManager.Instance.CurrentOpponentHero;
				bool p2Alive = oppHero == null || oppHero.health > 0f;
				return mCooldown[index].current == mCooldown[index].max && p2Alive && !WeakGlobalSceneBehavior<InGameImpl>.instance.gameOver;
			}
			return mCooldown[index].current == mCooldown[index].max && WeakGlobalSceneBehavior<InGameImpl>.instance.hero.health > 0f && !WeakGlobalSceneBehavior<InGameImpl>.instance.gameOver;
		}
		if (mCooldown[index].current != mCooldown[index].max || !(WeakGlobalSceneBehavior<InGameImpl>.instance.hero.health > 0f))
		{
			goto IL_0070;
		}
		if (shouldQueueAbility())
		{
			int? num = mQueuedAbility;
			if (num.HasValue)
			{
				goto IL_0070;
			}
		}
		int result = ((!WeakGlobalSceneBehavior<InGameImpl>.instance.gameOver) ? 1 : 0);
		goto IL_0071;
		IL_0071:
		return (byte)result != 0;
		IL_0070:
		result = 0;
		goto IL_0071;
	}

	private bool isAvailWithoutQueue(int index)
	{
		if (SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive)
		{
			if (SVZMultiplayerManager.Instance.CurrentGameMode == SVZMultiplayerManager.MultiplayerGameMode.ArmyOnly)
			{
				return mCooldown[index].current == mCooldown[index].max && !WeakGlobalSceneBehavior<InGameImpl>.instance.gameOver;
			}
			if (SVZMultiplayerManager.Instance.IsOnlineClient)
			{
				OpponentHero oppHero = SVZMultiplayerManager.Instance.CurrentOpponentHero;
				bool p2Alive = oppHero == null || oppHero.health > 0f;
				return mCooldown[index].current == mCooldown[index].max && p2Alive && !WeakGlobalSceneBehavior<InGameImpl>.instance.gameOver;
			}
		}
		return mCooldown[index].current == mCooldown[index].max && WeakGlobalSceneBehavior<InGameImpl>.instance.hero.health > 0f && !WeakGlobalSceneBehavior<InGameImpl>.instance.gameOver;
	}

	private bool shouldQueueAbility()
	{
		if (SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive)
		{
			return false;
		}
		return !WeakGlobalSceneBehavior<InGameImpl>.instance.hero.canUseSpecialAttack;
	}

	private float getCoolDownCallback(int index)
	{
		return mCooldown[index].current / mCooldown[index].max;
	}

	private void onTriggered(int index)
	{
		if (!isAvailWithoutQueue(index))
		{
			return;
		}
		if (shouldQueueAbility())
		{
			int? num = mQueuedAbility;
			if (!num.HasValue)
			{
				mQueuedAbility = index;
			}
		}
		else if (isAvailCallback(index) && mOnAbilityTrigger != null)
		{
			List<string> activeList = GetActiveAbilitiesList();
			if (index >= 0 && index < activeList.Count)
			{
				mOnAbilityTrigger(activeList[index]);
				mCooldown[index].current = 0f;
				mQueuedAbility = null;
				if (SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive)
				{
					SVZMultiplayerManager.Instance.P1AbilitiesUsed++;
				}
				if (WeakGlobalInstance<TutorialHookup>.instance != null)
				{
					WeakGlobalInstance<TutorialHookup>.instance.usedAbility = true;
				}
			}
		}
	}

	private void UpdateSpecialCaseTutorial()
	{
		if (mAbilityButtons.Count == 1 && string.Compare(mAbilityButtons[0].tag, "KatanaSlash", true) == 0 && Singleton<PlayStatistics>.instance.stats.lastWavePlayed == 1 && Singleton<PlayStatistics>.instance.stats.lastWaveLevel == 1)
		{
			mAbilityButtons[0].visible = WeakGlobalInstance<TutorialHookup>.instance.showKatanaSlash;
		}
	}
}
