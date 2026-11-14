using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class SUISprite : SUIWidget
{
	private static Dictionary<string, Texture2D> sOverrideTextureCache = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);

	public static Texture2D LoadOverrideTexture(string resourcePath)
	{
		if (string.IsNullOrEmpty(resourcePath))
		{
			return null;
		}
		Texture2D cached;
		if (sOverrideTextureCache.TryGetValue(resourcePath, out cached))
		{
			return cached;
		}
		Texture2D loaded = null;
		try
		{
			string rel = resourcePath.TrimStart('/').Replace('\\', '/');
			if (!rel.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
			{
				rel += ".png";
			}
			string[] roots = new string[]
			{
				Path.Combine(Application.persistentDataPath, "Overrides"),
				Path.Combine(Application.dataPath, "Overrides")
			};
			for (int i = 0; i < roots.Length; i++)
			{
				string candidate = Path.Combine(roots[i], rel);
				if (!File.Exists(candidate))
				{
					string lowerCandidate = Path.Combine(roots[i], rel.ToLower());
					if (File.Exists(lowerCandidate))
					{
						candidate = lowerCandidate;
					}
					else
					{
						continue;
					}
				}
				byte[] bytes = File.ReadAllBytes(candidate);
				if (bytes != null && bytes.Length > 0)
				{
					Texture2D tex = new Texture2D(2, 2, TextureFormat.ARGB32, false);
					if (tex.LoadImage(bytes))
					{
						tex.wrapMode = TextureWrapMode.Clamp;
						loaded = tex;
						break;
					}
					UnityEngine.Object.Destroy(tex);
				}
			}
		}
		catch
		{
			loaded = null;
		}
		sOverrideTextureCache[resourcePath] = loaded;
		return loaded;
	}

	private string mTextureFile;

	private string mLocalizedTextureFile;

	private GUITexture mTexture;

	private Vector2 mScale = new Vector2(1f, 1f);

	private Vector2 mHotspot = new Vector2(0.5f, 0.5f);

	private bool mAutoScaleKeepAspectRatio = true;

	private float mHDScaleRatio = 1f;

	public string texture
	{
		get
		{
			return mTextureFile;
		}
		set
		{
			if (mTextureFile != value)
			{
				mTextureFile = value;
				mLocalizedTextureFile = Singleton<Localizer>.instance.LocalizeSpritePath(mTextureFile);
				reloadTexture();
			}
		}
	}

	public override float alpha
	{
		get
		{
			return mTexture.color.a * 2f;
		}
		set
		{
			float num = value / 2f;
			if ((bool)mTexture && mTexture.color.a != num)
			{
				mTexture.color = new Color(0.5f, 0.5f, 0.5f, num);
			}
		}
	}

	public float width
	{
		get
		{
			return (mTexture != null && mTexture.texture != null) ? mTexture.texture.width : 0f;
		}
	}

	public float height
	{
		get
		{
			return (mTexture != null && mTexture.texture != null) ? mTexture.texture.height : 0f;
		}
	}

	public override Vector2 scale
	{
		get
		{
			return mScale;
		}
		set
		{
			if (mScale != value)
			{
				mScale = value;
				updateScale();
			}
		}
	}

	public bool autoscaleKeepAspectRatio
	{
		get
		{
			return mAutoScaleKeepAspectRatio;
		}
		set
		{
			mAutoScaleKeepAspectRatio = value;
			updateScale();
		}
	}

	public Vector2 hotspot
	{
		get
		{
			return mHotspot;
		}
		set
		{
			mHotspot = value;
			updateScale();
		}
	}

	public Vector2 hotspotPixels
	{
		get
		{
			return new Vector2(mHotspot.x * (width - 1f), mHotspot.y * (height - 1f));
		}
		set
		{
			hotspot = new Vector2(value.x / (width - 1f), value.y / (height - 1f));
		}
	}

	public override Rect area
	{
		get
		{
			if (mTexture == null || mTexture.texture == null)
			{
				return Rect.zero;
			}
			return SUIUtils.unityToUser(mTexture.GetScreenRect());
		}
	}

	public SUISprite()
	{
	}

	public SUISprite(string textureFile)
	{
		texture = textureFile;
	}

	public SUISprite(Color c, int width, int height)
	{
		Texture2D texture2D = new Texture2D(width, height);
		for (int i = 0; i < height; i++)
		{
			for (int j = 0; j < width; j++)
			{
				texture2D.SetPixel(j, i, c);
			}
		}
		texture2D.Apply();
		mTexture = (GUITexture)mGameObject.AddComponent<GUITexture>();
		mTexture.texture = texture2D;
		mTexture.transform.localScale = Vector3.zero;
	}

	public override void Destroy()
	{
		UnityEngine.Object.Destroy(mTexture);
		mTexture = null;
		base.Destroy();
	}

	public void SetDynamicTextureColor(Color c)
	{
		Texture2D texture2D = (Texture2D)mTexture.texture;
		for (int i = 0; (float)i < height; i++)
		{
			for (int j = 0; (float)j < width; j++)
			{
				texture2D.SetPixel(j, i, c);
			}
		}
		texture2D.Apply();
	}

	public override void EditorRenderOnGUI()
	{
		Color color = GUI.color;
		GUI.color = new Color(1f, 1f, 1f, alpha);
		GUI.DrawTexture(area, mTexture.texture);
		GUI.color = color;
	}

	private void reloadTexture()
	{
		if (mTexture == null)
		{
			mTexture = (GUITexture)mGameObject.AddComponent<GUITexture>();
		}
		Texture2D texture2D = null;
		if (SUIScreen.isDevice_iPad3)
		{
			texture2D = LoadOverrideTexture(mLocalizedTextureFile + "_ipad3");
			if (texture2D == null)
			{
				texture2D = (Texture2D)Resources.Load(mLocalizedTextureFile + "_ipad3", typeof(Texture2D));
			}
			if (texture2D != null)
			{
				mHDScaleRatio = 0.5f;
			}
		}
		if (texture2D == null)
		{
			texture2D = LoadOverrideTexture(mLocalizedTextureFile);
		}
		if (texture2D == null)
		{
			texture2D = (Texture2D)Resources.Load(mLocalizedTextureFile, typeof(Texture2D));
		}
		if (texture2D == null && !string.IsNullOrEmpty(mTextureFile))
		{
			texture2D = LoadOverrideTexture(mTextureFile);
			if (texture2D == null)
			{
				texture2D = (Texture2D)Resources.Load(mTextureFile, typeof(Texture2D));
			}
		}
		if (texture2D == null)
		{
			Debug.LogWarning("SUISprite -> Could not load texture: " + mTextureFile + " (Localized: " + mLocalizedTextureFile + ")");
			return;
		}
		mHDScaleRatio = 1f;
		mTexture.texture = texture2D;
		mTexture.transform.localScale = Vector3.zero;
		updateScale();
	}

	private void updateScale()
	{
		if (mTexture == null || mTexture.texture == null)
		{
			return;
		}
		AutoScaler autoScaler = WeakGlobalInstance<SUIScreen>.instance.autoScaler;
		Vector2 vector = ((!mAutoScaleKeepAspectRatio) ? new Vector2(autoScaler.toDeviceX((float)mTexture.texture.width * scale.x * mHDScaleRatio), autoScaler.toDeviceY((float)mTexture.texture.height * scale.y * mHDScaleRatio)) : new Vector2(autoScaler.toDevice((float)mTexture.texture.width * scale.x * mHDScaleRatio), autoScaler.toDevice((float)mTexture.texture.height * scale.y * mHDScaleRatio)));
		mTexture.pixelInset = new Rect(0f - vector.x * mHotspot.x, 0f - vector.y * (1f - mHotspot.y), vector.x, vector.y);
	}
}
