using System;
using System.Collections.Generic;
using Debug = UnityEngine.Debug;
using UnityEngine;

public class VillageArchers
{
	private const float kTimeFireRate = 2.5f;

	private const float kTimerTriggerDamages = 3.2f;

	private int mNumArchers = 1;

	private string mBowPrefabPath;

	private string mArcherPrefabPath;

	private List<TowerArcher> mArcherList = new List<TowerArcher>();

	private int mArcherLevel;

	private float mRange;

	private float mDamage;

	private float mAttackSpeed;

	private bool mAgainstPlayer;

	private Projectile.EProjectileType mArrowType = Projectile.EProjectileType.Arrow;

	public VillageArchers()
	{
		bool isMultiplayer = SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive;
		if (isMultiplayer)
		{
			mArcherLevel = Mathf.Max(8, Singleton<Profile>.instance.archerLevel);
		}
		else
		{
			switch (Singleton<PlayModesManager>.instance.selectedMode)
			{
			case "classic":
				mArcherLevel = Singleton<Profile>.instance.archerLevel;
				break;
			case "zombies":
				mArcherLevel = WeakGlobalInstance<WaveManager>.instance.villageArchersLevel;
				break;
			}
		}
		if (mArcherLevel != 0)
		{
			GetVillageArcherStats();
			if (isMultiplayer)
			{
				mNumArchers = Mathf.Max(2, mNumArchers);
			}
			if (Resources.Load(mArcherPrefabPath) == null && Resources.Load(LowercaseResourceRoot(mArcherPrefabPath)) == null)
			{
				Debug.LogError("VillageArchers: missing archer prefab, disabling village archers: " + mArcherPrefabPath);
				mArcherLevel = 0;
				return;
			}
			InGameImpl inGame = WeakGlobalSceneBehavior<InGameImpl>.instance;
			int spawned = 0;
			int maxArchers = Math.Min(mNumArchers, inGame.villageArcher.Length);
			bool leftAgainstPlayer = isMultiplayer
				? (SVZMultiplayerManager.Instance.P1Side == SVZMultiplayerManager.BattlefieldSide.RightGate)
				: mAgainstPlayer;
			for (int i = 0; i < maxArchers; i++)
			{
				if (inGame.villageArcher[i] == null)
				{
					Debug.LogWarning("VillageArchers: missing villageArcher scene point index " + i);
					continue;
				}
				TowerArcher towerArcher = new TowerArcher(mArcherPrefabPath, inGame.villageArcher[i].position, mBowPrefabPath, mArrowType, mDamage, mRange, mAttackSpeed, leftAgainstPlayer, FacingType.Right);
				if (towerArcher.controlledObject != null)
				{
					mArcherList.Add(towerArcher);
					spawned++;
				}
			}
			if (isMultiplayer)
			{
				bool rightAgainstPlayer = (SVZMultiplayerManager.Instance.P1Side == SVZMultiplayerManager.BattlefieldSide.LeftGate);
				string zombieArcherPrefab = "Characters/PFLzombieArcher";
				string zombieBowPrefab = "Props/PFlzombiebow";
				float leftGateZ = (inGame.enemiesTargetLeft != null)
					? inGame.enemiesTargetLeft.transform.position.z
					: ((inGame.heroWalkLeftEdge != null) ? inGame.heroWalkLeftEdge.position.z : 0f);
				float rightGateZ = (inGame.enemiesTargetRight != null)
					? inGame.enemiesTargetRight.transform.position.z
					: ((inGame.heroWalkRightEdge != null) ? inGame.heroWalkRightEdge.position.z : 3800f);

				for (int i = 0; i < maxArchers; i++)
				{
					if (inGame.villageArcher[i] == null)
					{
						continue;
					}
					Vector3 leftPos = inGame.villageArcher[i].position;
					float dzFromLeft = leftPos.z - leftGateZ;
					// Koprunun uzerine/korkuluguna binmemesi icin kuleyi koprunun gerisine (kapiya dogru) aliyoruz:
					float bridgeClearanceOffset = 450f;
					float rightZ = Mathf.Clamp(rightGateZ - dzFromLeft + bridgeClearanceOffset, leftGateZ + 1500f, rightGateZ - 180f);
					float leftGroundY = (WeakGlobalInstance<RailManager>.instance != null)
						? WeakGlobalInstance<RailManager>.instance.GetY(leftPos.z)
						: 38f;
					float rightGroundY = (WeakGlobalInstance<RailManager>.instance != null)
						? WeakGlobalInstance<RailManager>.instance.GetY(rightZ)
						: leftGroundY;
					float heightOffset = leftPos.y - leftGroundY;
					float rightX = leftPos.x - 25f; // Kopru korkuluklarindan guvenli mesafeye cekme
					Vector3 rightPos = new Vector3(rightX, rightGroundY + heightOffset, rightZ);

					CreateMirroredWatchtower(leftPos, rightPos, i);

					TowerArcher rightArcher = new TowerArcher(zombieArcherPrefab, rightPos, zombieBowPrefab, mArrowType, mDamage, mRange, mAttackSpeed, rightAgainstPlayer, FacingType.Left);
					if (rightArcher.controlledObject != null)
					{
						mArcherList.Add(rightArcher);
						spawned++;
					}
				}
			}
			if (spawned == 0)
			{
				mArcherLevel = 0;
			}
		}
	}

