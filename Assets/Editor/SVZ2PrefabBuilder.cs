using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class SVZ2PrefabBuilder
{
    private const string kDataBundlesDir = @"C:\Users\esatg\Downloads\Samurai-Vs-Zombies-2-R-1.1\Samurai-Vs-Zombies-2-R-1.1\DataBundles\English";
    private const string kGeneratedThemesDir = "Assets/Resources/sounds/svz2_themes";
    private const string kBaseCharSfxThemePath = "Assets/Prefab/BaseCharSFXTheme.prefab";

    private class Spec
    {
        public string pfName;
        public string schemaId;
        public string folder;
        public string tagFolderOverride;
        public string modelPrefab;
        public string fallbackMatPath;
        public string fallbackMountMatPath;
        public float baseScale;
        public bool snapToGround;
        public float walkSpeed;
        public bool preferFallbackMat;

        public Spec(
            string pfName,
            string schemaId,
            string folder,
            string tagFolderOverride,
            string modelPrefab,
            string fallbackMatPath,
            string fallbackMountMatPath,
            float baseScale,
            bool snapToGround,
            float walkSpeed)
            : this(pfName, schemaId, folder, tagFolderOverride, modelPrefab, fallbackMatPath, fallbackMountMatPath, baseScale, snapToGround, walkSpeed, false)
        {
        }

        public Spec(
            string pfName,
            string schemaId,
            string folder,
            string tagFolderOverride,
            string modelPrefab,
            string fallbackMatPath,
            string fallbackMountMatPath,
            float baseScale,
            bool snapToGround,
            float walkSpeed,
            bool preferFallbackMat)
        {
            this.pfName = pfName;
            this.schemaId = schemaId;
            this.folder = folder;
            this.tagFolderOverride = tagFolderOverride;
            this.modelPrefab = modelPrefab;
            this.fallbackMatPath = fallbackMatPath;
            this.fallbackMountMatPath = fallbackMountMatPath;
            this.baseScale = baseScale;
            this.snapToGround = snapToGround;
            this.walkSpeed = walkSpeed;
            this.preferFallbackMat = preferFallbackMat;
        }
    }

    private static readonly Spec[] kSpecs = new Spec[]
    {
        new Spec("PFBlindswordsman", "BlindSwordsman", "blindswordsman", null, "blindswordsman.prefab",
            "Assets/Resources/characters/blindswordsman/materials/blind_swordsman_D.mat", null, 100f, true, 125f),
        new Spec("PFShutendoji", "ShutenDoji", "shutendoji", null, "shutendoji.prefab",
            "Assets/Resources/characters/shutendoji/materials/shuten_doji_D.mat", null, 78f, true, 100f),
        new Spec("PFNioguardian", "NioGuardian", "nioguardian", null, "nioguardian.prefab",
            "Assets/Resources/characters/nioguardian/materials/nio_guardian_D.mat", null, 100f, true, 100f),
        new Spec("PFAkaname", "Akaname", "akaname", null, "akaname.prefab",
            "Assets/Resources/characters/akaname/materials/akaname_D.mat", null, 100f, true, 120f),
        new Spec("PFAmanojaku", "Amanojaku", "amanojaku", null, "amanojaku.prefab",
            "Assets/Resources/characters/amanojaku/materials/amanojaku_D.mat", null, 100f, true, 125f),
        new Spec("PFHarionago", "Harionago", "harionago", null, "harionago.prefab",
            "Assets/Resources/characters/harionago/materials/harionago_D.mat", null, 100f, true, 110f),
        new Spec("PFOnmoraki", "Onmoraki", "onmoraki", null, "onmoraki.prefab",
            "Assets/Resources/characters/onmoraki/materials/onmoraki_D.mat", null, 100f, false, 140f),
        new Spec("PFHorseArcher", "HorseArcher", "horsearcher", "Assets/Resources/characters/horsearcherhorse/horsearcherhorse_anim_tags", "HorseArcherPF.prefab",
            "Assets/Resources/characters/horsearcher/materials/horse_archer_D.mat",
            "Assets/Resources/characters/horsearcherhorse/materials/horse_archer_horse_D.mat", 100f, true, 220f),
        new Spec("PFHorseMaster", "HorseMaster", "horsemaster", "Assets/Resources/characters/horsemasterhorse/horsemasterhorse_anim_tags", "HorseMasterPF.prefab",
            "Assets/Resources/characters/horsemaster/materials/horse_master_D.mat",
            "Assets/Resources/characters/horsemasterhorse/materials/horse_masterhorse_D.mat", 100f, true, 200f),
        new Spec("PFRocketFarmer", "RocketFarmer", "rocketfarmer", null, "Rocketfarmer.prefab",
            "Assets/Resources/characters/rocketfarmer/materials/rocket_farmer_D.mat", null, 100f, true, 125f),
        new Spec("PFRockyTMan", "RockyTMan", "rockytman", null, "rockytman.prefab",
            "Assets/Resources/characters/rockytman/materials/rocky_t_man_D.mat", null, 100f, true, 110f),
        new Spec("PFWarriorHorse", "WarriorHorse", "warriorhorse", null, "warriorhorse.prefab",
            "Assets/Resources/characters/warriorhorse/materials/warrior_horse_D.mat", null, 100f, true, 200f),
        new Spec("PFCrawlingBody", "CrawlingBody", "crawlingbody", null, "crawlingbody.prefab",
            "Assets/Resources/characters/crawlingbody/materials/crawling_torso_D.mat", null, 100f, true, 90f),
        new Spec("PFCrawlingLegs", "CrawlingLegs", "crawlinglegs", null, "crawlinglegs.prefab",
            "Assets/Resources/characters/crawlinglegs/materials/crawling_torso_D.mat", null, 100f, true, 120f),
        new Spec("PFHeroKunoichi", "HeroAttack", "heroattack", null, "heroa.prefab",
            "Assets/Resources/characters/heroattack/materials/heroa_D.mat", null, 100f, true, 180f),
        new Spec("PFHeroDaimyo", "HeroLeadership", "heroleadership", null, "herol.prefab",
            "Assets/Resources/characters/heroleadership/materials/herol_D.mat", null, 100f, true, 150f),
        new Spec("PFHeroOnmyoji", "HeroAbilities", "heroabilities", null, "heroab.prefab",
            "Assets/Resources/characters/heroabilities/materials/heroab_D.mat", null, 100f, true, 140f),
        new Spec("PFHeroRonin", "HeroDefense", "herodefense", null, "herod.prefab",
            "Assets/Resources/characters/herodefense/materials/herod_D.mat", null, 100f, true, 130f),
        new Spec("PFHeroBalanced", "HeroBalanced", "herobalanced", null, "herob.prefab",
            "Assets/Resources/characters/herobalanced/materials/herob_D.mat", null, 100f, true, 150f),
        new Spec("PFShutendojiBlue", "ShutenDoji", "shutendoji", null, "shutendoji.prefab",
            "Assets/Resources/characters/shutendoji/materials/shuten_doji_blue_D.mat", null, 78f, true, 100f, true),
        new Spec("PFOnmorakiAlt", "Onmoraki", "onmoraki", null, "onmoraki.prefab",
            "Assets/Resources/characters/onmoraki/materials/onmoraki_alt_D.mat", null, 100f, false, 140f, true),
        new Spec("PFCrawlingBodyRed", "CrawlingBody", "crawlingbody", null, "crawlingbody.prefab",
            "Assets/Resources/characters/crawlingtorso/materials/crawling_torso_red_D.mat", null, 100f, true, 90f, true),
        new Spec("PFCrawlingLegsRed", "CrawlingLegs", "crawlinglegs", null, "crawlinglegs.prefab",
            "Assets/Resources/characters/crawlingtorso/materials/crawling_torso_red_D.mat", null, 100f, true, 120f, true)
    };

    private static readonly HashSet<string> kLoopClipNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "idle", "walk", "victoryloop", "vistoryloop", "cower",
        "rangedattackidle", "runback", "backpedal", "fly",
        "horseidle", "horsewalk", "stun", "charge"
    };

    private class SchemaData
    {
        public Dictionary<string, Dictionary<string, string>> characters;
        public Dictionary<string, Dictionary<string, string>> cmcs;
        public Dictionary<string, List<KeyValuePair<string, Dictionary<string, string>>>> joints;
        public Dictionary<string, List<Dictionary<string, string>>> randomSets;
        public Dictionary<string, List<string>> animClipGroups;
        public Dictionary<string, Dictionary<string, string>> tapSettings;
        public Dictionary<string, Dictionary<string, string>> soundThemes;
        public Dictionary<string, List<KeyValuePair<string, Dictionary<string, string>>>> soundEventSets;
        public Dictionary<string, List<string>> soundClipsets;
        public Dictionary<string, SoundTheme> builtThemes = new Dictionary<string, SoundTheme>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> resourcePathLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> audioPathLookupNoExt = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    [MenuItem("Tools/SVZ2/Build All SVZ2 Character Prefabs")]
    public static void BuildAll()
    {
        SchemaData schemas = LoadSchemas();

        CharacterModelController templateCtrl = null;
        SoundThemePlayer templateSound = null;
        GameObject ghostRef = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/characters/PFGhostRonin.prefab");
        if (ghostRef != null)
        {
            templateCtrl = ghostRef.GetComponent<CharacterModelController>();
            templateSound = ghostRef.GetComponent<SoundThemePlayer>();
        }

        int built = 0;
        int failed = 0;

        for (int i = 0; i < kSpecs.Length; i++)
        {
            Spec spec = kSpecs[i];
            if (BuildOne(spec, schemas, templateCtrl, templateSound))
            {
                built++;
            }
            else
            {
                failed++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[SVZ2PrefabBuilder] Built " + built + " prefabs (" + schemas.builtThemes.Count + " SoundThemes), failed " + failed + ".");
    }

    private static SchemaData LoadSchemas()
    {
        SchemaData sd = new SchemaData();
        BuildResourceFileIndex(sd);

        sd.characters = ExtractTopSectionMap(ParseUdaman(Path.Combine(kDataBundlesDir, "CharacterSchema.txt")), "Character");
        sd.cmcs = ExtractTopSectionMap(ParseUdaman(Path.Combine(kDataBundlesDir, "CharacterModelControllerSchema.txt")), "Character");
        sd.tapSettings = ExtractTopSectionMap(ParseUdaman(Path.Combine(kDataBundlesDir, "TaggedAnimPlayerSchema.txt")), "Character");
        sd.soundThemes = ExtractTopSectionMap(ParseUdaman(Path.Combine(kDataBundlesDir, "USoundThemeSetSchema.txt")), "Themes");

        sd.joints = ParseUdamanOrdered(Path.Combine(kDataBundlesDir, "JointSchema.txt"));
        sd.soundEventSets = ParseUdamanOrdered(Path.Combine(kDataBundlesDir, "USoundThemeEventSetSchema.txt"));

        Dictionary<string, List<KeyValuePair<string, Dictionary<string, string>>>> rawRand =
            ParseUdamanOrdered(Path.Combine(kDataBundlesDir, "RandomAnimationClipSetSchema.txt"));
        sd.randomSets = new Dictionary<string, List<Dictionary<string, string>>>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, List<KeyValuePair<string, Dictionary<string, string>>>> kv in rawRand)
        {
            List<Dictionary<string, string>> list = new List<Dictionary<string, string>>();
            for (int i = 0; i < kv.Value.Count; i++)
            {
                list.Add(kv.Value[i].Value);
            }
            sd.randomSets[kv.Key] = list;
        }

        Dictionary<string, List<KeyValuePair<string, Dictionary<string, string>>>> rawClips =
            ParseUdamanOrdered(Path.Combine(kDataBundlesDir, "AnimationClipSchema.txt"));
        sd.animClipGroups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, List<KeyValuePair<string, Dictionary<string, string>>>> kv in rawClips)
        {
            List<string> paths = new List<string>();
            for (int i = 0; i < kv.Value.Count; i++)
            {
                string cp;
                if (kv.Value[i].Value.TryGetValue("clip", out cp) && !string.IsNullOrEmpty(cp))
                {
                    paths.Add(cp);
                }
            }
            sd.animClipGroups[kv.Key] = paths;
        }

        Dictionary<string, List<KeyValuePair<string, Dictionary<string, string>>>> rawAudioSets =
            ParseUdamanOrdered(Path.Combine(kDataBundlesDir, "USoundThemeClipsetSchema.txt"));
        sd.soundClipsets = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, List<KeyValuePair<string, Dictionary<string, string>>>> kv in rawAudioSets)
        {
            List<string> paths = new List<string>();
            for (int i = 0; i < kv.Value.Count; i++)
            {
                string ap;
                if (kv.Value[i].Value.TryGetValue("audioClip", out ap) && !string.IsNullOrEmpty(ap))
                {
                    paths.Add(ap);
                }
            }
            sd.soundClipsets[kv.Key] = paths;
        }

        return sd;
    }

    private static void BuildResourceFileIndex(SchemaData sd)
    {
        if (!Directory.Exists("Assets/Resources")) return;
        string[] files = Directory.GetFiles("Assets/Resources", "*", SearchOption.AllDirectories);
        for (int i = 0; i < files.Length; i++)
        {
            string norm = files[i].Replace('\\', '/');
            if (norm.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
            sd.resourcePathLookup[norm] = norm;
            string ext = Path.GetExtension(norm).ToLowerInvariant();
            if (ext == ".wav" || ext == ".mp3" || ext == ".ogg")
            {
                string noExt = norm.Substring(0, norm.Length - ext.Length);
                if (!sd.audioPathLookupNoExt.ContainsKey(noExt))
                {
                    sd.audioPathLookupNoExt[noExt] = norm;
                }
                else if (ext == ".wav")
                {
                    sd.audioPathLookupNoExt[noExt] = norm;
                }
            }
        }
    }

    private static Dictionary<string, Dictionary<string, Dictionary<string, string>>> ParseUdaman(string path)
    {
        Dictionary<string, Dictionary<string, Dictionary<string, string>>> result =
            new Dictionary<string, Dictionary<string, Dictionary<string, string>>>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, List<KeyValuePair<string, Dictionary<string, string>>>> ordered = ParseUdamanOrdered(path);
        foreach (KeyValuePair<string, List<KeyValuePair<string, Dictionary<string, string>>>> top in ordered)
        {
            Dictionary<string, Dictionary<string, string>> recs = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < top.Value.Count; i++)
            {
                recs[top.Value[i].Key] = top.Value[i].Value;
            }
            result[top.Key] = recs;
        }
        return result;
    }

    private static Dictionary<string, List<KeyValuePair<string, Dictionary<string, string>>>> ParseUdamanOrdered(string path)
    {
        Dictionary<string, List<KeyValuePair<string, Dictionary<string, string>>>> result =
            new Dictionary<string, List<KeyValuePair<string, Dictionary<string, string>>>>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path))
        {
            Debug.LogWarning("[SVZ2PrefabBuilder] Schema file missing: " + path);
            return result;
        }

        string curTop = null;
        string curRec = null;
        Dictionary<string, string> curFields = null;

        string[] lines = File.ReadAllLines(path);
        for (int i = 0; i < lines.Length; i++)
        {
            string raw = lines[i];
            string trimmed = raw.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#") || trimmed.StartsWith(";"))
            {
                continue;
            }
            int indent = 0;
            while (indent < raw.Length && raw[indent] == ' ') indent++;

            if (indent == 0 && trimmed.StartsWith("[") && trimmed.EndsWith("]"))
            {
                curTop = trimmed.Substring(1, trimmed.Length - 2).Trim();
                if (!result.ContainsKey(curTop))
                {
                    result[curTop] = new List<KeyValuePair<string, Dictionary<string, string>>>();
                }
                curRec = null;
                curFields = null;
            }
            else if (curTop != null && indent <= 2 && trimmed.EndsWith(":") && trimmed.IndexOf('=') < 0)
            {
                curRec = trimmed.Substring(0, trimmed.Length - 1).Trim();
                curFields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                result[curTop].Add(new KeyValuePair<string, Dictionary<string, string>>(curRec, curFields));
            }
            else if (curTop != null && trimmed.IndexOf('=') >= 0)
            {
                if (curFields == null)
                {
                    curRec = "_root";
                    curFields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    result[curTop].Add(new KeyValuePair<string, Dictionary<string, string>>(curRec, curFields));
                }
                int eq = trimmed.IndexOf('=');
                string k = trimmed.Substring(0, eq).Trim();
                string v = trimmed.Substring(eq + 1).Trim();
                curFields[k] = v;
            }
        }
        return result;
    }

    private static Dictionary<string, Dictionary<string, string>> ExtractTopSectionMap(
        Dictionary<string, Dictionary<string, Dictionary<string, string>>> parsed, string topSection)
    {
        Dictionary<string, Dictionary<string, string>> inner;
        if (parsed != null && parsed.TryGetValue(topSection, out inner))
        {
            return inner;
        }
        return new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
    }

    private static string GetField(Dictionary<string, string> dict, string key, string def)
    {
        if (dict == null) return def;
        string val;
        if (dict.TryGetValue(key, out val) && !string.IsNullOrEmpty(val) &&
            !string.Equals(val, "NULL", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(val, "None", StringComparison.OrdinalIgnoreCase))
        {
            return val;
        }
        return def;
    }

    private static float ParseFloat(string s, float def)
    {
        if (string.IsNullOrEmpty(s)) return def;
        float v;
        if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v))
        {
            return v;
        }
        return def;
    }

    private static int ParseInt(string s, int def)
    {
        if (string.IsNullOrEmpty(s)) return def;
        int v;
        if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v))
        {
            return v;
        }
        return def;
    }

    private static bool ParseBool(string s, bool def)
    {
        if (string.IsNullOrEmpty(s)) return def;
        if (string.Equals(s, "True", StringComparison.OrdinalIgnoreCase) || s == "1") return true;
        if (string.Equals(s, "False", StringComparison.OrdinalIgnoreCase) || s == "0") return false;
        return def;
    }

    private static string ResolveResourceAssetPath(SchemaData sd, string schemaPath)
    {
        if (string.IsNullOrEmpty(schemaPath)) return null;
        string clean = schemaPath.Replace('\\', '/').Trim();
        if (clean.StartsWith("Assets/Game/Resources/", StringComparison.OrdinalIgnoreCase))
        {
            clean = "Assets/Resources/" + clean.Substring("Assets/Game/Resources/".Length);
        }
        else if (clean.StartsWith("Game/Resources/", StringComparison.OrdinalIgnoreCase))
        {
            clean = "Assets/Resources/" + clean.Substring("Game/Resources/".Length);
        }

        string exact;
        if (sd.resourcePathLookup.TryGetValue(clean, out exact))
        {
            return exact;
        }
        if (clean.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
        {
            string asPrefab = clean.Substring(0, clean.Length - 4) + ".prefab";
            if (sd.resourcePathLookup.TryGetValue(asPrefab, out exact))
            {
                return exact;
            }
        }
        return File.Exists(clean) ? clean : null;
    }

    private static AudioClip ResolveAudioClip(SchemaData sd, string schemaAudioPath)
    {
        if (string.IsNullOrEmpty(schemaAudioPath)) return null;
        string clean = schemaAudioPath.Replace('\\', '/').Trim();
        if (clean.StartsWith("Assets/Game/Resources/", StringComparison.OrdinalIgnoreCase))
        {
            clean = "Assets/Resources/" + clean.Substring("Assets/Game/Resources/".Length);
        }

        string exact;
        if (sd.resourcePathLookup.TryGetValue(clean, out exact))
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(exact);
            if (clip != null) return clip;
        }

        string ext = Path.GetExtension(clean);
        string noExt = string.IsNullOrEmpty(ext) ? clean : clean.Substring(0, clean.Length - ext.Length);
        if (sd.audioPathLookupNoExt.TryGetValue(noExt, out exact))
        {
            return AssetDatabase.LoadAssetAtPath<AudioClip>(exact);
        }
        return null;
    }

    private static SoundTheme EnsureSoundTheme(SchemaData sd, string themeKey)
    {
        if (string.IsNullOrEmpty(themeKey)) return null;
        string shortName = themeKey.Trim();
        if (shortName.StartsWith("Themes.", StringComparison.OrdinalIgnoreCase))
        {
            shortName = shortName.Substring("Themes.".Length);
        }
        if (string.IsNullOrEmpty(shortName) ||
            string.Equals(shortName, "None", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(shortName, "NULL", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        SoundTheme cached;
        if (sd.builtThemes.TryGetValue(shortName, out cached) && cached != null)
        {
            return cached;
        }

        Dictionary<string, string> themeEntry = null;
        sd.soundThemes.TryGetValue(shortName, out themeEntry);

        SoundTheme parentTheme = null;
        string parentKey = GetField(themeEntry, "parentThemeKey", null);
        if (!string.IsNullOrEmpty(parentKey))
        {
            parentTheme = EnsureSoundTheme(sd, parentKey);
        }
        else if (string.Equals(shortName, "Base", StringComparison.OrdinalIgnoreCase))
        {
            parentTheme = AssetDatabase.LoadAssetAtPath<SoundTheme>(kBaseCharSfxThemePath);
        }

        string eventSetKey = GetField(themeEntry, "events", shortName);
        List<SoundThemeEvent> events = new List<SoundThemeEvent>();
        HashSet<string> addedEventNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        List<KeyValuePair<string, Dictionary<string, string>>> evtList;
        if (sd.soundEventSets.TryGetValue(eventSetKey, out evtList))
        {
            for (int i = 0; i < evtList.Count; i++)
            {
                string evtName = evtList[i].Key;
                Dictionary<string, string> ekv = evtList[i].Value;
                if (string.IsNullOrEmpty(evtName)) continue;

                List<AudioClip> clips = new List<AudioClip>();
                string singleClipPath = GetField(ekv, "singleClip", null);
                if (!string.IsNullOrEmpty(singleClipPath))
                {
                    AudioClip c = ResolveAudioClip(sd, singleClipPath);
                    if (c != null) clips.Add(c);
                }

                string clipsetKey = GetField(ekv, "clipset", null);
                if (!string.IsNullOrEmpty(clipsetKey))
                {
                    List<string> clipPaths;
                    if (sd.soundClipsets.TryGetValue(clipsetKey, out clipPaths))
                    {
                        for (int j = 0; j < clipPaths.Count; j++)
                        {
                            AudioClip c = ResolveAudioClip(sd, clipPaths[j]);
                            if (c != null) clips.Add(c);
                        }
                    }
                }

                if (clips.Count == 0) continue;

                SoundThemeEvent ste = new SoundThemeEvent();
                ste.name = evtName;
                ste.clips = clips.ToArray();
                ste.volume = ParseFloat(GetField(ekv, "volume", null), 1f);
                ste.pitchVariance = ParseFloat(GetField(ekv, "pitchVariance", null), 0f);
                ste.percentChanceNone = ParseInt(GetField(ekv, "percentSilent", null), 0);
                ste.playLimit = ParseInt(GetField(ekv, "playLimit", null), 0);
                ste.priority = ParseInt(GetField(ekv, "priority", null), 128);
                ste.loop = ParseBool(GetField(ekv, "loop", null), false);
                ste.isVoice = ParseBool(GetField(ekv, "isVoice", null), false);
                ste.fadeInTime = ParseFloat(GetField(ekv, "fadeInTime", null), 0f);
                ste.fadeOutTime = ParseFloat(GetField(ekv, "fadeOutTime", null), 0f);
                ste.minDistanceOverride = ParseFloat(GetField(ekv, "minDistanceOverride", null), 0f);
                ste.maxDistanceOverride = ParseFloat(GetField(ekv, "maxDistanceOverride", null), 0f);
                ste.useLogarithmicRolloff = ParseBool(GetField(ekv, "logarithmicRolloff", null), false);
                events.Add(ste);
                addedEventNames.Add(evtName);
            }
        }

        // Compatibility alias: SVZ2 anim tags on heroes call "AttackA", schema uses "Attack".
        if (addedEventNames.Contains("Attack") && !addedEventNames.Contains("AttackA"))
        {
            for (int i = 0; i < events.Count; i++)
            {
                if (string.Equals(events[i].name, "Attack", StringComparison.OrdinalIgnoreCase))
                {
                    SoundThemeEvent clone = CloneSoundThemeEvent(events[i], "AttackA");
                    events.Add(clone);
                    addedEventNames.Add("AttackA");
                    break;
                }
            }
        }

        if (!Directory.Exists(kGeneratedThemesDir))
        {
            Directory.CreateDirectory(kGeneratedThemesDir);
        }
        string outPath = kGeneratedThemesDir + "/" + shortName + ".prefab";

        GameObject tmp = new GameObject(shortName);
        try
        {
            SoundTheme st = tmp.AddComponent<SoundTheme>();
            st.baseTheme = parentTheme;
            st.events = events.ToArray();

            UnityEngine.Object existing = AssetDatabase.LoadAssetAtPath<GameObject>(outPath);
            if (existing != null)
            {
                PrefabUtility.ReplacePrefab(tmp, existing, ReplacePrefabOptions.ReplaceNameBased);
            }
            else
            {
                PrefabUtility.CreatePrefab(outPath, tmp);
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(tmp);
        }

        SoundTheme loaded = AssetDatabase.LoadAssetAtPath<SoundTheme>(outPath);
        sd.builtThemes[shortName] = loaded;
        return loaded;
    }

    private static SoundThemeEvent CloneSoundThemeEvent(SoundThemeEvent src, string newName)
    {
        SoundThemeEvent dst = new SoundThemeEvent();
        dst.name = newName;
        dst.clips = src.clips;
        dst.percentChanceNone = src.percentChanceNone;
        dst.pitchVariance = src.pitchVariance;
        dst.volume = src.volume;
        dst.isVoice = src.isVoice;
        dst.playLimit = src.playLimit;
        dst.priority = src.priority;
        dst.trackPositionAndLifetime = src.trackPositionAndLifetime;
        dst.fadeInTime = src.fadeInTime;
        dst.fadeOutTime = src.fadeOutTime;
        dst.customEffect = src.customEffect;
        dst.loop = src.loop;
        dst.minDistanceOverride = src.minDistanceOverride;
        dst.maxDistanceOverride = src.maxDistanceOverride;
        dst.useLogarithmicRolloff = src.useLogarithmicRolloff;
        return dst;
    }

    private static bool BuildOne(Spec spec, SchemaData sd, CharacterModelController templateCtrl, SoundThemePlayer templateSound)
    {
        Dictionary<string, string> charEntry = null;
        sd.characters.TryGetValue(spec.schemaId, out charEntry);

        Dictionary<string, string> cmcEntry = null;
        sd.cmcs.TryGetValue(spec.schemaId, out cmcEntry);

        Dictionary<string, string> tapEntry = null;
        sd.tapSettings.TryGetValue(spec.schemaId, out tapEntry);

        string folderAssetPath = "Assets/Resources/characters/" + spec.folder;
        string modelPath = folderAssetPath + "/" + spec.modelPrefab;
        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (modelAsset == null)
        {
            Debug.LogError("[SVZ2PrefabBuilder] Missing model prefab: " + modelPath);
            return false;
        }

        string tagSearchDir = !string.IsNullOrEmpty(spec.tagFolderOverride) ? spec.tagFolderOverride : folderAssetPath;
        List<AnimationClip> tagClips = CollectTagClips(tagSearchDir);
        EnsureLoopClipWrapModes(tagClips);

        AnimationClip defaultTagClip = SelectDefaultClip(tagClips);

        GameObject root = new GameObject(spec.pfName);
        GameObject modelChild = null;
        try
        {
            float sx = ParseFloat(GetField(charEntry, "scaleX", null), 1f) * spec.baseScale;
            float sy = ParseFloat(GetField(charEntry, "scaleY", null), 1f) * spec.baseScale;
            float sz = ParseFloat(GetField(charEntry, "scaleZ", null), 1f) * spec.baseScale;

            root.transform.position = Vector3.zero;
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = new Vector3(sx, sy, sz);

            modelChild = (GameObject)UnityEngine.Object.Instantiate(modelAsset);
            modelChild.name = Path.GetFileNameWithoutExtension(spec.modelPrefab);
            modelChild.transform.SetParent(root.transform, false);
            modelChild.transform.localPosition = Vector3.zero;
            modelChild.transform.localRotation = Quaternion.identity;
            modelChild.transform.localScale = Vector3.one;

            ApplyAndVerifyMaterials(spec, sd, charEntry, modelChild);

            Animation rootAnim = root.AddComponent<Animation>();
            ConfigureAnimation(rootAnim, tagClips, defaultTagClip, true, AnimationCullingType.AlwaysAnimate);

            Animation[] allAnims = modelChild.GetComponentsInChildren<Animation>(true);
            Animation primaryChildAnim = allAnims.Length > 0 ? allAnims[0] : null;
            if (primaryChildAnim == null)
            {
                primaryChildAnim = modelChild.AddComponent<Animation>();
                allAnims = new Animation[] { primaryChildAnim };
            }

            for (int a = 0; a < allAnims.Length; a++)
            {
                if (allAnims[a] != null)
                {
                    ConfigureChildAnimation(allAnims[a], folderAssetPath);
                }
            }

            TaggedAnimPlayer tap = root.GetComponent<TaggedAnimPlayer>();
            if (tap == null) tap = root.AddComponent<TaggedAnimPlayer>();
            tap.jointAnimation = primaryChildAnim;
            tap.defaultBlendSpeed = ParseFloat(GetField(tapEntry, "defaultBlendSpeed", null), 0.1f);
            tap.alsoPlayOnAllChildren = allAnims.Length > 1;

            string paperdollKey = GetField(charEntry, "paperdollData", spec.schemaId);
            AutoPaperdoll doll = root.GetComponent<AutoPaperdoll>();
            if (doll == null) doll = root.AddComponent<AutoPaperdoll>();
            doll.joints = BuildJointsFromSchema(sd, paperdollKey, modelChild.transform, root.transform, spec.folder);

            if (root.GetComponent<AnimationEffects>() == null)
            {
                root.AddComponent<AnimationEffects>();
            }

            CharacterModelController ctrl = root.GetComponent<CharacterModelController>();
            if (ctrl == null) ctrl = root.AddComponent<CharacterModelController>();
            ctrl.animatedWalkSpeed = spec.walkSpeed;
            ctrl.snapToGround = ParseBool(GetField(cmcEntry, "snapToGround", null), spec.snapToGround);

            ConfigureRandomAnimSets(ctrl, sd, cmcEntry, tagClips);
            ConfigureImpactEffects(ctrl, sd, cmcEntry, templateCtrl);

            SoundThemePlayer stp = root.GetComponent<SoundThemePlayer>();
            if (stp == null) stp = root.AddComponent<SoundThemePlayer>();
            string soundThemeKey = GetField(charEntry, "soundTheme", null);
            SoundTheme builtTheme = EnsureSoundTheme(sd, soundThemeKey);
            if (builtTheme != null)
            {
                stp.baseTheme = builtTheme;
            }
            else if (templateSound != null)
            {
                stp.baseTheme = templateSound.baseTheme;
            }
            if (templateSound != null)
            {
                stp.defaultMinDistance = templateSound.defaultMinDistance;
                stp.defaultMaxDistance = templateSound.defaultMaxDistance;
            }
            stp.autoPlayEvent = ThemeHasEvent(stp.baseTheme, "Spawn") ? "Spawn" : string.Empty;

            string outPrefabPath = "Assets/Resources/characters/" + spec.pfName + ".prefab";
            UnityEngine.Object existing = AssetDatabase.LoadAssetAtPath<GameObject>(outPrefabPath);
            if (existing != null)
            {
                PrefabUtility.ReplacePrefab(root, existing, ReplacePrefabOptions.ReplaceNameBased);
            }
            else
            {
                PrefabUtility.CreatePrefab(outPrefabPath, root);
            }
            return true;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static bool ThemeHasEvent(SoundTheme theme, string eventName)
    {
        SoundTheme cur = theme;
        int guard = 0;
        while (cur != null && guard++ < 8)
        {
            if (cur.events != null)
            {
                for (int i = 0; i < cur.events.Length; i++)
                {
                    if (cur.events[i] != null && string.Equals(cur.events[i].name, eventName, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            cur = cur.baseTheme;
        }
        return false;
    }

    private static void ApplyAndVerifyMaterials(Spec spec, SchemaData sd, Dictionary<string, string> charEntry, GameObject modelChild)
    {
        string schemaMatPath = ResolveResourceAssetPath(sd, GetField(charEntry, "material", null));
        Material schemaMat = !string.IsNullOrEmpty(schemaMatPath) ? AssetDatabase.LoadAssetAtPath<Material>(schemaMatPath) : null;
        Material fallbackMat = !string.IsNullOrEmpty(spec.fallbackMatPath) ? AssetDatabase.LoadAssetAtPath<Material>(spec.fallbackMatPath) : null;
        Material mountMat = !string.IsNullOrEmpty(spec.fallbackMountMatPath) ? AssetDatabase.LoadAssetAtPath<Material>(spec.fallbackMountMatPath) : null;

        Renderer[] renderers = modelChild.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null) continue;

            Material chosen = null;
            if (renderers.Length == 1)
            {
                chosen = (spec.preferFallbackMat && fallbackMat != null) ? fallbackMat : (schemaMat != null ? schemaMat : fallbackMat);
            }
            else
            {
                bool isHorsePart = r.gameObject.name.IndexOf("horse:", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                   r.gameObject.name.IndexOf("_horse", StringComparison.OrdinalIgnoreCase) >= 0;
                if (isHorsePart && mountMat != null)
                {
                    chosen = mountMat;
                }
                else if (isHorsePart && schemaMat != null && !spec.preferFallbackMat)
                {
                    chosen = schemaMat;
                }
                else if (!isHorsePart && fallbackMat != null)
                {
                    chosen = fallbackMat;
                }
                else if (!IsMaterialTexturedValid(r.sharedMaterial))
                {
                    chosen = (spec.preferFallbackMat && fallbackMat != null) ? fallbackMat : (schemaMat != null ? schemaMat : fallbackMat);
                }
            }

            if (chosen != null)
            {
                r.sharedMaterial = chosen;
                r.sharedMaterials = new Material[] { chosen };
            }

            Material active = r.sharedMaterial;
            if (!IsMaterialTexturedValid(active))
            {
                Debug.LogWarning("[SVZ2PrefabBuilder] Renderer '" + r.name + "' on " + spec.pfName + " has invalid/untextured material: " + (active != null ? active.name : "<null>"));
            }
        }
    }

    private static bool IsMaterialTexturedValid(Material mat)
    {
        if (mat == null) return false;
        if (mat.shader == null || mat.shader.name.IndexOf("Error", StringComparison.OrdinalIgnoreCase) >= 0) return false;
        return mat.mainTexture != null;
    }

    private static void EnsureLoopClipWrapModes(List<AnimationClip> clips)
    {
        for (int i = 0; i < clips.Count; i++)
        {
            AnimationClip c = clips[i];
            if (c != null && kLoopClipNames.Contains(c.name) && c.wrapMode != WrapMode.Loop)
            {
                c.wrapMode = WrapMode.Loop;
                EditorUtility.SetDirty(c);
            }
        }
    }

    private static readonly KeyValuePair<string, string>[] kChildClipAliases = new KeyValuePair<string, string>[]
    {
        new KeyValuePair<string, string>("vistory", "victory"),
        new KeyValuePair<string, string>("vistoryloop", "victoryloop"),
        new KeyValuePair<string, string>("horseattack01", "attack01"),
        new KeyValuePair<string, string>("horsedie", "die"),
        new KeyValuePair<string, string>("horseknockback", "knockback"),
        new KeyValuePair<string, string>("horseknockbackland", "knockbackland"),
        new KeyValuePair<string, string>("horsevictory", "victory")
    };

    private static AnimationClip SelectDefaultClip(List<AnimationClip> clips)
    {
        AnimationClip def = FindClipByNames(clips, new string[]
        {
            "walk", "horsewalk", "fly", "idle", "horseidle", "rangedattackidle"
        });
        if (def != null) return def;
        for (int i = 0; i < clips.Count; i++)
        {
            if (clips[i] == null) continue;
            string n = clips[i].name.ToLowerInvariant();
            if (n != "die" && n != "horsedie" && n != "dismount" && n != "knockback" && n != "knockbackland")
            {
                return clips[i];
            }
        }
        return clips.Count > 0 ? clips[0] : null;
    }

    private static void CollectRelativePaths(Transform current, string prefix, HashSet<string> outPaths, ref bool isMountSkeleton)
    {
        for (int i = 0; i < current.childCount; i++)
        {
            Transform ch = current.GetChild(i);
            string p = string.IsNullOrEmpty(prefix) ? ch.name : (prefix + "/" + ch.name);
            outPaths.Add(p);
            string lower = ch.name.ToLowerInvariant();
            if (lower.IndexOf("horse:", StringComparison.Ordinal) >= 0 || lower.IndexOf("horse_skeleton", StringComparison.Ordinal) >= 0)
            {
                isMountSkeleton = true;
            }
            CollectRelativePaths(ch, p, outPaths, ref isMountSkeleton);
        }
    }

    private static bool ClipMatchesSkeleton(AnimationClip clip, HashSet<string> validPaths)
    {
        if (clip == null) return false;
        EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
        int nonEmpty = 0;
        int matched = 0;
        for (int i = 0; i < bindings.Length; i++)
        {
            string p = bindings[i].path;
            if (string.IsNullOrEmpty(p)) continue;
            nonEmpty++;
            if (validPaths.Contains(p))
            {
                matched++;
            }
        }
        if (nonEmpty > 0 && matched == 0)
        {
            return false;
        }
        return true;
    }

    private static void TryAddTopLevelClips(Animation anim, string dirAssetPath, HashSet<string> validPaths, List<AnimationClip> clips)
    {
        if (string.IsNullOrEmpty(dirAssetPath) || !Directory.Exists(dirAssetPath)) return;
        string[] topAnims = Directory.GetFiles(dirAssetPath, "*.anim", SearchOption.TopDirectoryOnly);
        Array.Sort(topAnims, StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < topAnims.Length; i++)
        {
            string norm = topAnims[i].Replace('\\', '/');
            AnimationClip extraClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(norm);
            if (extraClip != null && anim[extraClip.name] == null && ClipMatchesSkeleton(extraClip, validPaths))
            {
                anim.AddClip(extraClip, extraClip.name);
                clips.Add(extraClip);
            }
        }
    }

    private static void ConfigureChildAnimation(Animation anim, string folderAssetPath)
    {
        HashSet<string> validPaths = new HashSet<string>(StringComparer.Ordinal);
        bool isMountSkeleton = anim.name.IndexOf("horse", StringComparison.OrdinalIgnoreCase) >= 0;
        CollectRelativePaths(anim.transform, string.Empty, validPaths, ref isMountSkeleton);

        List<string> toRemove = new List<string>();
        List<AnimationClip> clips = new List<AnimationClip>();
        foreach (AnimationState st in anim)
        {
            if (st == null || st.clip == null) continue;
            if (!ClipMatchesSkeleton(st.clip, validPaths))
            {
                toRemove.Add(st.name);
            }
            else
            {
                clips.Add(st.clip);
            }
        }
        for (int i = 0; i < toRemove.Count; i++)
        {
            anim.RemoveClip(toRemove[i]);
        }

        if (isMountSkeleton && !string.IsNullOrEmpty(folderAssetPath) && Directory.Exists(folderAssetPath + "horse"))
        {
            TryAddTopLevelClips(anim, folderAssetPath + "horse", validPaths, clips);
        }
        TryAddTopLevelClips(anim, folderAssetPath, validPaths, clips);

        for (int i = 0; i < kChildClipAliases.Length; i++)
        {
            string srcName = kChildClipAliases[i].Key;
            string aliasName = kChildClipAliases[i].Value;
            if (anim[aliasName] == null && anim[srcName] != null && anim[srcName].clip != null && ClipMatchesSkeleton(anim[srcName].clip, validPaths))
            {
                AnimationClip aliasClip = EnsureChildClipAliasAsset(folderAssetPath, anim[srcName].clip, aliasName);
                if (aliasClip != null)
                {
                    anim.AddClip(aliasClip, aliasName);
                    clips.Add(aliasClip);
                }
            }
        }
        EnsureLoopClipWrapModes(clips);

        AnimationClip def = SelectDefaultClip(clips);
        if (def != null)
        {
            anim.clip = def;
        }
        anim.playAutomatically = true;
        anim.cullingType = AnimationCullingType.BasedOnRenderers;
    }

    private static AnimationClip EnsureChildClipAliasAsset(string folderAssetPath, AnimationClip srcClip, string aliasName)
    {
        if (string.IsNullOrEmpty(folderAssetPath) || srcClip == null) return srcClip;
        string aliasDir = folderAssetPath + "/anim_aliases";
        if (!Directory.Exists(aliasDir))
        {
            Directory.CreateDirectory(aliasDir);
        }
        string aliasPath = aliasDir + "/" + aliasName + ".anim";
        AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(aliasPath);
        if (existing != null)
        {
            return existing;
        }
        AnimationClip clone = UnityEngine.Object.Instantiate(srcClip);
        clone.name = aliasName;
        if (kLoopClipNames.Contains(aliasName))
        {
            clone.wrapMode = WrapMode.Loop;
        }
        AssetDatabase.CreateAsset(clone, aliasPath);
        return AssetDatabase.LoadAssetAtPath<AnimationClip>(aliasPath);
    }

    private static void ConfigureRandomAnimSets(
        CharacterModelController ctrl,
        SchemaData sd,
        Dictionary<string, string> cmcEntry,
        List<AnimationClip> tagClips)
    {
        List<CharacterModelController.RandomAnimSet> sets = new List<CharacterModelController.RandomAnimSet>();

        List<AnimationClip> attackClips = new List<AnimationClip>();
        string randKey = GetField(cmcEntry, "randomAnimSets", null);
        List<Dictionary<string, string>> schemaSetList;
        if (!string.IsNullOrEmpty(randKey) && sd.randomSets.TryGetValue(randKey, out schemaSetList))
        {
            for (int i = 0; i < schemaSetList.Count; i++)
            {
                string setName = GetField(schemaSetList[i], "name", "attack");
                string clipsGroup = GetField(schemaSetList[i], "clips", null);
                List<AnimationClip> resolved = new List<AnimationClip>();
                List<string> schemaClipPaths;
                if (!string.IsNullOrEmpty(clipsGroup) && sd.animClipGroups.TryGetValue(clipsGroup, out schemaClipPaths))
                {
                    for (int c = 0; c < schemaClipPaths.Count; c++)
                    {
                        string clipBase = Path.GetFileNameWithoutExtension(schemaClipPaths[c]);
                        AnimationClip tc = FindClipByNames(tagClips, new string[] { clipBase });
                        if (tc != null) resolved.Add(tc);
                    }
                }
                if (resolved.Count > 0 && string.Equals(setName, "attack", StringComparison.OrdinalIgnoreCase))
                {
                    attackClips = resolved;
                }
            }
        }

        if (attackClips.Count == 0)
        {
            CollectMatchingClips(tagClips, "attack", attackClips, "attackidle");
        }
        if (attackClips.Count == 0)
        {
            CollectMatchingClips(tagClips, "rangedattack", attackClips, "rangedattackidle");
        }
        if (attackClips.Count == 0)
        {
            CollectMatchingClips(tagClips, "blastoff", attackClips, null);
        }
        if (attackClips.Count > 0)
        {
            CharacterModelController.RandomAnimSet atkSet = new CharacterModelController.RandomAnimSet();
            atkSet.name = "attack";
            atkSet.clips = attackClips.ToArray();
            atkSet.onlyRandomizeOnce = false;
            sets.Add(atkSet);
        }

        List<AnimationClip> rangedClips = new List<AnimationClip>();
        CollectMatchingClips(tagClips, "rangedattack", rangedClips, "rangedattackidle");
        if (rangedClips.Count > 0)
        {
            CharacterModelController.RandomAnimSet rngSet = new CharacterModelController.RandomAnimSet();
            rngSet.name = "rangedattack";
            rngSet.clips = rangedClips.ToArray();
            rngSet.onlyRandomizeOnce = false;
            sets.Add(rngSet);
        }

        ctrl.randomAnimSets = sets.ToArray();
    }

    private static void ConfigureImpactEffects(
        CharacterModelController ctrl,
        SchemaData sd,
        Dictionary<string, string> cmcEntry,
        CharacterModelController templateCtrl)
    {
        ctrl.arrowImpactEffect = LoadFxOrFallback(sd, GetField(cmcEntry, "arrowImpactEffect", null),
            templateCtrl != null ? templateCtrl.arrowImpactEffect : null);
        ctrl.bladeImpactEffect = LoadFxOrFallback(sd, GetField(cmcEntry, "bladeImpactEffect", null),
            templateCtrl != null ? templateCtrl.bladeImpactEffect : null);
        ctrl.bladeCriticalImpactEffect = LoadFxOrFallback(sd, GetField(cmcEntry, "bladeCriticalImpactEffect", null),
            templateCtrl != null ? templateCtrl.bladeCriticalImpactEffect : null);
        ctrl.bluntImpactEffect = LoadFxOrFallback(sd, GetField(cmcEntry, "bluntImpactEffect", null),
            ctrl.bladeImpactEffect);
        ctrl.bluntCriticalImpactEffect = LoadFxOrFallback(sd, GetField(cmcEntry, "bluntCriticalImpactEffect", null),
            ctrl.bladeCriticalImpactEffect);
    }

    private static GameObject LoadFxOrFallback(SchemaData sd, string schemaPath, GameObject fallback)
    {
        string assetPath = ResolveResourceAssetPath(sd, schemaPath);
        if (!string.IsNullOrEmpty(assetPath))
        {
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (go != null) return go;
        }
        return fallback;
    }

    private static List<AnimationClip> CollectTagClips(string folderAssetPath)
    {
        Dictionary<string, AnimationClip> byName = new Dictionary<string, AnimationClip>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(folderAssetPath))
        {
            return new List<AnimationClip>();
        }
        string[] files = Directory.GetFiles(folderAssetPath, "*.anim", SearchOption.AllDirectories);
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < files.Length; i++)
        {
            string norm = files[i].Replace('\\', '/');
            if (norm.IndexOf("_anim_tags/", StringComparison.OrdinalIgnoreCase) < 0) continue;
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(norm);
            if (clip != null)
            {
                string key = Path.GetFileNameWithoutExtension(norm);
                if (!byName.ContainsKey(key))
                {
                    byName[key] = clip;
                }
            }
        }
        return new List<AnimationClip>(byName.Values);
    }

    private static void ConfigureAnimation(
        Animation anim,
        List<AnimationClip> clips,
        AnimationClip defaultClip,
        bool playAutomatically,
        AnimationCullingType cullingType)
    {
        if (anim == null) return;
        for (int i = 0; i < clips.Count; i++)
        {
            if (clips[i] != null)
            {
                anim.AddClip(clips[i], clips[i].name);
            }
        }
        if (defaultClip != null)
        {
            anim.clip = defaultClip;
        }
        anim.playAutomatically = playAutomatically;
        anim.cullingType = cullingType;
    }

    private static AnimationClip FindClipByNames(List<AnimationClip> clips, string[] names)
    {
        for (int n = 0; n < names.Length; n++)
        {
            for (int i = 0; i < clips.Count; i++)
            {
                if (clips[i] != null && string.Equals(clips[i].name, names[n], StringComparison.OrdinalIgnoreCase))
                {
                    return clips[i];
                }
            }
        }
        return null;
    }

    private static void CollectMatchingClips(List<AnimationClip> clips, string prefix, List<AnimationClip> results, string exactExclude)
    {
        for (int i = 0; i < clips.Count; i++)
        {
            AnimationClip c = clips[i];
            if (c != null && c.name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(exactExclude) && string.Equals(c.name, exactExclude, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                results.Add(c);
            }
        }
    }

    private static AutoPaperdoll.LabeledJoint[] BuildJointsFromSchema(
        SchemaData sd,
        string paperdollKey,
        Transform modelRoot,
        Transform rootTransform,
        string folder)
    {
        List<KeyValuePair<string, Dictionary<string, string>>> schemaJoints;
        if (!string.IsNullOrEmpty(paperdollKey) && sd.joints.TryGetValue(paperdollKey, out schemaJoints) && schemaJoints.Count > 0)
        {
            Transform[] allTransforms = modelRoot.GetComponentsInChildren<Transform>(true);
            List<AutoPaperdoll.LabeledJoint> result = new List<AutoPaperdoll.LabeledJoint>();
            HashSet<string> usedLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < schemaJoints.Count; i++)
            {
                Dictionary<string, string> kv = schemaJoints[i].Value;
                string rawLabel = GetField(kv, "label", schemaJoints[i].Key);
                if (rawLabel.StartsWith("JointLabel.", StringComparison.OrdinalIgnoreCase))
                {
                    rawLabel = rawLabel.Substring("JointLabel.".Length);
                }
                if (string.IsNullOrEmpty(rawLabel) || usedLabels.Contains(rawLabel)) continue;
                usedLabels.Add(rawLabel);

                string jointName = GetField(kv, "jointName", null);
                Transform joint = ResolveJointTransform(allTransforms, jointName, rootTransform, modelRoot);

                string prefabSchemaPath = GetField(kv, "prefab", null);
                GameObject attachPrefab = null;
                if (!string.IsNullOrEmpty(prefabSchemaPath))
                {
                    string assetPath = ResolveResourceAssetPath(sd, prefabSchemaPath);
                    if (!string.IsNullOrEmpty(assetPath))
                    {
                        attachPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                    }
                    if (attachPrefab == null)
                    {
                        Debug.LogWarning("[SVZ2PrefabBuilder] Missing autoAttachPrefab for " + paperdollKey + "." + rawLabel + ": " + prefabSchemaPath);
                    }
                }

                Vector3 pos = new Vector3(
                    ParseFloat(GetField(kv, "offsetPositionX", null), 0f),
                    ParseFloat(GetField(kv, "offsetPositionY", null), 0f),
                    ParseFloat(GetField(kv, "offsetPositionZ", null), 0f));
                Vector3 rot = new Vector3(
                    ParseFloat(GetField(kv, "offsetRotationX", null), 0f),
                    ParseFloat(GetField(kv, "offsetRotationY", null), 0f),
                    ParseFloat(GetField(kv, "offsetRotationZ", null), 0f));
                Vector3 scl = new Vector3(
                    ParseFloat(GetField(kv, "offsetScaleX", null), 0f),
                    ParseFloat(GetField(kv, "offsetScaleY", null), 0f),
                    ParseFloat(GetField(kv, "offsetScaleZ", null), 0f));

                result.Add(MakeJoint(rawLabel, joint, attachPrefab, pos, rot, scl));
            }
            return result.ToArray();
        }

        return BuildFallbackJoints(modelRoot, rootTransform);
    }

    private static Transform ResolveJointTransform(Transform[] all, string jointName, Transform fallbackRoot, Transform modelRoot)
    {
        if (string.IsNullOrEmpty(jointName))
        {
            return fallbackRoot;
        }
        for (int i = 0; i < all.Length; i++)
        {
            if (string.Equals(all[i].name, jointName, StringComparison.OrdinalIgnoreCase))
            {
                return all[i];
            }
        }
        int colon = jointName.IndexOf(':');
        if (colon >= 0 && colon + 1 < jointName.Length)
        {
            string afterColon = jointName.Substring(colon + 1);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name.EndsWith(afterColon, StringComparison.OrdinalIgnoreCase))
                {
                    return all[i];
                }
            }
            int charIdx = afterColon.IndexOf("charName_", StringComparison.OrdinalIgnoreCase);
            if (charIdx >= 0)
            {
                string suffix = afterColon.Substring(charIdx + "charName_".Length);
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i].name.EndsWith("_" + suffix, StringComparison.OrdinalIgnoreCase) ||
                        all[i].name.IndexOf(suffix, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return all[i];
                    }
                }
            }
        }
        return modelRoot != null ? modelRoot : fallbackRoot;
    }

    private static AutoPaperdoll.LabeledJoint[] BuildFallbackJoints(Transform modelRoot, Transform fallbackRoot)
    {
        Transform rightWrist = FindBone(modelRoot, new string[] { "_rt_wrist_jnt", "_rt_hand_jnt", "rt_weapon", "spine_mid_jnt" });
        Transform leftWrist = FindBone(modelRoot, new string[] { "_lf_wrist_jnt", "_lf_hand_jnt", "lf_weapon", "spine_mid_jnt" });
        Transform midSpine = FindBone(modelRoot, new string[] { "_spine_mid_jnt", "_spine_hi_jnt", "_spine_low_jnt", "skeleton_root_joint" });
        Transform headBone = FindBone(modelRoot, new string[] { "_head_b_jnt", "_head_a_jnt", "_neck_jnt", "_spine_end_jnt", "_spine_hi_jnt" });

        if (rightWrist == null) rightWrist = fallbackRoot;
        if (leftWrist == null) leftWrist = fallbackRoot;
        if (midSpine == null) midSpine = modelRoot;
        if (headBone == null) headBone = modelRoot;

        List<AutoPaperdoll.LabeledJoint> list = new List<AutoPaperdoll.LabeledJoint>();
        list.Add(MakeJoint("melee_weapon", rightWrist, null, Vector3.zero, Vector3.zero, Vector3.zero));
        list.Add(MakeJoint("ranged_weapon", leftWrist, null, Vector3.zero, Vector3.zero, Vector3.zero));
        list.Add(MakeJoint("projectile_spawn", rightWrist, null, Vector3.zero, Vector3.zero, Vector3.zero));
        list.Add(MakeJoint("arrow", rightWrist, null, Vector3.zero, Vector3.zero, Vector3.zero));
        list.Add(MakeJoint("impact_target", midSpine, null, Vector3.zero, Vector3.zero, Vector3.zero));
        list.Add(MakeJoint("buff_icon", headBone, null, Vector3.zero, Vector3.zero, Vector3.zero));
        list.Add(MakeJoint("head_effect", headBone, null, Vector3.zero, Vector3.zero, Vector3.zero));
        list.Add(MakeJoint("body_effect", midSpine, null, Vector3.zero, Vector3.zero, Vector3.zero));
        return list.ToArray();
    }

    private static AutoPaperdoll.LabeledJoint MakeJoint(
        string label,
        Transform joint,
        GameObject attachPrefab,
        Vector3 posOffset,
        Vector3 rotOffset,
        Vector3 scaleOffset)
    {
        AutoPaperdoll.LabeledJoint lj = new AutoPaperdoll.LabeledJoint();
        lj.label = label;
        lj.joint = joint;
        lj.autoAttachPrefab = attachPrefab;
        lj.transformOffset = new AutoPaperdoll.Offset();
        lj.transformOffset.position = posOffset;
        lj.transformOffset.rotation = rotOffset;
        lj.transformOffset.scale = scaleOffset;
        return lj;
    }

    private static Transform FindBone(Transform root, string[] suffixes)
    {
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int s = 0; s < suffixes.Length; s++)
        {
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name.IndexOf(suffixes[s], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return all[i];
                }
            }
        }
        return null;
    }

    [MenuItem("Tools/SVZ2/Render Zombie HUD Icons")]
    public static void RenderZombieIcons()
    {
        string outDir = "Temp/svz2_icon_renders";
        if (!Directory.Exists(outDir))
        {
            Directory.CreateDirectory(outDir);
        }

        string[] targets = new string[]
        {
            "PFAkaname",
            "PFHarionago",
            "PFOnmoraki",
            "PFNioguardian"
        };

        GameObject camGo = new GameObject("_TempIconCam");
        GameObject lightGo = new GameObject("_TempIconLight");
        RenderTexture rt = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32);
        RenderTexture prev = RenderTexture.active;
        try
        {
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.cullingMask = 1 << 31;
            cam.orthographic = false;
            cam.fieldOfView = 28f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 2000f;
            cam.targetTexture = rt;

            Light lgt = lightGo.AddComponent<Light>();
            lgt.type = LightType.Directional;
            lgt.intensity = 0.85f;
            lgt.color = Color.white;
            lightGo.transform.rotation = Quaternion.Euler(25f, 200f, 0f);

            for (int i = 0; i < targets.Length; i++)
            {
                string pfPath = "Assets/Resources/characters/" + targets[i] + ".prefab";
                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(pfPath);
                if (asset == null)
                {
                    Debug.LogWarning("[SVZ2PrefabBuilder] RenderZombieIcons missing: " + pfPath);
                    continue;
                }
                GameObject inst = (GameObject)UnityEngine.Object.Instantiate(asset);
                try
                {
                    inst.transform.position = Vector3.zero;
                    inst.transform.rotation = Quaternion.identity;

                    Transform[] trs = inst.GetComponentsInChildren<Transform>(true);
                    for (int t = 0; t < trs.Length; t++)
                    {
                        trs[t].gameObject.layer = 31;
                    }

                    Animation[] anims = inst.GetComponentsInChildren<Animation>(true);
                    for (int a = 0; a < anims.Length; a++)
                    {
                        if (anims[a] != null && anims[a]["idle"] != null)
                        {
                            anims[a].Play("idle");
                            anims[a].Sample();
                        }
                    }

                    Bounds b = new Bounds(Vector3.up * 60f, Vector3.one * 80f);
                    bool hasBounds = false;
                    for (int t = 0; t < trs.Length; t++)
                    {
                        if (trs[t] == inst.transform) continue;
                        if (!hasBounds) { b = new Bounds(trs[t].position, Vector3.zero); hasBounds = true; }
                        else { b.Encapsulate(trs[t].position); }
                    }

                    AutoPaperdoll doll = inst.GetComponent<AutoPaperdoll>();
                    float yRatio = (targets[i] == "PFNioguardian") ? 1.12f : 0.86f;
                    Vector3 headPos = new Vector3(b.center.x, b.min.y + b.size.y * yRatio, b.center.z);
                    if (doll != null && doll.joints != null && targets[i] != "PFNioguardian")
                    {
                        Transform hj = null;
                        for (int j = 0; j < doll.joints.Length; j++)
                        {
                            if (doll.joints[j] == null || doll.joints[j].joint == null) continue;
                            if (string.Equals(doll.joints[j].label, "head_effect", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(doll.joints[j].label, "buff_icon", StringComparison.OrdinalIgnoreCase))
                            {
                                hj = doll.joints[j].joint;
                                break;
                            }
                        }
                        if (hj != null && hj != inst.transform)
                        {
                            headPos = Vector3.Lerp(new Vector3(b.center.x, b.min.y + b.size.y * 0.78f, b.center.z), hj.position, 0.45f);
                        }
                    }

                    float extent = Mathf.Max(100f, b.size.y);
                    float dist = extent * 2.85f;
                    camGo.transform.position = headPos + new Vector3(dist * 0.50f, extent * 0.12f, dist * 0.86f);
                    camGo.transform.LookAt(headPos);

                    RenderTexture.active = rt;
                    cam.Render();
                    Texture2D tex = new Texture2D(256, 256, TextureFormat.ARGB32, false);
                    tex.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
                    tex.Apply();
                    byte[] png = tex.EncodeToPNG();
                    File.WriteAllBytes(Path.Combine(outDir, targets[i] + ".png"), png);
                    UnityEngine.Object.DestroyImmediate(tex);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(inst);
                }
            }
            Debug.Log("[SVZ2PrefabBuilder] Rendered zombie HUD portraits to " + outDir);
        }
        finally
        {
            RenderTexture.active = prev;
            camGo.GetComponent<Camera>().targetTexture = null;
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(camGo);
            UnityEngine.Object.DestroyImmediate(lightGo);
        }
    }

    [MenuItem("Tools/SVZ2/Audit SVZ2 Facing and Attacks")]
    public static void AuditSVZ2Models()
    {
        string outDir = "Temp/svz2_audit";
        if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

        string[] targets = new string[]
        {
            "PFOni", "PFUndeadshogun", "PFShutendoji", "PFShutendojiBlue",
            "PFAkaname", "PFAmanojaku", "PFHarionago", "PFOnmoraki", "PFOnmorakiAlt",
            "PFNioguardian", "PFCrawlingBody", "PFCrawlingLegs",
            "PFRockyTMan", "PFBlindSwordsman", "PFRocketFarmer",
            "PFHorseMaster", "PFWarriorHorse", "PFHorseArcher",
            "PFHeroKunoichi", "PFHeroDaimyo", "PFHeroOnmyoji", "PFHeroRonin", "PFHeroBalanced"
        };

        int cellW = 160;
        int cellH = 160;
        int cols = 6;
        int rows = ((targets.Length * 2) + cols - 1) / cols;
        Texture2D sheet = new Texture2D(cols * cellW, rows * cellH, TextureFormat.ARGB32, false);
        Color[] bgFill = new Color[sheet.width * sheet.height];
        for (int p = 0; p < bgFill.Length; p++) bgFill[p] = new Color(0.14f, 0.16f, 0.22f, 1f);
        sheet.SetPixels(bgFill);

        GameObject camGo = new GameObject("_TempAuditCam");
        GameObject lightGo = new GameObject("_TempAuditLight");
        RenderTexture rt = new RenderTexture(cellW, cellH, 24, RenderTextureFormat.ARGB32);
        RenderTexture prev = RenderTexture.active;
        try
        {
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.18f, 0.24f, 1f);
            cam.cullingMask = 1 << 31;
            cam.orthographic = false;
            cam.fieldOfView = 26f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 4000f;
            cam.targetTexture = rt;

            Light lgt = lightGo.AddComponent<Light>();
            lgt.type = LightType.Directional;
            lgt.intensity = 1.0f;
            lgt.color = Color.white;
            lightGo.transform.rotation = Quaternion.Euler(25f, 250f, 0f);

            int tileIdx = 0;
            for (int i = 0; i < targets.Length; i++)
            {
                string pfPath = "Assets/Resources/characters/" + targets[i] + ".prefab";
                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(pfPath);
                if (asset == null)
                {
                    Debug.LogWarning("[SVZ2Audit] Missing prefab: " + pfPath);
                    tileIdx += 2;
                    continue;
                }

                GameObject inst = (GameObject)UnityEngine.Object.Instantiate(asset);
                try
                {
                    inst.transform.position = Vector3.zero;
                    FacingType nf = CharacterModelController.GetModelNativeFacing(targets[i]);
                    // Simulate SetFacing(FacingType.Left): eulerY=180, scale.x = (Left == nf ? absX : -absX)
                    Vector3 baseScale = inst.transform.localScale;
                    float absX = Mathf.Max(0.001f, Mathf.Abs(baseScale.x));

                    Transform[] trs = inst.GetComponentsInChildren<Transform>(true);
                    for (int t = 0; t < trs.Length; t++) trs[t].gameObject.layer = 31;

                    // 1. Measure raw local facing BEFORE SetFacing rotation
                    inst.transform.rotation = Quaternion.identity;
                    inst.transform.localScale = new Vector3(absX, Mathf.Abs(baseScale.y), Mathf.Abs(baseScale.z));

                    Animation childAnim = null;
                    Animation[] anims = inst.GetComponentsInChildren<Animation>(true);
                    for (int a = 0; a < anims.Length; a++)
                    {
                        if (anims[a] != null && anims[a].gameObject != inst) { childAnim = anims[a]; break; }
                    }
                    if (childAnim == null && anims.Length > 0) childAnim = anims[0];

                    // Sample idle at identity to detect local forward axis (eye/jaw vs head/torso)
                    if (childAnim != null && childAnim["idle"] != null)
                    {
                        childAnim["idle"].clip.SampleAnimation(childAnim.gameObject, 0f);
                    }
                    Vector3 eyeAvg = Vector3.zero; int eyeCnt = 0;
                    Vector3 headAvg = Vector3.zero; int headCnt = 0;
                    Transform propJnt = null;
                    for (int t = 0; t < trs.Length; t++)
                    {
                        string ln = trs[t].name.ToLowerInvariant();
                        if (ln.Contains("eye") || ln.Contains("jaw") || ln.Contains("nose") || ln.Contains("muzzle"))
                        {
                            eyeAvg += trs[t].position; eyeCnt++;
                        }
                        if (ln.Contains("head_a") || ln.Contains("head_jnt") || ln.Contains("neck"))
                        {
                            headAvg += trs[t].position; headCnt++;
                        }
                        if (ln.Contains("prop") || ln.Contains("wrist") || ln.Contains("hand"))
                        {
                            propJnt = trs[t];
                        }
                    }
                    Vector3 fwdVec = (eyeCnt > 0 && headCnt > 0) ? ((eyeAvg / eyeCnt) - (headAvg / headCnt)) : Vector3.zero;
                    Vector3 propIdlePos = (propJnt != null) ? propJnt.position : Vector3.zero;

                    // Now apply SetFacing(FacingType.Left) as runtime does!
                    inst.transform.eulerAngles = new Vector3(0f, 180f, 0f);
                    inst.transform.localScale = new Vector3((nf == FacingType.Left) ? absX : -absX, Mathf.Abs(baseScale.y), Mathf.Abs(baseScale.z));

                    // Sample walk at 0.25
                    string walkName = (childAnim != null && childAnim["walk"] != null) ? "walk" : ((childAnim != null && childAnim["horsewalk"] != null) ? "horsewalk" : "idle");
                    if (childAnim != null && childAnim[walkName] != null)
                    {
                        AnimationClip wc = childAnim[walkName].clip;
                        wc.SampleAnimation(childAnim.gameObject, wc.length * 0.25f);
                    }

                    Bounds b = new Bounds(Vector3.up * 60f, Vector3.one * 80f);
                    Renderer[] rends = inst.GetComponentsInChildren<Renderer>(true);
                    bool hasBounds = false;
                    for (int r = 0; r < rends.Length; r++)
                    {
                        if (!hasBounds) { b = rends[r].bounds; hasBounds = true; }
                        else b.Encapsulate(rends[r].bounds);
                    }
                    float dist = Mathf.Max(140f, Mathf.Max(b.size.y, b.size.z) * 2.5f);
                    camGo.transform.position = b.center + new Vector3(dist, b.size.y * 0.1f, 0f);
                    camGo.transform.LookAt(b.center);

                    RenderTexture.active = rt;
                    cam.Render();
                    Texture2D tmp = new Texture2D(cellW, cellH, TextureFormat.ARGB32, false);
                    tmp.ReadPixels(new Rect(0, 0, cellW, cellH), 0, 0);
                    tmp.Apply();
                    int cx = (tileIdx % cols) * cellW;
                    int cy = (rows - 1 - (tileIdx / cols)) * cellH;
                    sheet.SetPixels(cx, cy, cellW, cellH, tmp.GetPixels());

                    // 2. Sample attack clip at 0.45
                    string atkName = null;
                    string[] atkCandidates = new string[] { "attack01", "attack", "rangedattack01", "rangedattack", "cast", "fly", "horseattack01" };
                    if (childAnim != null)
                    {
                        for (int k = 0; k < atkCandidates.Length; k++)
                        {
                            if (childAnim[atkCandidates[k]] != null) { atkName = atkCandidates[k]; break; }
                        }
                    }
                    float propDelta = -1f;
                    if (childAnim != null && atkName != null)
                    {
                        AnimationClip ac = childAnim[atkName].clip;
                        ac.SampleAnimation(childAnim.gameObject, ac.length * 0.45f);
                        if (propJnt != null) propDelta = Vector3.Distance(propJnt.position, propIdlePos);
                    }

                    cam.Render();
                    tmp.ReadPixels(new Rect(0, 0, cellW, cellH), 0, 0);
                    tmp.Apply();
                    int tile2 = tileIdx + 1;
                    int cx2 = (tile2 % cols) * cellW;
                    int cy2 = (rows - 1 - (tile2 / cols)) * cellH;
                    sheet.SetPixels(cx2, cy2, cellW, cellH, tmp.GetPixels());
                    UnityEngine.Object.DestroyImmediate(tmp);

                    Debug.Log(string.Format("[SVZ2Audit] {0} | nativeFacing={1} | scale={2:F1} | rawFwd=({3:F2},{4:F2},{5:F2}) | walk={6} | atk={7} | propDelta={8:F2}",
                        targets[i], nf, absX, fwdVec.x, fwdVec.y, fwdVec.z, walkName, atkName ?? "NONE", propDelta));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(inst);
                }
                tileIdx += 2;
            }
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(outDir, "svz2_audit_sheet.png"), sheet.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(sheet);
            Debug.Log("[SVZ2Audit] Wrote Temp/svz2_audit/svz2_audit_sheet.png");
        }
        finally
        {
            RenderTexture.active = prev;
            camGo.GetComponent<Camera>().targetTexture = null;
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(camGo);
            UnityEngine.Object.DestroyImmediate(lightGo);
        }
    }

    [MenuItem("Tools/SVZ2/Audit Shuten Attacks and Ranged")]
    public static void AuditShutenAndRanged()
    {
        string outDir = "Temp/svz2_audit";
        if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

        int cellW = 240;
        int cellH = 240;
        int cols = 4;
        int rows = 5;
        Texture2D sheet = new Texture2D(cols * cellW, rows * cellH, TextureFormat.ARGB32, false);
        Color[] bgFill = new Color[sheet.width * sheet.height];
        for (int p = 0; p < bgFill.Length; p++) bgFill[p] = new Color(0.14f, 0.16f, 0.22f, 1f);
        sheet.SetPixels(bgFill);

        GameObject camGo = new GameObject("_TempShutenCam");
        GameObject lightGo = new GameObject("_TempShutenLight");
        RenderTexture rt = new RenderTexture(cellW, cellH, 24, RenderTextureFormat.ARGB32);
        RenderTexture prev = RenderTexture.active;
        try
        {
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.18f, 0.24f, 1f);
            cam.cullingMask = 1 << 31;
            cam.orthographic = false;
            cam.fieldOfView = 28f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 4000f;
            cam.targetTexture = rt;

            Light lgt = lightGo.AddComponent<Light>();
            lgt.type = LightType.Directional;
            lgt.intensity = 1.0f;
            lgt.color = Color.white;
            lightGo.transform.rotation = Quaternion.Euler(25f, 250f, 0f);

            int tile = 0;
            Texture2D tmp = new Texture2D(cellW, cellH, TextureFormat.ARGB32, false);

            // Part A: PFShutendoji with weapon across 12 poses (rows 0..2)
            GameObject shutenAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/characters/PFShutendoji.prefab");
            GameObject clubAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/props/weapons/shutendojiclub.prefab");
            if (shutenAsset != null)
            {
                string[] clips = new string[] { "idle", "walk", "attack01", "attack01", "attack01", "attack01", "attack02", "attack02", "attack03", "attack03", "attack03", "victory" };
                float[] ts = new float[] { 0f, 0.25f, 0.20f, 0.36f, 0.55f, 0.80f, 0.30f, 0.65f, 0.25f, 0.50f, 0.75f, 0.50f };
                for (int k = 0; k < clips.Length; k++)
                {
                    GameObject inst = (GameObject)UnityEngine.Object.Instantiate(shutenAsset);
                    try
                    {
                        inst.transform.position = Vector3.zero;
                        inst.transform.eulerAngles = new Vector3(0f, 180f, 0f);
                        // Apply nativeFacing=Left scale
                        Vector3 bs = inst.transform.localScale;
                        inst.transform.localScale = new Vector3(Mathf.Abs(bs.x), Mathf.Abs(bs.y), Mathf.Abs(bs.z));

                        AutoPaperdoll doll = inst.GetComponent<AutoPaperdoll>();
                        if (doll != null && clubAsset != null && doll.joints != null)
                        {
                            for (int j = 0; j < doll.joints.Length; j++)
                            {
                                if (doll.joints[j] != null && string.Equals(doll.joints[j].label, "melee_weapon", StringComparison.OrdinalIgnoreCase) && doll.joints[j].joint != null)
                                {
                                    GameObject club = (GameObject)UnityEngine.Object.Instantiate(clubAsset, doll.joints[j].joint, false);
                                    club.transform.localPosition = doll.joints[j].transformOffset.position;
                                    club.transform.localEulerAngles = doll.joints[j].transformOffset.rotation;
                                    if (k == 0)
                                        Debug.Log("[SVZ2Audit] Attached shutendojiclub to " + club.transform.parent.name + " localPos=" + club.transform.localPosition + " lossyScale=" + club.transform.lossyScale);
                                    break;
                                }
                            }
                        }
                        Transform[] trs = inst.GetComponentsInChildren<Transform>(true);
                        Transform armBone = null;
                        Transform propBone = null;
                        for (int t = 0; t < trs.Length; t++)
                        {
                            trs[t].gameObject.layer = 31;
                            if (trs[t].name.Contains("rt_upArm")) armBone = trs[t];
                            if (trs[t].name.Contains("prop_jnt")) propBone = trs[t];
                        }

                        Animation childAnim = null;
                        Animation[] anims = inst.GetComponentsInChildren<Animation>(true);
                        for (int a = 0; a < anims.Length; a++) { if (anims[a] != null && anims[a].gameObject != inst) { childAnim = anims[a]; break; } }

                        if (childAnim != null && childAnim[clips[k]] != null)
                        {
                            AnimationClip c = childAnim[clips[k]].clip;
                            c.SampleAnimation(childAnim.gameObject, c.length * ts[k]);
                            Debug.Log(string.Format("[SVZ2Audit] Shuten[{0}] {1}@{2:F2}/{3:F2}s arm={4} prop={5}",
                                k, clips[k], c.length * ts[k], c.length,
                                armBone != null ? armBone.localEulerAngles.ToString("F1") : "null",
                                propBone != null ? propBone.position.ToString("F1") : "null"));
                        }
                        else
                        {
                            Debug.LogWarning("[SVZ2Audit] Missing clip on Shuten childAnim: " + clips[k]);
                        }
                        camGo.transform.position = new Vector3(1150f, 180f, 0f);
                        camGo.transform.LookAt(new Vector3(0f, 145f, 0f));
                        RenderTexture.active = rt;
                        cam.Render();
                        tmp.ReadPixels(new Rect(0, 0, cellW, cellH), 0, 0);
                        tmp.Apply();
                        int cx = (tile % cols) * cellW;
                        int cy = (rows - 1 - (tile / cols)) * cellH;
                        sheet.SetPixels(cx, cy, cellW, cellH, tmp.GetPixels());
                        tile++;
                    }
                    finally { UnityEngine.Object.DestroyImmediate(inst); }
                }
            }

            // Part B: 8 extra checks (rows 3..4)
            string[] ePrefabs = new string[] {
                "PFRocketFarmer", "PFRocketFarmer",
                "PFOnmoraki", "PFOnmoraki",
                "PFAmanojaku", "PFAmanojaku",
                "PFNioguardian", "PFHorseMaster"
            };
            string[] eClips = new string[] {
                "walk", "rangedattack01",
                "walk", "rangedattack01",
                "attack02", "cast",
                "attack02", "walk"
            };
            bool[] eRight = new bool[] { true, true, false, false, false, false, false, true };
            for (int i = 0; i < ePrefabs.Length; i++)
            {
                string pfp = "Assets/Resources/characters/" + ePrefabs[i] + ".prefab";
                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(pfp);
                if (asset == null) { tile++; continue; }
                GameObject inst = (GameObject)UnityEngine.Object.Instantiate(asset);
                try
                {
                    inst.transform.position = Vector3.zero;
                    FacingType nf = CharacterModelController.GetModelNativeFacing(ePrefabs[i]);
                    FacingType dir = eRight[i] ? FacingType.Right : FacingType.Left;
                    Vector3 bs = inst.transform.localScale;
                    inst.transform.eulerAngles = new Vector3(0f, (dir == FacingType.Left) ? 180f : 0f, 0f);
                    inst.transform.localScale = new Vector3((dir == nf) ? Mathf.Abs(bs.x) : -Mathf.Abs(bs.x), Mathf.Abs(bs.y), Mathf.Abs(bs.z));

                    if (ePrefabs[i] == "PFRocketFarmer")
                    {
                        GameObject rl = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/props/weapons/rocketlauncher.prefab");
                        AutoPaperdoll d = inst.GetComponent<AutoPaperdoll>();
                        if (d != null && rl != null && d.HasJoint("ranged_weapon")) d.InstantiateObjectOnJoint(rl, "ranged_weapon");
                    }

                    Transform[] trs = inst.GetComponentsInChildren<Transform>(true);
                    for (int t = 0; t < trs.Length; t++) trs[t].gameObject.layer = 31;

                    Animation[] anims = inst.GetComponentsInChildren<Animation>(true);
                    for (int a = 0; a < anims.Length; a++)
                    {
                        if (anims[a] != null && anims[a][eClips[i]] != null)
                        {
                            AnimationClip ac = anims[a][eClips[i]].clip;
                            ac.SampleAnimation(anims[a].gameObject, ac.length * 0.45f);
                        }
                    }

                    Renderer[] rends = inst.GetComponentsInChildren<Renderer>(true);
                    Bounds b = new Bounds(Vector3.up * 60f, Vector3.one * 80f);
                    bool hb = false;
                    for (int r = 0; r < rends.Length; r++)
                    {
                        if (ePrefabs[i] == "PFHorseMaster") Debug.Log("[SVZ2Audit] PFHorseMaster SMR[" + r + "] " + rends[r].name + " bounds=" + rends[r].bounds + " mat=" + (rends[r].sharedMaterial != null ? rends[r].sharedMaterial.name : "NULL"));
                        if (!hb) { b = rends[r].bounds; hb = true; }
                        else b.Encapsulate(rends[r].bounds);
                    }
                    float dist = Mathf.Max(220f, Mathf.Max(b.size.y, b.size.z) * 2.7f);
                    camGo.transform.position = b.center + new Vector3(dist, b.size.y * 0.12f, 0f);
                    camGo.transform.LookAt(b.center);
                    RenderTexture.active = rt;
                    cam.Render();
                    tmp.ReadPixels(new Rect(0, 0, cellW, cellH), 0, 0);
                    tmp.Apply();
                    int cx = (tile % cols) * cellW;
                    int cy = (rows - 1 - (tile / cols)) * cellH;
                    sheet.SetPixels(cx, cy, cellW, cellH, tmp.GetPixels());
                    tile++;
                }
                finally { UnityEngine.Object.DestroyImmediate(inst); }
            }

            UnityEngine.Object.DestroyImmediate(tmp);
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(outDir, "shuten_attacks_and_ranged.png"), sheet.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(sheet);
            Debug.Log("[SVZ2Audit] Wrote Temp/svz2_audit/shuten_attacks_and_ranged.png");
        }
        finally
        {
            RenderTexture.active = prev;
            camGo.GetComponent<Camera>().targetTexture = null;
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(camGo);
            UnityEngine.Object.DestroyImmediate(lightGo);
        }
    }

    [MenuItem("Tools/SVZ2/Audit Skinning Deformation")]
    public static void AuditSkinningDeformation()
    {
        string[] prefabs = new string[] {
            "PFShutendoji", "PFShutendojiBlue", "PFAkaname", "PFAmanojaku",
            "PFHarionago", "PFOnmoraki", "PFOnmorakiAlt", "PFNioguardian",
            "PFCrawlingBody", "PFCrawlingLegs", "PFRockyTMan", "PFBlindSwordsman",
            "PFRocketFarmer", "PFHorseMaster", "PFWarriorHorse", "PFHorseArcher",
            "PFHeroAttack", "PFHeroDefense", "PFHeroBalanced"
        };
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("=== SVZ2 SKINNING & DEFORMATION AUDIT ===");
        for (int i = 0; i < prefabs.Length; i++)
        {
            string pfp = "Assets/Resources/characters/" + prefabs[i] + ".prefab";
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(pfp);
            if (asset == null)
            {
                sb.AppendLine(prefabs[i] + " | MISSING PREFAB");
                continue;
            }
            GameObject inst = (GameObject)UnityEngine.Object.Instantiate(asset);
            try
            {
                Animation[] anims = inst.GetComponentsInChildren<Animation>(true);
                Animation childAnim = null;
                Animation rootAnim = inst.GetComponent<Animation>();
                for (int a = 0; a < anims.Length; a++)
                {
                    if (anims[a] != null && anims[a].gameObject != inst) { childAnim = anims[a]; break; }
                }
                if (childAnim == null) childAnim = rootAnim;

                AnimationClip idleClip = (childAnim != null && childAnim["idle"] != null) ? childAnim["idle"].clip : null;
                string[] atkTry = new string[] { "attack03", "attack01", "attack02", "rangedattack01", "walk" };
                string poseName = "NONE";
                AnimationClip poseClip = null;
                if (childAnim != null)
                {
                    for (int k = 0; k < atkTry.Length; k++)
                    {
                        if (childAnim[atkTry[k]] != null && childAnim[atkTry[k]].clip != null)
                        {
                            poseName = atkTry[k];
                            poseClip = childAnim[atkTry[k]].clip;
                            break;
                        }
                    }
                }

                SkinnedMeshRenderer[] smrs = inst.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (smrs.Length == 0)
                {
                    sb.AppendLine(prefabs[i] + " | NO SMR");
                }
                for (int s = 0; s < smrs.Length; s++)
                {
                    SkinnedMeshRenderer smr = smrs[s];
                    Mesh m = smr.sharedMesh;
                    int vCount = m != null ? m.vertexCount : 0;
                    BoneWeight[] bws = m != null ? m.boneWeights : new BoneWeight[0];
                    Matrix4x4[] bps = m != null ? m.bindposes : new Matrix4x4[0];
                    int nzWeights = 0;
                    for (int w = 0; w < bws.Length; w++)
                    {
                        if (bws[w].weight0 + bws[w].weight1 + bws[w].weight2 + bws[w].weight3 > 0.01f) nzWeights++;
                    }
                    Transform[] bones = smr.bones;
                    int nullBones = 0;
                    for (int b = 0; b < bones.Length; b++)
                    {
                        if (bones[b] == null) nullBones++;
                    }

                    Vector3[] idleVerts = null;
                    Vector3[] poseVerts = null;
                    Vector3[] idleBonePos = new Vector3[bones.Length];
                    Quaternion[] idleBoneRot = new Quaternion[bones.Length];
                    if (idleClip != null)
                    {
                        for (int a = 0; a < anims.Length; a++)
                        {
                            if (anims[a] != null && anims[a].gameObject != inst && anims[a]["idle"] != null && anims[a]["idle"].clip != null)
                                anims[a]["idle"].clip.SampleAnimation(anims[a].gameObject, 0f);
                        }
                    }
                    for (int b = 0; b < bones.Length; b++)
                    {
                        idleBonePos[b] = bones[b] != null ? bones[b].position : Vector3.zero;
                        idleBoneRot[b] = bones[b] != null ? bones[b].rotation : Quaternion.identity;
                    }
                    Mesh bakedIdle = new Mesh();
                    smr.BakeMesh(bakedIdle);
                    idleVerts = bakedIdle.vertices;
                    UnityEngine.Object.DestroyImmediate(bakedIdle);

                    if (poseClip != null)
                    {
                        for (int a = 0; a < anims.Length; a++)
                        {
                            if (anims[a] != null && anims[a].gameObject != inst)
                            {
                                AnimationClip pc = (anims[a][poseName] != null && anims[a][poseName].clip != null)
                                    ? anims[a][poseName].clip
                                    : (anims[a]["walk"] != null ? anims[a]["walk"].clip : null);
                                if (pc != null) pc.SampleAnimation(anims[a].gameObject, pc.length * 0.45f);
                            }
                        }
                    }
                    float maxBoneAngle = 0f;
                    for (int b = 0; b < bones.Length; b++)
                    {
                        if (bones[b] != null)
                        {
                            float ang = Quaternion.Angle(idleBoneRot[b], bones[b].rotation);
                            if (ang > maxBoneAngle) maxBoneAngle = ang;
                        }
                    }
                    Mesh bakedPose = new Mesh();
                    smr.BakeMesh(bakedPose);
                    poseVerts = bakedPose.vertices;
                    UnityEngine.Object.DestroyImmediate(bakedPose);

                    float maxVDelta = 0f;
                    double sumVDelta = 0.0;
                    int nCmp = Mathf.Min(idleVerts.Length, poseVerts.Length);
                    for (int v = 0; v < nCmp; v++)
                    {
                        float d = Vector3.Distance(idleVerts[v], poseVerts[v]);
                        if (d > maxVDelta) maxVDelta = d;
                        sumVDelta += d;
                    }
                    float meanVDelta = nCmp > 0 ? (float)(sumVDelta / nCmp) : 0f;

                    string bw0 = bws.Length > 0
                        ? string.Format("b0={0}({1:F2}) b1={2}({3:F2})", bws[0].boneIndex0, bws[0].weight0, bws[0].boneIndex1, bws[0].weight1)
                        : "EMPTY";
                    sb.AppendLine(string.Format(
                        "{0} | smr={1} | verts={2} bw={3}(nz={4}) [{5}] | bp={6} bones={7}(null={8}) rootBone={9} | offscr={10} | anims={11} child={12} pose={13} | maxBoneAng={14:F1} | maxVertDelta={15:F4} meanVertDelta={16:F4}",
                        prefabs[i], smr.name, vCount, bws.Length, nzWeights, bw0,
                        bps.Length, bones.Length, nullBones,
                        smr.rootBone != null ? smr.rootBone.name : "NULL",
                        smr.updateWhenOffscreen, anims.Length,
                        childAnim != null ? childAnim.name : "NULL", poseName,
                        maxBoneAngle, maxVDelta, meanVDelta));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(inst);
            }
        }
        string logPath = "Temp/svz2_audit/skinning_audit.txt";
        if (!Directory.Exists("Temp/svz2_audit")) Directory.CreateDirectory("Temp/svz2_audit");
        File.WriteAllText(logPath, sb.ToString());
        Debug.Log("[SVZ2Audit] Wrote " + logPath + "\n" + sb.ToString());
    }

    [MenuItem("Tools/SVZ2/Audit Live Store UI")]
    public static void AuditLiveStoreUI()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("[SVZ2Audit] Enter Play Mode in Store.unity first.");
            return;
        }
        string prevMode = Singleton<PlayModesManager>.instance.selectedMode;
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("=== STORE HELPERS ICON & ROSTER AUDIT ===");
        string[] modes = new string[] { "classic", "zombies" };
        for (int m = 0; m < modes.Length; m++)
        {
            Singleton<PlayModesManager>.instance.selectedMode = modes[m];
            List<StoreData.Item> items = StoreAvailability.GetList(StoreAvailability.Group.Helpers);
            sb.AppendLine("-- Mode: " + modes[m] + " (" + items.Count + " helpers) --");
            for (int i = 0; i < items.Count; i++)
            {
                StoreData.Item it = items[i];
                Texture2D tex = !string.IsNullOrEmpty(it.icon) ? Resources.Load<Texture2D>(it.icon) : null;
                string texInfo = tex != null ? string.Format("{0}x{1} fmt={2}", tex.width, tex.height, tex.format) : "NULL";
                sb.AppendLine(string.Format("  [{0}] id={1} | title={2} | locked={3} | icon={4} | tex={5}",
                    i, it.id, it.title, it.locked, it.icon, texInfo));
            }
        }
        if (!Directory.Exists("Temp/svz2_audit")) Directory.CreateDirectory("Temp/svz2_audit");
        File.WriteAllText("Temp/svz2_audit/store_items_audit.txt", sb.ToString());
        Debug.Log("[SVZ2Audit] Wrote Temp/svz2_audit/store_items_audit.txt");

        Singleton<PlayModesManager>.instance.selectedMode = "zombies";
        if (prevMode != "zombies")
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("Store");
        }
    }

    [MenuItem("Tools/SVZ2/Live Store - Zombie Allies Tab Bottom")]
    public static void LiveStoreZombieAlliesTabBottom()
    {
        if (!EditorApplication.isPlaying) return;
        StoreImpl store = UnityEngine.Object.FindObjectOfType<StoreImpl>();
        if (store == null) return;
        System.Reflection.BindingFlags bf = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
        System.Reflection.MethodInfo onTab = typeof(StoreImpl).GetMethod("OnTabTouched", bf);
        if (onTab != null) onTab.Invoke(store, new object[] { 1 });
        System.Reflection.FieldInfo fList = typeof(StoreImpl).GetField("mPurchaseList", bf);
        if (fList != null)
        {
            SUIScrollList sl = fList.GetValue(store) as SUIScrollList;
            if (sl != null)
            {
                sl.scrollPosition = new Vector2(0f, 2400f);
                sl.ForceRedrawList();
                Debug.Log("[SVZ2Audit] Store scrollPosition now=" + sl.scrollPosition);
            }
        }
    }

    [MenuItem("Tools/SVZ2/Live Combat - Switch Mode Zombies")]
    public static void LiveCombatSwitchModeZombies()
    {
        if (!EditorApplication.isPlaying) return;
        Singleton<PlayModesManager>.instance.selectedMode = "zombies";
        UnityEngine.SceneManagement.SceneManager.LoadScene("InGame_summernight");
    }

    [MenuItem("Tools/SVZ2/Live Combat - Clear Clutter For Clean SVZ2 Duel")]
    public static void LiveCombatClearClutter()
    {
        if (!EditorApplication.isPlaying) return;
        CharactersManager cm = WeakGlobalInstance<CharactersManager>.instance;
        InGameImpl ig = WeakGlobalSceneBehavior<InGameImpl>.instance;
        if (cm == null || ig == null) return;
        List<Character> alive = cm.allAlive;
        for (int i = 0; i < alive.Count; i++)
        {
            Character c = alive[i];
            if (c != null && c != ig.hero)
            {
                if (c.isBase) c.health = c.maxHealth;
                else c.health = 0f;
            }
        }
        if (ig.hero != null)
        {
            ig.hero.health = ig.hero.maxHealth;
        }
        Debug.Log("[SVZ2Audit] Cleared wave clutter cleanly for live SVZ2 duel.");
    }

    private static void OpenLegionDialogWithState(int unlockedLevel, int selectedLevel, bool enabled)
    {
        OpenModuleDialogWithState("LegionOnTheLoose", unlockedLevel, selectedLevel, enabled);
    }

    private static void OpenModuleDialogWithState(string moduleID, int unlockedLevel, int selectedLevel, bool enabled)
    {
        if (!EditorApplication.isPlaying) return;
        Singleton<Localizer>.instance.Reload("Turkish");
        Singleton<PlayModesManager>.instance.selectedMode = "zombies";
        if (moduleID == "DeathFromAbove")
        {
            Singleton<Profile>.instance.deathFromAboveLevel = unlockedLevel;
        }
        else
        {
            Singleton<Profile>.instance.legionOnTheLooseLevel = unlockedLevel;
        }
        Singleton<Profile>.instance.SetModuleSelectedLevel(moduleID, selectedLevel);
        Singleton<Profile>.instance.SetModuleEnabled(moduleID, enabled);

        List<StoreData.Item> items = new List<StoreData.Item>();
        StoreAvailability_LegionsOnTheLoose.GetAll(items);
        StoreData.Item target = null;
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].id == moduleID) { target = items[i]; break; }
        }
        StoreImpl store = UnityEngine.Object.FindObjectOfType<StoreImpl>();
        if (store != null && target != null)
        {
            target.locked = false;
            System.Reflection.BindingFlags bf = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
            System.Reflection.FieldInfo fDlg = typeof(StoreImpl).GetField("mDialogHandler", bf);
            if (fDlg != null)
            {
                DialogHandler existingDlg = fDlg.GetValue(store) as DialogHandler;
                if (existingDlg != null)
                {
                    existingDlg.Destroy();
                    fDlg.SetValue(store, null);
                }
            }
            System.Reflection.MethodInfo showPanel = typeof(StoreImpl).GetMethod("ShowPurchasePanel", bf);
            if (showPanel != null) showPanel.Invoke(store, new object[] { target });
        }
    }

    [MenuItem("Tools/SVZ2/Test Legion Dialog Lv8")]
    public static void TestLegionDialogLv8()
    {
        OpenLegionDialogWithState(8, 6, true);
    }

    [MenuItem("Tools/SVZ2/Test Legion Dialog Lv10 Max ON")]
    public static void TestLegionDialogLv10On()
    {
        OpenLegionDialogWithState(10, 8, true);
    }

    [MenuItem("Tools/SVZ2/Test Legion Dialog Lv10 Max OFF")]
    public static void TestLegionDialogLv10Off()
    {
        OpenLegionDialogWithState(10, 8, false);
    }

    [MenuItem("Tools/SVZ2/Test Legion Cooldown Dialog")]
    public static void TestLegionCooldownDialog()
    {
        OpenModuleCooldownDialog("LegionOnTheLoose", 10, 8, 3);
    }

    [MenuItem("Tools/SVZ2/Test DFA Dialog Lv6")]
    public static void TestDFADialogLv6()
    {
        OpenModuleDialogWithState("DeathFromAbove", 6, 4, true);
    }

    [MenuItem("Tools/SVZ2/Test DFA Dialog Lv4")]
    public static void TestDFADialogLv4()
    {
        OpenModuleDialogWithState("DeathFromAbove", 4, 2, true);
    }

    [MenuItem("Tools/SVZ2/Test DFA Dialog Lv6 OFF")]
    public static void TestDFADialogLv6Off()
    {
        OpenModuleDialogWithState("DeathFromAbove", 6, 6, false);
    }

    [MenuItem("Tools/SVZ2/Test DFA Cooldown Dialog")]
    public static void TestDFACooldownDialog()
    {
        OpenModuleCooldownDialog("DeathFromAbove", 6, 4, 5);
    }

    private static void OpenModuleCooldownDialog(string moduleID, int unlockedLevel, int selectedLevel, int cooldownTier)
    {
        if (!EditorApplication.isPlaying) return;
        Singleton<Localizer>.instance.Reload("Turkish");
        Singleton<PlayModesManager>.instance.selectedMode = "zombies";
        if (moduleID == "DeathFromAbove")
        {
            Singleton<Profile>.instance.deathFromAboveLevel = unlockedLevel;
        }
        else
        {
            Singleton<Profile>.instance.legionOnTheLooseLevel = unlockedLevel;
        }
        Singleton<Profile>.instance.SetModuleSelectedLevel(moduleID, selectedLevel);
        Singleton<Profile>.instance.SetModuleEnabled(moduleID, true);
        Singleton<Profile>.instance.SetModuleCooldownLevel(moduleID, cooldownTier);
        Singleton<Profile>.instance.SetModuleCooldownSelectedLevel(moduleID, cooldownTier);

        List<StoreData.Item> items = new List<StoreData.Item>();
        StoreAvailability_LegionsOnTheLoose.GetAll(items);
        StoreData.Item target = null;
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].id == moduleID) { target = items[i]; break; }
        }
        StoreImpl store = UnityEngine.Object.FindObjectOfType<StoreImpl>();
        if (store != null && target != null)
        {
            target.locked = false;
            System.Reflection.BindingFlags bf = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
            System.Reflection.FieldInfo fDlg = typeof(StoreImpl).GetField("mDialogHandler", bf);
            if (fDlg != null)
            {
                DialogHandler existingDlg = fDlg.GetValue(store) as DialogHandler;
                if (existingDlg != null)
                {
                    existingDlg.Destroy();
                }
                LegionCooldownDialog dlg = new LegionCooldownDialog(target, null, null);
                dlg.priority = 500f;
                fDlg.SetValue(store, new DialogHandler(499.5f, dlg));
            }
        }
    }

    [MenuItem("Tools/SVZ2/Live Combat - Start Oni Legion Test")]
    public static void LiveCombatStartOniLegionTest()
    {
        if (!EditorApplication.isPlaying) return;
        Singleton<PlayModesManager>.instance.selectedMode = "zombies";
        Singleton<Profile>.instance.legionOnTheLooseLevel = 10;
        Singleton<Profile>.instance.legionOnTheLooseSelectedLevel = 8; // Oni
        Singleton<Profile>.instance.legionOnTheLooseEnabled = true;
        Singleton<Profile>.instance.legionCooldownLevel = 10; // 15s
        Singleton<Profile>.instance.legionCooldownSelectedLevel = 10;
        Singleton<Profile>.instance.Save();
        UnityEngine.SceneManagement.SceneManager.LoadScene("InGame_summernight");
    }

    [MenuItem("Tools/SVZ2/Verify Legion Runtime Logic")]
    public static void VerifyLegionRuntimeLogic()
    {
        if (!EditorApplication.isPlaying) return;
        Singleton<PlayModesManager>.instance.selectedMode = "zombies";
        int origLvl = Singleton<Profile>.instance.legionOnTheLooseLevel;
        int origSel = Singleton<Profile>.instance.legionOnTheLooseSelectedLevel;
        bool origEn = Singleton<Profile>.instance.legionOnTheLooseEnabled;
        int origCd = Singleton<Profile>.instance.GetModuleCooldownLevel("LegionOnTheLoose");
        int origCdSel = Singleton<Profile>.instance.GetModuleCooldownSelectedLevel("LegionOnTheLoose");

        int origDfaLvl = Singleton<Profile>.instance.deathFromAboveLevel;
        int origDfaSel = Singleton<Profile>.instance.GetModuleSelectedLevel("DeathFromAbove");
        bool origDfaEn = Singleton<Profile>.instance.IsModuleEnabled("DeathFromAbove");
        int origDfaCd = Singleton<Profile>.instance.GetModuleCooldownLevel("DeathFromAbove");
        int origDfaCdSel = Singleton<Profile>.instance.GetModuleCooldownSelectedLevel("DeathFromAbove");
        try
        {
            // 1. Legion ON(Lv8,Sel5,15s) + DFA ON(Lv4,Sel2,24s)
            Singleton<Profile>.instance.legionOnTheLooseLevel = 8;
            Singleton<Profile>.instance.legionOnTheLooseSelectedLevel = 5;
            Singleton<Profile>.instance.legionOnTheLooseEnabled = true;
            Singleton<Profile>.instance.SetModuleCooldownLevel("LegionOnTheLoose", 10);
            Singleton<Profile>.instance.SetModuleCooldownSelectedLevel("LegionOnTheLoose", 10);

            Singleton<Profile>.instance.deathFromAboveLevel = 4;
            Singleton<Profile>.instance.SetModuleSelectedLevel("DeathFromAbove", 2);
            Singleton<Profile>.instance.SetModuleEnabled("DeathFromAbove", true);
            Singleton<Profile>.instance.SetModuleCooldownLevel("DeathFromAbove", 5); // 24s
            Singleton<Profile>.instance.SetModuleCooldownSelectedLevel("DeathFromAbove", 5);
            LegionOnTheLooseManager m1 = new LegionOnTheLooseManager();
            string log1 = DescribeLegionManager(m1);

            // 2. Legion ON(Lv10,Sel10,40s) + DFA ON(Lv6,Sel6,15s) -> TenguAirx3
            Singleton<Profile>.instance.legionOnTheLooseLevel = 10;
            Singleton<Profile>.instance.legionOnTheLooseSelectedLevel = 10;
            Singleton<Profile>.instance.SetModuleCooldownSelectedLevel("LegionOnTheLoose", 0);
            Singleton<Profile>.instance.deathFromAboveLevel = 6;
            Singleton<Profile>.instance.SetModuleSelectedLevel("DeathFromAbove", 6);
            Singleton<Profile>.instance.SetModuleCooldownLevel("DeathFromAbove", 10);
            Singleton<Profile>.instance.SetModuleCooldownSelectedLevel("DeathFromAbove", 10);
            LegionOnTheLooseManager m2 = new LegionOnTheLooseManager();
            string log2 = DescribeLegionManager(m2);

            // 3. DFA Sel4(TenguAirx1) & Legion Sel8(Oni) + DFA OFF check
            Singleton<Profile>.instance.SetModuleSelectedLevel("DeathFromAbove", 4);
            LegionOnTheLooseManager m2b = new LegionOnTheLooseManager();
            string log2b = DescribeLegionManager(m2b);

            Singleton<Profile>.instance.legionOnTheLooseSelectedLevel = 8;
            Singleton<Profile>.instance.SetModuleEnabled("DeathFromAbove", false);
            LegionOnTheLooseManager m3 = new LegionOnTheLooseManager();
            string log3 = DescribeLegionManager(m3);

            // 4. Both OFF
            Singleton<Profile>.instance.legionOnTheLooseEnabled = false;
            LegionOnTheLooseManager m4 = new LegionOnTheLooseManager();
            string log4 = DescribeLegionManager(m4);

            Debug.Log("[LegionVerify] C1(L5@15s+DFA2@24s): " + log1 + " | C2(L10@40s+DFA6@15s): " + log2 + " | C2b(DFA4@15s): " + log2b + " | C3(L8+DFA_OFF): " + log3 + " | C4(ALL_OFF): " + log4);
        }
        finally
        {
            Singleton<Profile>.instance.legionOnTheLooseLevel = origLvl;
            Singleton<Profile>.instance.legionOnTheLooseSelectedLevel = origSel;
            Singleton<Profile>.instance.legionOnTheLooseEnabled = origEn;
            Singleton<Profile>.instance.SetModuleCooldownLevel("LegionOnTheLoose", origCd);
            Singleton<Profile>.instance.SetModuleCooldownSelectedLevel("LegionOnTheLoose", origCdSel);

            Singleton<Profile>.instance.deathFromAboveLevel = origDfaLvl;
            Singleton<Profile>.instance.SetModuleSelectedLevel("DeathFromAbove", origDfaSel);
            Singleton<Profile>.instance.SetModuleEnabled("DeathFromAbove", origDfaEn);
            Singleton<Profile>.instance.SetModuleCooldownLevel("DeathFromAbove", origDfaCd);
            Singleton<Profile>.instance.SetModuleCooldownSelectedLevel("DeathFromAbove", origDfaCdSel);
            Singleton<Profile>.instance.Save();
        }
    }

    private static string DescribeLegionManager(LegionOnTheLooseManager mgr)
    {
        System.Reflection.FieldInfo f = typeof(LegionOnTheLooseManager).GetField("mEntries", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        System.Collections.IList entries = f != null ? (f.GetValue(mgr) as System.Collections.IList) : null;
        if (entries == null || entries.Count == 0) return "EMPTY";
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < entries.Count; i++)
        {
            object e = entries[i];
            System.Type t = e.GetType();
            string mod = t.GetField("moduleID").GetValue(e) as string;
            string hid = t.GetField("helperID").GetValue(e) as string;
            int n = (int)t.GetField("numSpawns").GetValue(e);
            float cd = (float)t.GetField("maxTimer").GetValue(e);
            if (i > 0) sb.Append(",");
            sb.Append(mod + "=" + hid + "x" + n + "@" + cd + "s");
        }
        return sb.ToString();
    }

    [MenuItem("Tools/SVZ2/Build Missing Variant Prefabs")]
    public static void BuildMissingVariantPrefabs()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        Material chochinRedMat = EnsureVariantMaterial(
            "Assets/Resources/characters/chochinobake/materials/chochinobake_alt.mat",
            "Assets/Resources/characters/chochinobake/materials/chochinobake_alt_red.mat",
            "Assets/Resources/characters/chochinobake/textures/chochinobake_alt_red_D.png");

        Material torsoRedMat = EnsureVariantMaterial(
            "Assets/Resources/characters/crawlingtorso/materials/crawlingtorso-crawlingtorso_CrawlingTorso1.mat",
            "Assets/Resources/characters/crawlingtorso/materials/crawling_torso_red_D.mat",
            "Assets/Resources/characters/crawlingtorso/textures/crawling_torso_red_D.png");

        AssetDatabase.SaveAssets();

        int built = 0;
        if (BuildVariantClone("Assets/Resources/characters/PFChochinobakeAlt.prefab",
                "Assets/Resources/characters/PFChochinobakeRed.prefab", chochinRedMat, 1.45f))
        {
            built++;
        }

        if (BuildVariantClone("Assets/Resources/characters/PFCrawlingtorso.prefab",
                "Assets/Resources/characters/PFCrawlingTorsoRed.prefab", torsoRedMat, 1.10f))
        {
            built++;
        }

        SchemaData schemas = LoadSchemas();
        CharacterModelController templateCtrl = null;
        SoundThemePlayer templateSound = null;
        GameObject ghostRef = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/characters/PFGhostRonin.prefab");
        if (ghostRef != null)
        {
            templateCtrl = ghostRef.GetComponent<CharacterModelController>();
            templateSound = ghostRef.GetComponent<SoundThemePlayer>();
        }

        for (int i = 0; i < kSpecs.Length; i++)
        {
            Spec s = kSpecs[i];
            if (string.Equals(s.pfName, "PFCrawlingBodyRed", StringComparison.Ordinal) ||
                string.Equals(s.pfName, "PFCrawlingLegsRed", StringComparison.Ordinal))
            {
                if (BuildOne(s, schemas, templateCtrl, templateSound))
                {
                    built++;
                }
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[SVZ2PrefabBuilder] BuildMissingVariantPrefabs complete: built=" + built);
    }

    private static Material EnsureVariantMaterial(string templateMatPath, string dstMatPath, string dstTexPath)
    {
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(dstTexPath);
        if (tex == null)
        {
            Debug.LogError("[SVZ2PrefabBuilder] Missing variant texture at " + dstTexPath);
            return null;
        }

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(dstMatPath);
        if (mat == null)
        {
            string dir = Path.GetDirectoryName(dstMatPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            Material tpl = AssetDatabase.LoadAssetAtPath<Material>(templateMatPath);
            mat = tpl != null ? new Material(tpl) : new Material(Shader.Find("Diffuse"));
            mat.name = Path.GetFileNameWithoutExtension(dstMatPath);
            mat.mainTexture = tex;
            AssetDatabase.CreateAsset(mat, dstMatPath);
            mat = AssetDatabase.LoadAssetAtPath<Material>(dstMatPath);
        }
        else
        {
            mat.mainTexture = tex;
            EditorUtility.SetDirty(mat);
        }

        return mat;
    }

    private static bool BuildVariantClone(string srcPrefabPath, string dstPrefabPath, Material mat, float childScale)
    {
        GameObject src = AssetDatabase.LoadAssetAtPath<GameObject>(srcPrefabPath);
        if (src == null)
        {
            Debug.LogError("[SVZ2PrefabBuilder] Missing source prefab: " + srcPrefabPath);
            return false;
        }

        GameObject root = (GameObject)UnityEngine.Object.Instantiate(src);
        try
        {
            root.name = Path.GetFileNameWithoutExtension(dstPrefabPath);
            root.transform.localScale = Vector3.one;
            if (Math.Abs(childScale - 1.0f) > 0.001f && root.transform.childCount > 0)
            {
                Transform child = root.transform.GetChild(0);
                child.localScale = child.localScale * childScale;
            }

            if (mat != null)
            {
                Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
                for (int r = 0; r < renderers.Length; r++)
                {
                    Material[] mats = renderers[r].sharedMaterials;
                    for (int m = 0; m < mats.Length; m++)
                    {
                        mats[m] = mat;
                    }
                    renderers[r].sharedMaterials = mats;
                }
            }

            UnityEngine.Object existing = AssetDatabase.LoadAssetAtPath<GameObject>(dstPrefabPath);
            if (existing != null)
            {
                PrefabUtility.ReplacePrefab(root, existing, ReplacePrefabOptions.ReplaceNameBased);
            }
            else
            {
                PrefabUtility.CreatePrefab(dstPrefabPath, root);
            }
            return true;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
