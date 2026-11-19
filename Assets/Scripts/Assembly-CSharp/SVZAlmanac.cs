using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mini-Almanac (Enemies & Allies Bestiary / Rehber)
/// Samurai vs Zombies Defense - Gemini Edition
/// Can be toggled in-game or in menus via [B] or through QoL / Dev panel.
/// Displays detailed stats, role classifications, combat tips, and lore.
/// </summary>
public class SVZAlmanac : MonoBehaviour
{
    private static SVZAlmanac sInstance;
    private bool mIsOpen = false;
    private int mSelectedCategory = 0; // 0 = Enemies, 1 = Allies
    private int mSelectedIndex = 0;
    private Vector2 mListScrollPos = Vector2.zero;
    private Vector2 mDetailScrollPos = Vector2.zero;

    public struct CharacterInfo
    {
        public string id;
        public string name;
        public string role;
        public string element;
        public int health;
        public int meleeDmg;
        public int rangedDmg;
        public float speed;
        public float attackSpeed;
        public float range;
        public string description;
        public string strategyTip;
        public string nameEn;
        public string roleEn;
        public string descriptionEn;
        public string strategyTipEn;

        public CharacterInfo(string id, string name, string role, string element, int hp, int mDmg, int rDmg, float spd, float atkSpd, float rng, string desc, string tip,
            string nameEn = null, string roleEn = null, string descEn = null, string tipEn = null)
        {
            this.id = id;
            this.name = name;
            this.role = role;
            this.element = element;
            this.health = hp;
            this.meleeDmg = mDmg;
            this.rangedDmg = rDmg;
            this.speed = spd;
            this.attackSpeed = atkSpd;
            this.range = rng;
            this.description = desc;
            this.strategyTip = tip;
            this.nameEn = string.IsNullOrEmpty(nameEn) ? id.Replace("_", " ") : nameEn;
            this.roleEn = string.IsNullOrEmpty(roleEn) ? role : roleEn;
            this.descriptionEn = string.IsNullOrEmpty(descEn) ? desc : descEn;
            this.strategyTipEn = string.IsNullOrEmpty(tipEn) ? tip : tipEn;
        }

        public string GetName() { return LocalizationManager.IsTurkish ? name : nameEn; }
        public string GetRole() { return LocalizationManager.IsTurkish ? role : roleEn; }
        public string GetDesc() { return LocalizationManager.IsTurkish ? description : descriptionEn; }
        public string GetTip() { return LocalizationManager.IsTurkish ? strategyTip : strategyTipEn; }
    }