	private static void CreateMirroredWatchtower(Vector3 sourceTopPos, Vector3 targetTopPos, int index)
	{
		try
		{
			GameObject towerRoot = new GameObject("RightArcherTower_" + index);
			towerRoot.transform.position = targetTopPos;

			float minX = sourceTopPos.x - 115f;
			float maxX = sourceTopPos.x + 115f;
			float minY = sourceTopPos.y - 230f;
			float maxY = sourceTopPos.y + 50f;
			float minZ = sourceTopPos.z - 115f;
			float maxZ = sourceTopPos.z + 115f;

			int totalTrianglesCopied = 0;
			Material fallbackWoodMat = null;
			MeshFilter[] allFilters = UnityEngine.Object.FindObjectsOfType<MeshFilter>();
			HashSet<Mesh> processedMeshes = new HashSet<Mesh>();

			for (int f = 0; f < allFilters.Length; f++)
			{
				MeshFilter mf = allFilters[f];
				if (mf == null || mf.sharedMesh == null) continue;
				if (mf.transform.IsChildOf(towerRoot.transform)) continue;
				if (mf.GetComponentInParent<CharacterModelController>() != null) continue;

				MeshRenderer mr = mf.GetComponent<MeshRenderer>();
				if (mr == null || !mr.enabled) continue;
				if (fallbackWoodMat == null && mr.sharedMaterial != null)
				{
					fallbackWoodMat = mr.sharedMaterial;
				}

				if (!mr.isPartOfStaticBatch)
				{
					Bounds b = mr.bounds;
					if (b.center.x >= minX && b.center.x <= maxX &&
						b.center.y >= minY && b.center.y <= maxY &&
						b.center.z >= minZ && b.center.z <= maxZ &&
						b.size.z < 350f && b.size.x < 350f)
					{
						GameObject part = new GameObject("TowerPart_" + f);
						part.transform.parent = towerRoot.transform;
						part.transform.position = mf.transform.position + (targetTopPos - sourceTopPos);
						part.transform.rotation = mf.transform.rotation;
						part.transform.localScale = mf.transform.lossyScale;
						MeshFilter pmf = part.AddComponent<MeshFilter>();
						pmf.sharedMesh = mf.sharedMesh;
						MeshRenderer pmr = part.AddComponent<MeshRenderer>();
						pmr.sharedMaterials = mr.sharedMaterials;
						pmr.lightmapIndex = mr.lightmapIndex;
						pmr.lightmapScaleOffset = mr.lightmapScaleOffset;
						totalTrianglesCopied += 100;
						continue;
					}
				}

				Mesh mesh = mf.sharedMesh;
				if (!mesh.isReadable || processedMeshes.Contains(mesh)) continue;
				processedMeshes.Add(mesh);

				Vector3[] verts = mesh.vertices;
				if (verts == null || verts.Length == 0) continue;
				Vector3[] norms = mesh.normals;
				Vector2[] uvs = mesh.uv;
				Vector2[] uv2s = mesh.uv2;
				bool hasNorms = norms != null && norms.Length == verts.Length;
				bool hasUvs = uvs != null && uvs.Length == verts.Length;
				bool hasUv2s = uv2s != null && uv2s.Length == verts.Length;

				int subCount = mesh.subMeshCount;
				for (int s = 0; s < subCount; s++)
				{
					int[] tris = mesh.GetTriangles(s);
					if (tris == null || tris.Length < 3) continue;

					List<int> matchingTriIndices = null;
					Vector3 subMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
					Vector3 subMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);

					for (int t = 0; t <= tris.Length - 3; t += 3)
					{
						int i0 = tris[t];
						int i1 = tris[t + 1];
						int i2 = tris[t + 2];
						if (i0 < 0 || i0 >= verts.Length || i1 < 0 || i1 >= verts.Length || i2 < 0 || i2 >= verts.Length) continue;

						Vector3 v0 = verts[i0];
						if (v0.x < subMin.x) subMin.x = v0.x; if (v0.x > subMax.x) subMax.x = v0.x;
						if (v0.y < subMin.y) subMin.y = v0.y; if (v0.y > subMax.y) subMax.y = v0.y;
						if (v0.z < subMin.z) subMin.z = v0.z; if (v0.z > subMax.z) subMax.z = v0.z;

						if (v0.x >= minX && v0.x <= maxX && v0.y >= minY && v0.y <= maxY && v0.z >= minZ && v0.z <= maxZ)
						{
							Vector3 v1 = verts[i1];
							Vector3 v2 = verts[i2];
							if (v1.x >= minX && v1.x <= maxX && v1.y >= minY && v1.y <= maxY && v1.z >= minZ && v1.z <= maxZ &&
								v2.x >= minX && v2.x <= maxX && v2.y >= minY && v2.y <= maxY && v2.z >= minZ && v2.z <= maxZ)
							{
								if (matchingTriIndices == null) matchingTriIndices = new List<int>();
								matchingTriIndices.Add(i0);
								matchingTriIndices.Add(i1);
								matchingTriIndices.Add(i2);
							}
						}
					}

					if (matchingTriIndices == null || matchingTriIndices.Count == 0) continue;

					Vector3 subSize = subMax - subMin;
					if (subSize.x > 900f && subSize.y > 500f) continue;

					Vector3 subCenter = (subMin + subMax) * 0.5f;
					MeshRenderer bestRenderer = mr;
					float bestDist = float.MaxValue;
					for (int k = 0; k < allFilters.Length; k++)
					{
						MeshFilter cmf = allFilters[k];
						if (cmf == null || cmf.sharedMesh != mesh) continue;
						MeshRenderer cmr = cmf.GetComponent<MeshRenderer>();
						if (cmr == null) continue;
						float d = Vector3.SqrMagnitude(cmr.bounds.center - subCenter);
						if (d < bestDist)
						{
							bestDist = d;
							bestRenderer = cmr;
						}
					}

					Dictionary<int, int> remap = new Dictionary<int, int>();
					List<Vector3> newVerts = new List<Vector3>();
					List<Vector3> newNorms = hasNorms ? new List<Vector3>() : null;
					List<Vector2> newUvs = hasUvs ? new List<Vector2>() : null;
					List<Vector2> newUv2s = hasUv2s ? new List<Vector2>() : null;
					int[] newTris = new int[matchingTriIndices.Count];

					for (int idx = 0; idx < matchingTriIndices.Count; idx++)
					{
						int oldVi = matchingTriIndices[idx];
						int newVi;
						if (!remap.TryGetValue(oldVi, out newVi))
						{
							newVi = newVerts.Count;
							remap[oldVi] = newVi;
							newVerts.Add(verts[oldVi] - sourceTopPos);
							if (hasNorms) newNorms.Add(norms[oldVi]);
							if (hasUvs) newUvs.Add(uvs[oldVi]);
							if (hasUv2s) newUv2s.Add(uv2s[oldVi]);
						}
						newTris[idx] = newVi;
					}

					Mesh extractedMesh = new Mesh();
					extractedMesh.name = "WatchtowerSubmesh_" + s;
					extractedMesh.vertices = newVerts.ToArray();
					if (hasNorms) extractedMesh.normals = newNorms.ToArray();
					if (hasUvs) extractedMesh.uv = newUvs.ToArray();
					if (hasUv2s) extractedMesh.uv2 = newUv2s.ToArray();
					extractedMesh.triangles = newTris;
					extractedMesh.RecalculateBounds();

					GameObject subPart = new GameObject("WatchtowerMesh_" + s);
					subPart.transform.parent = towerRoot.transform;
					subPart.transform.localPosition = Vector3.zero;
					subPart.transform.localRotation = Quaternion.identity;
					subPart.transform.localScale = Vector3.one;

					MeshFilter subMf = subPart.AddComponent<MeshFilter>();
					subMf.sharedMesh = extractedMesh;
					MeshRenderer subMr = subPart.AddComponent<MeshRenderer>();
					if (bestRenderer != null)
					{
						subMr.sharedMaterial = bestRenderer.sharedMaterial;
						subMr.lightmapIndex = bestRenderer.lightmapIndex;
						subMr.lightmapScaleOffset = bestRenderer.lightmapScaleOffset;
					}
					totalTrianglesCopied += newTris.Length / 3;
				}
			}

			if (totalTrianglesCopied == 0)
			{
				BuildFallbackWoodenWatchtower(towerRoot.transform, fallbackWoodMat);
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning("VillageArchers: CreateMirroredWatchtower fallback: " + ex.Message);
		}
	}

