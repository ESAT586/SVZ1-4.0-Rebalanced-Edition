using System.Collections;
using UnityEngine;

public class ResourcesManager : SingletonMonoBehaviour<ResourcesManager>
{
	private static bool mInitialized;

	private bool mShouldCheckVersionNumber;

	private AssetBundle mOnlineBundle;

	public static bool initialized
	{
		get
		{
			return mInitialized;
		}
	}

	public bool hasOnlineBundleLoaded
	{
		get
		{
			return mOnlineBundle != null;
		}
	}

	public event OnSUIGenericCallback onInvalidateCache;

	public void Init()
	{
		if (!mInitialized)
		{
			mInitialized = true;
			CheckOnlineUpdates();
			Singleton<Profile>.instance.UpgradeToExtraWaves();
		}
	}

	public void Update()
	{
	}

	public bool Exists(string path)
	{
		return Open(path) != null;
	}

	public SDFTreeNode Open(string path)
	{
		return Open(path, true);
	}

	public SDFTreeNode Open(string path, bool applySubstitutions)
	{
		if (applySubstitutions)
		{
			path = Singleton<PlayModesManager>.instance.ApplyPathSubstitutions(path);
		}
		try
		{
			string text = path.Substring(path.LastIndexOf("/") + 1);
			SDFTreeNode result;
			if ((Application.platform == RuntimePlatform.IPhonePlayer || Application.platform == RuntimePlatform.Android) && mOnlineBundle != null && mOnlineBundle.Contains(text))
			{
				Debug.Log("Loading " + text + " from online bundle.");
				result = SDFTree.LoadFromBundle(mOnlineBundle, text);
			}
			else
			{
				result = SDFTree.LoadFromResources(path);
			}
			return result;
		}
		catch
		{
			Debug.Log("ERROR: Unable to find the data file: " + path);
			return null;
		}
	}

	public void CheckOnlineUpdates()
	{
		StartCoroutine(AcquireAssetBundle());
	}

	public void BroadcastCacheReload()
	{
		if (this.onInvalidateCache != null)
		{
			this.onInvalidateCache();
		}
	}

	private IEnumerator AcquireAssetBundle()
	{
		yield break;
	}
}