    private static readonly List<CharacterInfo> sEnemies = new List<CharacterInfo>
    {
        new CharacterInfo("Zombie_Farmer", "Zombi Koylusu", "Temel Piyade", "Fiziksel", 80, 20, 0, 40f, 1.2f, 60f,
            "Mezardan yeni kalkmis en temel zombi birligi. Tek basina zayiftir ancak buyuk kalabaliklar halinde tehlikeli olabilir.",
            "Okcular veya kilic kombolari ile kolayca temizlenir.",
            "Zombie Farmer", "Basic Infantry", "Basic zombie risen from the grave. Weak alone, dangerous in hordes.", "Clear easily with Archers or sword combos."),

        new CharacterInfo("Zombie_Sword", "Zombi Kilicli", "Saldiri Piyadesi", "Fiziksel", 150, 40, 0, 45f, 1.0f, 65f,
            "Pasli katanasiyla hizla ilerleyen savasci zombi.",
            "Mizrakcilarin menzili sayesinde ona yaklasmadan hasar verebilirsiniz.",
            "Zombie Swordsman", "Melee Striker", "Fast advancing zombie wielding a rusty katana.", "Use Spear Samurai reach to strike before he gets close."),

        new CharacterInfo("Zombie_Spear", "Zombi Mizrakci", "Menzilli Piyade", "Fiziksel", 180, 50, 0, 40f, 1.1f, 110f,
            "Uzun menzilli mizragiyla savunma hatlarina vurabilir.",
            "Kalkanli Samuray ile mizrak saldirilarini bloke edin.",
            "Zombie Spearman", "Long Reach", "Strikes frontline defenders with a long spear.", "Block spear thrusts with Shield Samurai."),

        new CharacterInfo("Zombie_Archer", "Zombi Okcu", "Menzilli Nisanci", "Fiziksel", 90, 15, 30, 35f, 1.5f, 320f,
            "Arka saflardan kemik oklari firlatan sinsi nisanci.",
            "Kahramanin yay saldirisi veya Hanim Sultan cagirarak oklarini durdurun.",
            "Zombie Archer", "Ranged Marksman", "Snipes from the backline with bone arrows.", "Counter with your Hero's bow or fast flankers."),

        new CharacterInfo("Nobusuma", "Nobusuma (Ucan Iblis)", "Ucan Dusman", "Hava", 120, 35, 0, 65f, 1.0f, 70f,
            "Gokyuzunden suzulen kanatli yarasamsi iblis. Kara tuzaklarindan etkilenmez.",
            "Koy Okculari ve Samuray Okculari onu havada avlamakta cok etkilidir.",
            "Nobusuma (Flying Demon)", "Flying Unit", "Winged bat-demon gliding above ground traps.", "Shoot it down with Village Archers and Samurai Archers."),

        new CharacterInfo("Exploder", "Fici Bombacisi", "Intihar Bombacisi", "Ates / Patlama", 70, 250, 0, 80f, 0.5f, 50f,
            "Sirtinda barut ficisi tasiyarak hizla kapiniza veya kahramana kosar. Patladiginda devasa alan hasari verir.",
            "Hemen yay ile uzaktan vurarak kendi saflarinda patlatmaya calisin!",
            "Chochin Exploder", "Suicide Bomber", "Rushes with an explosive barrel dealing massive AoE damage.", "Shoot from range so it detonates inside enemy ranks!"),

        new CharacterInfo("Wayundo_wood", "Wayundo (Tahta Dev)", "Yarici / Hizli Agir", "Tahta", 750, 130, 0, 58f, 1.8f, 100f,
            "Yuvarlanarak samuray saflarini yarmaya calisan ahsap yapili dev. [Rebalanced 4.0 ile hizi ve cani dengelendi].",
            "Mizrakcilar ve Ates yetenekleri ile tahta govdesini kolayca yakin.",
            "Wayundo (Wood Wheel)", "Heavy Breaker", "Rolling wooden wheel demon trying to crush your frontline.", "Slow with Ice Archers and burn with fire skills."),

        new CharacterInfo("Wayundo_Fire", "Wayundo (Ates Devi)", "Yarici / Atesli", "Ates", 1150, 175, 0, 58f, 1.8f, 100f,
            "Alev sacan govdesiyle savunma hatlarini ezip gecen guc.",
            "Buz Okcusu ile dondurarak yuvarlanma hizini kesin.",
            "Wayundo (Fire Wheel)", "Flaming Breaker", "Blazing wheel monster tearing through defenders.", "Freeze with Ice Archers to halt its momentum."),

        new CharacterInfo("Wayundo_Metal", "Wayundo (Demir Dev)", "Zirhli Tank", "Metal", 1550, 210, 0, 58f, 1.8f, 100f,
            "Demir kaplamasi sayesinde cok yuksek darbe direncine sahip dev.",
            "Tufekciler ve agir samuraylarin zirh delici saldirilarini kullanin.",
            "Wayundo (Iron Wheel)", "Armored Tank", "Metal-plated rolling juggernaut with heavy armor.", "Use Riflemen and heavy melee armor-piercing attacks."),

        new CharacterInfo("Horseman", "Zombi Suvari", "Hizli Suvari", "Fiziksel", 450, 90, 0, 95f, 1.2f, 90f,
            "Yildirim hiziyla hatlara dalan suvari. Hiz bonusu yuksek darbe vurur.",
            "Mizrakli Samuraylar suvariye karsi ekstra hasar verir ve atini durdurur.",
            "Zombie Horseman", "Fast Cavalry", "Charges into your line at blinding speed.", "Spear Samurai deal bonus damage and stop cavalry charges."),

        new CharacterInfo("Evil_Panzer", "Panzer Zombi (Zirhli)", "Agir Kusatmacı", "Zirh", 2200, 180, 0, 30f, 2.0f, 80f,
            "Gullere dayanikli celik kalkan tasiyan devasa tank. Kapiya ulasirsa agir hasar verir.",
            "Tufekciler ve Kahramanin yildirim/ates yetenekleri zirhini deler.",
            "Armored Panzer Zombie", "Heavy Siege Tank", "Massive shielded juggernaut that crushes the gate if unchecked.", "Riflemen and Lightning/Katana skills punch through its armor."),

        new CharacterInfo("Evil_Ice", "Buz Zombi Devi", "Kontrol / Yavaslatici", "Buz", 1800, 140, 0, 35f, 2.2f, 85f,
            "Dondurucu soguk dalgasiyla samuraylarinizi dondurup savunmasiz birakir.",
            "Ates yetenekleri ve Nobunaga'nin moral liderligi ile karsi koyun.",
            "Frost Giant Zombie", "Crowd Control", "Chilling frost attacks slow your defenders down.", "Counter with fire skills and Nobunaga's leadership aura."),

        new CharacterInfo("Oni_Boss", "Kizil Oni (Dev Iblis)", "BOSS", "Iblis", 4500, 320, 0, 38f, 2.5f, 120f,
            "Devasa dikenli tokmagiyla tek vurusunda tum saflari savuran efsanevi kizil iblis.",
            "Kalkanli Samuraylar ile onu oyalarken, arka saftan Tufekci ve Buz Okculariyla vurun.",
            "Crimson Oni Giant", "BOSS", "Legendary demon with a spiked club that knocks back frontline armies.", "Hold him with Shield Samurai while Riflemen and Ice Archers fire."),

        new CharacterInfo("Necromancer_Boss", "Necromancer (Ruh Cagiran)", "BOSS / Buyucu", "Karanlik Ruh", 3800, 120, 220, 35f, 2.0f, 350f,
            "Surekli yeni zombiler diriltir ve uzaktan olum kureleri firlatir.",
            "Onu koruyan saflari atilma (Dash) veya Ozel Yetenekle asip dogrudan buyucuyu hedef alin.",
            "Necromancer", "BOSS / Summoner", "Continuously raises undead minions and hurls dark orbs from afar.", "Dash past guards or nuke directly with abilities."),

        new CharacterInfo("Shogun", "Kara Zirhli Shogun", "FINAL BOSS", "Karanlik Zirh", 6000, 450, 0, 42f, 1.5f, 110f,
            "Iblisler diyarindan gelen samuray generali. Hem yakin dovuste olumcul, hem de birliklerinizi geri puskurtur.",
            "Tum iksirlerinizi, Takeda ve Nobunaga birliklerinizi koordine ederek saldirin!",
            "Dark Shogun", "FINAL BOSS", "Demonic samurai warlord lethal in melee with heavy knockback.", "Coordinate potions, Takeda, and Nobunaga to overwhelm him!")
    };

