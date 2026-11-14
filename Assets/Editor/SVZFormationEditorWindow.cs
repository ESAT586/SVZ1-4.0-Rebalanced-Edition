using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class SVZFormationEditorWindow : EditorWindow
{
    private Vector2 mScroll = Vector2.zero;
    private int mSelectedEnemyIndex = 0;
    private string mStatus = "Oyun Play modundayken MenuWith3DLevel sahnesinde dizilisi canli duzenleyebilirsiniz.";

    [MenuItem("Tools/SVZ/3D Dusman Dizilis Editoru")]
    public static void ShowWindow()
    {
        SVZFormationEditorWindow window = GetWindow<SVZFormationEditorWindow>("SVZ Dizilis");
        window.minSize = new Vector2(380f, 500f);
        window.Show();
    }

    private void OnGUI()
    {
        GUILayout.Label("SVZ 3D Dusman Onizleme Dizilis Editoru", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(mStatus, MessageType.Info);
        GUILayout.Space(6f);

        if (!EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox("Bu editor, oyun Unity Editor'de CALISIRKEN (Play Mode) ve MenuWith3D sahnesindeyken 3D modelleri anlik olarak duzenler.", MessageType.Warning);
            return;
        }

        MenuWith3DLevelImpl impl = FindObjectOfType<MenuWith3DLevelImpl>();
        if (impl == null || impl.manager == null || impl.manager.enemiesPreview == null)
        {
            EditorGUILayout.HelpBox("MenuWith3DLevel sahnesi veya EnemiesPreview bulunamadi. Lutfen dalga secim / müttefik hazirlik ekranina gelin.", MessageType.Warning);
            return;
        }

        EnemiesPreview preview = impl.manager.enemiesPreview;
        List<Character> chars = preview.characters;
        List<string> ids = preview.enemyIDs;

        if (chars == null || chars.Count == 0)
        {
            GUILayout.Label("Bu dalgada onizlenecek dusman bulunmuyor.");
            return;
        }

        GUILayout.Label("Aktif Dalga: " + preview.waveNumber + " | Dusman Modelleri: " + chars.Count, EditorStyles.boldLabel);
        GUILayout.Space(6f);

        mScroll = EditorGUILayout.BeginScrollView(mScroll);

        for (int i = 0; i < chars.Count; i++)
        {
            Character c = chars[i];
            if (c == null || c.controller == null) continue;
            string enemyId = (i < ids.Count) ? ids[i] : ("Enemy_" + i);
            Vector3 pos = c.controller.position;

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();

            bool isSelected = (mSelectedEnemyIndex == i);
            GUI.color = isSelected ? new Color(0.4f, 0.8f, 1f, 1f) : Color.white;
            if (GUILayout.Button((isSelected ? "▶ #" : "  #") + (i + 1) + " " + enemyId, GUILayout.Height(24f)))
            {
                mSelectedEnemyIndex = i;
            }
            GUI.color = Color.white;

            if (GUILayout.Button("⬆️ One Al", GUILayout.Width(75f)))
            {
                pos.z -= 25f;
                c.controller.position = pos;
                mStatus = enemyId + " one alindi (Z: " + pos.z.ToString("0") + ")";
            }
            if (GUILayout.Button("⬇️ Arkaya", GUILayout.Width(75f)))
            {
                pos.z += 25f;
                c.controller.position = pos;
                mStatus = enemyId + " arkaya itildi (Z: " + pos.z.ToString("0") + ")";
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(string.Format("X: {0:0.0} | Y: {1:0.0} | Z: {2:0.0}", pos.x, pos.y, pos.z));
            if (GUILayout.Button("< Sol", GUILayout.Width(50f)))
            {
                pos.x -= 20f;
                c.controller.position = pos;
            }
            if (GUILayout.Button("Sag >", GUILayout.Width(50f)))
            {
                pos.x += 20f;
                c.controller.position = pos;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        GUILayout.Space(10f);

        // Fine Adjustment Sliders
        if (mSelectedEnemyIndex >= 0 && mSelectedEnemyIndex < chars.Count)
        {
            Character sel = chars[mSelectedEnemyIndex];
            string selId = (mSelectedEnemyIndex < ids.Count) ? ids[mSelectedEnemyIndex] : ("Enemy_" + mSelectedEnemyIndex);
            if (sel != null && sel.controller != null)
            {
                Vector3 p = sel.controller.position;
                EditorGUILayout.BeginVertical("box");
                GUILayout.Label("Secili: " + selId, EditorStyles.boldLabel);

                float newX = EditorGUILayout.Slider("X (Yatay Pozisyon)", p.x, -250f, 350f);
                float newY = EditorGUILayout.Slider("Y (Yukseklik)", p.y, -30f, 120f);
                float newZ = EditorGUILayout.Slider("Z (Derinlik / On-Arka)", p.z, 250f, 750f);

                if (Mathf.Abs(newX - p.x) > 0.1f || Mathf.Abs(newY - p.y) > 0.1f || Mathf.Abs(newZ - p.z) > 0.1f)
                {
                    sel.controller.position = new Vector3(newX, newY, newZ);
                }

                if (sel.controlledObject != null)
                {
                    Transform t = sel.controlledObject.transform;
                    float curScale = Mathf.Abs(t.localScale.x);
                    float newScale = EditorGUILayout.Slider("Olcek (Scale)", curScale, 0.4f, 2.5f);
                    if (Mathf.Abs(newScale - curScale) > 0.02f)
                    {
                        float sign = Mathf.Sign(t.localScale.x);
                        t.localScale = new Vector3(newScale * sign, newScale, newScale);
                    }
                }

                EditorGUILayout.EndVertical();
            }
        }

        GUILayout.Space(8f);

        // Presets
        GUILayout.Label("Formasyon Sablonlari", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("👑 V-Dizilisi (Boss Onde)"))
        {
            ApplyVFormation(chars);
            mStatus = "V-Dizilisi uygulandi.";
        }
        if (GUILayout.Button("⚡ Kademeli (Capraz)"))
        {
            ApplyStaggeredFormation(chars);
            mStatus = "Kademeli capraz dizilis uygulandi.";
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("🏹 Duz Hat"))
        {
            ApplyLineFormation(chars);
            mStatus = "Duz hat dizilisi uygulandi.";
        }
        if (GUILayout.Button("🌙 Yarim Daire (Arc)"))
        {
            ApplyArcFormation(chars);
            mStatus = "Yarim daire uygulandi.";
        }
        if (GUILayout.Button("↺ Orijinal"))
        {
            ResetToDefault(preview);
            mStatus = "Orijinal dizilise donuldu.";
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(10f);

        // Save & Export
        EditorGUILayout.BeginHorizontal();
        GUI.color = new Color(0.5f, 1f, 0.5f, 1f);
        if (GUILayout.Button("💾 BU DALGA ICIN KAYDET", GUILayout.Height(30f)))
        {
            SaveCustomFormation(preview);
            mStatus = "Dalga " + preview.waveNumber + " dizilisi kaydedildi!";
        }
        GUI.color = new Color(1f, 0.9f, 0.4f, 1f);
        if (GUILayout.Button("📋 C# KODUNU LOGLA", GUILayout.Height(30f)))
        {
            ExportCSharpCode(preview);
            mStatus = "C# Vector3 dizisi Unity Console'a loglandi!";
        }
        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("🗑️ Kayitli Dizilisi Sil"))
        {
            ClearSavedFormation(preview);
            ResetToDefault(preview);
            mStatus = "Kaydedilen dizilis silindi.";
        }

        EditorGUILayout.EndScrollView();
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
}
