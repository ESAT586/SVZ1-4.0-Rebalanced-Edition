using UnityEngine;

public class NUF
{
	public delegate void OnNeverShowAlertAgainDelegate();

	public static int ScheduleNotification(int timeDelay, string messageBody, string buttonText, string customSoundFilename)
	{
		return 1;
	}

	public static int CancelAllLocalNotification()
	{
		return 1;
	}

	public static int PresentLocalNotificationNow(string messageBody, string buttonText, string customSoundFilename)
	{
		return 1;
	}

	public static bool IsAppStorePaymentsAllowed()
	{
		return true;
	}

	public static void PresentRateMeAlert(string title, string messageBody, string cancelButton, string acceptButton, string url, string neverButton, OnNeverShowAlertAgainDelegate neverAgainCallback)
	{
		if (neverAgainCallback != null)
		{
			neverAgainCallback();
		}
	}

	public static void AllowPortrait(bool allow)
	{
		Screen.autorotateToPortrait = false;
		Screen.autorotateToPortraitUpsideDown = false;
		Screen.autorotateToLandscapeLeft = true;
		Screen.autorotateToLandscapeRight = true;
	}

	public static void GotoURL(string url)
	{
		if (!string.IsNullOrEmpty(url))
		{
			Application.OpenURL(url);
		}
	}
}
