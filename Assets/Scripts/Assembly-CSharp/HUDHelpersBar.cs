using System;
using System.Collections.Generic;
using UnityEngine;

public class HUDHelpersBar
{
	private SUILayout mLayoutRef;

	private List<HUDIconButton> mHelperHUDs = new List<HUDIconButton>();

	private bool mCreatedForPvP = false;

	public HUDHelpersBar(SUILayout layoutRef)
	{
		mLayoutRef = layoutRef;
	}

	private static Vector2 GetIconPosition(int index, int totalCount = 5)
	{
		if (totalCount > 5)
		{
			float startX = 56f;
			float endX = SUIScreen.width - 56f;
			float step = (endX - startX) / (float)Mathf.Max(1, totalCount - 1);
			return new Vector2(startX + index * step, SUIScreen.height - 64f);
		}
		return new Vector2(index * 110 + 70, SUIScreen.height - 70f);
	}

	public void Update()
	{
		bool isPvP = mCreatedForPvP || (SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive);
		if (!isPvP)
		{
			for (int i = 0; i < 5; i++)
			{
				if (mHelperHUDs.Count > i && PCInput.GetButtonDown("Helper" + (i + 1)))
				{
					mHelperHUDs[i].Trigger();
				}
			}
		}
		if (WeakGlobalInstance<Smithy>.instance == null)
		{
			return;
		}
		if (mHelperHUDs.Count == 0)
		{
			CreateHUD();
		}
		foreach (HUDIconButton mHelperHUD in mHelperHUDs)
		{
			mHelperHUD.UpdateCoolDown();
		}
	}

	public bool IsTouchZone(Vector2 pos)
	{
		foreach (HUDIconButton mHelperHUD in mHelperHUDs)
		{
			if (mHelperHUD.IsTouchZone(pos))
			{
				return true;
			}
		}
		return false;
	}

	private void DestroyHUD()
	{
		foreach (HUDIconButton mHelperHUD in mHelperHUDs)
		{
			mHelperHUD.Destroy();
		}
		mHelperHUDs.Clear();
	}

	private void CreateHUD()
	{
		DestroyHUD();
		if (SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive)
		{
			mCreatedForPvP = true;
			bool isClientP2 = SVZMultiplayerManager.Instance.IsOnlineClient;
			List<string> deck = isClientP2 ? SVZMultiplayerManager.Instance.P2Deck : SVZMultiplayerManager.Instance.P1Deck;
			int total = deck.Count;
			float iconScale = (total > 5) ? 0.85f : 1f;
			for (int i = 0; i < total; i++)
			{
				int index = i;
				string unitId = deck[i];
				string iconFile = SVZMultiplayerManager.GetUnitIconPath(unitId);
				HUDIconButton hUDIconButton = new HUDIconButton(mLayoutRef, GetIconPosition(i, total), iconFile, "Helper", i, isAvailCallback, getCoolDownCallback, iconScale);
				hUDIconButton.onTriggered = delegate
				{
					onHelperTriggered(index);
				};
				mHelperHUDs.Add(hUDIconButton);
			}
			return;
		}
		mCreatedForPvP = false;
		if (WeakGlobalInstance<Smithy>.instance.numTypes <= 0)
		{
			return;
		}
		for (int i = 0; i < WeakGlobalInstance<Smithy>.instance.numTypes; i++)
		{
			int index = i;
			HUDIconButton hUDIconButton = new HUDIconButton(mLayoutRef, GetIconPosition(i), WeakGlobalInstance<Smithy>.instance.GetIconFile(i), "Helper", i, isAvailCallback, getCoolDownCallback);
			hUDIconButton.onTriggered = delegate
			{
				onHelperTriggered(index);
			};
			mHelperHUDs.Add(hUDIconButton);
		}
	}

	private bool isAvailCallback(int index)
	{
		if (WeakGlobalInstance<Smithy>.instance == null)
		{
			return false;
		}
		if (mCreatedForPvP || (SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive))
		{
			if (SVZMultiplayerManager.Instance == null || !SVZMultiplayerManager.Instance.IsActive)
			{
				return false;
			}
			if (WeakGlobalSceneBehavior<InGameImpl>.instance == null || WeakGlobalSceneBehavior<InGameImpl>.instance.gameOver)
			{
				return false;
			}
			if (SVZMultiplayerManager.Instance.IsOnlineClient)
			{
				return SVZMultiplayerManager.Instance.IsP2SlotAvailable(index);
			}
			return SVZMultiplayerManager.Instance.IsP1SlotAvailable(index);
		}
		return WeakGlobalInstance<Smithy>.instance.IsAvailable(index);
	}

	private float getCoolDownCallback(int index)
	{
		if (WeakGlobalInstance<Smithy>.instance == null)
		{
			return 0f;
		}
		if (mCreatedForPvP || (SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive))
		{
			if (SVZMultiplayerManager.Instance == null || !SVZMultiplayerManager.Instance.IsActive)
			{
				return 0f;
			}
			if (SVZMultiplayerManager.Instance.IsOnlineClient)
			{
				return SVZMultiplayerManager.Instance.GetP2SlotCooldownRatio(index);
			}
			return SVZMultiplayerManager.Instance.GetP1SlotCooldownRatio(index);
		}
		return WeakGlobalInstance<Smithy>.instance.GetCoolDown(index);
	}

	private void onHelperTriggered(int index)
	{
		if (mCreatedForPvP || (SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive))
		{
			if (SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive &&
			    WeakGlobalSceneBehavior<InGameImpl>.instance != null && !WeakGlobalSceneBehavior<InGameImpl>.instance.gameOver)
			{
				if (SVZMultiplayerManager.Instance.IsOnlineClient)
				{
					SVZMultiplayerManager.Instance.SummonPlayer2Unit(index);
				}
				else
				{
					SVZMultiplayerManager.Instance.SummonPlayer1Slot(index);
				}
			}
			return;
		}
		if (WeakGlobalInstance<Smithy>.instance != null && !WeakGlobalSceneBehavior<InGameImpl>.instance.gameOver && WeakGlobalInstance<Smithy>.instance.IsAvailable(index))
		{
			WeakGlobalInstance<Smithy>.instance.Spawn(index);
			if (WeakGlobalInstance<TutorialHookup>.instance != null)
			{
				WeakGlobalInstance<TutorialHookup>.instance.summonedAlly = true;
			}
			Profile instance = Singleton<Profile>.instance;
			if (instance.hasSummonedFarmer && instance.hasSummonedSwordWarrior && instance.hasSummonedSpearWarrior && instance.hasSummonedBowman && instance.hasSummonedPanzerSamurai && instance.hasSummonedPriest && instance.hasSummonedNobunaga && instance.hasSummonedSpearHorseman && instance.hasSummonedTakeda && instance.hasSummonedRifleman && instance.hasSummonedFrostie && instance.hasSummonedSwordsmith)
			{
				SingletonMonoBehaviour<Achievements>.instance.Award("SAMUZOMBIE_ACHIEVE_WHOS_ALL_HERE");
			}
		}
	}
}