    private static readonly List<CharacterInfo> sAllies = new List<CharacterInfo>
    {
        new CharacterInfo("Farmer", "Ciftci", "Temel Birlik", "Ucuz / Hizli Uretim", 100, 25, 0, 45f, 1.1f, 60f,
            "Koyu savunmak icin orak ve kurekleriyle savasa katilan fedakar koyluler. Cok az liderlik puani harcar.",
            "On cephede zaman kazanmak ve dusman dikkatini dagitmak icin harikadir.",
            "Farmer", "Basic Unit", "Brave villagers fighting with tools. Very low leadership cost.", "Great for stalling enemies on the frontline."),

        new CharacterInfo("Samurai", "Kilicli Samuray", "Standart Piyade", "Dengeli", 220, 50, 0, 50f, 1.0f, 65f,
            "Bushido yoluna sadik, dengeli ve guvenilir yakin dovus savascisi.",
            "Genel hat savunmasinda her turlu temel zombiye karsi etkilidir.",
            "Samurai", "Standard Infantry", "Balanced and reliable melee warrior following Bushido.", "Strong all-around frontline defender."),

        new CharacterInfo("SamuraiArcher", "Samuray Okcusu", "Menzilli Destek", "Hizli Atis", 110, 15, 45, 40f, 1.4f, 320f,
            "Arka saflardan seri ok atislari yaparak dusman saflarini yumusatir.",
            "Ucan iblisleri ve zombi koylulerini kapiniza yaklasmadan yok eder.",
            "Samurai Archer", "Ranged Support", "Fires rapid arrows from the backline to thin hordes.", "Essential for shooting down flying demons."),

        new CharacterInfo("SpearSamurai", "Mizrakli Samuray", "Anti-Suvari / Menzil", "Mizrak", 260, 60, 0, 45f, 1.1f, 110f,
            "Uzun yari mizragi ile guvenli mesafeden yuksek hasar verir.",
            "Suvarilere ve yuvarlanan Wayundo devlerine karsi ekstra etkilidir.",
            "Spear Samurai", "Anti-Cavalry", "Long yari spear strikes safely from behind frontliners.", "Extra effective against Horseman and Wayundo."),

        new CharacterInfo("Rifleman", "Samuray Tufekcisi", "Zirh Delici Menzilli", "Kursun", 130, 20, 95, 38f, 2.2f, 360f,
            "Agir tufegi ile kalin zirhlari tek mermide delen elit nisanci.",
            "Panzer zombiler ve Dev Oni gibi yuksek canli hedeflere karsi zorunludur.",
            "Rifleman", "Armor Piercer", "Heavy matchlock rounds pierce armored targets at long range.", "Mandatory against Panzer tanks and Oni bosses."),

        new CharacterInfo("IceArcher", "Buz Okcusu", "Kontrol / Yavaslatici", "Buz Oku", 120, 15, 35, 40f, 1.6f, 300f,
            "Buz buyusuyle sarilmis oklari dusmanlarin hareket ve saldiri hizini yariya indirir.",
            "Hizli suvarileri ve Wayundo'yu durdurmak icin en iyi silahtir.",
            "Frost Archer", "Crowd Control", "Frost-imbued arrows chill enemy movement and attack speed.", "Best answer to fast Horsemen and Wayundo."),

        new CharacterInfo("Shieldsamurai", "Kalkanli Samuray", "Agir Tank", "Celik Kalkan", 750, 30, 0, 32f, 1.5f, 60f,
            "Devasa celik kalkani ile tum oklari ve saldirilari gogusleyen celik duvar.",
            "Arkasindaki okculari ve tufekcileri korumak icin on safta tutun.",
            "Armored Shield Samurai", "Heavy Tank", "Walking steel fortress absorbing heavy enemy fire.", "Keep in front to shield your Archers and Riflemen."),

        new CharacterInfo("HeavySamurai", "Agir Samuray (Nodachi)", "Agir Hasar Piyadesi", "Dev Kilic", 450, 110, 0, 42f, 1.6f, 80f,
            "Devasa iki elli katanasiyla tek vurusunda birden fazla dusmani bicer.",
            "Kalabalik zombi gruplarini dagitmak icin birebirdir.",
            "Heavy Samurai (Nodachi)", "Heavy Cleaver", "Two-handed greatsword slices through multiple zombies at once.", "Clears dense zombie packs quickly."),

        new CharacterInfo("SpearHorseman", "Samuray Suvarisi", "Suvari / Hucum", "Hucum Bonusu", 500, 120, 0, 90f, 1.3f, 95f,
            "Hizli atiyla dusman saflarina dalip on saflari tarumar eden elit atli.",
            "Menzilli dusman okcularinin saflarini bozmak icin kullanilir.",
            "Mounted Samurai", "Charge Cavalry", "Swift mounted warrior smashing deep into enemy lines.", "Disrupts enemy ranged backlines rapidly."),

        new CharacterInfo("NinjaGirl", "Suikastci Ninja Kizi", "Hizli Kombo / Kritik", "Hiz / Ceviklik", 280, 85, 0, 75f, 0.7f, 65f,
            "Cok yuksek saldiri hizi ve yuksek kritik vurus ihtimaline sahip golge savasci.",
            "Tekil guclu hedefleri seri sekilde eritir.",
            "Assassin Kunoichi", "Fast DPS / Crit", "Ultra-fast combo assassin with high critical strike rate.", "Melts priority targets in seconds."),

        new CharacterInfo("Nobunaga", "Oda Nobunaga", "Lider / Komutan", "Liderlik Aurasi", 900, 140, 0, 42f, 1.4f, 85f,
            "Cevresindeki tum dost birliklere moral, saldiri gucu ve hareket hizi bonusu saglar.",
            "Buyuk ordunuzun arkasina koyarak sahadaki gucunuzu ikiye katlayin!",
            "Oda Nobunaga", "Commander Aura", "Grants nearby allies bonus attack power and movement speed.", "Deploy behind your army to double combat output!"),

        new CharacterInfo("Takeda", "Takeda Shingen", "Efsanevi Komutan", "Hucum Aurasi", 1100, 180, 0, 70f, 1.4f, 100f,
            "At uzerindeki efsanevi general. Suvarilerinizi ve samuraylarinizi zafere tasir.",
            "Zorlu dalgalarda ve boss savaslarinda oyunun kaderini degistirir.",
            "Takeda Shingen", "Legendary General", "Legendary mounted general leading cavalry and samurai to victory.", "Turns the tide in endgame waves and boss battles.")
    };

