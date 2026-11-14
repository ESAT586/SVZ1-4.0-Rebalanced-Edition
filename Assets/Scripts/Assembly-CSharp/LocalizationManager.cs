using UnityEngine;

/// <summary>
/// LocalizationManager — v2.0
///
/// SADECE Localizer.Reload() çağırır.
/// GameVersion'a, MultiLanguages'a, sprite/font yollarına DOKUNMAZ.
/// Bu sayede orijinal oyun sistemleri bozulmaz.
/// </summary>
public class LocalizationManager : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────────
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

    // ── Dil tablosu ───────────────────────────────────────────────────────
    // kResourceNames = Resources/Text/ altındaki dosya adları
    private static readonly string[] kLanguageNames = { "English", "Turkish", "Chinese", "Japanese", "Korean" };
    private static readonly string[] kResourceNames = { "Default", "Turkish", "Chinese", "Japanese", "Korean" };

    // ── State ─────────────────────────────────────────────────────────────
    private const string LANG_PREF_KEY = "SVZ_Language";
    private string currentLanguage = "English";

    // Event — dış bileşenler dil değişiminden haberdar olmak için
    public delegate void LanguageChangedDelegate(string newLanguage);
    public event LanguageChangedDelegate OnLanguageChanged;

    // ── Lifecycle ─────────────────────────────────────────────────────────
    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        string saved = PlayerPrefs.GetString(LANG_PREF_KEY, "English");
        if (saved == "Default") saved = "English";
        currentLanguage = saved;
    }

    // ── Başlangıçta kayıtlı dili yükle ────────────────────────────────────
    // PCSettingsDialog.ApplyStartupSettings() içinden çağrılır
    public void ApplySavedLanguage()
    {
        string saved = PlayerPrefs.GetString(LANG_PREF_KEY, "English");
        if (saved == "Default") saved = "English";
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

    // ── Ana dil değiştirme metodu ─────────────────────────────────────────
    public void LoadLanguage(string langName)
    {
        if (langName == "Default") langName = "English";

        if (System.Array.IndexOf(kLanguageNames, langName) < 0)
        {
            Debug.LogWarning("[LocalizationManager] Bilinmeyen dil: " + langName);
            langName = "English";
        }

        currentLanguage = langName;
        PlayerPrefs.SetString(LANG_PREF_KEY, langName);
        PlayerPrefs.Save();

        // ── TEK YAPTIĞI ŞEY: Localizer cache'ini sıfırlamak ──────────────
        // YENİ: Artık fontları da güncelliyoruz ki Asya dilleri düzgün çalışsın
        if (Singleton<Localizer>.instance != null)
        {
            string resourceName = GetResourceName(langName);
            Singleton<Localizer>.instance.Reload(resourceName);
            
            if (Singleton<FontSetFinder>.IsAlive())
            {
                Singleton<FontSetFinder>.instance.Reload();
            }

            Debug.Log("[LocalizationManager] Yeni dil: " + resourceName);
        }
        else
        {
            Debug.LogWarning("[LocalizationManager] Localizer null!");
        }

        if (OnLanguageChanged != null)
            OnLanguageChanged(langName);
    }

    // ── Sonraki dile geç (buton için) ─────────────────────────────────────
    public void CycleLanguage()
    {
        int idx = System.Array.IndexOf(kLanguageNames, currentLanguage);
        if (idx < 0) idx = 0;
        idx = (idx + 1) % kLanguageNames.Length;
        LoadLanguage(kLanguageNames[idx]);
    }

    public string GetCurrentLanguage() { return currentLanguage; }

    private static string GetResourceName(string langName)
    {
        for (int i = 0; i < kLanguageNames.Length; i++)
            if (kLanguageNames[i] == langName) return kResourceNames[i];
        return "Default";
    }
}