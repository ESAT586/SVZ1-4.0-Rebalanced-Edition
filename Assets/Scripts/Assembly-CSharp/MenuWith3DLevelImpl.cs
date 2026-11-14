using System.Collections.Generic;
using UnityEngine;

public class MenuWith3DLevelImpl : SceneBehaviour
{
	public BoxCollider spawnArea;

	private MenuWith3DLevelManager mManager;

	public MenuWith3DLevelManager manager
	{
		get { return mManager; }
	}

    private void Start()
    {
        try
        {
            if (WeakGlobalInstance<SUIScreen>.instance != null)
            {
                WeakGlobalInstance<SUIScreen>.instance.fader.speed = 1f;
                WeakGlobalInstance<SUIScreen>.instance.fader.FadeFromBlack();
            }

            mManager = new MenuWith3DLevelManager(spawnArea);
            Debug.Log("mManager basariyla olusturuldu.");

            if (ApplicationUtilities.instance != null)
            {
                ApplicationUtilities.instance.ShowAds((ApplicationUtilities.AdPosition)17);
            }
        }
        catch (System.Exception e)
        {
            string realError = (e.InnerException != null) ? e.InnerException.Message : e.Message;
            string stackTrace = (e.InnerException != null) ? e.InnerException.StackTrace : e.StackTrace;

            Debug.LogError("Asil Hata: " + realError);
            Debug.LogError("Hata Kaynagi: " + stackTrace);
        }
    }

    private void OnDestroy()
	{
		ApplicationUtilities.instance.HideAds();
	}

    private void Update()
    {
        if (mManager == null) return;

        if (!SceneBehaviourUpdate())
        {
            mManager.Update();
        }
    }

    private void OnGUI()
	{
        // Formation Editor is disabled and hidden in both Editor and Builds.
	}

#if UNITY_EDITOR
    // --- 3D Character Formation Editor (Disabled) ---
    private bool mShowFormationEditor = false;
    private bool mFormationMinimized = false;
    private Rect mFormationWindowRect = new Rect(20f, 60f, 430f, 560f);
    private int mSelectedEnemyIndex = 0;
    private Vector2 mFormationScroll = Vector2.zero;
    private string mFormationStatus = "Dusman dizilisini ve on-arka (Z) derinlik sirasini buradan ayarlayin.";

    private void DrawFormationEditor()
    {
        if (mManager == null || mManager.enemiesPreview == null) return;
        EnemiesPreview preview = mManager.enemiesPreview;
        List<Character> chars = preview.characters;
        List<string> ids = preview.enemyIDs;
        if (chars == null || chars.Count == 0) return;

        GUI.depth = -5000;

        // Toggle button in top-left
        float btnWidth = 240f;
        string toggleLabel = mShowFormationEditor ? "[Dizilis Editorunu Gizle]" : "[3D Dizilis Editorunu Ac]";
        if (GUI.Button(new Rect(10f, 10f, btnWidth, 34f), toggleLabel))
        {
            mShowFormationEditor = !mShowFormationEditor;
        }

        if (!mShowFormationEditor) return;

        mFormationWindowRect = GUI.Window(987654, mFormationWindowRect, DrawFormationWindow, "3D DUSMAN DIZILIS DUZENLEYICI (EDITOR ONLY)");
    }

