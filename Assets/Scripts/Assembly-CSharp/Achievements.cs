using System.Collections.Generic;
using UnityEngine;

public class Achievements : SingletonMonoBehaviour<Achievements>
{
	private const float kPriority = 50000f;

	private const float kYOut = -26f;

	private const float kYIn = 32f;

	private const float kSpeed = 0.3f;

	private const float kShowingTimer = 2f;

	private const int kTextMaxWidth = 390;

	private readonly Vector2 kTextOffset = new Vector2(18f, 0f);

	private SUISprite mPopup;

	private SUILabel mText;

	private float mYPos = -26f;

	private float mSlideDir;

	private float mWaitTimer;

	private List<string> mQueue = new List<string>();

	private float deltaTime
	{
		get
		{
			float num = ((Time.timeScale != 1f) ? SUIScreen.deltaTime : Time.deltaTime);
			if (num > 0.1f)
			{
				num = 0.1f;
			}
			return num;
		}
	}

	private Vector2 position
	{
		get
		{
			return mPopup.position;
		}
		set
		{
			if (popupIsAlive)
			{
				mPopup.position = value;
				mText.position = value + kTextOffset;
			}
		}
	}

	public void Init()
	{
	}

	public void Update()
	{
		if (mPopup != null && !popupIsAlive)
		{
			ClearPopupReferences();
		}
		if (mPopup != null)
		{
			UpdatePopup();
		}
		else
		{
			CheckForNextPopup();
		}
	}

	public void Award(string gameCenterAchievementID)
	{
		AddToQueue(gameCenterAchievementID);
	}

	private bool AddToQueue(string gameCenterAchievementID)
	{
		List<string> achievementsDisplayed = Singleton<Profile>.instance.GetAchievementsDisplayed();
		if (achievementsDisplayed.Contains(gameCenterAchievementID))
		{
			return false;
		}
		
		achievementsDisplayed.Add(gameCenterAchievementID);
		Singleton<Profile>.instance.SetAchievementsDisplayed(achievementsDisplayed);
		
		// Add to visual queue
		mQueue.Add(gameCenterAchievementID);
		
		// Process Rewards
		AchievementData data = Singleton<AchievementsDatabase>.instance.GetData(gameCenterAchievementID);
		if (data != null)
		{
			if (data.RewardGems > 0)
			{
				Singleton<Profile>.instance.gems += data.RewardGems;
			}
			// Reward XP could be added to player level if that exists
            Singleton<Profile>.instance.Save();
		}
		
		return true;
	}

	private void StartNewSlideAnimation()
	{
		mYPos = -26f;
		mSlideDir = 1f;
		mWaitTimer = 0f;
	}

	private SUISprite mIcon;

	private bool popupIsAlive
	{
		get
		{
			return mPopup != null && mPopup.gameObject != null && mText != null && mText.gameObject != null && mIcon != null && mIcon.gameObject != null;
		}
	}

	private void SpawnPopup()
	{
		mPopup = new SUISprite("Sprites/Menus/gamecenter_popup");
		mPopup.priority = 50000f;
		
		mText = new SUILabel("default18");
		mText.priority = 50000.1f;
		mText.maxWidth = 300;
		mText.maxLines = 1;
		mText.anchor = TextAnchor.MiddleLeft;
		mText.alignment = TextAlignment.Left;
		
		mIcon = new SUISprite("Sprites/Menus/achievement_kill"); // Default
		mIcon.priority = 50000.1f;
		mIcon.scale = new Vector2(0.35f, 0.35f);
		
		UpdatePositions();
	}

	private void UpdatePositions()
	{
		if (popupIsAlive)
		{
			mPopup.position = new Vector2(512f, mYPos);
			mIcon.position = mPopup.position + new Vector2(-180f, 0f);
			mText.position = mPopup.position + new Vector2(-140f, 0f);
		}
	}

	private void UpdatePopup()
	{
		if (mSlideDir == 0f)
		{
			mWaitTimer += deltaTime;
			if (mWaitTimer >= 3f)
			{
				mSlideDir = -1f;
			}
			return;
		}
		
		mYPos += 193.33333f * deltaTime * mSlideDir;
		if (mYPos <= -26f)
		{
			mYPos = -26f;
			CheckForNextPopup();
		}
		else if (mYPos >= 32f)
		{
			mYPos = 32f;
			mWaitTimer = 0f;
			mSlideDir = 0f;
		}
		UpdatePositions();
	}

	private void PopAndDrawNextText()
	{
		string id = mQueue[0];
		mQueue.RemoveAt(0);
		
		AchievementData data = Singleton<AchievementsDatabase>.instance.GetData(id);
		if (data != null)
		{
			mText.text = data.Name.ToUpper();
			if (!string.IsNullOrEmpty(data.IconPath))
			{
				mIcon.texture = data.IconPath;
			}
		}
		else
		{
			string empty = string.Empty;
			empty = ((!Singleton<Localizer>.instance.Has(id)) ? Singleton<Localizer>.instance.Get("achievement_notext") : Singleton<Localizer>.instance.Get(id));
			mText.text = empty;
		}
		
		if (Singleton<SUISoundManager>.instance != null)
		{
            Singleton<SUISoundManager>.instance.Play("AchievementUnlock");
		}
	}

	private void CheckForNextPopup()
	{
		if (mQueue.Count > 0)
		{
			StartNewSlideAnimation();
			if (mPopup == null)
			{
				SpawnPopup();
			}
			PopAndDrawNextText();
		}
		else if (mPopup != null)
		{
			mPopup.Destroy();
			mPopup = null;
			mText.Destroy();
			mText = null;
			if (mIcon != null) { mIcon.Destroy(); mIcon = null; }
		}
	}

	private void ClearPopupReferences()
	{
		mPopup = null;
		mText = null;
		mIcon = null;
	}
}
