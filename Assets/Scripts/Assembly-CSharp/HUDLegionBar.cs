using UnityEngine;

public class HUDLegionBar
{
	private SUILabel mPercentRef;

	private int mPreviouslyKilledEnemies = -1;

	private int mPreviouslyTotalEnemies = -1;

	public HUDLegionBar(SUILayout layout)
	{
		mPercentRef = (SUILabel)layout["legionPercent"];
	}

	public void Update()
	{
		if (WeakGlobalInstance<WaveManager>.instance != null)
		{
			UpdateProgress();
		}
	}

	public bool IsTouchZone(Vector2 pos)
	{
		return false;
	}

	private void UpdateProgress()
	{
		if (mPercentRef == null || WeakGlobalInstance<WaveManager>.instance == null)
		{
			return;
		}
		int enemiesKilledSoFar = WeakGlobalInstance<WaveManager>.instance.enemiesKilledSoFar;
		int totalEnemies = WeakGlobalInstance<WaveManager>.instance.totalEnemies;
		if (mPreviouslyKilledEnemies != enemiesKilledSoFar || mPreviouslyTotalEnemies != totalEnemies)
		{
			mPreviouslyKilledEnemies = enemiesKilledSoFar;
			mPreviouslyTotalEnemies = totalEnemies;
			if (totalEnemies <= 0)
			{
				mPercentRef.text = "0%";
				return;
			}
			int num = Mathf.Clamp(enemiesKilledSoFar * 100 / totalEnemies, 0, 100);
			mPercentRef.text = num + "%";
		}
	}
}
