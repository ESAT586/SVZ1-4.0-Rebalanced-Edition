using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// LocalizationManager — v3.0 (World Top 30 Languages Edition)
///
/// Supports the Top 30 most spoken languages worldwide with instant switching
/// via Settings (Profile -> Language) or Dev Tools, plus a built-in 30-language
/// picker window, Localizer mod-key helper L(), and Arabic/Persian/Urdu RTL shaping.
/// </summary>
public class LocalizationManager : MonoBehaviour
{
    private static LocalizationManager _instance;
    public static LocalizationManager instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("[LocalizationManager]");
                _instance = go.AddComponent<LocalizationManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    public static bool IsAlive()
    {
        return _instance != null;
    }

    // World Top 30 Languages: English (Default) + 29 Major Global Languages
    private static readonly string[] kLanguageNames = {
        "English", "Chinese", "Hindi", "Spanish", "French",
        "Arabic", "Bengali", "Portuguese", "Russian", "Urdu",
        "Indonesian", "German", "Japanese", "Marathi", "Telugu",
        "Turkish", "Tamil", "Vietnamese", "Korean", "Hausa",
        "Persian", "Swahili", "Italian", "Thai", "Gujarati",
        "Punjabi", "Polish", "Ukrainian", "Malay", "Filipino"
    };

    private static readonly string[] kResourceNames = {
        "Default", "Chinese", "Hindi", "Spanish", "French",
        "Arabic", "Bengali", "Portuguese", "Russian", "Urdu",
        "Indonesian", "German", "Japanese", "Marathi", "Telugu",
        "Turkish", "Tamil", "Vietnamese", "Korean", "Hausa",
        "Persian", "Swahili", "Italian", "Thai", "Gujarati",
        "Punjabi", "Polish", "Ukrainian", "Malay", "Filipino"
    };

    private static readonly string[] kPickerLabels = {
        "[EN] English", "[ZH] Chinese / 中文", "[HI] Hindi / हिन्दी", "[ES] Spanish / Español", "[FR] French / Français",
        "[AR] Arabic / العربية", "[BN] Bengali / বাংলা", "[PT] Portuguese / Português", "[RU] Russian / Русский", "[UR] Urdu / اردو",
        "[ID] Indonesian", "[DE] German / Deutsch", "[JA] Japanese / 日本語", "[MR] Marathi / मराठी", "[TE] Telugu / తెలుగు",
        "[TR] Turkish / Türkçe", "[TA] Tamil / தமிழ்", "[VI] Vietnamese / Tiếng Việt", "[KO] Korean / 한국어", "[HA] Hausa",
        "[FA] Persian / فارسی", "[SW] Swahili / Kiswahili", "[IT] Italian / Italiano", "[TH] Thai / ไทย", "[GU] Gujarati / ગુજરાતી",
        "[PA] Punjabi / ਪੰਜਾਬੀ", "[PL] Polish / Polski", "[UK] Ukrainian / Українська", "[MS] Malay / Melayu", "[FIL] Filipino"
    };

    private const string LANG_PREF_KEY = "SVZ_Language";
    private string currentLanguage = "English";

    private static bool sPickerOpen = false;
    private static Vector2 sPickerScroll = Vector2.zero;
    private static Dictionary<int, Font> sDynFontCache = new Dictionary<int, Font>();

    public delegate void LanguageChangedDelegate(string newLanguage);
    public event LanguageChangedDelegate OnLanguageChanged;

    public static bool IsPickerOpen
    {
        get { return sPickerOpen; }
    }

    public static void OpenLanguagePicker()
    {
        // Ensure singleton exists so OnGUI draws immediately
        LocalizationManager mgr = instance;
        if (mgr != null)
        {
            sPickerOpen = true;
            sPickerScroll = Vector2.zero;
        }
    }

    public static void CloseLanguagePicker()
    {
        sPickerOpen = false;
    }