    private void DrawFormationWindow(int windowId)
    {
        EnemiesPreview preview = mManager.enemiesPreview;
        List<Character> chars = preview.characters;
        List<string> ids = preview.enemyIDs;

        GUILayout.BeginHorizontal();
        GUILayout.Label("Dalga: " + preview.waveNumber + " | Karakter Sayisi: " + chars.Count);
        if (GUILayout.Button(mFormationMinimized ? "[+ Ac]" : "[- Kucult]", GUILayout.Width(80f), GUILayout.Height(24f)))
        {
            mFormationMinimized = !mFormationMinimized;
            mFormationWindowRect.height = mFormationMinimized ? 80f : 560f;
        }
        GUILayout.EndHorizontal();

        if (mFormationMinimized)
        {
            GUI.DragWindow(new Rect(0, 0, 430, 80));
            return;
        }

        GUILayout.Space(4f);
        GUILayout.Box(mFormationStatus);
        GUILayout.Space(6f);

        // Character List & Reorder
        mFormationScroll = GUILayout.BeginScrollView(mFormationScroll, GUILayout.Height(190f));
        for (int i = 0; i < chars.Count; i++)
        {
            Character c = chars[i];
            if (c == null || c.controller == null) continue;
            string enemyId = (i < ids.Count) ? ids[i] : ("Enemy_" + i);
            Vector3 pos = c.controller.position;

            bool isSelected = (mSelectedEnemyIndex == i);
            GUI.color = isSelected ? new Color(0.4f, 0.8f, 1f, 1f) : Color.white;
            GUILayout.BeginVertical("box");
            GUI.color = Color.white;

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(isSelected ? "> #" + (i + 1) + " " + enemyId : "  #" + (i + 1) + " " + enemyId, GUILayout.Height(26f)))
            {
                mSelectedEnemyIndex = i;
            }

            // Quick Bring to Front (Z decreases in this view) / Send to Back (Z increases)
            if (GUILayout.Button("ONE AL", GUILayout.Width(72f), GUILayout.Height(26f)))
            {
                pos.z -= 25f;
                c.controller.position = pos;
                mFormationStatus = enemyId + " one getirildi (Z: " + pos.z.ToString("0") + ")";
            }
            if (GUILayout.Button("ARKAYA", GUILayout.Width(72f), GUILayout.Height(26f)))
            {
                pos.z += 25f;
                c.controller.position = pos;
                mFormationStatus = enemyId + " arkaya itildi (Z: " + pos.z.ToString("0") + ")";
            }
            GUILayout.EndHorizontal();

            // Quick Step horizontal
            GUILayout.BeginHorizontal();
            GUILayout.Label(string.Format("X: {0:0} | Y: {1:0} | Z: {2:0}", pos.x, pos.y, pos.z));
            if (GUILayout.Button("< Sol", GUILayout.Width(50f), GUILayout.Height(20f)))
            {
                pos.x -= 20f;
                c.controller.position = pos;
            }
            if (GUILayout.Button("Sag >", GUILayout.Width(50f), GUILayout.Height(20f)))
            {
                pos.x += 20f;
                c.controller.position = pos;
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }
        GUILayout.EndScrollView();

        GUILayout.Space(6f);

        // Fine Adjustment Sliders for Selected Character
        if (mSelectedEnemyIndex >= 0 && mSelectedEnemyIndex < chars.Count)
        {
            Character sel = chars[mSelectedEnemyIndex];
            string selId = (mSelectedEnemyIndex < ids.Count) ? ids[mSelectedEnemyIndex] : ("Enemy_" + mSelectedEnemyIndex);
            if (sel != null && sel.controller != null)
            {
                Vector3 p = sel.controller.position;

                GUILayout.Label("Secili Karakter: " + selId);

                // X slider
                GUILayout.BeginHorizontal();
                GUILayout.Label("X (Yatay): " + p.x.ToString("0"), GUILayout.Width(110f));
                float newX = GUILayout.HorizontalSlider(p.x, -250f, 350f);
                if (Mathf.Abs(newX - p.x) > 0.1f) { p.x = newX; sel.controller.position = p; }
                GUILayout.EndHorizontal();

                // Y slider
                GUILayout.BeginHorizontal();
                GUILayout.Label("Y (Yukseklik): " + p.y.ToString("0"), GUILayout.Width(110f));
                float newY = GUILayout.HorizontalSlider(p.y, -30f, 120f);
                if (Mathf.Abs(newY - p.y) > 0.1f) { p.y = newY; sel.controller.position = p; }
                GUILayout.EndHorizontal();

                // Z slider (Depth)
                GUILayout.BeginHorizontal();
                GUILayout.Label("Z (On/Arka): " + p.z.ToString("0"), GUILayout.Width(110f));
                float newZ = GUILayout.HorizontalSlider(p.z, 250f, 750f);
                if (Mathf.Abs(newZ - p.z) > 0.1f) { p.z = newZ; sel.controller.position = p; }
                GUILayout.EndHorizontal();

                // Scale slider
                if (sel.controlledObject != null)
                {
                    Transform t = sel.controlledObject.transform;
                    float curScale = Mathf.Abs(t.localScale.x);
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Olcek: " + curScale.ToString("0.00") + "x", GUILayout.Width(110f));
                    float newScale = GUILayout.HorizontalSlider(curScale, 0.4f, 2.5f);
                    if (Mathf.Abs(newScale - curScale) > 0.02f)
                    {
                        float sign = Mathf.Sign(t.localScale.x);
                        t.localScale = new Vector3(newScale * sign, newScale, newScale);
                    }
                    GUILayout.EndHorizontal();
                }
            }
        }

        GUILayout.Space(6f);

        // Formation Presets
        GUILayout.Label("Dizilis Sablonlari:");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("V-Dizilis (Boss Onde)", GUILayout.Height(28f)))
        {
            ApplyVFormation(chars);
            mFormationStatus = "V-Dizilisi uygulandi: Ilk karakter onde, digerleri arkaya dogru acildi.";
        }
        if (GUILayout.Button("Kademeli (Capraz)", GUILayout.Height(28f)))
        {
            ApplyStaggeredFormation(chars);
            mFormationStatus = "Kademeli dizilis uygulandi: Karakterler birbirini kapatmayacak sekilde dizildi.";
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Duz Hat", GUILayout.Height(26f)))
        {
            ApplyLineFormation(chars);
            mFormationStatus = "Duz hat dizilisi uygulandi.";
        }
        if (GUILayout.Button("Yarim Daire (Arc)", GUILayout.Height(26f)))
        {
            ApplyArcFormation(chars);
            mFormationStatus = "Yarim daire dizilisi uygulandi.";
        }
        if (GUILayout.Button("Orijinal", GUILayout.Height(26f)))
        {
            ResetToDefault(preview);
            mFormationStatus = "Orijinal fabrika dizilisine sifirlandi.";
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(8f);

        // Persistence & Export
        GUILayout.BeginHorizontal();
        GUI.color = new Color(0.5f, 1f, 0.5f, 1f);
        if (GUILayout.Button("BU DALGA ICIN KAYDET", GUILayout.Height(34f)))
        {
            SaveCustomFormation(preview);
            mFormationStatus = "Dalga " + preview.waveNumber + " dizilisi basariyla kaydedildi!";
        }
        GUI.color = new Color(1f, 0.9f, 0.4f, 1f);
        if (GUILayout.Button("C# KODUNU LOGLA", GUILayout.Height(34f)))
        {
            ExportCSharpCode(preview);
            mFormationStatus = "C# Vector3 dizisi Unity Console'a yazdirildi!";
        }
        GUI.color = Color.white;
        GUILayout.EndHorizontal();

        if (GUILayout.Button("Kaydedilen Dizilisi Sil (Varsayilana Don)", GUILayout.Height(24f)))
        {
            ClearSavedFormation(preview);
            ResetToDefault(preview);
            mFormationStatus = "Kaydedilen dizilis silindi, varsayilana donuldu.";
        }

        GUI.DragWindow(new Rect(0, 0, 430, 40));
    }

    private void ApplyVFormation(List<Character> chars)
    {
        if (chars == null || chars.Count == 0) return;
        for (int i = 0; i < chars.Count; i++)
        {
            if (chars[i] == null || chars[i].controller == null) continue;
            if (i == 0)
            {
                chars[i].controller.position = new Vector3(40f, 25f, 420f);
            }
            else
            {
                int pair = (i - 1) / 2 + 1;
                float side = ((i - 1) % 2 == 0) ? -1f : 1f;
                float x = 40f + (side * pair * 80f);
                float z = 420f + (pair * 75f);
                chars[i].controller.position = new Vector3(x, 25f, z);
            }
        }
    }

    private void ApplyStaggeredFormation(List<Character> chars)
    {
        if (chars == null) return;
        for (int i = 0; i < chars.Count; i++)
        {
            if (chars[i] == null || chars[i].controller == null) continue;
            int row = i % 2;
            int col = i / 2;
            float x = -60f + (col * 95f) + (row * 35f);
            float z = 420f + (row * 90f);
            chars[i].controller.position = new Vector3(x, 25f, z);
        }
    }

    private void ApplyLineFormation(List<Character> chars)
    {
        if (chars == null) return;
        float startX = -100f;
        float stepX = 300f / Mathf.Max(1, chars.Count - 1);
        for (int i = 0; i < chars.Count; i++)
        {
            if (chars[i] == null || chars[i].controller == null) continue;
            chars[i].controller.position = new Vector3(startX + (i * stepX), 25f, 490f);
        }
    }

    private void ApplyArcFormation(List<Character> chars)
    {
        if (chars == null) return;
        float angleStep = 120f / Mathf.Max(1, chars.Count - 1);
        float startAngle = -60f;
        for (int i = 0; i < chars.Count; i++)
        {
            if (chars[i] == null || chars[i].controller == null) continue;
            float rad = (startAngle + (i * angleStep)) * Mathf.Deg2Rad;
            float x = 40f + Mathf.Sin(rad) * 140f;
            float z = 430f + (1f - Mathf.Cos(rad)) * 90f;
            chars[i].controller.position = new Vector3(x, 25f, z);
        }
    }

    private void ResetToDefault(EnemiesPreview preview)
    {
        if (preview == null) return;
        List<Character> chars = preview.characters;
        List<Vector3> defs = preview.defaultPositions;
        for (int i = 0; i < chars.Count; i++)
        {
            if (chars[i] != null && chars[i].controller != null && i < defs.Count)
            {
                chars[i].controller.position = defs[i];
            }
        }
    }

    private void SaveCustomFormation(EnemiesPreview preview)
    {
        if (preview == null) return;
        List<Character> chars = preview.characters;
        int wave = preview.waveNumber;
        for (int i = 0; i < chars.Count; i++)
        {
            if (chars[i] != null && chars[i].controller != null)
            {
                Vector3 p = chars[i].controller.position;
                string key = "SVZ_Form_W" + wave + "_" + i;
                PlayerPrefs.SetFloat(key + "_x", p.x);
                PlayerPrefs.SetFloat(key + "_y", p.y);
                PlayerPrefs.SetFloat(key + "_z", p.z);
            }
        }
        PlayerPrefs.SetInt("SVZ_Form_W" + wave + "_Count", chars.Count);
        PlayerPrefs.Save();
    }

    private void ClearSavedFormation(EnemiesPreview preview)
    {
        if (preview == null) return;
        int wave = preview.waveNumber;
        int count = PlayerPrefs.GetInt("SVZ_Form_W" + wave + "_Count", preview.characters.Count);
        for (int i = 0; i < count; i++)
        {
            string key = "SVZ_Form_W" + wave + "_" + i;
            PlayerPrefs.DeleteKey(key + "_x");
            PlayerPrefs.DeleteKey(key + "_y");
            PlayerPrefs.DeleteKey(key + "_z");
        }
        PlayerPrefs.DeleteKey("SVZ_Form_W" + wave + "_Count");
        PlayerPrefs.Save();
    }

    private void ExportCSharpCode(EnemiesPreview preview)
    {
        if (preview == null) return;
        List<Character> chars = preview.characters;
        List<string> ids = preview.enemyIDs;
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("=== SVZ 3D ENEMY FORMATION EXPORT (Wave " + preview.waveNumber + ") ===");
        sb.AppendLine("Vector3[] customSpots = new Vector3[] {");
        for (int i = 0; i < chars.Count; i++)
        {
            if (chars[i] != null && chars[i].controller != null)
            {
                Vector3 p = chars[i].controller.position;
                string id = (i < ids.Count) ? ids[i] : ("Enemy_" + i);
                sb.AppendLine(string.Format("    new Vector3({0:0.0}f, {1:0.0}f, {2:0.0}f), // #{3} {4}", p.x, p.y, p.z, i + 1, id));
            }
        }
        sb.AppendLine("};");
        Debug.Log(sb.ToString());
    }
#endif
}
