using UnityEngine;

public class GameCenterBinding
{
	public static bool isGameCenterAvailable()
	{
		return false;
	}

	public static void authenticateLocalPlayer()
	{
	}

	public static bool isPlayerAuthenticated()
	{
		return false;
	}

	public static string playerAlias()
	{
		return "Samurai";
	}

	public static string playerIdentifier()
	{
		return string.Empty;
	}

	public static bool isUnderage()
	{
		return false;
	}

	public static void retrieveFriends()
	{
	}

	public static void loadPlayerData(string[] playerIdArray)
	{
	}

	public static void loadLeaderboardTitles()
	{
	}

	public static void reportScore(long score, string leaderboardId)
	{
	}

	public static void showLeaderboardWithTimeScope(GameCenterLeaderboardTimeScope timeScope)
	{
	}

	public static void showLeaderboardWithTimeScopeAndLeaderboard(GameCenterLeaderboardTimeScope timeScope, string leaderboardId)
	{
	}

	public static void retrieveScores(bool friendsOnly, GameCenterLeaderboardTimeScope timeScope, int start, int end)
	{
	}

	public static void retrieveScores(bool friendsOnly, GameCenterLeaderboardTimeScope timeScope, int start, int end, string leaderboardId)
	{
	}

	public static void retrieveScoresForPlayerId(string playerId)
	{
	}

	public static void retrieveScoresForPlayerId(string playerId, string leaderboardId)
	{
	}

	public static void reportAchievement(string identifier, float percent)
	{
	}

	public static void getAchievements()
	{
	}

	public static void resetAchievements()
	{
	}

	public static void showAchievements()
	{
	}

	public static void retrieveAchievementMetadata()
	{
	}

	public static void showCompletionBannerForAchievements()
	{
	}
}
