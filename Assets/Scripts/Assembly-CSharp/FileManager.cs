using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

public static class FileManager
{
	private const int kMaxFilePathLength = 256;

	private static Dictionary<string, FileData> findFilePathInCloudActions;

	private static Dictionary<string, FileData> saveFileActions;

	private static Dictionary<string, FileData> loadFileActions;

	private static bool useNativeFileIO;

	public static bool CloudStorageAvailable { get; private set; }

	static FileManager()
	{
		useNativeFileIO = false;
		CheckCloudStorageAvailability();
	}

	public static bool CheckCloudStorageAvailability()
	{
		CloudStorageAvailable = !string.IsNullOrEmpty(GetCloudContainerDirectoryPath());
		return CloudStorageAvailable;
	}

	public static string GetCloudContainerDirectoryPath()
	{
		return GetCloudContainerDirectoryPath(null);
	}

	public static string GetCloudContainerDirectoryPath(string containerID)
	{
		if (useNativeFileIO)
		{
		}
		return null;
	}

	public static void FindFilePathInCloud(string path, Action<FileData> onComplete)
	{
		FindFilePathInCloud(path, null, onComplete);
	}

	public static void FindFilePathInCloud(string fileName, string containerID, Action<FileData> onComplete)
	{
		FileData fileData = new FileData(fileName, onComplete);
		fileData.ContainerID = containerID;
		if (!useNativeFileIO && onComplete != null)
		{
			onComplete(fileData);
		}
	}

	public static string ConvertLocalPathToCloudPath(string localPath)
	{
		return ConvertLocalPathToCloudPath(localPath, null);
	}

	public static string ConvertLocalPathToCloudPath(string localPath, string containerID)
	{
		if (useNativeFileIO)
		{
		}
		return null;
	}

	public static void SaveFile(string path, byte[] data)
	{
		SaveFile(path, data, null);
	}

	public static void SaveFile(string path, byte[] data, Action<FileData> onComplete)
	{
		FileData fileData = new FileData(path, onComplete);
		fileData.Data = data;
		try
		{
			string dir = Path.GetDirectoryName(path);
			if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
			{
				Directory.CreateDirectory(dir);
			}
			using (FileStream output = new FileStream(path, FileMode.Create, FileAccess.Write))
			{
				using (BinaryWriter binaryWriter = new BinaryWriter(output))
				{
					binaryWriter.Write(data);
				}
			}
			fileData.Exists = true;
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[FileManager] SaveFile failed: " + ex.Message);
			fileData.Exists = false;
		}
		if (onComplete != null)
		{
			onComplete(fileData);
		}
	}

	public static void LoadFile(string path, Action<FileData> onComplete)
	{
		FileData fileData = new FileData(path, onComplete);
		try
		{
			if (File.Exists(path))
			{
				using (FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read))
				{
					using (BinaryReader binaryReader = new BinaryReader(fileStream))
					{
						fileData.Data = binaryReader.ReadBytes((int)fileStream.Length);
					}
				}
				fileData.Exists = true;
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[FileManager] LoadFile failed: " + ex.Message);
			fileData.Exists = false;
		}
		if (onComplete != null)
		{
			onComplete(fileData);
		}
	}

	private static bool AddAction(Dictionary<string, FileData> actions, FileData data)
	{
		if (data != null && !string.IsNullOrEmpty(data.Path))
		{
			if (!actions.ContainsKey(data.Path))
			{
				actions.Add(data.Path, data);
				return true;
			}
			actions[data.Path].OnCompleteActions.AddRange(data.OnCompleteActions);
		}
		return false;
	}

	private static FileData RemoveAction(Dictionary<string, FileData> actions, string path)
	{
		FileData value = null;
		if (!string.IsNullOrEmpty(path) && actions.TryGetValue(path, out value))
		{
			actions.Remove(path);
		}
		return value;
	}

	private static void FindFilePathInCloudComplete(string fileName, string cloudPath)
	{
		Debug.Log(string.Format("FindFilePathInCloudComplete: {0} = {1}", fileName, cloudPath));
		FileData fileData = RemoveAction(findFilePathInCloudActions, fileName);
		if (fileData == null)
		{
			return;
		}
		if (!string.IsNullOrEmpty(cloudPath))
		{
			fileData.Path = cloudPath;
			fileData.Exists = true;
		}
		foreach (Action<FileData> onCompleteAction in fileData.OnCompleteActions)
		{
			onCompleteAction(fileData);
		}
	}

	private static void SaveFileComplete(string path, bool success)
	{
		Debug.Log(string.Format("SaveFileComplete: {0} exists = {1}", path, success.ToString()));
		FileData fileData = RemoveAction(saveFileActions, path);
		if (fileData == null)
		{
			return;
		}
		fileData.Exists = success;
		foreach (Action<FileData> onCompleteAction in fileData.OnCompleteActions)
		{
			onCompleteAction(fileData);
		}
	}

	private static void LoadFileComplete(string path, IntPtr data, uint length)
	{
		Debug.Log(string.Format("LoadFileComplete: {0} = {1} bytes", path, length));
		FileData fileData = RemoveAction(loadFileActions, path);
		if (fileData == null)
		{
			return;
		}
		if (length != 0 && data != IntPtr.Zero)
		{
			fileData.Data = new byte[length];
			Marshal.Copy(data, fileData.Data, 0, (int)length);
			fileData.Exists = true;
		}
		foreach (Action<FileData> onCompleteAction in fileData.OnCompleteActions)
		{
			onCompleteAction(fileData);
		}
	}
}