	private static void BuildFallbackWoodenWatchtower(Transform root, Material woodMat)
	{
		float height = 190f;
		float halfW = 55f;
		Vector3[] corners = new Vector3[]
		{
			new Vector3(-halfW, -height * 0.5f, -halfW),
			new Vector3(halfW, -height * 0.5f, -halfW),
			new Vector3(-halfW, -height * 0.5f, halfW),
			new Vector3(halfW, -height * 0.5f, halfW)
		};
		for (int i = 0; i < corners.Length; i++)
		{
			CreateTowerBox(root, "Pillar_" + i, corners[i], new Vector3(14f, height, 14f), woodMat, new Color(0.42f, 0.25f, 0.14f));
		}
		CreateTowerBox(root, "PlatformDeck", new Vector3(0f, -6f, 0f), new Vector3(135f, 12f, 135f), woodMat, new Color(0.48f, 0.30f, 0.16f));
		CreateTowerBox(root, "TowerBody", new Vector3(0f, -95f, 0f), new Vector3(102f, 165f, 102f), woodMat, new Color(0.76f, 0.72f, 0.64f));
		CreateTowerBox(root, "RailFront", new Vector3(halfW + 5f, 18f, 0f), new Vector3(8f, 10f, 140f), woodMat, new Color(0.42f, 0.25f, 0.14f));
		CreateTowerBox(root, "RailBack", new Vector3(-halfW - 5f, 18f, 0f), new Vector3(8f, 10f, 140f), woodMat, new Color(0.42f, 0.25f, 0.14f));
		CreateTowerBox(root, "RailLeft", new Vector3(0f, 18f, -halfW - 5f), new Vector3(140f, 10f, 8f), woodMat, new Color(0.42f, 0.25f, 0.14f));
		CreateTowerBox(root, "RailRight", new Vector3(0f, 18f, halfW + 5f), new Vector3(140f, 10f, 8f), woodMat, new Color(0.42f, 0.25f, 0.14f));
	}

