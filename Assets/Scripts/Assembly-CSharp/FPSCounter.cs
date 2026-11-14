using UnityEngine;

public class FPSCounter
{
	private const string PREF_SHOW_FPS = "SVZ_ShowFPS";
	private const string PREF_FPS_POSITION = "SVZ_FPSPosition";
	private const float kUpdateInterval = 0.25f;
	private static readonly string[] kPositionLabels = { "TOP LEFT", "TOP RIGHT", "BOTTOM LEFT", "BOTTOM RIGHT" };

	private SUISprite mPanel;

	private SUILabel mLabel;

	private float mElapsed;

	private int mFrameCount;

	private float mMinFPS = float.MaxValue;

	private float mAverageFPS;

	private int mSampleCount;

	private int mLastPosition = -1;

	private bool mLastVisible;

	public static int positionCount
	{
		get
		{
			return kPositionLabels.Length;
		}
	}

	public static bool showFPS
	{
		get
		{
			if (SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive)
			{
				return false;
			}
			return false;
		}
		set
		{
			PlayerPrefs.SetInt(PREF_SHOW_FPS, value ? 1 : 0);
			PlayerPrefs.Save();
		}
	}

	public static int positionIndex
	{
		get
		{
			return Mathf.Clamp(PlayerPrefs.GetInt(PREF_FPS_POSITION, 1), 0, kPositionLabels.Length - 1);
		}
		set
		{
			PlayerPrefs.SetInt(PREF_FPS_POSITION, Mathf.Clamp(value, 0, kPositionLabels.Length - 1));
			PlayerPrefs.Save();
		}
	}

	public static string positionLabel
	{
		get
		{
			return kPositionLabels[positionIndex];
		}
	}

	public static void CyclePosition()
	{
		positionIndex = (positionIndex + 1) % kPositionLabels.Length;
	}

	public FPSCounter(string font, Vector2 pos, float priority)
	{
		mPanel = new SUISprite(new Color(0f, 0f, 0f, 0.65f), 180, 72);
		mPanel.autoscaleKeepAspectRatio = false;
		mPanel.priority = priority - 1f;
		mPanel.alpha = 0.9f;
		mLabel = new SUILabel(font);
		mLabel.priority = priority;
		mLabel.shadowOffset = new Vector2(2f, 2f);
		mLabel.shadowColor = Color.black;
		mLabel.text = "FPS --\nAVG --  MIN --\n-- ms";
		ApplySettings(true);
	}

	public void Update()
	{
		ApplySettings(false);
		if (!mLastVisible)
		{
			return;
		}
		float delta = Time.unscaledDeltaTime;
		if (delta <= 0f)
		{
			return;
		}
		mElapsed += delta;
		mFrameCount++;
		if (mElapsed >= kUpdateInterval)
		{
			float fps = mFrameCount / mElapsed;
			float ms = 1000f / Mathf.Max(fps, 0.01f);
			mMinFPS = Mathf.Min(mMinFPS, fps);
			mSampleCount++;
			mAverageFPS = (mSampleCount <= 1) ? fps : Mathf.Lerp(mAverageFPS, fps, 0.12f);
			mLabel.text = "FPS " + Mathf.RoundToInt(fps) + "\nAVG " + Mathf.RoundToInt(mAverageFPS) + "  MIN " + Mathf.RoundToInt(mMinFPS) + "\n" + ms.ToString("0.0") + " ms";
			mElapsed = 0f;
			mFrameCount = 0;
		}
	}

	private void ApplySettings(bool force)
	{
		bool visible = showFPS;
		if (force || visible != mLastVisible)
		{
			mPanel.visible = visible;
			mLabel.visible = visible;
			mLastVisible = visible;
		}
		int position = positionIndex;
		if (force || position != mLastPosition)
		{
			mLastPosition = position;
			ApplyPosition(position);
		}
	}

	private void ApplyPosition(int position)
	{
		const float margin = 16f;
		const float panelMargin = 8f;
		switch (position)
		{
		case 1:
			mPanel.hotspot = new Vector2(1f, 0f);
			mPanel.position = new Vector2(SUIScreen.width - margin + panelMargin, margin - panelMargin);
			mLabel.anchor = TextAnchor.UpperRight;
			mLabel.alignment = TextAlignment.Right;
			mLabel.position = new Vector2(SUIScreen.width - margin, margin);
			break;
		case 2:
			mPanel.hotspot = new Vector2(0f, 1f);
			mPanel.position = new Vector2(margin - panelMargin, SUIScreen.height - margin + panelMargin);
			mLabel.anchor = TextAnchor.LowerLeft;
			mLabel.alignment = TextAlignment.Left;
			mLabel.position = new Vector2(margin, SUIScreen.height - margin);
			break;
		case 3:
			mPanel.hotspot = new Vector2(1f, 1f);
			mPanel.position = new Vector2(SUIScreen.width - margin + panelMargin, SUIScreen.height - margin + panelMargin);
			mLabel.anchor = TextAnchor.LowerRight;
			mLabel.alignment = TextAlignment.Right;
			mLabel.position = new Vector2(SUIScreen.width - margin, SUIScreen.height - margin);
			break;
		default:
			mPanel.hotspot = new Vector2(0f, 0f);
			mPanel.position = new Vector2(margin - panelMargin, margin - panelMargin);
			mLabel.anchor = TextAnchor.UpperLeft;
			mLabel.alignment = TextAlignment.Left;
			mLabel.position = new Vector2(margin, margin);
			break;
		}
		mLabel.shadowOffset = new Vector2(2f, 2f);
	}
}
