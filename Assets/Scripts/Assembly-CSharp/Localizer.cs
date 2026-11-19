using UnityEngine;
using Debug = UnityEngine.Debug;

public class Localizer : Singleton<Localizer>
{
    private SDFTreeNode mData;

    // YENİ: Dil override — null ise GameVersion.language kullanılır
    private string mLanguageOverride = null;

    private string GetCurrentFontSet()
    {
        string lang = (mLanguageOverride != null) ? mLanguageOverride : Singleton<GameVersion>.instance.fontSet;
        if (lang == "Chinese") return "Chinese";
        if (lang == "Japanese") return "Japanese";
        if (lang == "Korean") return "Korean";
        return "Default";
    }


    // ── Mevcut metodlar ──────────────────────────────────────────────────

    public bool Has(string id)
    {
        CacheData();
        return mData.hasAttribute(id);
    }

    public string Get(string id)
    {
        CacheData();
        if (mData.hasAttribute(id))
        {
            return mData[id];
        }
        Debug.Log("ERROR: Unable to find localized text id: " + id);
        return "*MISSING*";
    }

    public string Parse(string source)
    {
        if (source.Length > 0 && source[0] == '@')
        {
            if (!Application.isMobilePlatform)
            {
                if (Singleton<GameVersion>.instance.language == "Default"
                    && source.Substring(1).StartsWith("tutorial_ingame")
                    || source.Substring(1).StartsWith("waveselect_inst"))
                {
                    return Get(source.Substring(1) + "_pc");
                }
            }
            return Get(source.Substring(1));
        }
        return source;
    }

    public string LocalizeSpritePath(string originalPath)
    {
        return originalPath.Replace("Sprites/Localized/", "Sprites/Localized/Default/");
    }

    public string LocalizeFontPath(string originalPath)
    {
        // Path için küçük harf kullanmak gerekebilir ama Windows'ta case-insensitive.
        // Yine de orijinal koda uygun olsun diye fonSet adını döndürüyoruz.
        string currentFontPath = "Fonts/" + GetCurrentFontSet().ToLower() + "/";
        // Registry okurken de LocalizeFontPath kullanılıyor olabilir, 
        // Ancak klasör adları küçük harfli (chinese, japanese, korean, default)
        return originalPath.Replace("Fonts/", currentFontPath);
    }

    // ══════════════════════════════════════════════════════════════════════
    //  YENİ: Çalışma zamanında dil değiştirme
    //  SADECE text cache'ini sıfırlar — sprite/font yollarına DOKUNMAZ
    // ══════════════════════════════════════════════════════════════════════
    /// <summary>
    /// Dili değiştirir. resourceName = Resources/Text/ altındaki dosya adı.
    /// "Default" (English), "Turkish", "Chinese", "Japanese", "Korean"
    /// </summary>
    public void Reload(string resourceName)
    {
        mLanguageOverride = resourceName;
        mData = null;   // Cache'i sıfırla → sonraki Get() yeni dili yükler

        Debug.Log("[Localizer] Dil değişti: " + resourceName);
    }

    // ── Cache ────────────────────────────────────────────────────────────
    private void CacheData()
    {
        if (mData == null)
        {
            // Override varsa onu kullan, yoksa orijinal GameVersion'dan oku
            string lang = (mLanguageOverride != null)
                ? mLanguageOverride
                : Singleton<GameVersion>.instance.language;

            mData = SingletonMonoBehaviour<ResourcesManager>.instance.Open("Text/" + lang);
        }
    }
}