	private static void CreateTowerBox(Transform parent, string name, Vector3 localPos, Vector3 scale, Material baseMat, Color tint)
	{
		GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
		box.name = name;
		Collider col = box.GetComponent<Collider>();
		if (col != null) UnityEngine.Object.Destroy(col);
		box.transform.parent = parent;
		box.transform.localPosition = localPos;
		box.transform.localRotation = Quaternion.identity;
		box.transform.localScale = scale;
		MeshRenderer mr = box.GetComponent<MeshRenderer>();
		if (mr != null)
		{
			if (baseMat != null) mr.sharedMaterial = baseMat;
			mr.material.color = tint;
		}
	}

	public void Update()
	{
		if (mArcherLevel == 0)
		{
			return;
		}
		foreach (TowerArcher mArcher in mArcherList)
		{
			mArcher.Update();
		}
	}

	private void GetVillageArcherStats()
	{
		SDFTreeNode sDFTreeNode = SingletonMonoBehaviour<ResourcesManager>.instance.Open("Registry/VillageArchers");
		mArcherPrefabPath = sDFTreeNode["archerPrefab"];
		mBowPrefabPath = sDFTreeNode["weaponRangedPrefab"];
		SDFTreeNode sDFTreeNode2 = sDFTreeNode.to(string.Format("{0:000}", mArcherLevel));
		if (sDFTreeNode2 == null)
		{
			int fallbackLevel = GetHighestDefinedLevel(sDFTreeNode);
			if (fallbackLevel <= 0)
			{
				Debug.LogError("VillageArchers: no village archer levels are defined.");
				mArcherLevel = 0;
				return;
			}
			Debug.LogWarning("VillageArchers: missing stats for level " + mArcherLevel + ", using level " + fallbackLevel);
			mArcherLevel = fallbackLevel;
			sDFTreeNode2 = sDFTreeNode.to(string.Format("{0:000}", mArcherLevel));
		}
		mRange = float.Parse(sDFTreeNode2["bowRange"]);
		mDamage = float.Parse(sDFTreeNode2["bowDamage"]);
		mNumArchers = int.Parse(sDFTreeNode2["numberOfArchers"]);
		mAttackSpeed = float.Parse(sDFTreeNode2["attackFrequency"]);
		mAgainstPlayer = Singleton<PlayModesManager>.instance.selectedMode != "classic";
		mArrowType = (Projectile.EProjectileType)(int)Enum.Parse(typeof(Projectile.EProjectileType), sDFTreeNode2["projectile"]);
	}

	private static int GetHighestDefinedLevel(SDFTreeNode node)
	{
		int result = 0;
		foreach (KeyValuePair<string, SDFTreeNode> child in node.childs)
		{
			int level;
			if (int.TryParse(child.Key, out level) && level > result)
			{
				result = level;
			}
		}
		return result;
	}

	private static string LowercaseResourceRoot(string path)
	{
		if (string.IsNullOrEmpty(path))
		{
			return path;
		}
		int slash = path.IndexOf('/');
		if (slash <= 0)
		{
			return path.ToLower();
		}
		return path.Substring(0, slash).ToLower() + path.Substring(slash);
	}
}
