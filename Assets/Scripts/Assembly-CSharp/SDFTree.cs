using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class SDFTree
{
	private static int kNumSpacesForTabs = 4;

	private const int kBackupCount = 3;

	private SDFTree()
	{
	}

	public static void Save(SDFTreeNode root, string filename)
	{
		if (root == null || string.IsNullOrEmpty(filename))
		{
			return;
		}
		string directoryName = Path.GetDirectoryName(filename);
		if (!string.IsNullOrEmpty(directoryName) && !Directory.Exists(directoryName))
		{
			Directory.CreateDirectory(directoryName);
		}
		string text = filename + ".tmp";
		WritePlainText(root, text);
		FileEncryptAndDecrypt.EncryptFile(text);
		string text2 = string.Empty;
		string text3 = string.Empty;
		if (!FileEncryptAndDecrypt.TryDecryptFile(text, out text2, out text3) || string.IsNullOrEmpty(text2))
		{
			DeleteIfExists(text);
			throw new IOException("Save verification failed: " + text3);
		}
		RotateBackups(filename);
		File.Copy(text, filename, true);
		string backupPath = GetBackupPath(filename, 1);
		if (File.Exists(backupPath))
		{
			File.Copy(backupPath, Profile.prevSaveFilePath, true);
		}
		DeleteIfExists(text);
		if (Debug.isDebugBuild)
		{
			File.WriteAllText(Profile.saveDirectory + "/saveAfter.data", text2);
		}
	}

	private static void WritePlainText(SDFTreeNode root, string filename)
	{
		TextWriter textWriter = new StreamWriter(filename);
		try
		{
			SaveRecursively(textWriter, root, 0);
		}
		finally
		{
			textWriter.Close();
			if (Debug.isDebugBuild)
			{
				File.Copy(filename, Profile.saveDirectory + "/saveBefore.data", true);
			}
		}
	}

	public static SDFTreeNode LoadFromResources(string filename)
	{
		string overridePath = System.IO.Path.Combine(Application.dataPath, "Overrides/" + filename + ".txt");
		if (System.IO.File.Exists(overridePath))
		{
			try
			{
				string text = System.IO.File.ReadAllText(overridePath);
				SDFTreeNode sDFTreeNode = LoadFromSingleString(text);
				sDFTreeNode.ExpandLinks();
				return sDFTreeNode;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("Failed loading override " + overridePath + ": " + ex.Message);
			}
		}
		TextAsset textAsset = (TextAsset)Resources.Load(filename, typeof(TextAsset));
		if (textAsset == null)
		{
			Debug.Log("ERROR: Unable to load the SDF file: '" + filename + "'");
			return null;
		}
		SDFTreeNode sDFTreeNode2 = LoadFromSingleString(textAsset.text);
		sDFTreeNode2.ExpandLinks();
		return sDFTreeNode2;
	}

	public static SDFTreeNode LoadFromBundle(AssetBundle bundle, string filename)
	{
		TextAsset textAsset = (TextAsset)bundle.LoadAsset(filename, typeof(TextAsset));
		if (textAsset == null)
		{
			Debug.Log("ERROR: Unable to load the SDF file from provided bundle: '" + filename + "'");
			return null;
		}
		SDFTreeNode sDFTreeNode = LoadFromSingleString(textAsset.text);
		sDFTreeNode.ExpandLinks();
		return sDFTreeNode;
	}

	public static SDFTreeNode LoadFromFile(string filename)
	{
		if (string.IsNullOrEmpty(filename) || !File.Exists(filename))
		{
			return null;
		}
		SDFTreeNode sDFTreeNode = null;
		if (TryLoadEncryptedTree(filename, out sDFTreeNode))
		{
			return sDFTreeNode;
		}
		string[] recoveryCandidates = GetRecoveryCandidates(filename);
		for (int i = 0; i < recoveryCandidates.Length; i++)
		{
			string text = recoveryCandidates[i];
			if (!File.Exists(text))
			{
				continue;
			}
			if (TryLoadEncryptedTree(text, out sDFTreeNode))
			{
				Debug.LogWarning("[SDFTree] Restored save from backup: " + text);
				File.Copy(text, filename, true);
				return sDFTreeNode;
			}
		}
		return null;
	}

	private static bool TryLoadEncryptedTree(string filename, out SDFTreeNode tree)
	{
		tree = null;
		try
		{
			string str = string.Empty;
			string text = string.Empty;
			if (!FileEncryptAndDecrypt.TryDecryptFile(filename, out str, out text))
			{
				if (Debug.isDebugBuild)
				{
					Debug.LogWarning("[SDFTree] Save decrypt failed: " + filename + " | " + text);
				}
				return false;
			}
			if (string.IsNullOrEmpty(str))
			{
				return false;
			}
			bool flag = RemoveNullCharacters(ref str);
			if (Debug.isDebugBuild)
			{
				Debug.Log("Save data is: " + flag);
				TextWriter textWriter = new StreamWriter(Profile.saveDirectory + "/save_load.data");
				textWriter.WriteLine(str);
				textWriter.Close();
			}
			tree = LoadFromSingleString(str);
		}
		catch (Exception ex)
		{
			if (Debug.isDebugBuild)
			{
				Debug.Log(ex.Message);
			}
			tree = null;
			return false;
		}
		if (tree == null || (tree.attributeCount == 0 && tree.childCount == 0))
		{
			tree = null;
			return false;
		}
		tree.ExpandLinks();
		return true;
	}

	public static string GetBackupPath(string filename, int index)
	{
		return filename + ".backup" + index;
	}

	public static string[] GetBackupPaths(string filename)
	{
		string[] array = new string[kBackupCount];
		for (int i = 0; i < kBackupCount; i++)
		{
			array[i] = GetBackupPath(filename, i + 1);
		}
		return array;
	}

	public static string GetManualBackupPath(string filename)
	{
		return filename + ".manual";
	}

	public static string GetEmergencyBackupPath(string filename)
	{
		return filename + ".emergency";
	}

	public static bool CreateManualBackup(string filename)
	{
		return CopyIfValid(filename, GetManualBackupPath(filename));
	}

	public static bool CreateEmergencyBackup(string filename)
	{
		return CopyIfValid(filename, GetEmergencyBackupPath(filename));
	}

	public static bool RestoreLatestBackup(string filename)
	{
		string[] recoveryCandidates = GetRecoveryCandidates(filename);
		for (int i = 0; i < recoveryCandidates.Length; i++)
		{
			if (CopyIfValid(recoveryCandidates[i], filename))
			{
				return true;
			}
		}
		return false;
	}

	public static bool VerifyEncryptedSave(string filename, out string error)
	{
		string text = string.Empty;
		if (!FileEncryptAndDecrypt.TryDecryptFile(filename, out text, out error))
		{
			return false;
		}
		try
		{
			SDFTreeNode sDFTreeNode = LoadFromSingleString(text);
			if (sDFTreeNode == null || (sDFTreeNode.attributeCount == 0 && sDFTreeNode.childCount == 0))
			{
				error = "Parsed save is empty";
				return false;
			}
			return true;
		}
		catch (Exception ex)
		{
			error = ex.Message;
			return false;
		}
	}

	private static string[] GetRecoveryCandidates(string filename)
	{
		string[] array = new string[kBackupCount + 2];
		for (int i = 0; i < kBackupCount; i++)
		{
			array[i] = GetBackupPath(filename, i + 1);
		}
		array[kBackupCount] = GetManualBackupPath(filename);
		array[kBackupCount + 1] = Profile.prevSaveFilePath;
		return array;
	}

	private static void RotateBackups(string filename)
	{
		for (int i = kBackupCount; i >= 2; i--)
		{
			string backupPath = GetBackupPath(filename, i - 1);
			string backupPath2 = GetBackupPath(filename, i);
			if (File.Exists(backupPath))
			{
				File.Copy(backupPath, backupPath2, true);
			}
		}
		if (File.Exists(filename))
		{
			File.Copy(filename, GetBackupPath(filename, 1), true);
		}
	}

	private static bool CopyIfValid(string source, string destination)
	{
		if (string.IsNullOrEmpty(source) || !File.Exists(source))
		{
			return false;
		}
		string error = string.Empty;
		if (!VerifyEncryptedSave(source, out error))
		{
			return false;
		}
		File.Copy(source, destination, true);
		return true;
	}

	private static void DeleteIfExists(string filename)
	{
		if (!string.IsNullOrEmpty(filename) && File.Exists(filename))
		{
			File.Delete(filename);
		}
	}

	private static void SaveRecursively(TextWriter tw, SDFTreeNode node, int level)
	{
		string text = new string('\t', level);
		foreach (KeyValuePair<string, string> attribute in node.attributes)
		{
			tw.WriteLine(text + attribute.Key + " = " + attribute.Value);
		}
		foreach (KeyValuePair<string, SDFTreeNode> child in node.childs)
		{
			tw.WriteLine(text + "[" + child.Key + "]");
			SaveRecursively(tw, child.Value, level + 1);
		}
	}

	public static SDFTreeNode LoadFromSingleString(string entireFile, bool needValidate = false)
	{
		char[] separator = new char[2] { '\r', '\n' };
		string[] source = entireFile.Split(separator, StringSplitOptions.RemoveEmptyEntries);
		SmarterStringIterator iter = new SmarterStringIterator(source);
		SDFTreeNode sDFTreeNode = new SDFTreeNode();
		LoadBlock(sDFTreeNode, 0, iter, needValidate, string.Empty);
		return sDFTreeNode;
	}

	private static void LoadBlock(SDFTreeNode node, int level, SmarterStringIterator iter, bool needValidate = false, string blockName = "")
	{
		while (iter.Current != null)
		{
			int num = FindLineLevel(iter.Current);
			string text = iter.Current.Trim();
			if (text.Length == 0 || text[0] == '/')
			{
				iter.MoveNext();
			}
			else
			{
				if (num < level)
				{
					break;
				}
				if (num > level)
				{
					Debug.Log("ERROR: the SDF indentation is malformed.");
					break;
				}
				iter.MoveNext();
				if (text[0] == '[')
				{
					string text2 = ExtractBlockTitle(text);
					if (text2 != string.Empty)
					{
						SDFTreeNode sDFTreeNode = new SDFTreeNode();
						LoadBlock(sDFTreeNode, level + 1, iter, needValidate, text2);
						if (needValidate && node.hasChild(text2))
						{
							sDFTreeNode.MergeFrom(node.to(text2));
						}
						node.SetChild(text2, sDFTreeNode);
					}
				}
				else if (text[0] == '{')
				{
					node.AddFileLink(ExtractFileLink(text));
				}
				else
				{
					KeyValuePair<string, string> keyValuePair = ExtractAttribute(text, needValidate, blockName);
					if (needValidate && node.hasAttribute(keyValuePair.Key))
					{
						keyValuePair = new KeyValuePair<string, string>(string.Empty, string.Empty);
					}
					if (keyValuePair.Key == string.Empty)
					{
						if (keyValuePair.Value == string.Empty)
						{
							Debug.Log("WARNING: SDF attribute malformed: " + text);
						}
						else
						{
							node[node.attributeCount] = keyValuePair.Value;
						}
					}
					else
					{
						node[keyValuePair.Key] = keyValuePair.Value;
					}
				}
			}
			if (iter.Current == null)
			{
				break;
			}
		}
	}

	private static int FindLineLevel(string line)
	{
		if (line == null || line.Length == 0)
		{
			return 0;
		}
		int num = 0;
		int num2 = 0;
		for (int i = 0; i < line.Length; i++)
		{
			switch (line[i])
			{
			case '\t':
				num++;
				continue;
			case ' ':
				num2++;
				if (num2 == kNumSpacesForTabs)
				{
					num2 = 0;
					num++;
				}
				continue;
			}
			break;
		}
		return num;
	}

	private static string ExtractBlockTitle(string blockTitle)
	{
		if (blockTitle[blockTitle.Length - 1] != ']')
		{
			Debug.Log("ERROR: malformed SDF block header: " + blockTitle);
			return string.Empty;
		}
		return blockTitle.Substring(1, blockTitle.Length - 2);
	}

	private static string ExtractFileLink(string line)
	{
		if (line[line.Length - 1] != '}')
		{
			Debug.Log("ERROR: malformed SDF file link: " + line);
			return string.Empty;
		}
		return line.Substring(1, line.Length - 2);
	}

	private static KeyValuePair<string, string> ExtractAttribute(string attributeLine, bool needValidate = false, string blockName = "")
	{
		int num = attributeLine.IndexOf('=');
		if (Debug.isDebugBuild && num == 0)
		{
			Debug.Log("**** splitIndex ZERO!!!!! ****");
		}
		if (num == -1 || num == 0)
		{
			return new KeyValuePair<string, string>(string.Empty, attributeLine.Trim());
		}
		string[] array = new string[2]
		{
			attributeLine.Substring(0, num - 1).Trim(),
			attributeLine.Substring(num + 1).Trim()
		};
		if (needValidate)
		{
			try
			{
				if (!IsKeyValidate(array[0], array[1], blockName))
				{
					array[0] = string.Empty;
					array[1] = string.Empty;
				}
			}
			catch (Exception ex)
			{
				if (Debug.isDebugBuild)
				{
					Debug.Log("IsKeyValidate (" + array[0] + ", " + array[1] + "): " + ex.Message);
				}
				return new KeyValuePair<string, string>(string.Empty, string.Empty);
			}
		}
		return new KeyValuePair<string, string>(array[0], array[1]);
	}

	private static bool RemoveNullCharacters(ref string str)
	{
		string text = string.Empty;
		bool result = true;
		bool flag = false;
		for (int i = 0; i < str.Length; i++)
		{
			if (str[i] != 0)
			{
				text += str[i];
				if (flag && char.IsLetterOrDigit(str[i]))
				{
					result = false;
				}
			}
			else
			{
				flag = true;
			}
		}
		str = text;
		return result;
	}

	private static bool IsKeyValidate(string key, string value, string blockName)
	{
		string[,] array = new string[51, 2]
		{
			{ "neverShowRateMeAlertAgain", "b" },
			{ "timeStamp", "f" },
			{ "timeRemainingUntilNotification", "i" },
			{ "currentDailyRewardNumber", "i" },
			{ "hasReportedUser", "b" },
			{ "hasReportedGemPurchase", "b" },
			{ "hasWonPachinkoBefore", "b" },
			{ "coins", "i" },
			{ "purchasedCoins", "i" },
			{ "earnsDoubleCoins", "b" },
			{ "gems", "i" },
			{ "purchasedGems", "i" },
			{ "pachinkoBalls", "i" },
			{ "pachinkoBallsLaunched", "i" },
			{ "storeVisitsCount", "i" },
			{ "wavesSinceLastBonusWave", "i" },
			{ "wavesSinceLastBonusWave_zombies", "i" },
			{ "bonusWaveToBeat", "i" },
			{ "bonusWaveToBeat_zombies", "i" },
			{ "waveToBeat", "i" },
			{ "profileName", "s" },
			{ "heroLevel", "i" },
			{ "heroID", "s" },
			{ "initialLeadershipLevel", "i" },
			{ "swordLevel", "i" },
			{ "swordID", "s" },
			{ "bowLevel", "i" },
			{ "bowID", "s" },
			{ "archerLevel", "i" },
			{ "baseLevel", "i" },
			{ "bellLevel", "i" },
			{ "fromTheShadows", "b" },
			{ "upgradedFarmers", "i" },
			{ "upgradedArchers", "i" },
			{ "UndeadKilledViaTroopTrample", "i" },
			{ "UndeadKilledViaKatanaSlash", "i" },
			{ "hasSummonedFarmer", "b" },
			{ "hasSummonedSwordWarrior", "b" },
			{ "hasSummonedSpearWarrior", "b" },
			{ "hasSummonedBowman", "b" },
			{ "hasSummonedPanzerSamurai", "b" },
			{ "hasSummonedPriest", "b" },
			{ "hasSummonedNobunaga", "b" },
			{ "hasSummonedSpearHorseman", "b" },
			{ "hasSummonedTakeda", "b" },
			{ "hasSummonedRifleman", "b" },
			{ "hasSummonedFrostie", "b" },
			{ "hasSummonedSwordsmith", "b" },
			{ "wavesWithZeroGateDamage", "i" },
			{ "waveRecycle", "i" },
			{ "fbp", "i" }
		};
		if (blockName.Length == 0)
		{
			for (int i = 0; i < array.GetLength(0); i++)
			{
				if (string.Compare(key, array[i, 0], true) == 0 && TryToConvert(value, array[i, 1]))
				{
					return true;
				}
			}
		}
		else if (string.Compare(blockName, "novelties", true) != 0 && string.Compare(blockName, "alreadyEarnedAchievements", true) != 0)
		{
			if (string.Compare(blockName, "helperLevels", true) == 0)
			{
				if (TryToConvert(value, "i"))
				{
					string[] allIDs = Singleton<HelpersDatabase>.instance.allIDs;
					foreach (string strB in allIDs)
					{
						if (string.Compare(key, strB, true) == 0)
						{
							return true;
						}
					}
				}
			}
			else if (string.Compare(blockName, "abilityLevels", true) == 0)
			{
				if (TryToConvert(value, "i"))
				{
					string[] allIDs2 = Singleton<AbilitiesDatabase>.instance.allIDs;
					foreach (string strB2 in allIDs2)
					{
						if (string.Compare(key, strB2, true) == 0)
						{
							return true;
						}
					}
				}
			}
			else if (string.Compare(blockName, "numPotions", true) == 0)
			{
				if (TryToConvert(value, "i"))
				{
					foreach (string allID in Singleton<PotionsDatabase>.instance.allIDs)
					{
						if (string.Compare(key, allID, true) == 0)
						{
							return true;
						}
					}
				}
			}
			else if (string.Compare(blockName, "numCharms", true) == 0)
			{
				if (TryToConvert(value, "i"))
				{
					foreach (string allID2 in Singleton<CharmsDatabase>.instance.allIDs)
					{
						if (string.Compare(key, allID2, true) == 0)
						{
							return true;
						}
					}
				}
			}
			else if (string.Compare(blockName, "chapterUnlock", true) != 0)
			{
				if (string.Compare(blockName, "waves", true) == 0)
				{
					if (key[0] == 'w')
					{
						string text = key.Substring(1);
						for (int l = 0; l < text.Length; l++)
						{
							if (!char.IsDigit(text[l]))
							{
								return false;
							}
						}
						if (TryToConvert(value, "i"))
						{
							return true;
						}
					}
				}
				else if (string.Compare(blockName, "tutorials", true) == 0)
				{
					string[] array2 = new string[17]
					{
						"TutorialWave2_leadership", "TutorialWave2_summonAlly", "TutorialWave1_protectGate", "TutorialWave1_showMovements", "TutorialWave1_autoAttack", "TutorialWave1_ability", "TutorialBonusWave1_welcome", "TutorialBonusWave1_allies", "TutorialBonusWave1_charm", "TutorialBonusWave1_unlock",
						"StoreDealPacks_dealpacks", "Store_intro", "PachinkoTutorial_lever", "PachinkoTutorial_spin", "PachinkoTutorial_multiplier", "reactives_flyingEnemies", "BoosterPacksTutorial_freePack"
					};
					if (TryToConvert(value, "b"))
					{
						for (int m = 0; m < array2.Length; m++)
						{
							if (string.Compare(key, array2[m], true) == 0)
							{
								return true;
							}
						}
					}
				}
				else if (string.Compare(blockName, "achievementsDisplayed", true) == 0)
				{
					if (key.Length == 3 && char.IsDigit(key[0]) && char.IsDigit(key[1]) && char.IsDigit(key[2]))
					{
						return true;
					}
				}
				else if (string.Compare(blockName, "stats_enemiesKilled", true) == 0 && TryToConvert(value, "i"))
				{
					SDFTreeNode sDFTreeNode = SingletonMonoBehaviour<ResourcesManager>.instance.Open("Registry/Enemies");
					foreach (KeyValuePair<string, SDFTreeNode> child in sDFTreeNode.childs)
					{
						if (string.Compare(child.Key, key, true) == 0)
						{
							return true;
						}
					}
				}
			}
		}
		return false;
	}

	private static bool TryToConvert(string str, string type)
	{
		switch (type)
		{
		case "i":
		{
			int result2;
			if (int.TryParse(str, out result2))
			{
				return true;
			}
			break;
		}
		case "b":
		{
			bool result3;
			if (bool.TryParse(str, out result3))
			{
				return true;
			}
			break;
		}
		case "f":
		{
			float result;
			if (float.TryParse(str, out result))
			{
				return true;
			}
			break;
		}
		case "s":
			return true;
		}
		return false;
	}
}