    public static string[] LanguageNames
    {
        get { return kLanguageNames; }
    }

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        string saved = PlayerPrefs.HasKey(LANG_PREF_KEY)
            ? PlayerPrefs.GetString(LANG_PREF_KEY, "English")
            : DetectInitialDeviceLanguage();
        if (saved == "Default") saved = "English";
        if (Array.IndexOf(kLanguageNames, saved) < 0) saved = "English";
        currentLanguage = saved;
    }

    private static string DetectInitialDeviceLanguage()
    {
        string sys = Application.systemLanguage.ToString();
        if (sys == "ChineseSimplified" || sys == "ChineseTraditional") sys = "Chinese";
        if (sys == "Indonesian") return "Indonesian";
        if (Array.IndexOf(kLanguageNames, sys) >= 0) return sys;
        return "English";
    }

    public void ApplySavedLanguage()
    {
        string saved = PlayerPrefs.HasKey(LANG_PREF_KEY)
            ? PlayerPrefs.GetString(LANG_PREF_KEY, "English")
            : DetectInitialDeviceLanguage();
        if (saved == "Default") saved = "English";
        if (Array.IndexOf(kLanguageNames, saved) < 0) saved = "English";
        currentLanguage = saved;

        if (Singleton<Localizer>.instance != null)
        {
            string resName = GetResourceName(saved);
            Singleton<Localizer>.instance.Reload(resName);

            if (Singleton<FontSetFinder>.IsAlive())
            {
                Singleton<FontSetFinder>.instance.Reload();
            }
        }
    }

    public void LoadLanguage(string langName)
    {
        if (langName == "Default") langName = "English";

        if (Array.IndexOf(kLanguageNames, langName) < 0)
        {
            Debug.LogWarning("[LocalizationManager] Unknown language: " + langName);
            langName = "English";
        }

        currentLanguage = langName;
        PlayerPrefs.SetString(LANG_PREF_KEY, langName);
        PlayerPrefs.Save();

        if (Singleton<Localizer>.instance != null)
        {
            string resourceName = GetResourceName(langName);
            Singleton<Localizer>.instance.Reload(resourceName);

            if (Singleton<FontSetFinder>.IsAlive())
            {
                Singleton<FontSetFinder>.instance.Reload();
            }

            Debug.Log("[LocalizationManager] Language switched to: " + langName + " (" + resourceName + ")");
        }

        if (OnLanguageChanged != null)
            OnLanguageChanged(langName);
    }

    public void CycleLanguage()
    {
        int idx = Array.IndexOf(kLanguageNames, currentLanguage);
        if (idx < 0) idx = 0;
        idx = (idx + 1) % kLanguageNames.Length;
        LoadLanguage(kLanguageNames[idx]);
    }

    public string GetCurrentLanguage() { return currentLanguage; }

    public string GetCurrentPickerLabel()
    {
        int idx = Array.IndexOf(kLanguageNames, currentLanguage);
        return (idx >= 0 && idx < kPickerLabels.Length) ? kPickerLabels[idx] : currentLanguage;
    }

    public static bool IsTurkish
    {
        get
        {
            string lang = (_instance != null) ? _instance.currentLanguage : PlayerPrefs.GetString(LANG_PREF_KEY, "English");
            return lang == "Turkish";
        }
    }

    public static bool IsRTL
    {
        get
        {
            string lang = (_instance != null) ? _instance.currentLanguage : PlayerPrefs.GetString(LANG_PREF_KEY, "English");
            return lang == "Arabic" || lang == "Urdu" || lang == "Persian";
        }
    }

    public static bool IsNoSpaceWrapScript
    {
        get
        {
            string lang = (_instance != null) ? _instance.currentLanguage : PlayerPrefs.GetString(LANG_PREF_KEY, "English");
            return lang == "Chinese" || lang == "Japanese" || lang == "Korean" || lang == "Thai";
        }
    }

    public static string T(string english, string turkish)
    {
        return IsTurkish ? turkish : english;
    }

    public static string L(string key, string fallbackEn, string fallbackTr = null)
    {
        if (Singleton<Localizer>.instance != null && Singleton<Localizer>.instance.Has(key))
        {
            string val = Singleton<Localizer>.instance.Get(key);
            if (!string.IsNullOrEmpty(val) && val != "*MISSING*")
            {
                return val;
            }
        }
        return (IsTurkish && !string.IsNullOrEmpty(fallbackTr)) ? fallbackTr : fallbackEn;
    }

    private static string GetResourceName(string langName)
    {
        for (int i = 0; i < kLanguageNames.Length; i++)
            if (kLanguageNames[i] == langName) return kResourceNames[i];
        return "Default";
    }

    public static Font GetDynamicOSFont(int pixelSize)
    {
        int clamped = Mathf.Clamp(pixelSize, 12, 72);
        Font cached;
        if (sDynFontCache.TryGetValue(clamped, out cached) && cached != null)
        {
            return cached;
        }
        Font f = null;
        try
        {
            f = Font.CreateDynamicFontFromOSFont(
                new string[] {
                    "Segoe UI", "Noto Sans", "Roboto", "Droid Sans", "Droid Sans Fallback",
                    "Nirmala UI", "Leelawadee UI", "Microsoft YaHei", "Yu Gothic UI", "Malgun Gothic",
                    "Tahoma", "Arial Unicode MS", "Arial",
                    "Noto Sans CJK SC", "Noto Sans Devanagari", "Noto Sans Bengali",
                    "Noto Sans Tamil", "Noto Sans Telugu", "Noto Sans Gujarati",
                    "Noto Sans Gurmukhi", "Noto Sans Thai", "Noto Naskh Arabic"
                },
                clamped);
        }
        catch (Exception)
        {
            f = null;
        }
        if (f != null)
        {
            sDynFontCache[clamped] = f;
        }
        return f;
    }

    // ── Built-in 30-Language Selector Popup ─────────────────────────────
    private void Update()
    {
        if (sPickerOpen && Input.GetKeyUp(KeyCode.Escape))
        {
            sPickerOpen = false;
        }
    }

    private void OnGUI()
    {
        if (!sPickerOpen) return;

        float scale, virtualW, virtualH;
        Rect safeRect;
        SVZDeviceCompatibility.GetSafeVirtualViewport(720f, 960f, out scale, out virtualW, out virtualH, out safeRect);

        Matrix4x4 oldMatrix = GUI.matrix;
        Color oldGuiColor = GUI.color;
        Color oldBgColor = GUI.backgroundColor;
        int oldBtn = GUI.skin.button.fontSize;
        int oldLbl = GUI.skin.label.fontSize;
        Font oldBtnFont = GUI.skin.button.font;
        Font oldLblFont = GUI.skin.label.font;

        try
        {
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            GUI.depth = -15000;

            Font dyn = GetDynamicOSFont(18);
            if (dyn != null)
            {
                GUI.skin.button.font = dyn;
                GUI.skin.label.font = dyn;
            }
            GUI.skin.button.fontSize = 15;
            GUI.skin.label.fontSize = 18;

            GUI.color = new Color(0f, 0f, 0f, 0.78f);
            GUI.DrawTexture(new Rect(0f, 0f, virtualW, virtualH), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float winW = Mathf.Min(920f, safeRect.width * 0.96f);
            float winH = Mathf.Min(640f, safeRect.height * 0.92f);
            float winX = safeRect.x + (safeRect.width - winW) * 0.5f;
            float winY = safeRect.y + (safeRect.height - winH) * 0.5f;

            GUI.color = new Color(0.06f, 0.07f, 0.11f, 0.98f);
            GUI.DrawTexture(new Rect(winX, winY, winW, winH), Texture2D.whiteTexture);
            GUI.color = new Color(0.95f, 0.75f, 0.25f, 0.95f);
            GUI.DrawTexture(new Rect(winX, winY, winW, 3f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(winX, winY + winH - 3f, winW, 3f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUIStyle hdr = new GUIStyle(GUI.skin.label);
            hdr.alignment = TextAnchor.MiddleCenter;
            hdr.fontStyle = FontStyle.Bold;
            hdr.fontSize = 18;
            hdr.richText = true;
            if (dyn != null) hdr.font = dyn;
            GUI.Label(new Rect(winX + 16f, winY + 10f, winW - 32f, 32f),
                "<color=#FFE066>" + L("mod_select_language", "SELECT LANGUAGE (TOP 30 WORLD LANGUAGES)", "OYUN DILINI SEC (EN POPULER 30 DIL)") + "</color>", hdr);

            GUIStyle subHdr = new GUIStyle(GUI.skin.label);
            subHdr.alignment = TextAnchor.MiddleCenter;
            subHdr.fontSize = 13;
            subHdr.richText = true;
            if (dyn != null) subHdr.font = dyn;
            GUI.Label(new Rect(winX + 16f, winY + 40f, winW - 32f, 22f),
                "<color=#AAD8FF>" + GetCurrentPickerLabel() + "</color>", subHdr);

            float gridTop = winY + 68f;
            float gridH = winH - 130f;
            float gridW = winW - 28f;
            Rect scrollOuter = new Rect(winX + 14f, gridTop, gridW, gridH);

            int cols = 3;
            int rows = Mathf.CeilToInt((float)kLanguageNames.Length / (float)cols);
            float cellH = 44f;
            float gap = 6f;
            float contentH = rows * (cellH + gap) + 8f;
            sPickerScroll = GUI.BeginScrollView(scrollOuter, sPickerScroll, new Rect(0f, 0f, gridW - 22f, contentH));

            GUIStyle cellBtn = new GUIStyle(GUI.skin.button);
            if (dyn != null) cellBtn.font = dyn;
            cellBtn.fontSize = 15;
            cellBtn.fontStyle = FontStyle.Bold;
            cellBtn.normal.textColor = Color.white;
            cellBtn.hover.textColor = new Color(1f, 0.94f, 0.55f);
            cellBtn.active.textColor = Color.white;
            cellBtn.focused.textColor = Color.white;

            float colW = ((gridW - 26f) - (cols - 1) * gap) / (float)cols;
            for (int i = 0; i < kLanguageNames.Length; i++)
            {
                int r = i / cols;
                int c = i % cols;
                float bx = c * (colW + gap);
                float by = r * (cellH + gap);
                bool active = (kLanguageNames[i] == currentLanguage);
                GUI.color = Color.white;
                GUI.backgroundColor = active ? new Color(0.20f, 0.72f, 0.34f, 1f) : new Color(0.26f, 0.33f, 0.48f, 1f);
                if (GUI.Button(new Rect(bx, by, colW, cellH), FormatPickerLabel(kPickerLabels[i]), cellBtn))
                {
                    LoadLanguage(kLanguageNames[i]);
                    sPickerOpen = false;
                }
            }
            GUI.color = Color.white;
            GUI.backgroundColor = new Color(0.60f, 0.22f, 0.22f, 1f);
            GUI.EndScrollView();

            if (GUI.Button(new Rect(winX + (winW - 220f) * 0.5f, winY + winH - 52f, 220f, 40f), L("mod_close", "CLOSE", "KAPAT"), cellBtn))
            {
                sPickerOpen = false;
            }
        }
        finally
        {
            GUI.backgroundColor = oldBgColor;
            GUI.color = oldGuiColor;
            GUI.skin.button.fontSize = oldBtn;
            GUI.skin.label.fontSize = oldLbl;
            GUI.skin.button.font = oldBtnFont;
            GUI.skin.label.font = oldLblFont;
            GUI.matrix = oldMatrix;
        }
    }

    // ── Lightweight Arabic/Persian/Urdu Shaper & RTL Reorderer ──────────
    private struct ArabicForm { public char isolated, finalF, initialF, medialF; public bool connectsNext; public ArabicForm(char iso, char fin, char ini, char med, bool cn) { isolated = iso; finalF = fin; initialF = ini; medialF = med; connectsNext = cn; } }
    private static Dictionary<char, ArabicForm> sArabicMap;

    private static void EnsureArabicMap()
    {
        if (sArabicMap != null) return;
        sArabicMap = new Dictionary<char, ArabicForm>();
        sArabicMap['\u0627'] = new ArabicForm('\uFE8D', '\uFE8E', '\uFE8D', '\uFE8E', false); // Alif
        sArabicMap['\u0623'] = new ArabicForm('\uFE83', '\uFE84', '\uFE83', '\uFE84', false);
        sArabicMap['\u0625'] = new ArabicForm('\uFE87', '\uFE88', '\uFE87', '\uFE88', false);
        sArabicMap['\u0622'] = new ArabicForm('\uFE81', '\uFE82', '\uFE81', '\uFE82', false);
        sArabicMap['\u0628'] = new ArabicForm('\uFE8F', '\uFE90', '\uFE91', '\uFE92', true);  // Ba
        sArabicMap['\u067E'] = new ArabicForm('\uFB56', '\uFB57', '\uFB58', '\uFB59', true);  // Pe (Persian/Urdu)
        sArabicMap['\u062A'] = new ArabicForm('\uFE95', '\uFE96', '\uFE97', '\uFE98', true);  // Ta
        sArabicMap['\u062B'] = new ArabicForm('\uFE99', '\uFE9A', '\uFE9B', '\uFE9C', true);
        sArabicMap['\u062C'] = new ArabicForm('\uFE9D', '\uFE9E', '\uFE9F', '\uFEA0', true);  // Jim
        sArabicMap['\u0686'] = new ArabicForm('\uFB7A', '\uFB7B', '\uFB7C', '\uFB7D', true);  // Che
        sArabicMap['\u062D'] = new ArabicForm('\uFEA1', '\uFEA2', '\uFEA3', '\uFEA4', true);
        sArabicMap['\u062E'] = new ArabicForm('\uFEA5', '\uFEA6', '\uFEA7', '\uFEA8', true);
        sArabicMap['\u062F'] = new ArabicForm('\uFEA9', '\uFEAA', '\uFEA9', '\uFEAA', false);
        sArabicMap['\u0630'] = new ArabicForm('\uFEAB', '\uFEAC', '\uFEAB', '\uFEAC', false);
        sArabicMap['\u0631'] = new ArabicForm('\uFEAD', '\uFEAE', '\uFEAD', '\uFEAE', false);
        sArabicMap['\u0632'] = new ArabicForm('\uFEAF', '\uFEB0', '\uFEAF', '\uFEB0', false);
        sArabicMap['\u0698'] = new ArabicForm('\uFB8A', '\uFB8B', '\uFB8A', '\uFB8B', false);
        sArabicMap['\u0633'] = new ArabicForm('\uFEB1', '\uFEB2', '\uFEB3', '\uFEB4', true);
        sArabicMap['\u0634'] = new ArabicForm('\uFEB5', '\uFEB6', '\uFEB7', '\uFEB8', true);
        sArabicMap['\u0635'] = new ArabicForm('\uFEB9', '\uFEBA', '\uFEBB', '\uFEBC', true);
        sArabicMap['\u0636'] = new ArabicForm('\uFEBD', '\uFEBE', '\uFEBF', '\uFEC0', true);
        sArabicMap['\u0637'] = new ArabicForm('\uFEC1', '\uFEC2', '\uFEC3', '\uFEC4', true);
        sArabicMap['\u0638'] = new ArabicForm('\uFEC5', '\uFEC6', '\uFEC7', '\uFEC8', true);
        sArabicMap['\u0639'] = new ArabicForm('\uFEC9', '\uFECA', '\uFECB', '\uFECC', true);
        sArabicMap['\u063A'] = new ArabicForm('\uFECD', '\uFECE', '\uFECF', '\uFED0', true);
        sArabicMap['\u0641'] = new ArabicForm('\uFED1', '\uFED2', '\uFED3', '\uFED4', true);
        sArabicMap['\u0642'] = new ArabicForm('\uFED5', '\uFED6', '\uFED7', '\uFED8', true);
        sArabicMap['\u0643'] = new ArabicForm('\uFED9', '\uFEDA', '\uFEDB', '\uFEDC', true);
        sArabicMap['\u06A9'] = new ArabicForm('\uFB8E', '\uFB8F', '\uFB90', '\uFB91', true);  // Keheh
        sArabicMap['\u06AF'] = new ArabicForm('\uFB92', '\uFB93', '\uFB94', '\uFB95', true);  // Gaf
        sArabicMap['\u0644'] = new ArabicForm('\uFEDD', '\uFEDE', '\uFEDF', '\uFEE0', true);  // Lam
        sArabicMap['\u0645'] = new ArabicForm('\uFEE1', '\uFEE2', '\uFEE3', '\uFEE4', true);  // Mim
        sArabicMap['\u0646'] = new ArabicForm('\uFEE5', '\uFEE6', '\uFEE7', '\uFEE8', true);  // Nun
        sArabicMap['\u0647'] = new ArabicForm('\uFEE9', '\uFEEA', '\uFEEB', '\uFEEC', true);  // Ha
        sArabicMap['\u0629'] = new ArabicForm('\uFE93', '\uFE94', '\uFE93', '\uFE94', false); // Ta marbuta
        sArabicMap['\u0648'] = new ArabicForm('\uFEED', '\uFEEE', '\uFEED', '\uFEEE', false); // Waw
        sArabicMap['\u064A'] = new ArabicForm('\uFEF1', '\uFEF2', '\uFEF3', '\uFEF4', true);  // Ya
        sArabicMap['\u06CC'] = new ArabicForm('\uFBFC', '\uFBFD', '\uFBFE', '\uFBFF', true);  // Farsi Ya
        sArabicMap['\u0649'] = new ArabicForm('\uFEEF', '\uFEF0', '\uFEEF', '\uFEF0', false);
        sArabicMap['\u0626'] = new ArabicForm('\uFE89', '\uFE8A', '\uFE8B', '\uFE8C', true);
        sArabicMap['\u0624'] = new ArabicForm('\uFE85', '\uFE86', '\uFE85', '\uFE86', false);
        sArabicMap['\u0621'] = new ArabicForm('\uFE80', '\uFE80', '\uFE80', '\uFE80', false);
        sArabicMap['\u06D2'] = new ArabicForm('\uFBAE', '\uFBAF', '\uFBAE', '\uFBAF', false); // Urdu Yeh Barree
        sArabicMap['\u06C1'] = new ArabicForm('\uFBA6', '\uFBA7', '\uFBA8', '\uFBA9', true);  // Urdu Heh Goal
        sArabicMap['\u06BE'] = new ArabicForm('\uFBAA', '\uFBAB', '\uFBAC', '\uFBAD', true);  // Doachashmee
        sArabicMap['\u06BA'] = new ArabicForm('\uFB9E', '\uFB9F', '\uFB9E', '\uFB9F', false); // Noon Ghunna
        sArabicMap['\u0679'] = new ArabicForm('\uFB66', '\uFB67', '\uFB68', '\uFB69', true);  // Tteh
        sArabicMap['\u0688'] = new ArabicForm('\uFB88', '\uFB89', '\uFB88', '\uFB89', false); // Ddal
        sArabicMap['\u0691'] = new ArabicForm('\uFB8C', '\uFB8D', '\uFB8C', '\uFB8D', false); // Rreh
    }

    private static bool IsArabicDiacritic(char c)
    {
        return (c >= '\u064B' && c <= '\u065F') || c == '\u0670';
    }

    private static bool IsArabicChar(char c)
    {
        return (c >= '\u0600' && c <= '\u06FF') || (c >= '\uFB50' && c <= '\uFDFF') || (c >= '\uFE70' && c <= '\uFEFF');
    }

    private static string FormatPickerLabel(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return raw;
        int sep = raw.IndexOf(" / ");
        if (sep < 0) return raw;
        string tail = raw.Substring(sep + 3);
        bool hasRtl = false;
        for (int i = 0; i < tail.Length; i++)
        {
            if (IsArabicChar(tail[i])) { hasRtl = true; break; }
        }
        if (!hasRtl) return raw;
        EnsureArabicMap();
        return raw.Substring(0, sep + 3) + ShapeSingleRTLLine(tail);
    }

    public static string ShapeRTLIfNeeded(string input)
    {
        if (string.IsNullOrEmpty(input) || !IsRTL) return input;
        bool hasRtl = false;
        for (int i = 0; i < input.Length; i++)
        {
            if (IsArabicChar(input[i])) { hasRtl = true; break; }
        }
        if (!hasRtl) return input;

        EnsureArabicMap();
        string[] lines = input.Split('\n');
        for (int li = 0; li < lines.Length; li++)
        {
            lines[li] = ShapeSingleRTLLine(lines[li]);
        }
        return string.Join("\n", lines);
    }

    private static char FindPrevNonDiacritic(string s, int index)
    {
        for (int i = index - 1; i >= 0; i--)
        {
            if (!IsArabicDiacritic(s[i])) return s[i];
        }
        return '\0';
    }

    private static char FindNextNonDiacritic(string s, int index)
    {
        for (int i = index + 1; i < s.Length; i++)
        {
            if (!IsArabicDiacritic(s[i])) return s[i];
        }
        return '\0';
    }

    private static string ShapeSingleRTLLine(string line)
    {
        if (string.IsNullOrEmpty(line)) return line;
        int len = line.Length;
        char[] shaped = new char[len];
        for (int i = 0; i < len; i++)
        {
            char c = line[i];
            if (IsArabicDiacritic(c))
            {
                shaped[i] = c;
                continue;
            }
            ArabicForm form;
            if (sArabicMap.TryGetValue(c, out form))
            {
                bool prevConn = false;
                char prevCh = FindPrevNonDiacritic(line, i);
                ArabicForm pf;
                if (prevCh != '\0' && sArabicMap.TryGetValue(prevCh, out pf) && pf.connectsNext)
                    prevConn = true;

                bool nextConn = false;
                char nextCh = FindNextNonDiacritic(line, i);
                if (nextCh != '\0' && sArabicMap.ContainsKey(nextCh))
                    nextConn = form.connectsNext;

                if (prevConn && nextConn) shaped[i] = form.medialF;
                else if (prevConn) shaped[i] = form.finalF;
                else if (nextConn) shaped[i] = form.initialF;
                else shaped[i] = form.isolated;
            }
            else
            {
                shaped[i] = c;
            }
        }

        // Visual LTR reverse while preserving embedded LTR runs (digits/Latin/placeholders)
        StringBuilder outSb = new StringBuilder(len);
        int pos = len - 1;
        while (pos >= 0)
        {
            char ch = shaped[pos];
            if (!IsArabicChar(ch) && ch != ' ')
            {
                int end = pos;
                while (pos >= 0 && !IsArabicChar(shaped[pos]) && shaped[pos] != ' ')
                {
                    pos--;
                }
                for (int k = pos + 1; k <= end; k++)
                {
                    outSb.Append(shaped[k]);
                }
            }
            else
            {
                outSb.Append(ch);
                pos--;
            }
        }
        return outSb.ToString();
    }
}