    public static SVZAlmanac Instance
    {
        get { return sInstance; }
    }

    public static bool IsOpen
    {
        get { return sInstance != null && sInstance.mIsOpen; }
    }

    public static void Toggle()
    {
        if (sInstance == null)
        {
            GameObject go = new GameObject("[SVZAlmanac]");
            DontDestroyOnLoad(go);
            sInstance = go.AddComponent<SVZAlmanac>();
        }
        sInstance.mIsOpen = !sInstance.mIsOpen;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        if (sInstance == null)
        {
            GameObject go = new GameObject("[SVZAlmanac]");
            DontDestroyOnLoad(go);
            sInstance = go.AddComponent<SVZAlmanac>();
        }
    }

    private void Awake()
    {
        if (sInstance == null)
        {
            sInstance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (sInstance != this)
        {
            Destroy(gameObject);
        }
    }

    private float mTouchDragAccum;
    private int mLastTouchScrollFrame = -1;

    private void Update()
    {
        // [B] opens Almanac / Bestiary
        if (Input.GetKeyDown(KeyCode.B))
        {
            mIsOpen = !mIsOpen;
        }

        if (mIsOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            mIsOpen = false;
        }
    }

    private void HandleTouchScroll(Rect listRect, Rect detailRect, float scale)
    {
        if (Input.touchCount != 1)
        {
            mTouchDragAccum = 0f;
            return;
        }

        Touch t = Input.GetTouch(0);
        Vector2 virtualTouch = new Vector2(t.position.x / scale, (Screen.height - t.position.y) / scale);
        if (t.phase == TouchPhase.Began)
        {
            mTouchDragAccum = 0f;
        }
        else if (t.phase == TouchPhase.Moved)
        {
            mTouchDragAccum += Mathf.Abs(t.deltaPosition.y);
            if (mTouchDragAccum > 6f && mLastTouchScrollFrame != Time.frameCount)
            {
                mLastTouchScrollFrame = Time.frameCount;
                float dy = t.deltaPosition.y / scale;
                if (listRect.Contains(virtualTouch))
                {
                    mListScrollPos.y = Mathf.Max(0f, mListScrollPos.y + dy);
                }
                else if (detailRect.Contains(virtualTouch))
                {
                    mDetailScrollPos.y = Mathf.Max(0f, mDetailScrollPos.y + dy);
                }
            }
        }
    }

    private void OnGUI()
    {
        if (!mIsOpen)
        {
            return;
        }

        float scale, virtualW, virtualH;
        Rect safeRect;
        SVZDeviceCompatibility.GetSafeVirtualViewport(720f, 960f, out scale, out virtualW, out virtualH, out safeRect);

        Matrix4x4 prevMatrix = GUI.matrix;
        Font oldLblFont = GUI.skin.label.font;
        Font oldBtnFont = GUI.skin.button.font;
        try
        {
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            Font dyn = LocalizationManager.GetDynamicOSFont(15);
            if (dyn != null)
            {
                GUI.skin.label.font = dyn;
                GUI.skin.button.font = dyn;
            }

            // Dark modal backdrop
            GUI.color = new Color(0f, 0f, 0f, 0.78f);
            GUI.DrawTexture(new Rect(0f, 0f, virtualW, virtualH), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float width = Mathf.Min(880f, safeRect.width * 0.96f);
            float height = Mathf.Min(620f, safeRect.height * 0.94f);
            float x = safeRect.x + (safeRect.width - width) * 0.5f;
            float y = safeRect.y + (safeRect.height - height) * 0.5f;

            // Main Panel Background
            GUI.color = new Color(0.06f, 0.07f, 0.10f, 0.96f);
            GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture);

            // Gold border lines
            GUI.color = new Color(0.85f, 0.70f, 0.25f, 0.85f);
            GUI.DrawTexture(new Rect(x, y, width, 3f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x, y + height - 3f, width, 3f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x, y, 3f, height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x + width - 3f, y, 3f, height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Header Title
            GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontSize = 20;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.alignment = TextAnchor.MiddleCenter;
            titleStyle.richText = true;

            GUI.Label(new Rect(x + 48f, y + 10f, width - 96f, 32f),
                "<color=#FFE855>" + LocalizationManager.L("mod_almanac_title", "SAMURAI VS ZOMBIES - ALMANAC & GUIDE", "SAMURAI VS ZOMBIES - REHBER & ALMANAC") + "</color>", titleStyle);

            // Close Button [X]
            if (GUI.Button(new Rect(x + width - 46f, y + 8f, 36f, 32f), "X"))
            {
                mIsOpen = false;
            }

            // Category Tabs
            float tabW = Mathf.Min(230f, (width - 52f) * 0.5f);
            float tabH = 34f;
            float tabY = y + 48f;

            bool isEnemies = mSelectedCategory == 0;
            GUI.color = isEnemies ? new Color(0.85f, 0.25f, 0.25f, 1f) : new Color(0.2f, 0.2f, 0.25f, 1f);
            if (GUI.Button(new Rect(x + 18f, tabY, tabW, tabH), LocalizationManager.L("mod_almanac_enemies", "ENEMIES (ZOMBIES)", "DUSMANLAR (ZOMBILER)")))
            {
                mSelectedCategory = 0;
                mSelectedIndex = 0;
                mListScrollPos = Vector2.zero;
                mDetailScrollPos = Vector2.zero;
            }

            GUI.color = !isEnemies ? new Color(0.25f, 0.65f, 0.95f, 1f) : new Color(0.2f, 0.2f, 0.25f, 1f);
            if (GUI.Button(new Rect(x + 28f + tabW, tabY, tabW, tabH), LocalizationManager.L("mod_almanac_allies", "ALLIES (SAMURAI)", "MUTTEFIKLER (SAMURAYLAR)")))
            {
                mSelectedCategory = 1;
                mSelectedIndex = 0;
                mListScrollPos = Vector2.zero;
                mDetailScrollPos = Vector2.zero;
            }
            GUI.color = Color.white;

            // Main content area
            float contentY = tabY + tabH + 12f;
            float contentH = height - (contentY - y) - 32f;
            float listW = Mathf.Min(270f, width * 0.34f);

            List<CharacterInfo> currentList = mSelectedCategory == 0 ? sEnemies : sAllies;
            if (mSelectedIndex >= currentList.Count)
            {
                mSelectedIndex = 0;
            }

            Rect listRect = new Rect(x + 16f, contentY, listW, contentH);
            float detailX = x + listW + 28f;
            float detailW = width - listW - 44f;
            Rect detailRect = new Rect(detailX, contentY, detailW, contentH);

            HandleTouchScroll(listRect, detailRect, scale);

            // 1. Left List (Scrollable)
            GUI.color = new Color(0.04f, 0.04f, 0.06f, 0.8f);
            GUI.DrawTexture(listRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            Rect viewRect = new Rect(0f, 0f, listW - 22f, currentList.Count * 44f);
            mListScrollPos = GUI.BeginScrollView(listRect, mListScrollPos, viewRect);

            GUIStyle itemBtnStyle = new GUIStyle(GUI.skin.button);
            itemBtnStyle.alignment = TextAnchor.MiddleLeft;
            itemBtnStyle.richText = true;
            itemBtnStyle.fontSize = 13;

            for (int i = 0; i < currentList.Count; i++)
            {
                bool isSelected = (i == mSelectedIndex);
                GUI.color = isSelected ? new Color(0.9f, 0.75f, 0.2f, 1f) : (i % 2 == 0 ? new Color(0.22f, 0.22f, 0.26f, 0.9f) : new Color(0.18f, 0.18f, 0.22f, 0.9f));

                string prefix = mSelectedCategory == 0 ? LocalizationManager.T("[E] ", "[D] ") : LocalizationManager.T("[A] ", "[M] ");
                string label = prefix + currentList[i].GetName();

                if (GUI.Button(new Rect(4f, i * 44f + 2f, listW - 26f, 40f), label, itemBtnStyle))
                {
                    mSelectedIndex = i;
                    mDetailScrollPos = Vector2.zero;
                }
            }
            GUI.EndScrollView();
            GUI.color = Color.white;

            // 2. Right Detail Card
            GUI.color = new Color(0.04f, 0.05f, 0.07f, 0.85f);
            GUI.DrawTexture(detailRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            CharacterInfo info = currentList[mSelectedIndex];

            GUILayout.BeginArea(new Rect(detailX + 16f, contentY + 12f, detailW - 32f, contentH - 24f));
            mDetailScrollPos = GUILayout.BeginScrollView(mDetailScrollPos);

            // Header: Name & Role Badge
            GUILayout.BeginHorizontal();
            string roleColor = mSelectedCategory == 0 ? "#FF5555" : "#55CCFF";
            GUILayout.Label(string.Format("<size=20><b><color=#FFEE55>{0}</color></b></size>", info.GetName()), new GUIStyle(GUI.skin.label) { richText = true });
            GUILayout.FlexibleSpace();
            GUILayout.Label(string.Format("<size=14><b><color={0}>[{1}]</color></b></size>", roleColor, info.GetRole()), new GUIStyle(GUI.skin.label) { richText = true, alignment = TextAnchor.MiddleRight });
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            DrawHorizontalLine(Color.gray);
            GUILayout.Space(8f);

            // Stat Grid
            DrawStatRow(LocalizationManager.T("Health (HP):", "Can (HP):"), info.health.ToString(), info.health / 5000f, new Color(0.3f, 0.85f, 0.3f));
            DrawStatRow(LocalizationManager.T("Melee Dmg:", "Yakin Hasar:"), info.meleeDmg > 0 ? info.meleeDmg.ToString() : "-", info.meleeDmg / 450f, new Color(0.95f, 0.35f, 0.35f));
            DrawStatRow(LocalizationManager.T("Ranged Dmg:", "Menzilli Hasar:"), info.rangedDmg > 0 ? info.rangedDmg.ToString() : "-", info.rangedDmg / 250f, new Color(0.95f, 0.65f, 0.25f));
            DrawStatRow(LocalizationManager.T("Move Speed:", "Hareket Hizi:"), info.speed.ToString("0.0"), info.speed / 100f, new Color(0.35f, 0.75f, 0.95f));
            DrawStatRow(LocalizationManager.T("Attack Rate:", "Saldiri Araligi:"), info.attackSpeed.ToString("0.0") + "s", 1.0f - Mathf.Clamp01(info.attackSpeed / 3.0f), new Color(0.8f, 0.4f, 0.9f));
            DrawStatRow(LocalizationManager.T("Range:", "Menzil:"), info.range.ToString("0.0"), info.range / 400f, new Color(0.9f, 0.9f, 0.4f));

            GUILayout.Space(12f);
            DrawHorizontalLine(new Color(0.3f, 0.3f, 0.35f));
            GUILayout.Space(8f);

            // Description Section
            GUILayout.Label(LocalizationManager.T("<b><color=#FFDD66>INFO & LORE:</color></b>", "<b><color=#FFDD66>BILGI & LORE:</color></b>"), new GUIStyle(GUI.skin.label) { richText = true, fontSize = 14 });
            GUIStyle textStyle = new GUIStyle(GUI.skin.label);
            textStyle.richText = true;
            textStyle.wordWrap = true;
            textStyle.fontSize = 13;
            GUILayout.Label("<color=#DDDDDD>" + info.GetDesc() + "</color>", textStyle);

            GUILayout.Space(10f);

            // Strategy Tips Section
            GUILayout.Label(LocalizationManager.T("<b><color=#55FFAA>BATTLE STRATEGY & TIPS:</color></b>", "<b><color=#55FFAA>SAVAS STRATEJISI & TAKTIK:</color></b>"), new GUIStyle(GUI.skin.label) { richText = true, fontSize = 14 });
            GUILayout.Label("<color=#BBEEFF>" + info.GetTip() + "</color>", textStyle);

            GUILayout.EndScrollView();
            GUILayout.EndArea();

            // Footer hint
            string footerText = SVZDeviceCompatibility.IsMobileOrTablet
                ? LocalizationManager.T("<color=#888888>Tap [GUIDE] or [X] at the top right to close/open.</color>", "<color=#888888>Sag ustteki [REHBER] veya [X] butonu ile acip kapatabilirsiniz.</color>")
                : LocalizationManager.T("<color=#888888>Press [B] anytime to open or close this guide.</color>", "<color=#888888>[B] Tusuna basarak bu rehberi istediginiz zaman acip kapatabilirsiniz.</color>");
            GUI.Label(new Rect(x + 16f, y + height - 26f, width - 32f, 20f), footerText, new GUIStyle(GUI.skin.label) { richText = true, alignment = TextAnchor.MiddleCenter, fontSize = 11 });
        }
        finally
        {
            GUI.skin.label.font = oldLblFont;
            GUI.skin.button.font = oldBtnFont;
            GUI.matrix = prevMatrix;
        }
    }

    private void DrawStatRow(string label, string val, float ratio, Color barColor)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(string.Format("<b>{0}</b>", label), new GUIStyle(GUI.skin.label) { richText = true, fixedWidth = 120f });
        GUILayout.Label(string.Format("<b><color=#FFFFFF>{0}</color></b>", val), new GUIStyle(GUI.skin.label) { richText = true, fixedWidth = 60f });

        // Bar
        Rect barRect = GUILayoutUtility.GetRect(160f, 14f, GUILayout.ExpandWidth(true));
        GUI.color = new Color(0.12f, 0.12f, 0.15f, 1f);
        GUI.DrawTexture(barRect, Texture2D.whiteTexture);

        GUI.color = barColor;
        float fillW = Mathf.Clamp01(ratio) * barRect.width;
        GUI.DrawTexture(new Rect(barRect.x, barRect.y, fillW, barRect.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUILayout.EndHorizontal();
        GUILayout.Space(2f);
    }

    private void DrawHorizontalLine(Color c)
    {
        Rect r = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(1f), GUILayout.ExpandWidth(true));
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}
