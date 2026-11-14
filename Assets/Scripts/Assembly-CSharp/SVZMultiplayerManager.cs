using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// "Next-Level" Comprehensive Multiplayer Gameplay Controller for Samurai vs Zombies Defense
/// Coordinates 1v1 PvP, Lane Battles, Gate HP synchronization, Unit Spawns, Mutators, Perks, and Game Modes.
/// Supports Cross-Faction Mixed Armies (Samurai & Zombies), Symmetrical Gate Breaches, Free-Cam RTS, and Side Selection.
/// </summary>
public class SVZMultiplayerManager : MonoBehaviour
{
    private static SVZMultiplayerManager sInstance;

    public enum MultiplayerGameMode
    {
        HeroAndArmy, // 🗡️ Kahramanlı Meydan Savaşı: Kahraman sahada + birlikler
        ArmyOnly     // 🎖️ Taktiksel Ordu Savaşı (No Hero RTS): Yalnızca birlikler çarpışır!
    }

    public enum BattlefieldSide
    {
        LeftGate,   // 🏰 Sol Kale (Samuray Üssü)
        RightGate   // 🧟 Sağ Kale (Zombi Kapısı)
    }

    public enum UnitFaction
    {
        Samurai,
        Zombie
    }

    public enum HeroType
    {
        Samurai, // 🗡️ Samuray Kahramanı (PFHero01)
        Zombie   // 💀 Zombi Kahramanı (PFZombieHero)
    }

    public enum UnitTier
    {
        Core = 1,       // ⚔️ Temel Birlikler (Piyadeler, Okçular, Zombiler)
        Elite = 2,      // 🛡️ Elit Birlikler (Zırhlılar, Ağır Süvariler, Şifacılar)
        Legendary = 3   // 👑 Efsanevi / Boss Birlikler (Maksimum 1 Adet!)
    }

    [System.Serializable]
    public class UnitMeta
    {
        public string id;
        public string displayName;
        public string role;
        public int cost;
        public string description;
        public int hp;
        public int damage;
        public int speed;
        public int range;
        public UnitFaction faction;
        public UnitTier tier { get { return GetUnitTier(id); } }

        public UnitMeta(string id, string name, string role, int cost, string desc, int hp, int dmg, int spd, int rng, UnitFaction faction = UnitFaction.Samurai)
        {
            this.id = id;
            this.displayName = name;
            this.role = role;
            this.cost = cost;
            this.description = desc;
            this.hp = hp;
            this.damage = dmg;
            this.speed = spd;
            this.range = rng;
            this.faction = faction;
        }
    }

    [System.Serializable]
    public class SpellMeta
    {
        public string id;
        public string displayName;
        public string role;
        public float cooldown;
        public string description;
        public string iconPath;
        public UnitFaction faction;

        public SpellMeta(string id, string name, string role, float cd, string desc, string icon, UnitFaction faction)
        {
            this.id = id;
            this.displayName = name;
            this.role = role;
            this.cooldown = cd;
            this.description = desc;
            this.iconPath = icon;
            this.faction = faction;
        }
    }

    // Match & Mode States
    private bool mIsActive = false;
    private bool mIsSamePC = false;
    private bool mIsMatchOver = false;
    private int mWinner = 0; // 0 = None, 1 = P1, 2 = P2
    private MultiplayerGameMode mCurrentGameMode = MultiplayerGameMode.HeroAndArmy;
    private BattlefieldSide mP1Side = BattlefieldSide.LeftGate;
    private HeroType mP1HeroType = HeroType.Samurai;
    private HeroType mP2HeroType = HeroType.Zombie;
    private string mPlayer1Name = "1. Oyuncu";
    private string mPlayer2Name = "2. Oyuncu";
    private bool mBlindPick = false;
    private bool mAllowBackAttack = false;
    private bool mIsP1Ready = true;
    private bool mIsP2Ready = false;
    public bool AllowBackAttack { get { return mAllowBackAttack; } set { mAllowBackAttack = value; } }

    // Arena Selection
    public static readonly string[] sArenaScenes = new string[]
    {
        "InGame",
        "InGame_winterday",
        "InGame_winternight",
        "InGame_hell",
        "InGame_hell_purple",
        "InGame_underworld",
        "InGame_evil",
        "InGame_fallday",
        "InGame_summernight",
        "InGame_springday"
    };

    public static readonly string[] sArenaNames = new string[]
    {
        "🌸 Klasik Köy (Gündüz)",
        "❄️ Karlı Köy (Kış Gündüzü)",
        "⛄ Karlı Gece (Kış Gecesi)",
        "🔥 Cehennem Vadisi",
        "🔮 Mor Alev Cehennemi",
        "💀 Yeraltı Dünyası",
        "👹 Şeytani İblis Köyü",
        "🍂 Sonbahar Köyü",
        "🌙 Yaz Gece Bahçesi",
        "🌸 Kiraz Baharı"
    };

    private string mSelectedArena = "InGame";

    // Match Mutators
    private float mGateMaxHealthSetting = 5000f;
    private float mEnergySpeed = 1.0f; // 1.0f (Normal), 1.5f (Hızlı), 2.5f (Çılgın Turbo)

    // Commander Perks / Talents
    private int mP1Perk = 0; // 0 = Çelik Zırh (+%20 Can), 1 = Disiplin (+%20 Liderlik), 2 = Ateşli Oklar (+%15 Hasar)
    private int mP2Perk = 0; // 0 = Zombi Vebası (+%20 Can), 1 = Vahşi Hücum (+%25 Hız), 2 = Beyin Şöleni (+%25 Beyin)

    public static readonly string[] sP1PerkNames = new string[]
    {
        "🛡️ Çelik Zırh (+%20 Birlik Canı)",
        "⚡ Samuray Disiplini (+%20 Liderlik)",
        "🏹 Ateşli Oklar (+%15 Hasar)"
    };

    public static readonly string[] sP2PerkNames = new string[]
    {
        "☣️ Veba Dayanıklılığı (+%20 Birlik Canı)",
        "🐎 Vahşi Hücum (+%25 Hareket Hızı)",
        "🧠 Beyin Şöleni (+%25 Beyin Üretimi)"
    };

    // Gates Health (1v1 PvP)
    private float mGate1Health = 5000f;
    private float mGate1MaxHealth = 5000f;
    private float mGate2Health = 5000f;
    private float mGate2MaxHealth = 5000f;
    private bool mGateInitialized = false;

    // Troop Levels (Seviyeler 1 - 10, Her iki taraf için eşit)
    private int mP1TroopLevel = 5;
    private int mP2TroopLevel = 5;
    private int mPendingTroopLevelProposal = 0; // 0 = Yok, 1 / 5 / 10 = Teklif edilen seviye
    private int mPendingTroopLevelProposer = 0; // 1 = P1 teklif etti, 2 = P2 teklif etti

    // Player 2 Resource / Leadership (Brains)
    private float mPlayer2Leadership = 35f;
    private float mPlayer2MaxLeadership = 100f;
    private float mPlayer2LeadershipRegen = 4.5f;

    // Decks (5 units in HeroAndArmy, 10 units in ArmyOnly) & Spells (2 spells each)
    private List<string> mP1Deck = new List<string> { "Farmer", "SamuraiArcher", "Samurai", "SpearSamurai", "Rifleman" };
    private List<string> mP2Deck = new List<string> { "ZombieSpearman", "HeavyArcher", "ZombieWarhammer", "Oni", "EvilNobunaga" };
    private List<string> mP1FiveUnitDeck = new List<string> { "Farmer", "SamuraiArcher", "Samurai", "SpearSamurai", "Rifleman" };
    private List<string> mP2FiveUnitDeck = new List<string> { "ZombieSpearman", "HeavyArcher", "ZombieWarhammer", "Oni", "EvilNobunaga" };
    private List<string> mP1TenUnitDeck = new List<string> { "Farmer", "NinjaGirl", "Samurai", "SamuraiArcher", "SpearSamurai", "Rifleman", "IceArcher", "HeavySamurai", "Priest", "Nobunaga" };
    private List<string> mP2TenUnitDeck = new List<string> { "Zombie", "ZombieMelee", "ZombieAxe", "ZombieArcher", "ZombieSpearman", "HeavyArcher", "ZombieWarhammer", "Oni", "ZombieHorseman", "EvilNobunaga" };
    private List<string> mP1Spells = new List<string> { "GiantWave", "Lethargy" };
    private List<string> mP2Spells = new List<string> { "DragonAttack", "GraveHands" };

    // --- ALL 13 SPELLS / ABILITIES CATALOG ---
    public static readonly List<SpellMeta> sAllSpells = new List<SpellMeta>
    {
        // Samurai Spells (7)
        new SpellMeta("KatanaSlash", "Katana Darbesi", "⚔️ Yakın Alan Hasarı", 10f, "Ön saftaki düşmanları biçen ölümcül kılıç dalgası.", "Sprites/Icons/ab_katana_slash", UnitFaction.Samurai),
        new SpellMeta("SummonLightning", "Yıldırım Çağrısı", "⚡ Gökyüzü Şimşeği", 15f, "Düşman hattına gökten yıkıcı şimşekler indirir.", "Sprites/Icons/ab_lightning_strike", UnitFaction.Samurai),
        new SpellMeta("GroundShock", "Deprem Darbesi", "🌋 Sarsıntı & Geri İtme", 15f, "Yeri sarsarak düşmanları sersemletir ve geri savurur.", "Sprites/Icons/ab_burst_slam", UnitFaction.Samurai),
        new SpellMeta("Lethargy", "Yavaşlatma Laneti", "🐌 Tüm Sahayı Yavaşlat", 23f, "Sahadaki tüm düşman birliklerini ağır çekime sokar.", "Sprites/Icons/ab_lethargy", UnitFaction.Samurai),
        new SpellMeta("DivineIntervention", "İlahi Müdahale", "✨ Destek & Birlik Şifası", 25f, "Gökten takviye asker indirir ve dost birlikleri iyileştirir.", "Sprites/Icons/ab_divine_intervention", UnitFaction.Samurai),
        new SpellMeta("SummonTornadoes", "Kasırga Hortumu", "🌪️ İlerleyen Hortum", 20f, "Koridor boyunca ilerleyip düşmanları biçen kasırga salar.", "Sprites/Icons/ab_katana_whirlwind", UnitFaction.Samurai),
        new SpellMeta("GiantWave", "Süvari Hücumu", "🐎 Dev Süvari Dalgası", 45f, "Tüm koridoru boydan boya ezip geçen dev süvari ordusu.", "Sprites/Icons/ab_troop_trample", UnitFaction.Samurai),

        // Zombie Spells (6)
        new SpellMeta("LifeSteal", "Can Çalma", "🩸 Ruh Sömürüsü", 10f, "Düşmanların yaşam enerjisini çekip dost saflara aktarır.", "Sprites/Icons/ab_enemy_lifesteal", UnitFaction.Zombie),
        new SpellMeta("GraveHands", "Mezar Elleri", "✋ Yeraltı Pençeleri", 15f, "Yerden çıkan iskelet eller düşmanları ezer ve mıhlar.", "Sprites/Icons/ab_enemy_grave_hands", UnitFaction.Zombie),
        new SpellMeta("LethargyZM", "Karanlık Yavaşlatma", "☠️ Zehirli Yavaşlatma", 23f, "Düşman ordusunu karanlık büyüyle zehirler ve yavaşlatır.", "Sprites/Icons/ab_enemy_lethargy", UnitFaction.Zombie),
        new SpellMeta("NightOfTheDead", "Ölüler Gecesi", "🌙 Karanlık Çılgınlık", 23f, "Dost birliklerin hızını artırır ve yaralarını sarar.", "Sprites/Icons/ab_enemy_night", UnitFaction.Zombie),
        new SpellMeta("RaiseDead", "Ölüleri Dirilt", "🪦 Mezar Çağrısı", 25f, "Savaş alanının ortasında yerden zombi savaşçılar diriltir.", "Sprites/Icons/ab_enemy_raise_the_dead", UnitFaction.Zombie),
        new SpellMeta("DragonAttack", "Ejderha Saldırısı", "🐉 Cehennem Ejderhası", 45f, "Tüm koridoru alevleriyle yakıp kül eden dev ejderha.", "Sprites/Icons/ab_enemy_dragon_damage", UnitFaction.Zombie)
    };

    // --- FULL SAMURAI ROSTER (18 UNITS - Exact 1:1 with registry/helpers/) ---
    public static readonly List<UnitMeta> sUnitMetaP1 = new List<UnitMeta>
    {
        new UnitMeta("Farmer", "Köylü", "🗡️ Ucuz Piyade", 5, "Hızlı üretilir, ön safta yem olur.", 40, 15, 75, 10, UnitFaction.Samurai),
        new UnitMeta("NinjaGirl", "Gölge Ninja", "⚡ Hızlı Suikastçı", 5, "Yüksek hareket hızı ve seri darbeler.", 70, 70, 100, 20, UnitFaction.Samurai),
        new UnitMeta("Samurai", "Kılıçlı Samuray", "⚔️ Dengeli Savaşçı", 10, "Standart dayanıklı yakın dövüşçü.", 85, 50, 70, 15, UnitFaction.Samurai),
        new UnitMeta("SamuraiArcher", "Samuray Okçu", "🏹 Menzilli Destek", 10, "Uzaktan güvenli ok yağdırır.", 45, 35, 70, 95, UnitFaction.Samurai),
        new UnitMeta("Rifleman", "Tüfekçi Asker", "💥 Okçu Geliştirici (Max 1)", 10, "Sahadaki okçuları tüfekçiye dönüştürür (Sınır: 1).", 55, 85, 55, 90, UnitFaction.Samurai),
        new UnitMeta("Swordsmith", "Kılıç Ustası", "⚒️ Köylü Geliştirici (Max 1)", 10, "Köylüleri süper köylüye dönüştürür (Sınır: 1).", 85, 40, 65, 25, UnitFaction.Samurai),
        new UnitMeta("FarmerOnStilts", "Uzunbacak Köylü", "🦗 Çevik Atlama", 15, "Uzun bacaklarıyla safları aşar.", 45, 20, 85, 15, UnitFaction.Samurai),
        new UnitMeta("SpearSamurai", "Mızraklı Samuray", "🛡️ Anti-Süvari", 15, "Mızrağıyla atlıları ve canavarları durdurur.", 90, 45, 65, 30, UnitFaction.Samurai),
        new UnitMeta("IceArcher", "Buz Okçusu", "❄️ Dondurucu Menzil", 20, "Düşmanları donduran buz okları atar.", 50, 40, 65, 95, UnitFaction.Samurai),
        new UnitMeta("Handmaiden", "Hizmetçi Kız", "🌸 Hızlı Destek", 20, "Müttefiklerin moralini ve gücünü artırır.", 50, 15, 70, 30, UnitFaction.Samurai),
        new UnitMeta("HeavySamurai", "Ağır Samuray", "🛡️ Zırhlı Savaşçı", 30, "Ağır zırhıyla darbeleri karşılar.", 110, 55, 60, 15, UnitFaction.Samurai),
        new UnitMeta("Priest", "Şinto Rahibi", "✨ Şifacı Destek (Max 3)", 35, "Dost birliklerin canını yeniler (Sınır: 3).", 60, 10, 60, 50, UnitFaction.Samurai),
        new UnitMeta("Shieldsamurai", "Kalkanlı Samuray", "🛡️ Mega Tank (Max 5)", 40, "Devasa canı ve kalkanıyla hasarı emer (Sınır: 5).", 160, 45, 50, 15, UnitFaction.Samurai),
        new UnitMeta("SpearHorseman", "Mızraklı Atlı", "🐎 Ağır Süvari", 45, "Hızlı hücum eder, düşman hattını yarar.", 110, 80, 95, 35, UnitFaction.Samurai),
        new UnitMeta("Nobunaga", "Oda Nobunaga", "👑 Efsanevi Hükümdar (Max 3)", 55, "Alandaki tüm birlikleri coşturup güçlendirir (Sınır: 3).", 140, 95, 75, 40, UnitFaction.Samurai),
        new UnitMeta("ShieldSamuraiGreen", "Yeşil Kalkanlı", "💚 Zümrüt Muhafız (Max 5)", 55, "Zümrüt kalkanıyla kırılmaz bir hat kurar (Sınır: 5).", 180, 50, 48, 15, UnitFaction.Samurai),
        new UnitMeta("Takeda", "Shingen Takeda", "🏇 Efsane Süvari (Max 3)", 65, "At sırtında korku salan dev komutan (Sınır: 3).", 150, 95, 90, 40, UnitFaction.Samurai),
        new UnitMeta("ShieldSamuraiIce", "Buz Kalkanlı", "🧊 Buz Duvarı (Max 3)", 70, "Darbe aldığında saldıranları dondurur (Sınır: 3).", 200, 45, 45, 15, UnitFaction.Samurai)
    };

    // --- FULL ZOMBIE ROSTER (31 UNITS - Exact 1:1 with registry/helpers_zm/) ---
    public static readonly List<UnitMeta> sUnitMetaP2 = new List<UnitMeta>
    {
        new UnitMeta("CrawlingTorso", "Sürünen Gövde", "🧟 Ucuz Yem", 2, "Çok ucuza çağrılan hızlı sürünen zombi.", 30, 10, 80, 10, UnitFaction.Zombie),
        new UnitMeta("Zombie", "Yürüyen Zombi", "🧟 Temel Zombi", 3, "Sürü halinde saldıran temel ölümsüz.", 40, 15, 70, 10, UnitFaction.Zombie),
        new UnitMeta("ZombieMelee", "Kılıçlı Zombi", "⚔️ Zombi Savaşçı", 5, "Kılıcıyla dengeli yakın dövüş yapan zombi.", 65, 25, 75, 15, UnitFaction.Zombie),
        new UnitMeta("Chochinobake", "Alevli Fener", "🏮 Patlayan Ruh", 5, "Hedefe ulaştığında patlayarak alan hasarı verir.", 45, 55, 85, 20, UnitFaction.Zombie),
        new UnitMeta("EvilFarmer", "Lanetli Köylü", "🗡️ Zombi Köylü", 8, "Karanlık tarafa geçmiş hızlı köylü savaşçı.", 55, 22, 80, 15, UnitFaction.Zombie),
        new UnitMeta("ZombieAxe", "Baltalı Zombi", "🪓 Hızlı Hücumcu", 10, "Baltasıyla seri ve sert darbeler vurur.", 75, 35, 75, 15, UnitFaction.Zombie),
        new UnitMeta("ZombieArcher", "Zombi Okçu", "🏹 Menzilli Zombi", 10, "Uzaktan zehirli oklar fırlatır.", 45, 35, 70, 90, UnitFaction.Zombie),
        new UnitMeta("SmallOni", "Küçük Oni", "👹 Çevik İblis", 10, "Hızlı koşan ve pençeleriyle saldıran küçük iblis.", 80, 38, 85, 15, UnitFaction.Zombie),
        new UnitMeta("GhostRonin", "Hayalet Ronin", "👻 Ruh Kılıççısı (Max 1)", 10, "Kılıcıyla ön safta çarpışan hayalet samuray (Sınır: 1).", 95, 45, 75, 20, UnitFaction.Zombie),
        new UnitMeta("EvilNinja", "Kara Ninja", "👤 Gölge Katili", 10, "Hem yakın dövüş hem yıldız fırlatan zombi ninja.", 80, 75, 100, 50, UnitFaction.Zombie),
        new UnitMeta("ZombieDrummer", "Taiko Davulcusu", "🥁 Güçlendirici (Max 1)", 10, "Davuluyla dost birliklerin hasarını artırır (Sınır: 1).", 85, 15, 60, 80, UnitFaction.Zombie),
        new UnitMeta("ZombieSpearman", "Mızraklı Zombi", "🛡️ Anti-Süvari", 15, "Uzun mızrağıyla hücumları ve süvarileri durdurur.", 85, 42, 70, 35, UnitFaction.Zombie),
        new UnitMeta("Tengu", "Tengu Savaşçısı", "🦅 İblis Savaşçı (Max 3)", 15, "Yüksek geri itme gücüne sahip Tengu (Sınır: 3, 10sn süre).", 90, 55, 80, 25, UnitFaction.Zombie),
        new UnitMeta("HeavyArcher", "Ağır Zombi Okçu", "🏹 Elit Menzilli", 20, "Çelik uçlu ağır oklarla yüksek menzilli hasar verir.", 70, 55, 65, 95, UnitFaction.Zombie),
        new UnitMeta("ZombieWarhammer", "Balyozlu Zombi", "🔨 Zırh Kırıcı", 20, "Ağır balyozuyla kalkanlı ve zırhlı hedefleri ezer.", 100, 60, 60, 20, UnitFaction.Zombie),
        new UnitMeta("Komusou", "Flütçü Büyücü", "🎋 Destek Büyücüsü (Max 1)", 20, "Flütüyle dost ordunun saldırı gücünü artırır (Sınır: 1).", 85, 30, 65, 60, UnitFaction.Zombie),
        new UnitMeta("Wanyudo", "Ahşap Çark", "🎡 Ağır Çark", 25, "Dönerek ilerleyen ve safları ezen dev tekerlek.", 130, 65, 60, 20, UnitFaction.Zombie),
        new UnitMeta("Onibaba", "Onibaba Cadısı", "🧙‍♀️ Zombi Şifacı", 30, "Karanlık büyüsüyle yaralı zombileri iyileştirir.", 95, 45, 70, 60, UnitFaction.Zombie),
        new UnitMeta("ZombieRifleman", "Zombi Tüfekçi", "💥 Barutlu Nişancı", 30, "Tüfeğiyle uzaktan zırh delici mermiler ateşler.", 75, 85, 60, 90, UnitFaction.Zombie),
        new UnitMeta("EvilHeavySamurai", "Şeytani Panzer", "🛡️ Zırhlı Canavar", 30, "Ağır zırhlı ve yüksek dayanıklılığa sahip kara samuray.", 145, 70, 55, 20, UnitFaction.Zombie),
        new UnitMeta("Oni", "Kırmızı Oni", "👹 Ezici Dev Tank", 35, "Devasa sopasıyla geniş alana yıkıcı hasar saçar.", 155, 85, 55, 25, UnitFaction.Zombie),
        new UnitMeta("EvilIceArcher", "Karanlık Buz Okçusu", "❄️ Dondurucu Nişancı", 35, "Attığı buz oklarıyla düşmanları dondurur ve yavaşlatır.", 75, 65, 65, 95, UnitFaction.Zombie),
        new UnitMeta("WanyudoFire", "Ateşli Çark", "🔥 Cehennem Çarkı", 40, "Önüne gelen her şeyi yakan alevli cehennem çarkı.", 155, 90, 65, 25, UnitFaction.Zombie),
        new UnitMeta("Necromancer", "Ölüçağıran", "💀 Ruh Ustası (Max 1)", 40, "Sürekli yerden kılıçlı zombi savaşçılar diriltir (Sınır: 1).", 140, 60, 55, 70, UnitFaction.Zombie),
        new UnitMeta("ZombieHorseman", "Zombi Atlısı", "🐎 Zombi Süvari", 50, "Dörtnala hücum ederek düşman hattını yarar.", 130, 85, 95, 35, UnitFaction.Zombie),
        new UnitMeta("WanyudoMetal", "Demir Çark", "⚙️ Zırhlı Dev Çark", 55, "Çelik zırhla kaplı kırılmaz ölüm tekerleği.", 185, 95, 60, 25, UnitFaction.Zombie),
        new UnitMeta("TenguAir", "Tengu Air", "🦅 Uçan Tengu (Max 3)", 55, "Havadan uçarak saldırır ve yakın dövüşten etkilenmez (Sınır: 3, 10sn).", 135, 75, 85, 30, UnitFaction.Zombie),
        new UnitMeta("Jurogumo", "Jurogumo", "🕷️ Örümcek İblis", 70, "Öldüğünde yavru örümcekler çıkaran dev örümcek kadın.", 165, 85, 65, 35, UnitFaction.Zombie),
        new UnitMeta("EvilNobunaga", "Karanlık Nobunaga", "👑 Şeytani Hükümdar (Max 2)", 75, "Cehennem ateşiyle yanan kara hükümdar (Sınır: 2).", 175, 100, 70, 35, UnitFaction.Zombie),
        new UnitMeta("EvilTakeda", "Karanlık Takeda", "🏇 Kara Süvari (Max 2)", 85, "Karanlık zırhlı efsanevi süvari komutanı (Sınır: 2).", 180, 105, 90, 40, UnitFaction.Zombie),
        new UnitMeta("Shogun", "Karanlık Şogun", "⚔️ Lanetli Lider (Max 1)", 100, "Zombi ordusunun en güçlü başkomutanı (Sınır: 1, 30sn süre).", 200, 115, 70, 35, UnitFaction.Zombie)
    };

    public static bool IsHiddenOrSubHelper(string unitId)
    {
        if (string.IsNullOrEmpty(unitId)) return false;
        switch (unitId)
        {
            case "SuperFarmer":
            case "SuperNinja":
            case "SamuraiRifleman":
            case "Oni_boss":
            case "CharmOni":
            case "CharmWanyudoFire":
            case "CharmWanyudoMetal":
            case "CharmJurogumo":
            case "JurogumoBaby":
            case "ZombieMelee_Summon":
                return true;
            default:
                return false;
        }
    }

    public static string NormalizeUnitIdForCap(string unitId)
    {
        if (string.IsNullOrEmpty(unitId)) return string.Empty;
        switch (unitId)
        {
            case "Evil_Nobunaga": return "EvilNobunaga";
            case "Evil_Takeda": return "EvilTakeda";
            case "Necromancer_Boss": return "Necromancer";
            case "Taiko_Drummer": return "ZombieDrummer";
            case "Tengu_air": return "TenguAir";
            default: return unitId;
        }
    }

    public static int GetUniversalMaxAliveCap(string unitId)
    {
        if (DisableEliteLimitsStatic) return 999;
        switch (NormalizeUnitIdForCap(unitId))
        {
            case "Swordsmith":
            case "Rifleman":
            case "Shogun":
            case "Necromancer":
            case "Komusou":
            case "ZombieDrummer":
            case "GhostRonin":
                return 1;

            case "EvilNobunaga":
            case "EvilTakeda":
                return 2;

            case "Takeda":
            case "Nobunaga":
            case "ShieldSamuraiIce":
            case "Tengu":
            case "TenguAir":
            case "Priest":
                return 3;

            case "ShieldSamuraiGreen":
            case "Shieldsamurai":
                return 5;

            default:
                return 999;
        }
    }

    public static int CountAliveUnitsOfType(string unitId, bool isEnemyTeam)
    {
        string targetNorm = NormalizeUnitIdForCap(unitId);
        if (string.IsNullOrEmpty(targetNorm)) return 0;
        CharactersManager charMgr = WeakGlobalInstance<CharactersManager>.instance;
        if (charMgr == null || charMgr.allAlive == null) return 0;
        int count = 0;
        List<Character> list = charMgr.allAlive;
        for (int i = 0; i < list.Count; i++)
        {
            Character c = list[i];
            if (c != null && c.health > 0f && !c.isBase && !c.isPlayer && c.isEnemy == isEnemyTeam)
            {
                if (NormalizeUnitIdForCap(c.uniqueID) == targetNorm)
                {
                    count++;
                }
            }
        }
        return count;
    }

    public static bool CanTeamSpawnUnit(string unitId, bool isEnemyTeam)
    {
        if (DisableEliteLimitsStatic) return true;
        int cap = GetUniversalMaxAliveCap(unitId);
        if (cap >= 999) return true;
        return CountAliveUnitsOfType(unitId, isEnemyTeam) < cap;
    }

    public static float GetUniversalSpawnCooldown(string unitId, int spawnCountAfterThis, float defaultCooldown)
    {
        switch (NormalizeUnitIdForCap(unitId))
        {
            case "Tengu":
            case "TenguAir":
                return 10f;
            case "Shogun":
                return 30f;
            case "Priest":
                return (spawnCountAfterThis > 0 && (spawnCountAfterThis % 3) == 0) ? 20f : 5f;
            default:
                return defaultCooldown;
        }
    }

    public static readonly List<UnitMeta> sAllUnitMetas = new List<UnitMeta>();
    private static readonly Dictionary<string, int> sUnitCosts = new Dictionary<string, int>();
    private static readonly Dictionary<string, UnitMeta> sUnitMetaLookup = new Dictionary<string, UnitMeta>();
    private static readonly Dictionary<string, SpellMeta> sSpellMetaLookup = new Dictionary<string, SpellMeta>();
    private static readonly HashSet<string> sSamuraiIds = new HashSet<string>();
    private static readonly HashSet<string> sZombieIds = new HashSet<string>();
    private static readonly Dictionary<string, AudioClip> sSfxCache = new Dictionary<string, AudioClip>();

    static SVZMultiplayerManager()
    {
        sAllUnitMetas.AddRange(sUnitMetaP1);
        sAllUnitMetas.AddRange(sUnitMetaP2);

        foreach (UnitMeta u in sUnitMetaP1)
        {
            sUnitCosts[u.id] = u.cost;
            sUnitMetaLookup[u.id] = u;
            sSamuraiIds.Add(u.id);
        }
        foreach (UnitMeta u in sUnitMetaP2)
        {
            sUnitCosts[u.id] = u.cost;
            sUnitMetaLookup[u.id] = u;
            sZombieIds.Add(u.id);
        }
        foreach (SpellMeta sp in sAllSpells)
        {
            sSpellMetaLookup[sp.id] = sp;
        }
    }

    public static UnitTier GetUnitTier(string unitId)
    {
        switch (unitId)
        {
            // --- 👑 EFSANEVİ / BOSS BİRLİKLER (Destede Maksimum 1 Adet!) ---
            case "Nobunaga":
            case "Takeda":
            case "ShieldSamuraiIce":
            case "ShieldSamuraiGreen":
            case "SpearHorseman":
            case "Necromancer":
            case "EvilNobunaga":
            case "EvilTakeda":
            case "Shogun":
            case "Jurogumo":
            case "TenguAir":
            case "WanyudoMetal":
            case "WanyudoFire":
                return UnitTier.Legendary;

            // --- 🛡️ ELİT BİRLİKLER (Destede Maksimum 2 Adet!) ---
            case "Shieldsamurai":
            case "HeavySamurai":
            case "Priest":
            case "IceArcher":
            case "Handmaiden":
            case "Oni":
            case "EvilHeavySamurai":
            case "EvilIceArcher":
            case "ZombieRifleman":
            case "ZombieHorseman":
            case "Wanyudo":
            case "Onibaba":
            case "HeavyArcher":
            case "ZombieWarhammer":
            case "Komusou":
                return UnitTier.Elite;

            // --- ⚔️ TEMEL BİRLİKLER (En az 2 Adet!) ---
            default:
                return UnitTier.Core;
        }
    }

    public static string GetTierBadge(UnitTier tier)
    {
        switch (tier)
        {
            case UnitTier.Legendary: return "<color=#FFD700><b>[👑 EFSANEVİ]</b></color>";
            case UnitTier.Elite: return "<color=#66CCFF><b>[🛡️ ELİT]</b></color>";
            default: return "<color=#88FF88><b>[⚔️ TEMEL]</b></color>";
        }
    }

    public static void GetDeckTierCounts(List<string> deck, out int legendaryCount, out int eliteCount, out int coreCount)
    {
        legendaryCount = 0;
        eliteCount = 0;
        coreCount = 0;
        if (deck == null) return;
        for (int i = 0; i < deck.Count; i++)
        {
            UnitTier t = GetUnitTier(deck[i]);
            if (t == UnitTier.Legendary) legendaryCount++;
            else if (t == UnitTier.Elite) eliteCount++;
            else coreCount++;
        }
    }

    public static bool CanEquipUnitToDeck(List<string> currentDeck, int targetSlotIndex, string newUnitId, out string failureReason)
    {
        failureReason = string.Empty;
        if (currentDeck == null || targetSlotIndex < 0 || targetSlotIndex >= currentDeck.Count)
        {
            failureReason = "Geçersiz deste yuvası.";
            return false;
        }

        // 1. Check duplicate
        for (int i = 0; i < currentDeck.Count; i++)
        {
            if (i != targetSlotIndex && currentDeck[i] == newUnitId)
            {
                UnitMeta meta = GetUnitMeta(newUnitId);
                failureReason = "⚠️ '" + (meta != null ? meta.displayName : newUnitId) + "' destede zaten mevcut!";
                return false;
            }
        }

        // In 10-Unit Heroless Mode (ArmyOnly) or if Elite Restrictions are disabled, any distinct units can be freely selected!
        if (DisableEliteLimitsStatic || currentDeck.Count > 5)
        {
            return true;
        }

        UnitTier newTier = GetUnitTier(newUnitId);

        // Count current tiers in other slots (excluding the slot we are replacing)
        int otherLegendary = 0;
        int otherElite = 0;
        string existingLegendaryName = string.Empty;

        for (int i = 0; i < currentDeck.Count; i++)
        {
            if (i == targetSlotIndex) continue;
            UnitTier t = GetUnitTier(currentDeck[i]);
            if (t == UnitTier.Legendary)
            {
                otherLegendary++;
                UnitMeta lm = GetUnitMeta(currentDeck[i]);
                if (lm != null) existingLegendaryName = lm.displayName;
            }
            else if (t == UnitTier.Elite)
            {
                otherElite++;
            }
        }

        // 2. Legendary Quota: Maximum 1 Legendary Boss per deck
        if (newTier == UnitTier.Legendary && otherLegendary >= 1)
        {
            UnitMeta meta = GetUnitMeta(newUnitId);
            string newName = (meta != null) ? meta.displayName : newUnitId;
            failureReason = "⚠️ KOTA SINIRI: Destede en fazla 1 Efsanevi (Boss) birim bulunabilir!\n'" + existingLegendaryName + "' varken '" + newName + "' seçilemez.";
            return false;
        }

        // 3. Elite Quota: Maximum 2 Elite units per deck
        if (newTier == UnitTier.Elite && otherElite >= 2)
        {
            failureReason = "⚠️ KOTA SINIRI: Destede en fazla 2 Elit birim bulunabilir!\nOrdunuzu dengelemek için diğer yuvalara Temel birlik seçmelisiniz.";
            return false;
        }

        return true;
    }

    // Elite & Boss Unit Limitations Toggle (false = Standard Quota, true = Unlimited Elites & Bosses)
    private bool mDisableEliteLimits = false;
    public bool DisableEliteLimits
    {
        get { return mDisableEliteLimits; }
        set { mDisableEliteLimits = value; }
    }
    public static bool DisableEliteLimitsStatic
    {
        get { return (sInstance != null) && sInstance.mDisableEliteLimits; }
    }

    public void ToggleDisableEliteLimits()
    {
        mDisableEliteLimits = !mDisableEliteLimits;
        if (SVZNetworkManager.Instance != null && SVZNetworkManager.Instance.IsConnected)
        {
            SVZNetworkManager.Instance.SendPacket("DISABLE_ELITE_LIMITS", mDisableEliteLimits.ToString());
        }
        BroadcastLobbyConfig();
        PlaySFX("UI_Upgrade_01", 0.9f);
        AddChatMessage("[LOBİ]", mDisableEliteLimits
            ? "⚡ ELİT VE BOSS BİRLİK SINIRLAMALARI KALDIRILDI! (Artık destede ve savaş alanında sınırsız sayıda Boss ve Elit birlik bulunabilir!)"
            : "🛡️ Elit ve Boss birlik sınırlamaları standart moda alındı (Maks 1 Boss / 2 Elit).");
    }

    // Evaluates dense AoE target clusters (within Z window) and elite/boss presence for area ultimates
    public static void EvaluateHostileAoECluster(bool targetIsEnemy, float windowRadius, out int maxClusterCount, out bool hasHighValueEliteOrBoss, out float bestClusterDistFromRef, float refZ)
    {
        maxClusterCount = 0;
        hasHighValueEliteOrBoss = false;
        bestClusterDistFromRef = 99999f;

        CharactersManager charMgr = WeakGlobalInstance<CharactersManager>.instance;
        if (charMgr == null || charMgr.allAlive == null) return;

        List<Character> hostiles = new List<Character>();
        List<Character> alive = charMgr.allAlive;
        for (int i = 0; i < alive.Count; i++)
        {
            Character c = alive[i];
            if (c != null && c.health > 0f && !c.isBase && c.isEnemy == targetIsEnemy)
            {
                hostiles.Add(c);
            }
        }

        for (int i = 0; i < hostiles.Count; i++)
        {
            Character center = hostiles[i];
            float centerZ = center.position.z;
            int localCount = 0;
            bool localHasEliteOrBoss = false;

            for (int j = 0; j < hostiles.Count; j++)
            {
                Character other = hostiles[j];
                if (Mathf.Abs(other.position.z - centerZ) <= windowRadius)
                {
                    localCount++;
                    string uid = !string.IsNullOrEmpty(other.uniqueID) ? other.uniqueID : ((other.controlledObject != null) ? other.controlledObject.name : string.Empty);
                    UnitTier tier = !string.IsNullOrEmpty(uid) ? GetUnitTier(uid) : UnitTier.Core;
                    UnitTacticalRole role = !string.IsNullOrEmpty(uid) ? GetUnitTacticalRole(uid) : UnitTacticalRole.SwarmScout;
                    if (other.isBoss || tier == UnitTier.Legendary || role == UnitTacticalRole.ApexBoss || (!other.isPlayer && other.maxHealth >= 650f))
                    {
                        localHasEliteOrBoss = true;
                    }
                }
            }

            float distToRef = Mathf.Abs(centerZ - refZ);
            if (localCount > maxClusterCount)
            {
                maxClusterCount = localCount;
                bestClusterDistFromRef = distToRef;
            }
            else if (localCount == maxClusterCount && distToRef < bestClusterDistFromRef)
            {
                bestClusterDistFromRef = distToRef;
            }

            if (localHasEliteOrBoss && distToRef <= 620f)
            {
                hasHighValueEliteOrBoss = true;
            }
        }
    }

    // Smart Tactical Spell Evaluator for Bot Hero & P2 Match Bot
    public static bool ShouldBotCastSpell(string spellId, Character nearestEnemy, float nearestEnemyDist, int enemiesInMelee, int p1Count, int p2Count, float botHeroHpPercent, bool gateUnderAttack)
    {
        if (string.IsNullOrEmpty(spellId)) return false;

        switch (spellId)
        {
            case "DivineIntervention":
            case "NightOfTheDead":
                if (botHeroHpPercent <= 0.65f) return true;
                if (p2Count >= 2 && botHeroHpPercent <= 0.82f && nearestEnemyDist <= 380f) return true;
                if (p2Count == 0 && p1Count >= 2 && nearestEnemyDist <= 320f) return true;
                return false;

            case "LifeSteal":
                return (botHeroHpPercent <= 0.88f && (enemiesInMelee >= 1 || nearestEnemyDist <= 165f));

            case "GiantWave":
            case "DragonAttack":
            case "SummonTornadoes":
            {
                CharactersManager cm = WeakGlobalInstance<CharactersManager>.instance;
                if (cm != null && cm.allAlive != null)
                {
                    bool targetIsEnemy = (nearestEnemy != null) ? nearestEnemy.isEnemy : false;
                    float refZ = (nearestEnemy != null) ? nearestEnemy.position.z : 0f;
                    int maxCluster;
                    bool hasEliteOrBossLine;
                    float clusterDist;
                    EvaluateHostileAoECluster(targetIsEnemy, 320f, out maxCluster, out hasEliteOrBossLine, out clusterDist, refZ);

                    if (maxCluster >= 3 && (nearestEnemyDist <= 620f || gateUnderAttack)) return true;
                    if (hasEliteOrBossLine && nearestEnemyDist <= 600f) return true;
                    if (gateUnderAttack && (maxCluster >= 2 || p1Count >= 2)) return true;
                    if (nearestEnemy != null && nearestEnemy.isPlayer && maxCluster >= 2 && nearestEnemyDist <= 450f) return true;
                    return false;
                }
                if (gateUnderAttack && (p1Count >= 2 || (nearestEnemy != null && (nearestEnemy.isBoss || nearestEnemy.maxHealth >= 650f)))) return true;
                if (p1Count >= 3 && nearestEnemyDist <= 580f) return true;
                if (nearestEnemy != null && (nearestEnemy.isBoss || (!nearestEnemy.isPlayer && nearestEnemy.maxHealth >= 650f)) && nearestEnemyDist <= 550f) return true;
                if (nearestEnemy != null && nearestEnemy.isPlayer && p1Count >= 2 && nearestEnemyDist <= 450f) return true;
                return false;
            }

            case "Lethargy":
            case "LethargyZM":
                if (p1Count >= 2 && nearestEnemyDist <= 480f) return true;
                if (nearestEnemy != null && nearestEnemy.isPlayer && nearestEnemyDist <= 400f) return true;
                if (gateUnderAttack && p1Count >= 1) return true;
                return false;

            case "GroundShock":
                return (enemiesInMelee >= 1 || (nearestEnemyDist <= 220f && nearestEnemy != null));

            case "RaiseDead":
                if (p2Count <= 1 && p1Count >= 1 && nearestEnemyDist <= 450f) return true;
                if (gateUnderAttack) return true;
                if (nearestEnemy != null && nearestEnemy.isPlayer && nearestEnemyDist <= 320f) return true;
                return false;

            case "KatanaSlash":
            case "SummonLightning":
            case "GraveHands":
                if (nearestEnemyDist <= 240f) return true;
                if (nearestEnemy != null && nearestEnemy.isPlayer && nearestEnemyDist <= 380f) return true;
                return false;

            default:
                return (nearestEnemyDist <= 320f || enemiesInMelee >= 1);
        }
    }

    // Live Match Statistics
    private float mMatchTimer = 0f;
    private int mP1UnitsSpawned = 0;
    private int mP2UnitsSpawned = 0;
    private int mP1AbilitiesUsed = 0;
    private int mP2AbilitiesUsed = 0;
    private float mP1DamageDealt = 0f;
    private float mP2DamageDealt = 0f;
    private string mMvpUnit = "Samuray";
    private bool mSuddenDeathActive = false;

    // Player 2 Forge / Leadership Upgrade Level (1..4)
    private int mP2ForgeLevel = 1;

    // AI Bot Difficulty (0=Kolay, 1=Orta, 2=Zor, 3=Cok Zor, 4=Imkansiz)
    private int mBotDifficulty = 2;
    public static readonly string[] sBotDifficultyNames = new string[]
    {
        "Kolay",
        "Orta",
        "Zor",
        "Cok Zor",
        "Imkansiz"
    };

    // Per-Slot Summon Cooldowns & Per-Unit Burst Spawn Counters (supports up to 10 slots)
    private float[] mP1SlotCooldowns = new float[10];
    private float[] mP1SlotMaxCooldowns = new float[10];
    private float[] mP2SlotCooldowns = new float[10];
    private float[] mP2SlotMaxCooldowns = new float[10];
    private int[] mP2SlotSummonCounts = new int[10];
    private float[] mP2SlotLastSummonTimes = new float[10];
    private Dictionary<string, int> mP1UnitSpawnCounts = new Dictionary<string, int>();
    private Dictionary<string, int> mP2UnitSpawnCounts = new Dictionary<string, int>();

    // Per-Slot Spell Cooldowns
    private float[] mP1SpellCooldowns = new float[2];
    private float[] mP1SpellMaxCooldowns = new float[2];
    private float[] mP2SpellCooldowns = new float[2];
    private float[] mP2SpellMaxCooldowns = new float[2];

    // Smooth Scene Loading Transition State
    private bool mIsLoadingScene = false;
    private int mLoadingFramesRemaining = 0;
    private string mPendingSceneName = string.Empty;

    // Optional AI Commander for Player 2 in Local Mode
    private bool mP2AutoBot = false;
    private float mP2BotThinkTimer = 0f;

    // Human controls P2 side (swap teams feature)
    private bool mHumanControlsP2 = false;

    // Opponent Hero Champion (PFZombieHero or PFHero01)
    private OpponentHero mOpponentHero = null;
    private float mOpponentHeroRespawnTimer = 0f;
    private const float kOpponentHeroMaxRespawn = 20f;

    private float mGateSyncTimer = 0f;
    private float mHeroSyncTimer = 0f;
    private List<string> mChatMessages = new List<string>();

    #region Properties

    public static SVZMultiplayerManager Instance
    {
        get
        {
            if (sInstance == null) EnsureExists();
            return sInstance;
        }
    }
    public bool IsActive { get { return mIsActive; } set { mIsActive = value; } }
    public bool IsSamePC { get { return mIsSamePC; } set { mIsSamePC = value; } }
    public bool IsOnlineClient
    {
        get
        {
            return !mIsSamePC && SVZNetworkManager.Instance != null && SVZNetworkManager.Instance.IsConnected && !SVZNetworkManager.Instance.IsHost;
        }
    }
    public bool P2AutoBot { get { return mP2AutoBot; } set { mP2AutoBot = value; } }
    public bool HumanControlsP2 { get { return mHumanControlsP2; } set { mHumanControlsP2 = value; } }

    public void SwapTeamsAndSide()
    {
        mHumanControlsP2 = !mHumanControlsP2;
        mP1Side = (mP1Side == BattlefieldSide.LeftGate) ? BattlefieldSide.RightGate : BattlefieldSide.LeftGate;

        HeroType tmpHero = mP1HeroType;
        mP1HeroType = mP2HeroType;
        mP2HeroType = tmpHero;

        List<string> tmpDeck = new List<string>(mP1Deck);
        mP1Deck = new List<string>(mP2Deck);
        mP2Deck = tmpDeck;

        List<string> tmpFive = new List<string>(mP1FiveUnitDeck);
        mP1FiveUnitDeck = new List<string>(mP2FiveUnitDeck);
        mP2FiveUnitDeck = tmpFive;

        List<string> tmpTen = new List<string>(mP1TenUnitDeck);
        mP1TenUnitDeck = new List<string>(mP2TenUnitDeck);
        mP2TenUnitDeck = tmpTen;

        List<string> tmpSpells = new List<string>(mP1Spells);
        mP1Spells = new List<string>(mP2Spells);
        mP2Spells = tmpSpells;

        int tmpPerk = mP1Perk;
        mP1Perk = mP2Perk;
        mP2Perk = tmpPerk;

        int tmpLvl = mP1TroopLevel;
        mP1TroopLevel = mP2TroopLevel;
        mP2TroopLevel = tmpLvl;

        EnsureValidSpellsForPlayer(1);
        EnsureValidSpellsForPlayer(2);

        BroadcastLobbyConfig();
        SendFullLobbySync();
    }
    public int BotDifficulty { get { return mBotDifficulty; } set { mBotDifficulty = Mathf.Clamp(value, 0, 4); } }
    public int P2ForgeLevel { get { return mP2ForgeLevel; } }
    public bool IsSuddenDeath { get { return mSuddenDeathActive; } }
    public bool IsLoadingScene { get { return mIsLoadingScene; } }
    public bool IsMatchOver { get { return mIsMatchOver; } set { mIsMatchOver = value; } }
    public int Winner { get { return mWinner; } }
    public MultiplayerGameMode CurrentGameMode
    {
        get { return mCurrentGameMode; }
        set
        {
            if (mCurrentGameMode != value)
            {
                if (mCurrentGameMode == MultiplayerGameMode.HeroAndArmy && mP1Deck.Count == 5)
                {
                    mP1FiveUnitDeck = new List<string>(mP1Deck);
                }
                else if (mCurrentGameMode == MultiplayerGameMode.ArmyOnly && mP1Deck.Count == 10)
                {
                    mP1TenUnitDeck = new List<string>(mP1Deck);
                }
                if (mCurrentGameMode == MultiplayerGameMode.HeroAndArmy && mP2Deck.Count == 5)
                {
                    mP2FiveUnitDeck = new List<string>(mP2Deck);
                }
                else if (mCurrentGameMode == MultiplayerGameMode.ArmyOnly && mP2Deck.Count == 10)
                {
                    mP2TenUnitDeck = new List<string>(mP2Deck);
                }
                mCurrentGameMode = value;
                EnsureDeckSizeForCurrentMode();
            }
        }
    }
    public BattlefieldSide P1Side { get { return mP1Side; } set { mP1Side = value; } }
    public HeroType P1HeroType
    {
        get { return mP1HeroType; }
        set
        {
            mP1HeroType = value;
            EnsureValidSpellsForPlayer(1);
        }
    }
    public HeroType P2HeroType
    {
        get { return mP2HeroType; }
        set
        {
            mP2HeroType = value;
            EnsureValidSpellsForPlayer(2);
        }
    }
    public string SelectedArena { get { return mSelectedArena; } set { mSelectedArena = value; } }
    public float GateMaxHealthSetting { get { return mGateMaxHealthSetting; } set { mGateMaxHealthSetting = Mathf.Clamp(value, 1000f, 25000f); } }
    public float EnergySpeed { get { return mEnergySpeed; } set { mEnergySpeed = Mathf.Clamp(value, 0.5f, 3.0f); } }
    public int P1Perk { get { return mP1Perk; } set { mP1Perk = Mathf.Clamp(value, 0, 2); } }
    public int P2Perk { get { return mP2Perk; } set { mP2Perk = Mathf.Clamp(value, 0, 2); } }
    public float Gate1Health { get { return mGate1Health; } }
    public float Gate1MaxHealth { get { return mGate1MaxHealth; } }
    public float Gate2Health { get { return mGate2Health; } }
    public float Gate2MaxHealth { get { return mGate2MaxHealth; } }
    public int P1TroopLevel { get { return mP1TroopLevel; } set { mP1TroopLevel = Mathf.Clamp(value, 1, 10); } }
    public int P2TroopLevel { get { return mP2TroopLevel; } set { mP2TroopLevel = Mathf.Clamp(value, 1, 10); } }
    public int SharedTroopLevel
    {
        get { return mP1TroopLevel; }
        set
        {
            int v = Mathf.Clamp(value, 1, 10);
            mP1TroopLevel = v;
            mP2TroopLevel = v;
        }
    }
    public int PendingTroopLevelProposal { get { return mPendingTroopLevelProposal; } }
    public int PendingTroopLevelProposer { get { return mPendingTroopLevelProposer; } }

    public void RequestOrSetTroopLevel(int targetLevel, int requesterPlayer)
    {
        targetLevel = Mathf.Clamp(targetLevel, 1, 10);
        SVZNetworkManager net = SVZNetworkManager.Instance;
        bool isOnlineConnected = (!mIsSamePC && net != null && net.IsConnected && net.Role != SVZNetworkManager.NetworkRole.SamePC && !mP2AutoBot);

        if (!isOnlineConnected)
        {
            SharedTroopLevel = targetLevel;
            mPendingTroopLevelProposal = 0;
            mPendingTroopLevelProposer = 0;
            PlaySFX("UI_Upgrade_01", 0.85f);
            AddChatMessage("[LOBİ]", string.Format("⚔️ Birlik Seviyeleri her iki taraf için EŞİT: Lv.{0} olarak ayarlandı!", targetLevel));
            return;
        }

        // Online connected: if opponent already proposed this exact level, clicking it approves it!
        if (mPendingTroopLevelProposal == targetLevel && mPendingTroopLevelProposer != 0 && mPendingTroopLevelProposer != requesterPlayer)
        {
            ApproveTroopLevelProposal();
            return;
        }

        if (SharedTroopLevel == targetLevel && mPendingTroopLevelProposal == 0)
        {
            return;
        }

        mPendingTroopLevelProposal = targetLevel;
        mPendingTroopLevelProposer = requesterPlayer;
        PlaySFX("UI_Click_01", 0.85f);
        AddChatMessage("[LOBİ]", string.Format("⏳ {0}. Oyuncu Birlik Seviyesini Lv.{1} (Eşit) yapmak istiyor! Karşı tarafın onayı bekleniyor...", requesterPlayer, targetLevel));
        net.SendPacket("TROOP_LVL_PROPOSE", string.Format("{0}|{1}", targetLevel, requesterPlayer));
    }

    public void ApproveTroopLevelProposal()
    {
        if (mPendingTroopLevelProposal <= 0) return;
        int approvedLvl = mPendingTroopLevelProposal;
        SharedTroopLevel = approvedLvl;
        mPendingTroopLevelProposal = 0;
        mPendingTroopLevelProposer = 0;
        PlaySFX("UI_Upgrade_01", 0.9f);
        AddChatMessage("[LOBİ]", string.Format("✅ Karşı taraf onayladı! Birlik Seviyeleri EŞİT: Lv.{0} oldu!", approvedLvl));
        SVZNetworkManager net = SVZNetworkManager.Instance;
        if (net != null && net.IsConnected)
        {
            net.SendPacket("TROOP_LVL_APPROVE", approvedLvl.ToString());
        }
    }

    public void RejectTroopLevelProposal()
    {
        if (mPendingTroopLevelProposal <= 0) return;
        int rejectedLvl = mPendingTroopLevelProposal;
        mPendingTroopLevelProposal = 0;
        mPendingTroopLevelProposer = 0;
        PlaySFX("UI_Cannot_Afford_01", 0.85f);
        AddChatMessage("[LOBİ]", string.Format("❌ Lv.{0} birlik seviyesi teklifi reddedildi (Mevcut Eşit Seviye: Lv.{1}).", rejectedLvl, SharedTroopLevel));
        SVZNetworkManager net = SVZNetworkManager.Instance;
        if (net != null && net.IsConnected)
        {
            net.SendPacket("TROOP_LVL_REJECT", rejectedLvl.ToString());
        }
    }
    public float Player2Leadership { get { return mPlayer2Leadership; } }
    public float Player2MaxLeadership { get { return mPlayer2MaxLeadership; } }
    public List<string> P1Deck { get { return mP1Deck; } set { mP1Deck = value; } }
    public List<string> P2Deck { get { return mP2Deck; } set { mP2Deck = value; } }
    public List<string> P1Spells { get { return mP1Spells; } set { mP1Spells = value; } }
    public List<string> P2Spells { get { return mP2Spells; } set { mP2Spells = value; } }
    public string Player1Name { get { return mPlayer1Name; } set { mPlayer1Name = value; } }
    public string Player2Name { get { return mPlayer2Name; } set { mPlayer2Name = value; } }
    public float MatchTimer { get { return mMatchTimer; } }
    public int P1UnitsSpawned { get { return mP1UnitsSpawned; } }
    public int P2UnitsSpawned { get { return mP2UnitsSpawned; } }
    public int P1AbilitiesUsed { get { return mP1AbilitiesUsed; } set { mP1AbilitiesUsed = value; } }
    public int P2AbilitiesUsed { get { return mP2AbilitiesUsed; } }
    public float P1DamageDealt { get { return mP1DamageDealt; } }
    public float P2DamageDealt { get { return mP2DamageDealt; } }
    public string MvpUnit { get { return mMvpUnit; } }
    public List<string> ChatMessages { get { return mChatMessages; } }
    public OpponentHero CurrentOpponentHero { get { return mOpponentHero; } }
    public float OpponentHeroRespawnTimer { get { return mOpponentHeroRespawnTimer; } }
    public bool BlindPick { get { return mBlindPick; } set { mBlindPick = value; } }
    public bool IsP1Ready { get { return mIsP1Ready; } set { mIsP1Ready = value; } }
    public bool IsP2Ready { get { return mIsP2Ready; } set { mIsP2Ready = value; } }

    #endregion

    public int GetTargetDeckSize()
    {
        return (mCurrentGameMode == MultiplayerGameMode.ArmyOnly) ? 10 : 5;
    }

    public void EnsureDeckSizeForCurrentMode()
    {
        if (mCurrentGameMode == MultiplayerGameMode.ArmyOnly)
        {
            if (mP1Deck.Count != 10)
            {
                List<string> nextP1 = new List<string>();
                for (int i = 0; i < mP1TenUnitDeck.Count && nextP1.Count < 10; i++)
                {
                    if (!nextP1.Contains(mP1TenUnitDeck[i])) nextP1.Add(mP1TenUnitDeck[i]);
                }
                for (int i = 0; i < sUnitMetaP1.Count && nextP1.Count < 10; i++)
                {
                    if (!nextP1.Contains(sUnitMetaP1[i].id)) nextP1.Add(sUnitMetaP1[i].id);
                }
                mP1Deck = nextP1;
            }
            if (mP2Deck.Count != 10)
            {
                List<string> nextP2 = new List<string>();
                for (int i = 0; i < mP2TenUnitDeck.Count && nextP2.Count < 10; i++)
                {
                    if (!nextP2.Contains(mP2TenUnitDeck[i])) nextP2.Add(mP2TenUnitDeck[i]);
                }
                for (int i = 0; i < sUnitMetaP2.Count && nextP2.Count < 10; i++)
                {
                    if (!nextP2.Contains(sUnitMetaP2[i].id)) nextP2.Add(sUnitMetaP2[i].id);
                }
                mP2Deck = nextP2;
            }
        }
        else
        {
            if (mP1Deck.Count != 5)
            {
                mP1Deck = new List<string>(mP1FiveUnitDeck);
                while (mP1Deck.Count > 5) mP1Deck.RemoveAt(mP1Deck.Count - 1);
            }
            if (mP2Deck.Count != 5)
            {
                mP2Deck = new List<string>(mP2FiveUnitDeck);
                while (mP2Deck.Count > 5) mP2Deck.RemoveAt(mP2Deck.Count - 1);
            }
        }
    }

    public static UnitFaction GetHeroFaction(HeroType heroType)
    {
        return (heroType == HeroType.Samurai) ? UnitFaction.Samurai : UnitFaction.Zombie;
    }

    public static List<SpellMeta> GetSpellsForHero(HeroType heroType)
    {
        UnitFaction targetFaction = GetHeroFaction(heroType);
        List<SpellMeta> list = new List<SpellMeta>();
        for (int i = 0; i < sAllSpells.Count; i++)
        {
            if (sAllSpells[i].faction == targetFaction)
            {
                list.Add(sAllSpells[i]);
            }
        }
        return list;
    }

    public static bool CanHeroUseSpell(HeroType heroType, string spellId)
    {
        if (string.IsNullOrEmpty(spellId)) return false;
        SpellMeta meta;
        if (!sSpellMetaLookup.TryGetValue(spellId, out meta)) return false;
        return meta.faction == GetHeroFaction(heroType);
    }

    public void EnsureValidSpellsForPlayer(int playerNum)
    {
        HeroType hType = (playerNum == 1) ? mP1HeroType : mP2HeroType;
        List<string> spells = (playerNum == 1) ? mP1Spells : mP2Spells;
        List<SpellMeta> allowed = GetSpellsForHero(hType);
        if (allowed.Count < 2) return;

        if (spells == null)
        {
            spells = new List<string>();
            if (playerNum == 1) mP1Spells = spells;
            else mP2Spells = spells;
        }
        string def0 = (hType == HeroType.Samurai) ? "GiantWave" : "DragonAttack";
        string def1 = (hType == HeroType.Samurai) ? "Lethargy" : "GraveHands";
        while (spells.Count < 2)
        {
            spells.Add(spells.Count == 0 ? def0 : def1);
        }

        for (int i = 0; i < 2; i++)
        {
            if (!CanHeroUseSpell(hType, spells[i]) || (i == 1 && spells[1] == spells[0]))
            {
                string other = (i == 0) ? spells[1] : spells[0];
                string pref = (i == 0) ? def0 : def1;
                if (CanHeroUseSpell(hType, pref) && pref != other)
                {
                    spells[i] = pref;
                }
                else
                {
                    for (int a = 0; a < allowed.Count; a++)
                    {
                        if (allowed[a].id != other)
                        {
                            spells[i] = allowed[a].id;
                            break;
                        }
                    }
                }
            }
        }
    }

    public static SpellMeta GetSpellMeta(string spellId)
    {
        SpellMeta meta;
        if (!string.IsNullOrEmpty(spellId) && sSpellMetaLookup.TryGetValue(spellId, out meta))
        {
            return meta;
        }
        return new SpellMeta(spellId ?? "GiantWave", spellId ?? "Büyü", "🔮 Büyü", 20f, "", "Sprites/Icons/ab_troop_trample", UnitFaction.Samurai);
    }

    public static string GetSpellIconPath(string spellId)
    {
        return GetSpellMeta(spellId).iconPath;
    }

    public bool IsP2SpellAvailable(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= mP2Spells.Count || slotIndex >= mP2SpellCooldowns.Length) return false;
        return mP2SpellCooldowns[slotIndex] <= 0f;
    }

    public float GetP2SpellCooldownRemaining(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= mP2SpellCooldowns.Length) return 0f;
        return Mathf.Max(0f, mP2SpellCooldowns[slotIndex]);
    }

    public float GetP2SpellCooldownRatio(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= mP2SpellCooldowns.Length) return 1f;
        if (mP2SpellCooldowns[slotIndex] <= 0f) return 1f;
        float maxCd = Mathf.Max(1f, mP2SpellMaxCooldowns[slotIndex]);
        return Mathf.Clamp01(1f - (mP2SpellCooldowns[slotIndex] / maxCd));
    }

    public static float GetUnitCooldownDuration(string unitId)
    {
        int cost = GetUnitCost(unitId);
        return Mathf.Clamp(1.5f + cost * 0.05f, 1.8f, 5.5f);
    }

    public bool IsP1SlotAvailable(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= mP1Deck.Count) return false;
        if (slotIndex < mP1SlotCooldowns.Length && mP1SlotCooldowns[slotIndex] > 0f) return false;
        string unitId = mP1Deck[slotIndex];
        if (!CanTeamSpawnUnit(unitId, false)) return false;
        Smithy smithy = WeakGlobalInstance<Smithy>.instance;
        if (smithy == null) return false;
        int cost = GetUnitCost(unitId);
        return smithy.leadership >= (float)cost;
    }

    public float GetP1SlotCooldownRatio(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= mP1Deck.Count) return 0f;
        if (slotIndex < mP1SlotCooldowns.Length && mP1SlotCooldowns[slotIndex] > 0f)
        {
            float maxCd = Mathf.Max(0.1f, mP1SlotMaxCooldowns[slotIndex]);
            return Mathf.Clamp01(1f - (mP1SlotCooldowns[slotIndex] / maxCd));
        }
        Smithy smithy = WeakGlobalInstance<Smithy>.instance;
        if (smithy == null) return 0f;
        float cost = Mathf.Max(1f, (float)GetUnitCost(mP1Deck[slotIndex]));
        return Mathf.Clamp01(smithy.leadership / cost);
    }

    public bool IsP2SlotAvailable(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= mP2Deck.Count) return false;
        if (slotIndex < mP2SlotCooldowns.Length && mP2SlotCooldowns[slotIndex] > 0f) return false;
        string unitId = mP2Deck[slotIndex];
        if (!CanTeamSpawnUnit(unitId, true)) return false;
        int cost = GetUnitCost(unitId);
        return mPlayer2Leadership >= (float)cost;
    }

    public float GetP2SlotCooldownRatio(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= mP2Deck.Count) return 0f;
        if (slotIndex < mP2SlotCooldowns.Length && mP2SlotCooldowns[slotIndex] > 0f)
        {
            float maxCd = Mathf.Max(0.1f, mP2SlotMaxCooldowns[slotIndex]);
            return Mathf.Clamp01(1f - (mP2SlotCooldowns[slotIndex] / maxCd));
        }
        float cost = Mathf.Max(1f, (float)GetUnitCost(mP2Deck[slotIndex]));
        return Mathf.Clamp01(mPlayer2Leadership / cost);
    }

    public static float GetSmithyMaxCapacityForLevel(int level)
    {
        switch (Mathf.Clamp(level, 1, 4))
        {
            case 1: return 50f;
            case 2: return 65f;
            case 3: return 100f;
            default: return 135f;
        }
    }

    public static float GetSmithyRegenRateForLevel(int level, float energySpeed, bool hasEnergyPerk, bool suddenDeath)
    {
        float baseRate;
        switch (Mathf.Clamp(level, 1, 4))
        {
            case 1: baseRate = 2.8f; break;
            case 2: baseRate = 3.8f; break;
            case 3: baseRate = 4.8f; break;
            default: baseRate = 6.0f; break;
        }
        float perkMult = hasEnergyPerk ? 1.20f : 1.0f;
        float sdMult = suddenDeath ? 2.0f : 1.0f;
        return baseRate * energySpeed * perkMult * sdMult;
    }

    public int GetP2ForgeUpgradeCost()
    {
        switch (mP2ForgeLevel)
        {
            case 1: return 30;
            case 2: return 50;
            case 3: return 80;
            default: return 0;
        }
    }

    public void UpgradePlayer2Forge()
    {
        if (mP2ForgeLevel >= 4) return;
        int cost = GetP2ForgeUpgradeCost();
        if (mPlayer2Leadership >= (float)cost)
        {
            mPlayer2Leadership -= (float)cost;
            mP2ForgeLevel++;
            mPlayer2MaxLeadership = GetSmithyMaxCapacityForLevel(mP2ForgeLevel);
            if (IsOnlineClient && WeakGlobalInstance<Smithy>.instance != null)
            {
                WeakGlobalInstance<Smithy>.instance.level = mP2ForgeLevel;
                WeakGlobalInstance<Smithy>.instance.resources = mPlayer2Leadership;
            }
            PlaySFX("UI_Upgrade_01", 0.9f);
            AddChatMessage(mPlayer2Name, string.Format("⚡ Demirci Ocağını (Smithy) Lv.{0} seviyesine yükseltti! (Max: {1:0})", mP2ForgeLevel, mPlayer2MaxLeadership));
            if (SVZNetworkManager.Instance != null && SVZNetworkManager.Instance.IsConnected)
            {
                SVZNetworkManager.Instance.SendPacket("P2_FORGE", mP2ForgeLevel.ToString());
            }
        }
    }

    public static bool ShouldUnitMarchRight(bool isEnemy, bool defaultLeftToRight)
    {
        if (sInstance != null && sInstance.mIsActive)
        {
            bool p1OnRight = (sInstance.mP1Side == BattlefieldSide.RightGate);
            return (!isEnemy) ? !p1OnRight : p1OnRight;
        }
        return isEnemy ? !defaultLeftToRight : defaultLeftToRight;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void EnsureExists()
    {
        if (sInstance == null)
        {
            GameObject go = new GameObject("[SVZMultiplayerManager]");
            DontDestroyOnLoad(go);
            sInstance = go.AddComponent<SVZMultiplayerManager>();
        }
    }

    private void Awake()
    {
        if (sInstance != null && sInstance != this)
        {
            Destroy(gameObject);
            return;
        }
        sInstance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        HookNetworkEvents();
    }

    private void OnEnable()
    {
        HookNetworkEvents();
    }

    public void HookNetworkEvents()
    {
        SVZNetworkManager net = SVZNetworkManager.Instance;
        if (net != null)
        {
            net.OnPacketReceived -= HandleNetworkPacket;
            net.OnPacketReceived += HandleNetworkPacket;
            net.OnConnected -= HandleNetworkConnected;
            net.OnConnected += HandleNetworkConnected;
            net.OnDisconnected -= HandleNetworkDisconnected;
            net.OnDisconnected += HandleNetworkDisconnected;
        }
    }

    private void HandleNetworkConnected()
    {
        SVZNetworkManager net = SVZNetworkManager.Instance;
        if (net == null) return;

        if (net.IsHost)
        {
            AddChatMessage("[LOBİ]", "🟢 2. OYUNCU ODANIZA KATILDI!");
            PlaySFX("UI_Window_Open_01", 0.9f);
            SendFullLobbySync();
        }
        else
        {
            AddChatMessage("[LOBİ]", "🟢 ODAYA BAŞARIYLA BAĞLANDINIZ! Kurucu bekleniyor...");
            PlaySFX("UI_Equip_01", 0.9f);
            SendP2LoadoutSync();
        }
    }

    public void SendP2LoadoutSync()
    {
        SVZNetworkManager net = SVZNetworkManager.Instance;
        if (net != null && net.IsConnected && !net.IsHost)
        {
            for (int i = 0; i < mP2Deck.Count; i++)
            {
                BroadcastLobbyPick(2, "SLOT", i, mP2Deck[i]);
            }
            for (int i = 0; i < mP2Spells.Count; i++)
            {
                BroadcastLobbyPick(2, "SPELL", i, mP2Spells[i]);
            }
            BroadcastLobbyPick(2, "HERO", 0, mP2HeroType.ToString());
            BroadcastLobbyPick(2, "PERK", 0, mP2Perk.ToString());
        }
    }

    private void HandleNetworkDisconnected()
    {
        mIsP2Ready = false;
        AddChatMessage("[LOBİ]", "🔴 Bağlantı kesildi!");
    }

    public void BroadcastLobbyConfig()
    {
        SVZNetworkManager net = SVZNetworkManager.Instance;
        if (net != null && net.IsHost && net.IsConnected)
        {
            string payload = string.Format("{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}",
                mSelectedArena,
                mCurrentGameMode,
                mGateMaxHealthSetting,
                mEnergySpeed,
                mBlindPick,
                mAllowBackAttack,
                SharedTroopLevel,
                mDisableEliteLimits);
            net.SendPacket("LOBBY_CONFIG", payload);
        }
    }

    public void BroadcastLobbyPick(int playerNum, string pickType, int slotIndex, string value)
    {
        SVZNetworkManager net = SVZNetworkManager.Instance;
        if (net != null && net.IsConnected)
        {
            string payload = string.Format("{0}|{1}|{2}|{3}", playerNum, pickType, slotIndex, value);
            net.SendPacket("LOBBY_PICK", payload);
        }
    }

    public void SendReadyState(bool ready)
    {
        mIsP2Ready = ready;
        SVZNetworkManager net = SVZNetworkManager.Instance;
        if (net != null && net.IsConnected)
        {
            net.SendPacket("LOBBY_READY", ready.ToString());
        }
    }

    public void SendFullLobbySync()
    {
        SVZNetworkManager net = SVZNetworkManager.Instance;
        if (net != null && net.IsHost && net.IsConnected)
        {
            string payload = string.Format("{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}|{9}|{10}|{11}",
                mSelectedArena,
                mCurrentGameMode,
                mGateMaxHealthSetting,
                mEnergySpeed,
                mBlindPick,
                mP1HeroType,
                mP1Perk,
                string.Join(",", mP1Deck.ToArray()),
                string.Join(",", mP1Spells.ToArray()),
                mAllowBackAttack,
                SharedTroopLevel,
                mDisableEliteLimits);
            net.SendPacket("LOBBY_FULL_SYNC", payload);
        }
    }

    public void BroadcastFullLobbySync()
    {
        SendFullLobbySync();
    }

    private void OnDestroy()
    {
        if (sInstance == this) sInstance = null;
    }

    private void OnLevelWasLoaded(int level)
    {
        mIsLoadingScene = false;
        mPendingSceneName = string.Empty;
    }

    public static void PlaySFX(string soundName, float volume = 1.0f)
    {
        try
        {
            if (string.IsNullOrEmpty(soundName)) return;
            AudioClip clip;
            if (!sSfxCache.TryGetValue(soundName, out clip) || clip == null)
            {
                clip = Resources.Load("sounds/" + soundName) as AudioClip;
                if (clip != null) sSfxCache[soundName] = clip;
            }
            if (clip != null)
            {
                Vector3 pos = Vector3.zero;
                if (Camera.main != null) pos = Camera.main.transform.position;
                AudioSource.PlayClipAtPoint(clip, pos, volume);
            }
        }
        catch { }
    }

    public void LoadSceneWithLoadingScreen(string sceneName)
    {
        if (SingletonMonoBehaviour<WaitingIconBetweenScenes>.instance != null)
        {
            SingletonMonoBehaviour<WaitingIconBetweenScenes>.instance.visible = true;
        }
        mIsLoadingScene = true;
        mLoadingFramesRemaining = 3;
        mPendingSceneName = sceneName;
    }

    private void Update()
    {
        if (mIsLoadingScene)
        {
            mLoadingFramesRemaining--;
            if (mLoadingFramesRemaining <= 0 && !string.IsNullOrEmpty(mPendingSceneName))
            {
                string targetScene = mPendingSceneName;
                mPendingSceneName = string.Empty;
                if (SingletonMonoBehaviour<WaitingIconBetweenScenes>.instance != null)
                {
                    SingletonMonoBehaviour<WaitingIconBetweenScenes>.instance.visible = true;
                }
                Singleton<MenusFlow>.instance.LoadScene(targetScene);
            }
            return;
        }

        if (!mIsActive) return;

        InGameImpl inGame = WeakGlobalSceneBehavior<InGameImpl>.instance;
        bool isInBattle = inGame != null && (mCurrentGameMode == MultiplayerGameMode.ArmyOnly || inGame.hero != null);

        if (isInBattle)
        {
            mMatchTimer += Time.deltaTime;

            for (int i = 0; i < mP1SlotCooldowns.Length; i++)
            {
                if (mP1SlotCooldowns[i] > 0f) mP1SlotCooldowns[i] = Mathf.Max(0f, mP1SlotCooldowns[i] - Time.deltaTime);
            }
            for (int i = 0; i < mP2SlotCooldowns.Length; i++)
            {
                if (mP2SlotCooldowns[i] > 0f) mP2SlotCooldowns[i] = Mathf.Max(0f, mP2SlotCooldowns[i] - Time.deltaTime);
            }
            for (int i = 0; i < mP1SpellCooldowns.Length; i++)
            {
                if (mP1SpellCooldowns[i] > 0f) mP1SpellCooldowns[i] = Mathf.Max(0f, mP1SpellCooldowns[i] - Time.deltaTime);
            }
            for (int i = 0; i < mP2SpellCooldowns.Length; i++)
            {
                if (mP2SpellCooldowns[i] > 0f) mP2SpellCooldowns[i] = Mathf.Max(0f, mP2SpellCooldowns[i] - Time.deltaTime);
            }

            // Initialize Both Gates' Health once
            if (!mGateInitialized && inGame.gate != null)
            {
                inGame.gate.maxHealth = mGate1MaxHealth;
                inGame.gate.health = mGate1MaxHealth;
                if (inGame.enemyGate != null)
                {
                    inGame.enemyGate.maxHealth = mGate2MaxHealth;
                    inGame.enemyGate.health = mGate2MaxHealth;
                }
                mGateInitialized = true;
            }

            // Update Opponent Hero (Zombie Hero / Samurai Hero duel) in HeroAndArmy mode
            if (mCurrentGameMode == MultiplayerGameMode.HeroAndArmy && mGateInitialized)
            {
                UpdateOpponentHeroLifecycle(inGame);
            }

            // Sync P1 Gate Health & P2 Gate Health with inGame Base objects (Gate HP can NEVER increase!)
            if (inGame.gate != null)
            {
                if (inGame.gate.health > mGate1Health)
                {
                    inGame.gate.health = mGate1Health;
                }
                float prevG1 = mGate1Health;
                mGate1Health = Mathf.Min(mGate1Health, inGame.gate.health);
                if (mGate1Health < prevG1) mP2DamageDealt += (prevG1 - mGate1Health);
            }
            if (inGame.enemyGate != null)
            {
                if (inGame.enemyGate.health > mGate2Health)
                {
                    inGame.enemyGate.health = mGate2Health;
                }
                float prevG2 = mGate2Health;
                mGate2Health = Mathf.Min(mGate2Health, inGame.enemyGate.health);
                if (mGate2Health < prevG2) mP1DamageDealt += (prevG2 - mGate2Health);
            }

            // Check Sudden Death Overtime (Triggered at 120s / 02:00)
            if (!mSuddenDeathActive && mMatchTimer >= 120f)
            {
                mSuddenDeathActive = true;
                PlaySFX("UI_BossBanner_Appear_ST_OS_01", 1.0f);
                AddChatMessage("[ANI OLUM!]", "🔥 02:00 ANI OLUM BASLADI! HER IKI TARAF ICIN 2X ENERJI URETIMI!");
            }
            // Equalized Smithy / Energy regeneration for Player 1 and Player 2
            float[] botTierMults = new float[] { 0.85f, 1.00f, 1.12f, 1.25f, 1.40f };
            bool isBotActive = mP2AutoBot && !IsOnlineClient && (mIsSamePC || SVZNetworkManager.Instance == null || !SVZNetworkManager.Instance.IsConnected);
            float botMult = isBotActive ? botTierMults[Mathf.Clamp(mBotDifficulty, 0, 4)] : 1.0f;

            if (mCurrentGameMode == MultiplayerGameMode.ArmyOnly)
            {
                // 10-Unit Heroless Mode: Smithy fills up to 225 at max speed (8 per second)!
                mP2ForgeLevel = 4;
                mPlayer2MaxLeadership = 225f;
                float p2RegenRate = 8f * (mSuddenDeathActive ? 2.0f : 1.0f);
                if (mPlayer2Leadership < mPlayer2MaxLeadership)
                {
                    mPlayer2Leadership = Mathf.Min(mPlayer2MaxLeadership, mPlayer2Leadership + p2RegenRate * Time.deltaTime);
                }

                Smithy smithy = WeakGlobalInstance<Smithy>.instance;
                if (smithy != null)
                {
                    if (IsOnlineClient)
                    {
                        smithy.EnforceMaxCapacityAndRate(225f, 0f);
                        smithy.resources = mPlayer2Leadership;
                    }
                    else
                    {
                        smithy.EnforceMaxCapacityAndRate(225f, 8f * (mSuddenDeathActive ? 2.0f : 1.0f));
                    }
                }
            }
            else
            {
                mPlayer2MaxLeadership = GetSmithyMaxCapacityForLevel(mP2ForgeLevel);
                float p2RegenRate = GetSmithyRegenRateForLevel(mP2ForgeLevel, mEnergySpeed, mP2Perk == 1, mSuddenDeathActive) * botMult;
                if (mPlayer2Leadership < mPlayer2MaxLeadership)
                {
                    mPlayer2Leadership = Mathf.Min(mPlayer2MaxLeadership, mPlayer2Leadership + p2RegenRate * Time.deltaTime);
                }

                // Synchronize Player 1 native Smithy rate & capacity (or mirror P2 Smithy when playing as Online Client)
                Smithy smithy = WeakGlobalInstance<Smithy>.instance;
                if (smithy != null)
                {
                    if (IsOnlineClient)
                    {
                        if (smithy.level != mP2ForgeLevel) smithy.level = mP2ForgeLevel;
                        smithy.increaseRate = 0f;
                        smithy.resources = mPlayer2Leadership;
                    }
                    else
                    {
                        smithy.increaseRate = GetSmithyRegenRateForLevel(smithy.level, mEnergySpeed, mP1Perk == 1, mSuddenDeathActive);
                    }
                }
            }

            // Fallback gate damage only if a scene lacks enemyGate
            if (inGame != null && inGame.enemyGate == null)
            {
                CharactersManager charMgr = WeakGlobalInstance<CharactersManager>.instance;
                if (charMgr != null)
                {
                    float rightEdgeZ = (inGame.heroWalkRightEdge != null) ? inGame.heroWalkRightEdge.position.z : 1000f;
                    float leftEdgeZ = (inGame.heroWalkLeftEdge != null) ? inGame.heroWalkLeftEdge.position.z : 0f;
                    bool p1MarchRight = (mP1Side != BattlefieldSide.RightGate);
                    List<Character> chars = charMgr.allAlive;
                    if (chars != null)
                    {
                        for (int i = 0; i < chars.Count; i++)
                        {
                            Character c = chars[i];
                            if (c != null && c.health > 0f && !c.isEnemy)
                            {
                                bool atGoal = p1MarchRight ? (c.position.z >= rightEdgeZ - 130f) : (c.position.z <= leftEdgeZ + 130f);
                                if (atGoal)
                                {
                                    float dmg = c.meleeDamage * Time.deltaTime * 0.75f;
                                    DamageOpponentGate(dmg);
                                    mP1DamageDealt += dmg;
                                }
                            }
                        }
                    }
                }
            }

            // Controls for Player 1: Number keys 1-5 (and 6-0 in 10-Unit Mode) to summon, B to upgrade Smithy
            bool isP1Local = !IsOnlineClient;
            if (isP1Local)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1)) SummonPlayer1Slot(0);
                if (Input.GetKeyDown(KeyCode.Alpha2)) SummonPlayer1Slot(1);
                if (Input.GetKeyDown(KeyCode.Alpha3)) SummonPlayer1Slot(2);
                if (Input.GetKeyDown(KeyCode.Alpha4)) SummonPlayer1Slot(3);
                if (Input.GetKeyDown(KeyCode.Alpha5)) SummonPlayer1Slot(4);
                if (mP1Deck.Count > 5)
                {
                    if (Input.GetKeyDown(KeyCode.Alpha6)) SummonPlayer1Slot(5);
                    if (Input.GetKeyDown(KeyCode.Alpha7)) SummonPlayer1Slot(6);
                    if (Input.GetKeyDown(KeyCode.Alpha8)) SummonPlayer1Slot(7);
                    if (Input.GetKeyDown(KeyCode.Alpha9)) SummonPlayer1Slot(8);
                    if (Input.GetKeyDown(KeyCode.Alpha0)) SummonPlayer1Slot(9);
                }
            }

            // Controls for Player 2 (Hotseat Input / Online Client Input + AI Bot)
            bool isP2HumanLocal = (mIsSamePC && !mP2AutoBot) || IsOnlineClient;
            if (isP2HumanLocal)
            {
                HandlePlayer2Input();
            }
            if (isBotActive && !mIsMatchOver)
            {
                UpdateP2Bot();
            }

            // Host synchronizes Gate and Hero states
            if (SVZNetworkManager.Instance != null && SVZNetworkManager.Instance.IsHost && SVZNetworkManager.Instance.IsConnected)
            {
                mGateSyncTimer += Time.deltaTime;
                if (mGateSyncTimer >= 0.35f)
                {
                    mGateSyncTimer = 0f;
                    string payload = string.Format("{0:0}|{1:0}", mGate1Health, mGate2Health);
                    SVZNetworkManager.Instance.SendPacket("GATE_SYNC", payload);
                }

                mHeroSyncTimer += Time.deltaTime;
                if (mHeroSyncTimer >= 0.04f) // Smooth 25 Hz Network Tick Rate
                {
                    mHeroSyncTimer = 0f;
                    if (mCurrentGameMode == MultiplayerGameMode.HeroAndArmy)
                    {
                        if (inGame != null && inGame.hero != null)
                        {
                            float p1Hp = inGame.hero.health;
                            float p1Z = inGame.hero.position.z;
                            int p1Facing = (inGame.hero.controller != null && inGame.hero.controller.facing == FacingType.Right) ? 1 : 0;

                            float p2Hp = (mOpponentHero != null) ? mOpponentHero.health : 0f;
                            float p2Z = (mOpponentHero != null && mOpponentHero.controller != null) ? mOpponentHero.position.z : 0f;
                            int p2Facing = (mOpponentHero != null && mOpponentHero.controller != null && mOpponentHero.controller.facing == FacingType.Right) ? 1 : 0;

                            string heroPayload = string.Format("{0:0}|{1:0.0}|{2}|{3:0}|{4:0.0}|{5}", p1Hp, p1Z, p1Facing, p2Hp, p2Z, p2Facing);
                            SVZNetworkManager.Instance.SendPacket("HERO_SYNC", heroPayload);
                        }
                    }
                }
            }

            // Check Win/Lose Condition
            if (!mIsMatchOver)
            {
                if (mGate1Health <= 0f)
                {
                    EndMatch(2); // P2 wins
                }
                else if (mGate2Health <= 0f)
                {
                    EndMatch(1); // P1 wins
                }
                else if (mCurrentGameMode == MultiplayerGameMode.HeroAndArmy)
                {
                    if (inGame.hero != null && inGame.hero.health <= 0f)
                    {
                        AddChatMessage("[KAHRAMAN DÜELLOSU]", "☠️ 1. Oyuncunun Kahramanı öldü! 2. Oyuncu MAÇI KAZANDI!");
                        EndMatch(2); // P2 wins
                    }
                    else if (mOpponentHero != null && mOpponentHero.health <= 0f)
                    {
                        AddChatMessage("[KAHRAMAN DÜELLOSU]", "👑 2. Oyuncunun Kahramanı (" + mOpponentHero.DisplayName + ") devrildi! 1. Oyuncu MAÇI KAZANDI!");
                        EndMatch(1); // P1 wins
                    }
                }
            }
        }
    }

    public enum UnitTacticalRole
    {
        SwarmScout,       // Cheap fast bodies / fodder (Farmer, CrawlingTorso, Zombie, ZombieMelee, EvilFarmer, etc.)
        TankShield,       // High-HP frontline blockers (Shieldsamurai, HeavySamurai, Oni, EvilHeavySamurai, GhostRonin, etc.)
        AntiCavalrySpear, // Long-reach spear stoppers vs Cavalry/Assassins (SpearSamurai, ZombieSpearman)
        ArmorBreaker,     // High-impact heavy crushers vs Tanks/Shields (ZombieWarhammer, Wanyudo, WanyudoFire, WanyudoMetal, Rifleman)
        RangedDPS,        // Backline bow/gun/ice attackers & Anti-Air (SamuraiArcher, IceArcher, ZombieArcher, HeavyArcher, ZombieRifleman, EvilIceArcher)
        FastFlanker,      // High-speed cavalry & assassins vs Ranged/Support (NinjaGirl, SpearHorseman, ZombieHorseman, EvilNinja, SmallOni, Tengu)
        FlyingAir,        // Airborne / exploding units (TenguAir, Chochinobake)
        SupportBuffer,    // Healers, buffers, upgraders (Priest, Handmaiden, Swordsmith, ZombieDrummer, Komusou, Onibaba)
        ApexBoss          // Ultimate commanders & titans (Takeda, Nobunaga, Shogun, EvilNobunaga, EvilTakeda, Necromancer, Jurogumo)
    }

    public static UnitTacticalRole GetUnitTacticalRole(string unitId)
    {
        switch (unitId)
        {
            // Swarm / Early Scouts
            case "Farmer":
            case "FarmerOnStilts":
            case "CrawlingTorso":
            case "Zombie":
            case "ZombieMelee":
            case "EvilFarmer":
            case "ZombieAxe":
            case "L_Zombie_Melee":
            case "L_Zombie_Ax":
            case "L_Zombie_Sword":
                return UnitTacticalRole.SwarmScout;

            // Frontline Tanks & Shield Walls
            case "Samurai":
            case "HeavySamurai":
            case "Shieldsamurai":
            case "ShieldSamuraiGreen":
            case "ShieldSamuraiIce":
            case "GhostRonin":
            case "Oni":
            case "EvilHeavySamurai":
            case "H_Zombie_Sword":
            case "H_Zombie_Ax":
            case "Evil_Panzer":
                return UnitTacticalRole.TankShield;

            // Anti-Cavalry / Anti-Assassin Spearmen
            case "SpearSamurai":
            case "ZombieSpearman":
            case "L_Zombie_Spear":
            case "H_Zombie_Spear":
                return UnitTacticalRole.AntiCavalrySpear;

            // Armor Breakers / Shield Crushers
            case "ZombieWarhammer":
            case "Wanyudo":
            case "WanyudoFire":
            case "WanyudoMetal":
            case "Rifleman":
            case "L_Zombie_Warhammer":
            case "Wayundo_wood":
            case "Wayundo_Fire":
                return UnitTacticalRole.ArmorBreaker;

            // Ranged DPS & Anti-Air
            case "SamuraiArcher":
            case "IceArcher":
            case "ZombieArcher":
            case "HeavyArcher":
            case "ZombieRifleman":
            case "EvilIceArcher":
            case "L_Zombie_Bow":
            case "H_Zombie_Bow":
                return UnitTacticalRole.RangedDPS;

            // Fast Flankers / Cavalry / Assassins
            case "NinjaGirl":
            case "SpearHorseman":
            case "EvilNinja":
            case "SmallOni":
            case "Tengu":
            case "ZombieHorseman":
            case "Evil_Ninja":
            case "Horseman":
                return UnitTacticalRole.FastFlanker;

            // Flying / Exploding Units
            case "TenguAir":
            case "Chochinobake":
                return UnitTacticalRole.FlyingAir;

            // Support / Healers / Buffers
            case "Priest":
            case "Handmaiden":
            case "Swordsmith":
            case "ZombieDrummer":
            case "Komusou":
            case "Onibaba":
            case "Taiko_Drummer":
                return UnitTacticalRole.SupportBuffer;

            // Apex Bosses / Warlords
            case "Takeda":
            case "Nobunaga":
            case "Shogun":
            case "EvilNobunaga":
            case "EvilTakeda":
            case "Necromancer":
            case "Jurogumo":
            case "Evil_Nobunaga":
            case "Evil_Takeda":
            case "Necromancer_Boss":
                return UnitTacticalRole.ApexBoss;

            default:
                return UnitTacticalRole.SwarmScout;
        }
    }

    private string PickCounterUnitFromPool(UnitTacticalRole desiredRole, bool allowSamurai, bool allowZombie)
    {
        List<string> candidates = new List<string>();
        for (int i = 0; i < sAllUnitMetas.Count; i++)
        {
            UnitMeta u = sAllUnitMetas[i];
            if (u.faction == UnitFaction.Samurai && !allowSamurai) continue;
            if (u.faction == UnitFaction.Zombie && !allowZombie) continue;
            if (GetUnitTacticalRole(u.id) == desiredRole && u.cost <= mPlayer2MaxLeadership)
            {
                candidates.Add(u.id);
            }
        }
        if (candidates.Count == 0) return string.Empty;
        // On Cok Zor / Imkansiz, prefer stronger tier counters within what P2's max leadership supports
        if (mBotDifficulty >= 3 && candidates.Count > 1)
        {
            candidates.Sort((a, b) => GetUnitCost(b).CompareTo(GetUnitCost(a)));
            int topSlice = Mathf.Max(1, candidates.Count / 2);
            return candidates[UnityEngine.Random.Range(0, topSlice)];
        }
        return candidates[UnityEngine.Random.Range(0, candidates.Count)];
    }

    private void UpdateP2Bot()
    {
        mP2BotThinkTimer -= Time.deltaTime;
        if (mP2BotThinkTimer > 0f) return;

        // 5-Tier Reaction Speeds: 0=Kolay, 1=Orta, 2=Zor, 3=Cok Zor, 4=Imkansiz
        float[] minWaits = new float[] { 1.20f, 0.75f, 0.35f, 0.18f, 0.08f };
        float[] maxWaits = new float[] { 1.80f, 1.15f, 0.55f, 0.30f, 0.15f };
        int diffIdx = Mathf.Clamp(mBotDifficulty, 0, 4);
        mP2BotThinkTimer = UnityEngine.Random.Range(minWaits[diffIdx], maxWaits[diffIdx]);

        if (mP2Deck == null || mP2Deck.Count == 0) return;

        // Keep player-selected P2 deck 100% intact (respect lobby picks & randomizer)
        bool hasDeckUnitAboveCap = false;
        for (int i = 0; i < mP2Deck.Count; i++)
        {
            if (GetUnitCost(mP2Deck[i]) > mPlayer2MaxLeadership)
            {
                hasDeckUnitAboveCap = true;
                break;
            }
        }

        // 1. Deep Live Battlefield Scan (recognizes all 49 Samurai & Zombie units, Heroes, and Z-axis frontline)
        int p1Count = 0, p2Count = 0;
        int p1Swarm = 0, p1Tanks = 0, p1Spears = 0, p1Breakers = 0, p1Ranged = 0, p1Flankers = 0, p1Flying = 0, p1Support = 0, p1Bosses = 0;
        int p2Swarm = 0, p2Tanks = 0, p2Spears = 0, p2Breakers = 0, p2Ranged = 0, p2Flankers = 0, p2Flying = 0, p2Support = 0, p2Bosses = 0;
        bool p2GateUnderAttack = false;
        int p1NearP2Gate = 0;

        InGameImpl inGame = WeakGlobalSceneBehavior<InGameImpl>.instance;
        CharactersManager charMgr = WeakGlobalInstance<CharactersManager>.instance;
        float p2GateZ = (inGame != null && inGame.enemyGate != null) ? inGame.enemyGate.position.z : 900f;
        float p1GateZ = (inGame != null && inGame.gate != null) ? inGame.gate.position.z : (p2GateZ - 1400f);
        float laneSpan = Mathf.Max(500f, Mathf.Abs(p2GateZ - p1GateZ));

        bool hasP1Presence = false;
        float closestP1DistToP2Gate = laneSpan;
        float foremostP1Z = p1GateZ;
        bool hasP2Presence = false;
        float closestP2DistToP1Gate = laneSpan;
        float foremostP2Z = p2GateZ;

        if (charMgr != null && charMgr.allAlive != null)
        {
            List<Character> chars = charMgr.allAlive;
            for (int i = 0; i < chars.Count; i++)
            {
                Character c = chars[i];
                if (c == null || c.health <= 0f || c.isBase) continue;

                string uid = !string.IsNullOrEmpty(c.uniqueID) ? c.uniqueID : ((c.controlledObject != null) ? c.controlledObject.name : string.Empty);
                UnitTacticalRole role = !string.IsNullOrEmpty(uid) ? GetUnitTacticalRole(uid) : UnitTacticalRole.SwarmScout;
                if (string.IsNullOrEmpty(uid))
                {
                    if (c.isFlying) role = UnitTacticalRole.FlyingAir;
                    else if (c.isBoss) role = UnitTacticalRole.ApexBoss;
                    else if (c.bowAttackRange > 100f) role = UnitTacticalRole.RangedDPS;
                    else if (c.maxHealth >= 220f) role = UnitTacticalRole.TankShield;
                }

                if (!c.isEnemy)
                {
                    hasP1Presence = true;
                    float distToP2Gate = Mathf.Abs(c.position.z - p2GateZ);
                    if (distToP2Gate < closestP1DistToP2Gate)
                    {
                        closestP1DistToP2Gate = distToP2Gate;
                        foremostP1Z = c.position.z;
                    }

                    if (c.isPlayer)
                    {
                        p1Tanks++;
                    }
                    else
                    {
                        p1Count++;
                        switch (role)
                        {
                            case UnitTacticalRole.SwarmScout: p1Swarm++; break;
                            case UnitTacticalRole.TankShield: p1Tanks++; break;
                            case UnitTacticalRole.AntiCavalrySpear: p1Spears++; break;
                            case UnitTacticalRole.ArmorBreaker: p1Breakers++; break;
                            case UnitTacticalRole.RangedDPS: p1Ranged++; break;
                            case UnitTacticalRole.FastFlanker: p1Flankers++; break;
                            case UnitTacticalRole.FlyingAir: p1Flying++; break;
                            case UnitTacticalRole.SupportBuffer: p1Support++; break;
                            case UnitTacticalRole.ApexBoss: p1Bosses++; p1Tanks++; break;
                        }
                    }
                    if (distToP2Gate < 260f)
                    {
                        p2GateUnderAttack = true;
                        p1NearP2Gate++;
                    }
                }
                else
                {
                    hasP2Presence = true;
                    float distToP1Gate = Mathf.Abs(c.position.z - p1GateZ);
                    if (distToP1Gate < closestP2DistToP1Gate)
                    {
                        closestP2DistToP1Gate = distToP1Gate;
                        foremostP2Z = c.position.z;
                    }

                    if (c == mOpponentHero) continue;
                    p2Count++;
                    switch (role)
                    {
                        case UnitTacticalRole.SwarmScout: p2Swarm++; break;
                        case UnitTacticalRole.TankShield: p2Tanks++; break;
                        case UnitTacticalRole.AntiCavalrySpear: p2Spears++; break;
                        case UnitTacticalRole.ArmorBreaker: p2Breakers++; break;
                        case UnitTacticalRole.RangedDPS: p2Ranged++; break;
                        case UnitTacticalRole.FastFlanker: p2Flankers++; break;
                        case UnitTacticalRole.FlyingAir: p2Flying++; break;
                        case UnitTacticalRole.SupportBuffer: p2Support++; break;
                        case UnitTacticalRole.ApexBoss: p2Bosses++; p2Tanks++; break;
                    }
                }
            }
        }

        // Dynamic Z-axis clash / frontline estimation relative to P2 Gate
        float frontlineZ = hasP1Presence ? (hasP2Presence ? Mathf.Lerp(foremostP1Z, foremostP2Z, 0.35f) : foremostP1Z)
                                         : (hasP2Presence ? foremostP2Z : ((p1GateZ + p2GateZ) * 0.5f));
        float frontlineDistToP2 = hasP1Presence ? Mathf.Min(closestP1DistToP2Gate, Mathf.Abs(frontlineZ - p2GateZ))
                                                : Mathf.Abs(frontlineZ - p2GateZ);
        float frontlineProximityToP2 = 1f - Mathf.Clamp01(frontlineDistToP2 / laneSpan);
        bool frontlineThreateningP2 = hasP1Presence && (frontlineProximityToP2 >= 0.62f || frontlineDistToP2 <= 480f);
        bool frontlineSafeForEconomy = !hasP1Presence || (frontlineProximityToP2 <= 0.52f && !p2GateUnderAttack);

        int p2Frontline = p2Tanks + p2Spears + p2Breakers + p2Swarm;
        bool gateEmergency = p2GateUnderAttack && p2Count == 0;

        // 2. Proactive Smart Tactical Spells (Only in HeroAndArmy mode when Heroes are active)
        if (mCurrentGameMode == MultiplayerGameMode.HeroAndArmy)
        {
            float botHpPct = (mOpponentHero != null && mOpponentHero.maxHealth > 0f) ? (mOpponentHero.health / mOpponentHero.maxHealth) : 1.0f;
            float nearestDist = 9999f;
            Character nearestHostile = null;
            int enemiesInMelee = 0;
            float meleeThreshold = (mOpponentHero != null && mOpponentHero.health > 0f) ? Mathf.Max(155f, mOpponentHero.meleeAttackRange + 15f) : 165f;
            if (charMgr != null && charMgr.allAlive != null)
            {
                float refZ = (mOpponentHero != null && mOpponentHero.health > 0f && mOpponentHero.controller != null) ? mOpponentHero.controller.position.z : p2GateZ;
                for (int cIdx = 0; cIdx < charMgr.allAlive.Count; cIdx++)
                {
                    Character ch = charMgr.allAlive[cIdx];
                    if (ch != null && ch.health > 0f && !ch.isBase && !ch.isEnemy)
                    {
                        float d = Mathf.Abs(ch.position.z - refZ);
                        if (!ch.isFlying && d <= meleeThreshold)
                        {
                            enemiesInMelee++;
                        }
                        if (d < nearestDist)
                        {
                            nearestDist = d;
                            nearestHostile = ch;
                        }
                    }
                }
            }

            for (int spSlot = 0; spSlot < 2; spSlot++)
            {
                if (IsP2SpellAvailable(spSlot) && spSlot < mP2Spells.Count)
                {
                    string spId = mP2Spells[spSlot];
                    if (ShouldBotCastSpell(spId, nearestHostile, nearestDist, enemiesInMelee, p1Count, p2Count, botHpPct, p2GateUnderAttack))
                    {
                        TriggerPlayer2Spell(spSlot);
                        return;
                    }
                }
            }
        }

        // Check if an affordable frontline tank/defender is ready to spawn right now
        bool hasAffordableEmergencyFrontliner = false;
        for (int m = 0; m < mP2Deck.Count; m++)
        {
            if (IsP2SlotAvailable(m))
            {
                UnitTacticalRole r = GetUnitTacticalRole(mP2Deck[m]);
                if (r == UnitTacticalRole.TankShield || r == UnitTacticalRole.AntiCavalrySpear || r == UnitTacticalRole.ArmorBreaker || GetUnitCost(mP2Deck[m]) <= 20)
                {
                    hasAffordableEmergencyFrontliner = true;
                    break;
                }
            }
        }

        // 3. Dynamic Smithy / Forge Level Progression vs Frontline Defense
        if (mCurrentGameMode != MultiplayerGameMode.ArmyOnly && mP2ForgeLevel < 4 && !gateEmergency)
        {
            int upgCost = GetP2ForgeUpgradeCost();
            Smithy p1Smithy = WeakGlobalInstance<Smithy>.instance;
            int p1SmithyLvl = (p1Smithy != null) ? p1Smithy.level : 1;
            bool behindP1Smithy = (mP2ForgeLevel < p1SmithyLvl);
            bool wantLevelUpNow =
                (mP2ForgeLevel == 1 && (mP2UnitsSpawned >= 1 || mMatchTimer >= 8f || hasDeckUnitAboveCap)) ||
                (mP2ForgeLevel == 2 && (mP2UnitsSpawned >= 3 || mMatchTimer >= 22f || behindP1Smithy || hasDeckUnitAboveCap)) ||
                (mP2ForgeLevel == 3 && (mMatchTimer >= 42f || mP2UnitsSpawned >= 5 || behindP1Smithy || hasDeckUnitAboveCap));

            bool urgentFrontlineHold = frontlineThreateningP2 && p2Frontline == 0 && (p1Count >= 1 || p1Tanks >= 1) && hasAffordableEmergencyFrontliner;
            if (mPlayer2Leadership >= (float)upgCost && !urgentFrontlineHold && !p2GateUnderAttack)
            {
                UpgradePlayer2Forge();
                return;
            }
            else if (wantLevelUpNow && !urgentFrontlineHold)
            {
                // Save leadership for the Forge upgrade only when the frontline is pushed to midfield/enemy side or well-held!
                if (!p2GateUnderAttack && (frontlineSafeForEconomy || p2Frontline >= 2) && (hasDeckUnitAboveCap || mPlayer2Leadership >= (float)upgCost * 0.45f || p2Count >= 2))
                {
                    return; // Hold energy for Forge upgrade!
                }
            }
        }

        // 4. Strategic Deck Slot Evaluation (Ensures all 5 slots are actively, intelligently utilized!)
        int bestSlot = -1;
        float bestScore = -99999f;
        int maxSlotSpawns = 0;
        for (int s = 0; s < mP2Deck.Count; s++)
        {
            int spCount = (s < mP2SlotSummonCounts.Length) ? mP2SlotSummonCounts[s] : 0;
            if (spCount > maxSlotSpawns) maxSlotSpawns = spCount;
        }

        for (int i = 0; i < mP2Deck.Count; i++)
        {
            string uid = mP2Deck[i];
            int cost = GetUnitCost(uid);
            if (cost > mPlayer2MaxLeadership) continue; // Requires Forge upgrade first
            if (!CanTeamSpawnUnit(uid, true)) continue; // At universal max alive cap

            UnitTacticalRole role = GetUnitTacticalRole(uid);
            UnitTier tier = GetUnitTier(uid);
            float cd = (i < mP2SlotCooldowns.Length) ? mP2SlotCooldowns[i] : 0f;

            // Base tactical score proportional to cost & tier
            float score = 25f + cost * 1.15f;
            if (tier == UnitTier.Legendary) score += 60f;
            else if (tier == UnitTier.Elite) score += 35f;

            // A. Anti-Starvation & Fair Slot Rotation (Guarantee every slot gets summoned!)
            int slotSpawns = (i < mP2SlotSummonCounts.Length) ? mP2SlotSummonCounts[i] : 0;
            if (slotSpawns == 0)
            {
                score += 55f; // Huge incentive to summon unplayed deck units early!
            }
            else
            {
                score += (maxSlotSpawns - slotSpawns) * 14f; // Catch-up bonus for lagging slots
            }
            float lastSpawnTime = (i < mP2SlotLastSummonTimes.Length) ? mP2SlotLastSummonTimes[i] : -999f;
            float timeSinceLast = (lastSpawnTime >= 0f) ? (mMatchTimer - lastSpawnTime) : 99f;
            score += Mathf.Clamp(timeSinceLast * 1.5f, 0f, 40f);

            // B. Anti-Spam / Army Diversity (Penalize spamming duplicate units of the same type)
            int aliveCount = CountAliveUnitsOfType(uid, true);
            if (aliveCount >= 1) score -= 30f;
            if (aliveCount >= 2) score -= 75f;
            if (aliveCount >= 3) score -= 150f;

            // C. Combined-Arms Tactical Composition (Tank Frontline -> Ranged/Healer Support -> Bosses/Flankers)
            if (p2Frontline == 0)
            {
                // Must establish frontline first!
                if (role == UnitTacticalRole.TankShield) score += 85f;
                else if (role == UnitTacticalRole.ArmorBreaker || role == UnitTacticalRole.AntiCavalrySpear) score += 60f;
                else if (role == UnitTacticalRole.ApexBoss) score += 70f;
                else if (role == UnitTacticalRole.SupportBuffer) score -= 70f;
                else if (role == UnitTacticalRole.RangedDPS) score -= 25f;
            }
            else
            {
                // Frontline is established! Reinforce with Ranged, Support, Breakers, and Bosses!
                if (role == UnitTacticalRole.RangedDPS && p2Ranged < 3) score += 70f;
                if (role == UnitTacticalRole.SupportBuffer && p2Support < 2) score += 75f;
                if (role == UnitTacticalRole.ArmorBreaker && p2Breakers < 2) score += 60f;
                if (role == UnitTacticalRole.TankShield && p2Tanks < 2) score += 45f;
                if (role == UnitTacticalRole.ApexBoss) score += 80f;
                if (role == UnitTacticalRole.FastFlanker) score += 50f;
                if (role == UnitTacticalRole.FlyingAir) score += 55f;
            }

            // Frontline pressure on P2 half of lane boosts immediate cheap tanks/spears
            if (frontlineThreateningP2 && p2Frontline <= 1)
            {
                if (role == UnitTacticalRole.TankShield || role == UnitTacticalRole.AntiCavalrySpear || role == UnitTacticalRole.ArmorBreaker) score += 55f;
                if (cost <= 25) score += 35f;
            }

            // D. Tactical Counters Against Player 1's Active Army
            if (p1Flying > 0 && (role == UnitTacticalRole.RangedDPS || role == UnitTacticalRole.FlyingAir))
            {
                score += 85f;
            }
            if (p1Flankers > 0 && (role == UnitTacticalRole.AntiCavalrySpear || role == UnitTacticalRole.TankShield))
            {
                score += 80f;
            }
            if ((p1Tanks + p1Bosses) >= 1 && (role == UnitTacticalRole.ArmorBreaker || role == UnitTacticalRole.ApexBoss))
            {
                score += 75f;
            }
            if ((p1Ranged + p1Support) >= 2 && (role == UnitTacticalRole.FastFlanker || role == UnitTacticalRole.FlyingAir))
            {
                score += 65f;
            }

            // E. Emergency Gate Defense
            if (gateEmergency)
            {
                if (role == UnitTacticalRole.TankShield || role == UnitTacticalRole.AntiCavalrySpear || role == UnitTacticalRole.ArmorBreaker)
                    score += 75f;
                if (cost <= 20) score += 50f;
                if (role == UnitTacticalRole.SupportBuffer) score -= 90f;
            }

            // F. Cooldown penalty if on long cooldown
            if (cd > 2.0f)
            {
                score -= cd * 12f;
            }

            // G. Difficulty noise
            float noise = (diffIdx == 0) ? UnityEngine.Random.Range(0f, 25f) : UnityEngine.Random.Range(0f, Mathf.Max(2f, 10f - diffIdx * 2f));
            score += noise;

            if (score > bestScore)
            {
                bestScore = score;
                bestSlot = i;
            }
        }

        if (bestSlot < 0) return;

        int targetCost = GetUnitCost(mP2Deck[bestSlot]);
        float targetCd = (bestSlot < mP2SlotCooldowns.Length) ? mP2SlotCooldowns[bestSlot] : 0f;

        // 5. Action Execution: Summon Target Unit or Reserve Energy for It!
        if (mPlayer2Leadership >= (float)targetCost && targetCd <= 0f)
        {
            SummonPlayer2Unit(bestSlot);
            return;
        }

        // If target slot is currently cooling down (> 1.5s), check if another strong ready slot can be summoned right now
        if (targetCd > 1.5f)
        {
            int altReadySlot = -1;
            float altScore = -99999f;
            for (int j = 0; j < mP2Deck.Count; j++)
            {
                if (j == bestSlot) continue;
                if (IsP2SlotAvailable(j))
                {
                    int c = GetUnitCost(mP2Deck[j]);
                    int alv = CountAliveUnitsOfType(mP2Deck[j], true);
                    if (alv >= 2) continue; // Don't spam duplicate
                    float sc = c * 1.1f - alv * 40f;
                    if (sc > altScore)
                    {
                        altScore = sc;
                        altReadySlot = j;
                    }
                }
            }
            if (altReadySlot >= 0 && altScore > 10f)
            {
                SummonPlayer2Unit(altReadySlot);
                return;
            }
        }

        // If gate is in an emergency and we can't afford target, summon any cheap ready defender immediately
        if (gateEmergency)
        {
            int emergencySlot = -1;
            int cheapestCost = 999;
            for (int k = 0; k < mP2Deck.Count; k++)
            {
                if (IsP2SlotAvailable(k))
                {
                    int c = GetUnitCost(mP2Deck[k]);
                    if (c < cheapestCost)
                    {
                        cheapestCost = c;
                        emergencySlot = k;
                    }
                }
            }
            if (emergencySlot >= 0)
            {
                SummonPlayer2Unit(emergencySlot);
                return;
            }
        }

        // Under close frontline push toward P2 Gate with empty frontline, summon an affordable cheap tank instead of stalling
        if (((frontlineThreateningP2 && p2Frontline == 0) || (p1NearP2Gate >= 2 && p2Count == 0)) && targetCost > 25)
        {
            int cheapDefSlot = -1;
            float cheapDefScore = -9999f;
            for (int m = 0; m < mP2Deck.Count; m++)
            {
                if (IsP2SlotAvailable(m))
                {
                    int c = GetUnitCost(mP2Deck[m]);
                    UnitTacticalRole r = GetUnitTacticalRole(mP2Deck[m]);
                    float sc = (50f - c) + ((r == UnitTacticalRole.TankShield || r == UnitTacticalRole.AntiCavalrySpear) ? 35f : 0f);
                    if (sc > cheapDefScore)
                    {
                        cheapDefScore = sc;
                        cheapDefSlot = m;
                    }
                }
            }
            if (cheapDefSlot >= 0)
            {
                SummonPlayer2Unit(cheapDefSlot);
                return;
            }
        }

        // Otherwise: PROACTIVE ENERGY RESERVATION!
        // Intentionally hold leadership and save up until targetCost is reached!
        // (Do NOT drain leadership on cheap units while saving for targetSlot!)
        return;
    }

    private void HandlePlayer2Input()
    {
        bool p1UsesTenKeys = (mP1Deck != null && mP1Deck.Count > 5);
        if (Input.GetKeyDown(KeyCode.Keypad1) || (!p1UsesTenKeys && Input.GetKeyDown(KeyCode.Alpha6)) || (!mIsSamePC && Input.GetKeyDown(KeyCode.Alpha1))) SummonPlayer2Unit(0);
        if (Input.GetKeyDown(KeyCode.Keypad2) || (!p1UsesTenKeys && Input.GetKeyDown(KeyCode.Alpha7)) || (!mIsSamePC && Input.GetKeyDown(KeyCode.Alpha2))) SummonPlayer2Unit(1);
        if (Input.GetKeyDown(KeyCode.Keypad3) || (!p1UsesTenKeys && Input.GetKeyDown(KeyCode.Alpha8)) || (!mIsSamePC && Input.GetKeyDown(KeyCode.Alpha3))) SummonPlayer2Unit(2);
        if (Input.GetKeyDown(KeyCode.Keypad4) || (!p1UsesTenKeys && Input.GetKeyDown(KeyCode.Alpha9)) || (!mIsSamePC && Input.GetKeyDown(KeyCode.Alpha4))) SummonPlayer2Unit(3);
        if (Input.GetKeyDown(KeyCode.Keypad5) || (!p1UsesTenKeys && Input.GetKeyDown(KeyCode.Alpha0)) || (!mIsSamePC && Input.GetKeyDown(KeyCode.Alpha5))) SummonPlayer2Unit(4);
        if (Input.GetKeyDown(KeyCode.Keypad6) || (!mIsSamePC && Input.GetKeyDown(KeyCode.Alpha6))) SummonPlayer2Unit(5);
        if (Input.GetKeyDown(KeyCode.Keypad7) || (!mIsSamePC && Input.GetKeyDown(KeyCode.Alpha7))) SummonPlayer2Unit(6);
        if (Input.GetKeyDown(KeyCode.Keypad8) || (!mIsSamePC && Input.GetKeyDown(KeyCode.Alpha8))) SummonPlayer2Unit(7);
        if (Input.GetKeyDown(KeyCode.Keypad9) || (!mIsSamePC && Input.GetKeyDown(KeyCode.Alpha9))) SummonPlayer2Unit(8);
        if (Input.GetKeyDown(KeyCode.Keypad0) || (!mIsSamePC && Input.GetKeyDown(KeyCode.Alpha0))) SummonPlayer2Unit(9);

        if (mCurrentGameMode != MultiplayerGameMode.ArmyOnly)
        {
            if (Input.GetKeyDown(KeyCode.U) || Input.GetKeyDown(KeyCode.KeypadMultiply) || (!mIsSamePC && Input.GetKeyDown(KeyCode.B)))
            {
                UpgradePlayer2Forge();
            }

            if (Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.KeypadPlus) || (!mIsSamePC && Input.GetKeyDown(KeyCode.Q)))
            {
                TriggerPlayer2Spell(0);
            }

            if (Input.GetKeyDown(KeyCode.P) || (!mIsSamePC && Input.GetKeyDown(KeyCode.E)))
            {
                TriggerPlayer2Spell(1);
            }
        }
    }

    public void StartMultiplayerMatch(bool samePc)
    {
        if (mP2AutoBot && (SVZNetworkManager.Instance == null || !SVZNetworkManager.Instance.IsConnected))
        {
            samePc = true;
        }
        EnsureDeckSizeForCurrentMode();
        EnsureValidSpellsForPlayer(1);
        EnsureValidSpellsForPlayer(2);

        mIsActive = true;
        mIsSamePC = samePc;
        mIsMatchOver = false;
        mWinner = 0;
        mGateInitialized = false;
        mSuddenDeathActive = false;

        if (SVZMultiplayerUI.Instance != null)
        {
            SVZMultiplayerUI.Instance.ShowLobby = false;
        }
        if (mCurrentGameMode == MultiplayerGameMode.ArmyOnly)
        {
            mP2ForgeLevel = 4;
            mPlayer2MaxLeadership = 225f;
        }
        else
        {
            mP2ForgeLevel = 1;
            mPlayer2MaxLeadership = GetSmithyMaxCapacityForLevel(1);
        }

        mMatchTimer = 0f;
        mP1UnitsSpawned = 0;
        mP2UnitsSpawned = 0;
        mP1AbilitiesUsed = 0;
        mP2AbilitiesUsed = 0;
        mP1DamageDealt = 0f;
        mP2DamageDealt = 0f;
        mP1UnitSpawnCounts.Clear();
        mP2UnitSpawnCounts.Clear();
        for (int i = 0; i < 10; i++)
        {
            mP1SlotCooldowns[i] = 0f;
            mP1SlotMaxCooldowns[i] = 1f;
            mP2SlotCooldowns[i] = 0f;
            mP2SlotMaxCooldowns[i] = 1f;
            mP2SlotSummonCounts[i] = 0;
            mP2SlotLastSummonTimes[i] = -999f;
        }
        for (int i = 0; i < 2; i++)
        {
            mP1SpellCooldowns[i] = 0f;
            mP1SpellMaxCooldowns[i] = (i < mP1Spells.Count) ? GetSpellMeta(mP1Spells[i]).cooldown : 20f;
            mP2SpellCooldowns[i] = 0f;
            mP2SpellMaxCooldowns[i] = (i < mP2Spells.Count) ? GetSpellMeta(mP2Spells[i]).cooldown : 20f;
        }

        mGate1Health = mGateMaxHealthSetting;
        mGate1MaxHealth = mGateMaxHealthSetting;
        mGate2Health = mGateMaxHealthSetting;
        mGate2MaxHealth = mGateMaxHealthSetting;
        mPlayer2Leadership = 20f;
        mPlayer2LeadershipRegen = (mCurrentGameMode == MultiplayerGameMode.ArmyOnly)
            ? 8f
            : GetSmithyRegenRateForLevel(1, mEnergySpeed, mP2Perk == 1, false);

        if (mCurrentGameMode == MultiplayerGameMode.HeroAndArmy)
        {
            if (mP1HeroType == HeroType.Zombie)
            {
                if (Singleton<PlayModesManager>.instance != null)
                {
                    Singleton<PlayModesManager>.instance.selectedMode = "zombies";
                }
                if (Singleton<Profile>.instance != null)
                {
                    Singleton<Profile>.instance.heroID = "HeroZombie";
                    Singleton<Profile>.instance.swordID = "Katana_ZM";
                    Singleton<Profile>.instance.bowID = "Bow_ZM";
                }
            }
            else
            {
                if (Singleton<PlayModesManager>.instance != null)
                {
                    Singleton<PlayModesManager>.instance.selectedMode = "classic";
                }
                if (Singleton<Profile>.instance != null)
                {
                    Singleton<Profile>.instance.heroID = "HeroSamurai";
                    Singleton<Profile>.instance.swordID = "Katana";
                    Singleton<Profile>.instance.bowID = "Bow";
                }
            }
        }
        else
        {
            if (Singleton<PlayModesManager>.instance != null)
            {
                Singleton<PlayModesManager>.instance.selectedMode = "classic";
            }
        }

        // Equip abilities into Profile for native HUD (only in HeroAndArmy mode, and strictly validated for Hero)
        if (Singleton<Profile>.instance != null)
        {
            for (int i = 0; i < sAllSpells.Count; i++)
            {
                string spId = sAllSpells[i].id;
                if (Singleton<Profile>.instance.GetAbilityLevel(spId) < 1)
                {
                    Singleton<Profile>.instance.SetAbilityLevel(spId, 3);
                }
            }
            if (mCurrentGameMode == MultiplayerGameMode.ArmyOnly)
            {
                Singleton<Profile>.instance.SetSelectedAbilities(new List<string>());
            }
            else
            {
                EnsureValidSpellsForPlayer(1);
                Singleton<Profile>.instance.SetSelectedAbilities(new List<string>(mP1Spells));
            }
        }

        // Broadcast to Online Client
        if (!samePc && SVZNetworkManager.Instance != null && SVZNetworkManager.Instance.IsHost)
        {
            string payload = string.Format("{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}|{9}|{10}|{11}|{12}|{13}|{14}|{15}",
                mCurrentGameMode,
                mP1TroopLevel,
                mP2TroopLevel,
                mSelectedArena,
                mGateMaxHealthSetting,
                mEnergySpeed,
                mP1Perk,
                mP2Perk,
                string.Join(",", mP1Deck.ToArray()),
                string.Join(",", mP2Deck.ToArray()),
                mP1Side,
                mP1HeroType,
                mP2HeroType,
                string.Join(",", mP1Spells.ToArray()),
                string.Join(",", mP2Spells.ToArray()),
                mAllowBackAttack);

            SVZNetworkManager.Instance.SendPacket("MATCH_START", payload);
        }

        PlaySFX("UI_LegionBanner_Appear_ST_OS_01", 0.9f);
        Time.timeScale = 1.0f;
        LoadSceneWithLoadingScreen(mSelectedArena);

        string modeName = (mCurrentGameMode == MultiplayerGameMode.ArmyOnly)
            ? "🎖️ 10 BİRLİK HEROSUZ ORDU SAVAŞI (225 Smithy / 8 Hız)"
            : "🗡️ KAHRAMANLI MEYDAN SAVAŞI";

        AddChatMessage("[SİSTEM]", string.Format("1v1 PvP Başladı! Mod: {0} | Arena: {1}", modeName, mSelectedArena));
    }

    public void ResetMatchState()
    {
        mIsActive = false;
        mIsMatchOver = false;
        mWinner = 0;
        mMatchTimer = 0f;
        mSuddenDeathActive = false;
        mP2ForgeLevel = 1;
        mPlayer2MaxLeadership = GetSmithyMaxCapacityForLevel(1);
        mP1UnitsSpawned = 0;
        mP2UnitsSpawned = 0;
        mP1AbilitiesUsed = 0;
        mP2AbilitiesUsed = 0;
        mP1DamageDealt = 0f;
        mP2DamageDealt = 0f;
        mP1UnitSpawnCounts.Clear();
        mP2UnitSpawnCounts.Clear();
        mGateInitialized = false;
        mOpponentHero = null;
        mOpponentHeroRespawnTimer = 0f;
        for (int i = 0; i < 10; i++)
        {
            mP1SlotCooldowns[i] = 0f;
            mP2SlotCooldowns[i] = 0f;
            mP2SlotSummonCounts[i] = 0;
            mP2SlotLastSummonTimes[i] = -999f;
        }
        for (int i = 0; i < 2; i++)
        {
            mP1SpellCooldowns[i] = 0f;
            mP2SpellCooldowns[i] = 0f;
        }
    }

    public void Rematch()
    {
        ResetMatchState();
        mIsActive = true;
        Time.timeScale = 1.0f;
        PlaySFX("UI_LegionBanner_Appear_ST_OS_01", 0.9f);
        LoadSceneWithLoadingScreen(mSelectedArena);

        if (!mIsSamePC && SVZNetworkManager.Instance != null && SVZNetworkManager.Instance.IsHost)
        {
            SVZNetworkManager.Instance.SendPacket("MATCH_REMATCH", "");
        }
    }

    public void SummonPlayer1Slot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= mP1Deck.Count) return;
        if (slotIndex < mP1SlotCooldowns.Length && mP1SlotCooldowns[slotIndex] > 0f) return;
        string unitId = mP1Deck[slotIndex];
        if (!CanTeamSpawnUnit(unitId, false)) return;
        int cost = GetUnitCost(unitId);

        Smithy smithy = WeakGlobalInstance<Smithy>.instance;
        if (smithy != null && smithy.leadership >= cost)
        {
            smithy.leadership -= cost;
            string normId = NormalizeUnitIdForCap(unitId);
            int curCount = 0;
            mP1UnitSpawnCounts.TryGetValue(normId, out curCount);
            curCount++;
            mP1UnitSpawnCounts[normId] = curCount;

            if (slotIndex < mP1SlotCooldowns.Length)
            {
                float cd = GetUniversalSpawnCooldown(unitId, curCount, GetUnitCooldownDuration(unitId));
                mP1SlotMaxCooldowns[slotIndex] = cd;
                mP1SlotCooldowns[slotIndex] = cd;
            }
            ExecuteSpawnP1Unit(unitId);
            mP1UnitsSpawned++;
            if (SVZNetworkManager.Instance != null)
            {
                SVZNetworkManager.Instance.SendPacket("P1_SUMMON", unitId);
            }
            AddChatMessage(mPlayer1Name, "Birlik çağırdı: " + GetUnitMeta(unitId).displayName);

            if (unitId == "Nobunaga" || unitId == "Takeda" || unitId == "Shogun" || unitId == "EvilNobunaga" || unitId == "EvilTakeda")
            {
                PlaySFX("UI_BossBanner_Appear_ST_OS_01", 0.9f);
                mMvpUnit = GetUnitMeta(unitId).displayName;
            }
        }
    }

    public void SummonPlayer2Unit(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= mP2Deck.Count) return;
        if (slotIndex < mP2SlotCooldowns.Length && mP2SlotCooldowns[slotIndex] > 0f) return;
        string unitId = mP2Deck[slotIndex];
        if (!CanTeamSpawnUnit(unitId, true)) return;
        int cost = GetUnitCost(unitId);

        if (mPlayer2Leadership >= cost)
        {
            mPlayer2Leadership -= cost;
            string normId = NormalizeUnitIdForCap(unitId);
            int curCount = 0;
            mP2UnitSpawnCounts.TryGetValue(normId, out curCount);
            curCount++;
            mP2UnitSpawnCounts[normId] = curCount;

            if (slotIndex < mP2SlotCooldowns.Length)
            {
                float cd = GetUniversalSpawnCooldown(unitId, curCount, GetUnitCooldownDuration(unitId));
                mP2SlotMaxCooldowns[slotIndex] = cd;
                mP2SlotCooldowns[slotIndex] = cd;
            }
            if (slotIndex >= 0 && slotIndex < mP2SlotSummonCounts.Length)
            {
                mP2SlotSummonCounts[slotIndex]++;
                mP2SlotLastSummonTimes[slotIndex] = mMatchTimer;
            }
            ExecuteSpawnP2Unit(unitId);
            mP2UnitsSpawned++;
            if (SVZNetworkManager.Instance != null)
            {
                SVZNetworkManager.Instance.SendPacket("P2_SUMMON", unitId);
            }
            AddChatMessage(mPlayer2Name, "Birlik çağırdı: " + GetUnitMeta(unitId).displayName);

            if (unitId == "Shogun" || unitId == "EvilNobunaga" || unitId == "EvilTakeda" || unitId == "Oni" || unitId == "Nobunaga")
            {
                PlaySFX("UI_BossBanner_Appear_ST_OS_01", 0.9f);
                mMvpUnit = GetUnitMeta(unitId).displayName;
            }
        }
    }

    public static int GetUnitCost(string unitId)
    {
        return sUnitCosts.ContainsKey(unitId) ? sUnitCosts[unitId] : 15;
    }

    public static UnitMeta GetUnitMeta(string unitId)
    {
        UnitMeta meta;
        if (sUnitMetaLookup.TryGetValue(unitId, out meta))
        {
            return meta;
        }
        return new UnitMeta(unitId, unitId, "⚔️ Birlik", GetUnitCost(unitId), "", 50, 30, 70, 20);
    }

    public static string GetUnitIconPath(string unitId)
    {
        switch (unitId)
        {
            // Samurai Helpers (18 - registry/helpers/)
            case "Farmer": return "Sprites/Icons/ally_farmer";
            case "FarmerOnStilts": return "Sprites/Icons/ally_farmeronstilts";
            case "SamuraiArcher": return "Sprites/Icons/ally_samurai_archer";
            case "IceArcher": return "Sprites/Icons/ally_samurai_archer_ice";
            case "Samurai": return "Sprites/Icons/ally_samurai_melee";
            case "SpearSamurai": return "Sprites/Icons/ally_samurai_spearman";
            case "HeavySamurai": return "Sprites/Icons/ally_heavy_samurai";
            case "NinjaGirl": return "Sprites/Icons/ally_assassin";
            case "Rifleman": return "Sprites/Icons/ally_rifleman";
            case "Priest": return "Sprites/Icons/ally_priest";
            case "Handmaiden": return "Sprites/Icons/ally_handmaiden";
            case "Swordsmith": return "Sprites/Icons/ally_swordsman";
            case "SpearHorseman": return "Sprites/Icons/ally_samurai_horseman";
            case "Shieldsamurai": return "Sprites/Icons/ally_shieldsamurai";
            case "ShieldSamuraiGreen": return "Sprites/Icons/ally_shieldsamuraigreen";
            case "ShieldSamuraiIce": return "Sprites/Icons/ally_shieldsamuraiice";
            case "Takeda": return "Sprites/Icons/ally_shingen";
            case "Nobunaga": return "Sprites/Icons/ally_nobanoga";

            // Zombie Helpers (31 - registry/helpers_zm/)
            case "CrawlingTorso": return "Sprites/Icons/enemy_crawling_torso";
            case "Zombie": return "Sprites/Icons/enemy_zombie_melee";
            case "ZombieMelee": return "Sprites/Icons/enemy_zombie_sword";
            case "Chochinobake": return "Sprites/Icons/enemy_chochinobake";
            case "EvilFarmer": return "Sprites/Icons/enemy_evil_farmer";
            case "ZombieAxe": return "Sprites/Icons/enemy_zombieaxe";
            case "ZombieArcher": return "Sprites/Icons/enemy_zombie_archer";
            case "SmallOni": return "Sprites/Icons/enemy_smalloni";
            case "GhostRonin": return "Sprites/Icons/enemy_ghost-ronin";
            case "EvilNinja": return "Sprites/Icons/enemy_assasin";
            case "ZombieDrummer": return "Sprites/Icons/enemy_zombie_drummer";
            case "ZombieSpearman": return "Sprites/Icons/enemy_zombie_spearman";
            case "Tengu": return "Sprites/Icons/enemy_tengu";
            case "HeavyArcher": return "Sprites/Icons/enemy_heavyarcher";
            case "ZombieWarhammer": return "Sprites/Icons/enemy_zombie_warhammer";
            case "Komusou": return "Sprites/Icons/enemy_komusou";
            case "Wanyudo": return "Sprites/Icons/enemy_wanyudo";
            case "Onibaba": return "Sprites/Icons/enemy_onibaba";
            case "ZombieRifleman": return "Sprites/Icons/enemy_zombierifleman";
            case "EvilHeavySamurai": return "Sprites/Icons/enemy_evil_heavy_samurai";
            case "Oni": return "Sprites/Icons/enemy_oni";
            case "EvilIceArcher": return "Sprites/Icons/IceArcherZomibe";
            case "WanyudoFire": return "Sprites/Icons/enemy_wanyudofire";
            case "Necromancer": return "Sprites/Icons/enemy_necromancer";
            case "ZombieHorseman": return "Sprites/Icons/enemy_zombie_horseman";
            case "WanyudoMetal": return "Sprites/Icons/enemy_wanyudometal";
            case "TenguAir": return "Sprites/Icons/enemy_tenguair";
            case "Jurogumo": return "Sprites/Icons/enemy_jurogumo";
            case "EvilNobunaga": return "Sprites/Icons/enemy_evil_nabonaga";
            case "EvilTakeda": return "Sprites/Icons/enemy_evil_takeda_shingen";
            case "Shogun": return "Sprites/Icons/enemy_shogun";

            default: return "Sprites/Icons/ally_samurai_melee";
        }
    }

    public static bool IsSamuraiUnit(string unitId)
    {
        return sSamuraiIds.Contains(unitId);
    }

    public static bool IsZombieUnit(string unitId)
    {
        return sZombieIds.Contains(unitId);
    }

    private bool mSpawningForP2 = false;

    public int GetPvPHelperLevel(string helperID)
    {
        if (mSpawningForP2)
        {
            return Mathf.Clamp(mP2TroopLevel, 1, 10);
        }
        if (mP2Deck != null && mP2Deck.Contains(helperID) && (mP1Deck == null || !mP1Deck.Contains(helperID)))
        {
            return Mathf.Clamp(mP2TroopLevel, 1, 10);
        }
        return Mathf.Clamp(mP1TroopLevel, 1, 10);
    }

    public void ExecuteSpawnP1Unit(string unitId)
    {
        InGameImpl inGame = WeakGlobalSceneBehavior<InGameImpl>.instance;
        Vector3 spawnPos = (inGame != null && inGame.helpersSpawnArea != null)
            ? inGame.helpersSpawnArea.transform.position
            : Vector3.zero;

        Character spawnedChar = null;
        mSpawningForP2 = false;
        Smithy smithy = WeakGlobalInstance<Smithy>.instance;
        if (smithy != null)
        {
            spawnedChar = smithy.ForceSpawnAt(unitId, spawnPos, false);
        }
        if (spawnedChar == null)
        {
            WaveManager waveMgr = WeakGlobalInstance<WaveManager>.instance;
            if (waveMgr != null)
            {
                spawnedChar = waveMgr.ConstructEnemyAt(unitId, spawnPos, true);
            }
        }

        if (spawnedChar != null)
        {
            // P1 Perk 0: Iron Armor (+20% HP)
            if (mP1Perk == 0)
            {
                spawnedChar.maxHealth *= 1.20f;
                spawnedChar.health = spawnedChar.maxHealth;
            }

            // P1 Perk 2: Fire Arrows (+15% Damage)
            if (mP1Perk == 2)
            {
                spawnedChar.meleeDamage *= 1.15f;
                spawnedChar.bowDamage *= 1.15f;
            }
        }
    }

    public void ExecuteSpawnP2Unit(string unitId)
    {
        InGameImpl inGame = WeakGlobalSceneBehavior<InGameImpl>.instance;
        Vector3 spawnPos = (inGame != null && inGame.enemiesSpawnArea != null)
            ? inGame.enemiesSpawnArea.transform.position
            : Vector3.zero;

        Character spawnedChar = null;
        mSpawningForP2 = true;
        Smithy smithy = WeakGlobalInstance<Smithy>.instance;
        if (smithy != null)
        {
            spawnedChar = smithy.ForceSpawnAt(unitId, spawnPos, true);
        }
        mSpawningForP2 = false;
        if (spawnedChar == null)
        {
            WaveManager waveMgr = WeakGlobalInstance<WaveManager>.instance;
            if (waveMgr != null)
            {
                spawnedChar = waveMgr.ConstructEnemyAt(unitId, spawnPos, false);
            }
        }

        if (spawnedChar != null)
        {
            // P2 Perk 1: Frenzy Rush (+25% Speed)
            if (mP2Perk == 1 && spawnedChar.controller != null)
            {
                spawnedChar.controller.speedModifier = 1.25f;
            }

            // P2 Perk 0: Plague Toughness (+20% HP)
            if (mP2Perk == 0)
            {
                spawnedChar.maxHealth *= 1.20f;
                spawnedChar.health = spawnedChar.maxHealth;
            }
        }
    }

    public void TriggerPlayer1SpellById(string spellId)
    {
        if (mCurrentGameMode == MultiplayerGameMode.ArmyOnly) return;
        if (string.IsNullOrEmpty(spellId)) return;
        if (!CanHeroUseSpell(mP1HeroType, spellId)) return;
        SpellMeta meta = GetSpellMeta(spellId);
        int slot = mP1Spells.IndexOf(spellId);
        if (slot >= 0 && slot < mP1SpellCooldowns.Length)
        {
            mP1SpellMaxCooldowns[slot] = meta.cooldown;
            mP1SpellCooldowns[slot] = meta.cooldown;
        }
        mP1AbilitiesUsed++;
        ExecuteSpellForPlayer(1, spellId);
        AddChatMessage(mPlayer1Name, "🔮 Büyü kullandı: " + meta.displayName);
        if (SVZNetworkManager.Instance != null && SVZNetworkManager.Instance.IsConnected)
        {
            SVZNetworkManager.Instance.SendPacket("P1_SPELL", spellId);
        }
    }

    public void TriggerPlayer2SpellById(string spellId)
    {
        if (mCurrentGameMode == MultiplayerGameMode.ArmyOnly) return;
        if (string.IsNullOrEmpty(spellId)) return;
        if (!CanHeroUseSpell(mP2HeroType, spellId)) return;
        int slot = mP2Spells.IndexOf(spellId);
        if (slot >= 0)
        {
            TriggerPlayer2Spell(slot);
        }
        else
        {
            SpellMeta meta = GetSpellMeta(spellId);
            mP2AbilitiesUsed++;
            ExecuteSpellForPlayer(2, spellId);
            AddChatMessage(mPlayer2Name, "🔮 Büyü kullandı: " + meta.displayName);
            if (SVZNetworkManager.Instance != null && SVZNetworkManager.Instance.IsConnected)
            {
                SVZNetworkManager.Instance.SendPacket("P2_SPELL", spellId);
            }
        }
    }

    public void TriggerPlayer2Spell(int slotIndex)
    {
        if (mCurrentGameMode == MultiplayerGameMode.ArmyOnly) return;
        if (slotIndex < 0 || slotIndex >= mP2Spells.Count || slotIndex >= mP2SpellCooldowns.Length) return;
        if (mP2SpellCooldowns[slotIndex] > 0f) return;

        string spellId = mP2Spells[slotIndex];
        if (!CanHeroUseSpell(mP2HeroType, spellId)) return;
        SpellMeta meta = GetSpellMeta(spellId);
        mP2SpellMaxCooldowns[slotIndex] = meta.cooldown;
        mP2SpellCooldowns[slotIndex] = meta.cooldown;
        mP2AbilitiesUsed++;

        ExecuteSpellForPlayer(2, spellId);
        AddChatMessage(mPlayer2Name, "🔮 Büyü kullandı: " + meta.displayName);
        if (SVZNetworkManager.Instance != null && SVZNetworkManager.Instance.IsConnected)
        {
            SVZNetworkManager.Instance.SendPacket("P2_SPELL", spellId);
        }
    }

    public void ExecuteSpellForPlayer(int playerNum, string spellId)
    {
        if (mCurrentGameMode == MultiplayerGameMode.ArmyOnly) return;
        if (string.IsNullOrEmpty(spellId)) return;
        HeroType hType = (playerNum == 1) ? mP1HeroType : mP2HeroType;
        if (!CanHeroUseSpell(hType, spellId)) return;

        InGameImpl inGame = WeakGlobalSceneBehavior<InGameImpl>.instance;
        CharactersManager charMgr = WeakGlobalInstance<CharactersManager>.instance;
        if (inGame == null || charMgr == null) return;

        bool casterIsP1 = (playerNum == 1);
        bool targetIsEnemyFlag = casterIsP1; // P1's enemies have isEnemy == true; P2's enemies have isEnemy == false
        bool p1OnLeft = (mP1Side != BattlefieldSide.RightGate);
        bool casterMarchRight = casterIsP1 ? p1OnLeft : !p1OnLeft;
        float dirSign = casterMarchRight ? 1f : -1f;

        Character casterHero = casterIsP1 ? (Character)inGame.hero : (Character)mOpponentHero;
        if (casterHero != null && casterHero.health > 0f && mCurrentGameMode == MultiplayerGameMode.HeroAndArmy && casterHero.controller != null)
        {
            casterHero.controller.PerformSpecialAction("castforward", null);
        }

        // Compute caster frontline / origin position
        Vector3 casterOrigin = Vector3.zero;
        if (casterHero != null && casterHero.health > 0f && mCurrentGameMode == MultiplayerGameMode.HeroAndArmy)
        {
            casterOrigin = casterHero.position;
        }
        else
        {
            Transform spawnArea = casterIsP1 ? inGame.helpersSpawnArea.transform : inGame.enemiesSpawnArea.transform;
            casterOrigin = (spawnArea != null) ? spawnArea.position : new Vector3(0f, 0f, casterMarchRight ? 250f : 750f);
            // Advance origin to most forward allied troop if any exist
            List<Character> alive = charMgr.allAlive;
            if (alive != null)
            {
                for (int i = 0; i < alive.Count; i++)
                {
                    Character c = alive[i];
                    if (c != null && c.health > 0f && !c.isBase && c.isEnemy == !casterIsP1)
                    {
                        if ((casterMarchRight && c.position.z > casterOrigin.z) || (!casterMarchRight && c.position.z < casterOrigin.z))
                        {
                            casterOrigin = c.position;
                        }
                    }
                }
            }
        }

        // Find primary enemy cluster / target position
        Vector3 targetClusterPos = casterOrigin + new Vector3(0f, 0f, dirSign * 180f);
        List<Character> all = charMgr.allAlive;
        if (all != null)
        {
            float bestDist = 99999f;
            for (int i = 0; i < all.Count; i++)
            {
                Character c = all[i];
                if (c != null && c.health > 0f && !c.isBase && c.isEnemy == targetIsEnemyFlag)
                {
                    float d = Mathf.Abs(c.position.z - casterOrigin.z);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        targetClusterPos = c.position;
                    }
                }
            }
        }

        switch (spellId)
        {
            case "KatanaSlash":
            {
                PlaySFX("Hero_Samurai_Slash", 1.0f);
                GameObject fxPrefab = Resources.Load("FX/PFKatanaSlash") as GameObject;
                if (fxPrefab != null)
                {
                    GameObject fx = UnityEngine.Object.Instantiate(fxPrefab, targetClusterPos, Quaternion.identity) as GameObject;
                    if (fx != null) UnityEngine.Object.Destroy(fx, 2.5f);
                }
                if (casterHero != null && casterHero.controller != null && casterHero.health > 0f)
                {
                    casterHero.controller.PerformSpecialAction("katanaslash", null);
                }
                if (all != null)
                {
                    for (int i = 0; i < all.Count; i++)
                    {
                        Character c = all[i];
                        if (c != null && c.health > 0f && !c.isBase && c.isEnemy == targetIsEnemyFlag && Mathf.Abs(c.position.z - targetClusterPos.z) <= 220f)
                        {
                            float dmg = c.isPlayer ? 140f : 240f;
                            c.RecievedAttack(EAttackType.Slice, dmg);
                            c.TryKnockback(160);
                        }
                    }
                }
                break;
            }

            case "SummonLightning":
            {
                PlaySFX("Hero_Samurai_LightningStrike", 1.0f);
                GameObject fxPrefab = Resources.Load("FX/LightningStrike") as GameObject;
                GameObject smokePrefab = Resources.Load("FX/LightningSmoke") as GameObject;
                if (fxPrefab != null)
                {
                    GameObject fx = UnityEngine.Object.Instantiate(fxPrefab, targetClusterPos, Quaternion.identity) as GameObject;
                    if (fx != null) UnityEngine.Object.Destroy(fx, 3.0f);
                }
                if (smokePrefab != null)
                {
                    GameObject smoke = UnityEngine.Object.Instantiate(smokePrefab, targetClusterPos, Quaternion.identity) as GameObject;
                    if (smoke != null) UnityEngine.Object.Destroy(smoke, 3.0f);
                }
                if (all != null)
                {
                    for (int i = 0; i < all.Count; i++)
                    {
                        Character c = all[i];
                        if (c != null && c.health > 0f && !c.isBase && c.isEnemy == targetIsEnemyFlag && Mathf.Abs(c.position.z - targetClusterPos.z) <= 230f)
                        {
                            float dmg = c.isPlayer ? 160f : 275f;
                            c.RecievedAttack(EAttackType.Lightning, dmg);
                            c.SetDamageOverTime(25f, 0.5f, 4f);
                        }
                    }
                }
                break;
            }

            case "GroundShock":
            {
                PlaySFX("Gen_Explosion_Big_01", 0.95f);
                GameObject fxPrefab = Resources.Load("FX/GroundShock") as GameObject;
                if (fxPrefab != null)
                {
                    GameObject fx = UnityEngine.Object.Instantiate(fxPrefab, targetClusterPos, Quaternion.identity) as GameObject;
                    if (fx != null)
                    {
                        GroundShockAttack gsa = fx.GetComponent<GroundShockAttack>();
                        if (gsa != null) gsa.targetEnemies = targetIsEnemyFlag;
                        UnityEngine.Object.Destroy(fx, 2.5f);
                    }
                }
                if (all != null)
                {
                    for (int i = 0; i < all.Count; i++)
                    {
                        Character c = all[i];
                        if (c != null && c.health > 0f && !c.isBase && c.isEnemy == targetIsEnemyFlag && Mathf.Abs(c.position.z - targetClusterPos.z) <= 250f)
                        {
                            float dmg = c.isPlayer ? 120f : 190f;
                            c.RecievedAttack(EAttackType.Stomp, dmg);
                            c.TryKnockback(220);
                        }
                    }
                }
                break;
            }

            case "Lethargy":
            case "LethargyZM":
            {
                PlaySFX("Hero_Samurai_Lethargy", 1.0f);
                GameObject fxPrefab = Resources.Load("FX/Lethargy") as GameObject;
                bool isDarkLethargy = (spellId == "LethargyZM");
                if (all != null)
                {
                    for (int i = 0; i < all.Count; i++)
                    {
                        Character c = all[i];
                        if (c != null && c.health > 0f && !c.isBase && c.isEnemy == targetIsEnemyFlag)
                        {
                            c.SetSpeedModifier(0.40f, 7.5f);
                            if (isDarkLethargy)
                            {
                                float dmg = c.isPlayer ? 40f : 55f;
                                c.RecievedAttack(EAttackType.Blade, dmg);
                                c.MaterialColorFadeInOut(new Color(0.45f, 0.95f, 0.25f), 0.3f, 4.5f, 0.5f);
                            }
                            if (fxPrefab != null && c.controller != null)
                            {
                                GameObject headFx = c.controller.InstantiateObjectOnJoint(fxPrefab, "head_effect");
                                if (headFx != null) UnityEngine.Object.Destroy(headFx, 7.5f);
                            }
                        }
                    }
                }
                break;
            }

            case "DivineIntervention":
            {
                PlaySFX("Hero_Samurai_Divine", 1.0f);
                GameObject fxPrefab = Resources.Load("FX/Divine") as GameObject;
                // 1) Heal all alive non-base allied units (+120 HP)
                if (all != null)
                {
                    for (int i = 0; i < all.Count; i++)
                    {
                        Character c = all[i];
                        if (c != null && c.health > 0f && !c.isBase && c.isEnemy == !casterIsP1)
                        {
                            c.health = Mathf.Min(c.maxHealth, c.health + 120f);
                            if (fxPrefab != null)
                            {
                                GameObject fx = UnityEngine.Object.Instantiate(fxPrefab, c.position, Quaternion.identity) as GameObject;
                                if (fx != null) UnityEngine.Object.Destroy(fx, 2.5f);
                            }
                        }
                    }
                }
                // 2) Summon 2 reinforcement units from caster's deck
                List<string> deck = casterIsP1 ? mP1Deck : mP2Deck;
                if (deck != null && deck.Count > 0)
                {
                    for (int k = 0; k < 2; k++)
                    {
                        string uid = deck[UnityEngine.Random.Range(0, deck.Count)];
                        if (casterIsP1) ExecuteSpawnP1Unit(uid);
                        else ExecuteSpawnP2Unit(uid);
                    }
                }
                break;
            }

            case "SummonTornadoes":
            {
                PlaySFX("Hero_Samurai_Slash", 0.95f);
                GameObject fxPrefab = Resources.Load("FX/WhirlWind") as GameObject;
                if (fxPrefab != null)
                {
                    GameObject go = UnityEngine.Object.Instantiate(fxPrefab, targetClusterPos, Quaternion.identity) as GameObject;
                    if (go != null)
                    {
                        WhirlWindDamageAura aura = go.GetComponent<WhirlWindDamageAura>();
                        if (aura == null) aura = go.AddComponent<WhirlWindDamageAura>();
                        aura.targetEnemies = targetIsEnemyFlag;
                        UnityEngine.Object.Destroy(go, 6.5f);
                    }
                }
                break;
            }

            case "GiantWave":
            case "DragonAttack":
            {
                bool isDragon = (spellId == "DragonAttack");
                string prefabPath = isDragon ? "Characters/PFDragonDamage" : "FX/CloudMass";
                PlaySFX(isDragon ? "Enemy_Hero_Samurai_DragonRage_Amb_01" : "Gen_Explosion_Big_01", 1.0f);
                GameObject wavePrefab = Resources.Load(prefabPath) as GameObject;
                if (wavePrefab != null)
                {
                    Vector3 spawnPos = casterOrigin;
                    if (inGame != null)
                    {
                        if (casterMarchRight && inGame.helpersSpawnArea != null)
                        {
                            spawnPos = inGame.helpersSpawnArea.transform.position;
                        }
                        else if (!casterMarchRight && inGame.enemiesSpawnArea != null)
                        {
                            spawnPos = inGame.enemiesSpawnArea.transform.position;
                        }
                    }
                    if (WeakGlobalInstance<RailManager>.instance != null)
                    {
                        spawnPos.y = WeakGlobalInstance<RailManager>.instance.GetY(spawnPos.z);
                    }
                    Quaternion rot = casterMarchRight ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f);
                    GameObject waveObj = UnityEngine.Object.Instantiate(wavePrefab, spawnPos, rot) as GameObject;
                    if (waveObj != null)
                    {
                        GiantWave gw = waveObj.GetComponent<GiantWave>();
                        if (gw == null) gw = waveObj.AddComponent<GiantWave>();
                        gw.useCustomSettings = true;
                        gw.customAbilityID = spellId;
                        gw.targetEnemies = targetIsEnemyFlag;
                        gw.moveLeftToRight = casterMarchRight;
                    }
                }
                break;
            }

            case "LifeSteal":
            {
                PlaySFX("Hero_Samurai_Divine", 0.9f);
                GameObject fxPrefab = Resources.Load("FX/LifeSteal") as GameObject;
                if (fxPrefab != null)
                {
                    Vector3 fxPos = (casterHero != null && casterHero.health > 0f) ? casterHero.position : casterOrigin;
                    GameObject fx = UnityEngine.Object.Instantiate(fxPrefab, fxPos, Quaternion.identity) as GameObject;
                    if (fx != null) UnityEngine.Object.Destroy(fx, 2.0f);
                }
                float totalDrained = 0f;
                if (all != null)
                {
                    for (int i = 0; i < all.Count; i++)
                    {
                        Character c = all[i];
                        if (c != null && c.health > 0f && !c.isBase && c.isEnemy == targetIsEnemyFlag && Mathf.Abs(c.position.z - targetClusterPos.z) <= 260f)
                        {
                            float dmg = c.isPlayer ? 110f : 185f;
                            c.RecievedAttack(EAttackType.Blade, dmg);
                            totalDrained += 55f;
                        }
                    }
                    // Heal allied non-base units
                    float allyHeal = Mathf.Clamp(totalDrained, 80f, 180f);
                    for (int i = 0; i < all.Count; i++)
                    {
                        Character c = all[i];
                        if (c != null && c.health > 0f && !c.isBase && c.isEnemy == !casterIsP1)
                        {
                            c.health = Mathf.Min(c.maxHealth, c.health + allyHeal);
                        }
                    }
                }
                break;
            }

            case "GraveHands":
            {
                PlaySFX("Enemy_Zombie_Spawn_01", 1.0f);
                GameObject fxPrefab = Resources.Load("FX/LightningStrike") as GameObject;
                GameObject fogPrefab = Resources.Load("FX/FogRed") as GameObject;
                if (fxPrefab != null)
                {
                    GameObject fx = UnityEngine.Object.Instantiate(fxPrefab, targetClusterPos, Quaternion.identity) as GameObject;
                    if (fx != null) UnityEngine.Object.Destroy(fx, 3.0f);
                }
                if (fogPrefab != null)
                {
                    GameObject fog = UnityEngine.Object.Instantiate(fogPrefab, targetClusterPos, Quaternion.identity) as GameObject;
                    if (fog != null) UnityEngine.Object.Destroy(fog, 4.0f);
                }
                if (all != null)
                {
                    for (int i = 0; i < all.Count; i++)
                    {
                        Character c = all[i];
                        if (c != null && c.health > 0f && !c.isBase && c.isEnemy == targetIsEnemyFlag && Mathf.Abs(c.position.z - targetClusterPos.z) <= 230f)
                        {
                            float dmg = c.isPlayer ? 135f : 230f;
                            c.RecievedAttack(EAttackType.Blunt, dmg);
                            c.SetSpeedModifier(0.20f, 4.0f);
                        }
                    }
                }
                break;
            }

            case "NightOfTheDead":
            {
                PlaySFX("Enemy_Zombie_Spawn_01", 1.0f);
                GameObject fxPrefab = Resources.Load("FX/NightOfDead") as GameObject;
                if (fxPrefab != null)
                {
                    GameObject fx = UnityEngine.Object.Instantiate(fxPrefab, casterOrigin, Quaternion.identity) as GameObject;
                    if (fx != null) UnityEngine.Object.Destroy(fx, 3.5f);
                }
                if (all != null)
                {
                    for (int i = 0; i < all.Count; i++)
                    {
                        Character c = all[i];
                        if (c != null && c.health > 0f && !c.isBase && c.isEnemy == !casterIsP1)
                        {
                            c.health = Mathf.Min(c.maxHealth, c.health + 90f);
                            c.SetSpeedModifier(1.38f, 10f);
                            c.MaterialColorFadeInOut(new Color(0.65f, 0.25f, 0.95f), 0.25f, 6.0f, 0.5f);
                        }
                    }
                }
                break;
            }

            case "RaiseDead":
            {
                PlaySFX("Enemy_Zombie_Spawn_01", 1.0f);
                GameObject fxPrefab = Resources.Load("FX/DivineEnemy") as GameObject;
                GameObject revPrefab = Resources.Load("FX/ReviveZombie") as GameObject;
                Vector3 raisePos = casterOrigin + new Vector3(0f, 0f, dirSign * 90f);
                if (fxPrefab != null)
                {
                    GameObject fx = UnityEngine.Object.Instantiate(fxPrefab, raisePos, Quaternion.identity) as GameObject;
                    if (fx != null) UnityEngine.Object.Destroy(fx, 3.0f);
                }
                if (revPrefab != null)
                {
                    GameObject rev = UnityEngine.Object.Instantiate(revPrefab, raisePos, Quaternion.identity) as GameObject;
                    if (rev != null) UnityEngine.Object.Destroy(rev, 3.0f);
                }
                string[] risenPool = new string[] { "ZombieMelee", "ZombieSpearman", "ZombieAxe" };
                Smithy smithy = WeakGlobalInstance<Smithy>.instance;
                for (int k = 0; k < 2; k++)
                {
                    string zId = risenPool[UnityEngine.Random.Range(0, risenPool.Length)];
                    Vector3 pos = raisePos + new Vector3(0f, 0f, (k - 0.5f) * 35f);
                    if (smithy != null)
                    {
                        smithy.ForceSpawnAt(zId, pos, !casterIsP1);
                    }
                }
                break;
            }

            default:
            {
                // Fallback execution
                PlaySFX("Gen_Explosion_Big_01", 0.9f);
                break;
            }
        }
    }

    public void TriggerPlayer1Heal()
    {
        TriggerPlayer1SpellById("DivineIntervention");
    }

    public void TriggerPlayer2Poison()
    {
        TriggerPlayer2Spell(1);
    }

    public void TriggerPlayer2Barrage()
    {
        TriggerPlayer2Spell(0);
    }

    public void DamageOpponentGate(float damage)
    {
        mGate2Health = Mathf.Max(0f, mGate2Health - damage);
        InGameImpl inGame = WeakGlobalSceneBehavior<InGameImpl>.instance;
        if (inGame != null && inGame.enemyGate != null)
        {
            inGame.enemyGate.health = mGate2Health;
        }
    }

    public void UpdateOpponentHeroLifecycle(InGameImpl inGame)
    {
        if (inGame == null || mIsMatchOver) return;

        if (mOpponentHero == null)
        {
            if (mOpponentHeroRespawnTimer > 0f)
            {
                mOpponentHeroRespawnTimer -= Time.deltaTime;
                if (mOpponentHeroRespawnTimer <= 0f)
                {
                    SpawnOpponentHero(inGame);
                }
            }
            else
            {
                SpawnOpponentHero(inGame);
            }
        }
    }

    public void SpawnOpponentHero(InGameImpl inGame)
    {
        if (inGame == null) return;

        bool p2AtRight = (mP1Side != BattlefieldSide.RightGate);
        Vector3 spawnPos = (p2AtRight && inGame.enemiesSpawnArea != null)
            ? inGame.enemiesSpawnArea.transform.position
            : ((inGame.heroSpawnPointLeft != null) ? inGame.heroSpawnPointLeft.position : Vector3.zero);

        float zTarget = p2AtRight
            ? ((inGame.heroWalkLeftEdge != null) ? inGame.heroWalkLeftEdge.position.z : 0f)
            : ((inGame.heroWalkRightEdge != null) ? inGame.heroWalkRightEdge.position.z : 1000f);

        bool p2IsZombie = (mP2HeroType == HeroType.Zombie);
        string model = p2IsZombie ? "Characters/PFZombieHero" : "Characters/PFHero01";
        bool faceLeft = p2AtRight;

        mOpponentHero = new OpponentHero(model, zTarget, spawnPos, faceLeft, 1.0f);
        if (WeakGlobalInstance<CharactersManager>.instance != null)
        {
            WeakGlobalInstance<CharactersManager>.instance.AddCharacter(mOpponentHero);
        }

        mOpponentHeroRespawnTimer = 0f;
        PlaySFX(p2IsZombie ? "Enemy_Zombie_Spawn_01" : "Hero_Samurai_Divine", 0.9f);
        AddChatMessage("[DÜELLO]", "💀 " + mOpponentHero.DisplayName + " sahaya indi! 1v1 Kahraman Düellosu başladı!");
    }

    public void OnOpponentHeroKilled(OpponentHero hero)
    {
        PlaySFX("Hero_Samurai_Divine", 1.0f);
        PlaySFX("Enemy_Zombie_Spawn_01", 0.6f);

        Smithy smithy = WeakGlobalInstance<Smithy>.instance;
        if (smithy != null)
        {
            smithy.leadership = Mathf.Min((float)smithy.maxLeadership, smithy.leadership + 20f);
        }
        Singleton<Profile>.instance.coins += 500;
        mP1DamageDealt += 1000f;

        AddChatMessage("[DÜELLO]", "⚔️ " + hero.DisplayName + " katledildi!");

        if (mCurrentGameMode == MultiplayerGameMode.HeroAndArmy)
        {
            AddChatMessage("[KAHRAMAN DÜELLOSU]", "👑 " + hero.DisplayName + " devrildi! 1. Oyuncu MAÇI KAZANDI!");
            EndMatch(1);
        }
        else
        {
            mOpponentHeroRespawnTimer = kOpponentHeroMaxRespawn;
        }
    }

    public void EndMatch(int winner)
    {
        if (mIsMatchOver) return;
        mIsMatchOver = true;
        mWinner = winner;

        if (winner == 1)
        {
            PlaySFX("Sting_Game_Win_01", 1.0f);
            AddChatMessage("[ZAFER!]", string.Format("{0} MAÇI KAZANDI!", mPlayer1Name));
        }
        else
        {
            PlaySFX("Sting_Game_Lose_01", 1.0f);
            AddChatMessage("[ZAFER!]", string.Format("{0} MAÇI KAZANDI!", mPlayer2Name));
        }

        if (SVZNetworkManager.Instance != null && SVZNetworkManager.Instance.IsHost)
        {
            SVZNetworkManager.Instance.SendPacket("MATCH_OVER", winner.ToString());
        }
    }

    public void ApplyDeckPreset(int presetIndex)
    {
        switch (presetIndex)
        {
            case 0: // Dengeli Klasik
                mP1FiveUnitDeck = new List<string> { "Samurai", "SamuraiArcher", "SpearSamurai", "HeavySamurai", "Nobunaga" };
                mP2FiveUnitDeck = new List<string> { "ZombieSpearman", "HeavyArcher", "ZombieWarhammer", "Oni", "EvilNobunaga" };
                mP1TenUnitDeck = new List<string> { "Farmer", "Samurai", "SamuraiArcher", "SpearSamurai", "Rifleman", "Priest", "HeavySamurai", "Shieldsamurai", "Takeda", "Nobunaga" };
                mP2TenUnitDeck = new List<string> { "Zombie", "ZombieMelee", "ZombieArcher", "ZombieSpearman", "HeavyArcher", "ZombieWarhammer", "EvilHeavySamurai", "Oni", "EvilTakeda", "EvilNobunaga" };
                mP1Spells = new List<string> { "GiantWave", "Lethargy" };
                mP2Spells = new List<string> { "DragonAttack", "GraveHands" };
                break;
            case 1: // Zırhlı Savunma
                mP1FiveUnitDeck = new List<string> { "Samurai", "Rifleman", "HeavySamurai", "Shieldsamurai", "Takeda" };
                mP2FiveUnitDeck = new List<string> { "ZombieSpearman", "GhostRonin", "ZombieWarhammer", "EvilHeavySamurai", "EvilTakeda" };
                mP1TenUnitDeck = new List<string> { "Farmer", "Swordsmith", "Samurai", "SpearSamurai", "Rifleman", "Priest", "HeavySamurai", "Shieldsamurai", "ShieldSamuraiGreen", "Takeda" };
                mP2TenUnitDeck = new List<string> { "EvilFarmer", "ZombieSpearman", "GhostRonin", "HeavyArcher", "ZombieRifleman", "ZombieWarhammer", "EvilHeavySamurai", "Oni", "WanyudoMetal", "EvilTakeda" };
                mP1Spells = new List<string> { "DivineIntervention", "GroundShock" };
                mP2Spells = new List<string> { "RaiseDead", "NightOfTheDead" };
                break;
            case 2: // Hızlı Hücum
                mP1FiveUnitDeck = new List<string> { "FarmerOnStilts", "NinjaGirl", "Samurai", "IceArcher", "SpearHorseman" };
                mP2FiveUnitDeck = new List<string> { "ZombieAxe", "Chochinobake", "Tengu", "ZombieHorseman", "TenguAir" };
                mP1TenUnitDeck = new List<string> { "Farmer", "FarmerOnStilts", "NinjaGirl", "Samurai", "SamuraiArcher", "SpearSamurai", "IceArcher", "Handmaiden", "SpearHorseman", "Nobunaga" };
                mP2TenUnitDeck = new List<string> { "CrawlingTorso", "Zombie", "ZombieAxe", "Chochinobake", "EvilNinja", "SmallOni", "Tengu", "Wanyudo", "ZombieHorseman", "TenguAir" };
                mP1Spells = new List<string> { "KatanaSlash", "SummonTornadoes" };
                mP2Spells = new List<string> { "LifeSteal", "DragonAttack" };
                break;
            case 3: // Zombi / Taktiksel Sürü
                mP1FiveUnitDeck = new List<string> { "Farmer", "Swordsmith", "Priest", "Shieldsamurai", "ShieldSamuraiIce" };
                mP2FiveUnitDeck = new List<string> { "Zombie", "ZombieMelee", "ZombieAxe", "Oni", "Necromancer" };
                mP1TenUnitDeck = new List<string> { "Farmer", "Swordsmith", "SamuraiArcher", "Rifleman", "Priest", "Handmaiden", "Shieldsamurai", "ShieldSamuraiGreen", "ShieldSamuraiIce", "Takeda" };
                mP2TenUnitDeck = new List<string> { "CrawlingTorso", "Zombie", "ZombieMelee", "ZombieAxe", "ZombieDrummer", "Onibaba", "Komusou", "Oni", "Necromancer", "Jurogumo" };
                mP1Spells = new List<string> { "SummonLightning", "DivineIntervention" };
                mP2Spells = new List<string> { "RaiseDead", "LethargyZM" };
                break;
            case 4: // Karma / Hibrit Ordu
                mP1FiveUnitDeck = new List<string> { "Samurai", "NinjaGirl", "HeavySamurai", "Oni", "Nobunaga" };
                mP2FiveUnitDeck = new List<string> { "ZombieArcher", "SpearSamurai", "Shieldsamurai", "ZombieHorseman", "Shogun" };
                mP1TenUnitDeck = new List<string> { "Samurai", "NinjaGirl", "ZombieSpearman", "Rifleman", "Priest", "HeavySamurai", "Oni", "ZombieHorseman", "TenguAir", "Nobunaga" };
                mP2TenUnitDeck = new List<string> { "ZombieArcher", "SpearSamurai", "GhostRonin", "HeavyArcher", "ZombieWarhammer", "Shieldsamurai", "EvilIceArcher", "ZombieHorseman", "EvilTakeda", "Shogun" };
                mP1Spells = new List<string> { "GiantWave", "KatanaSlash" };
                mP2Spells = new List<string> { "DragonAttack", "GraveHands" };
                break;
        }

        if (mCurrentGameMode == MultiplayerGameMode.ArmyOnly)
        {
            mP1Deck = new List<string>(mP1TenUnitDeck);
            mP2Deck = new List<string>(mP2TenUnitDeck);
        }
        else
        {
            mP1Deck = new List<string>(mP1FiveUnitDeck);
            mP2Deck = new List<string>(mP2FiveUnitDeck);
        }
        EnsureValidSpellsForPlayer(1);
        EnsureValidSpellsForPlayer(2);
    }

    private static void ShuffleList<T>(List<T> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(0, i + 1);
            T temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }

    private string mLastP1RandomDeckSig = string.Empty;
    private string mLastP2RandomDeckSig = string.Empty;

    public void GenerateRandomSet(int player, bool randomizeHero = false)
    {
        int seed = System.Environment.TickCount ^ System.Guid.NewGuid().GetHashCode() ^ (int)(Time.realtimeSinceStartup * 10000f);
        System.Random rng = new System.Random(seed);

        int targetCount = GetTargetDeckSize();
        HeroType effectiveHero = (player == 1) ? mP1HeroType : mP2HeroType;

        if (randomizeHero)
        {
            effectiveHero = (rng.Next(0, 2) == 0) ? HeroType.Samurai : HeroType.Zombie;
            if (player == 1) mP1HeroType = effectiveHero;
            else mP2HeroType = effectiveHero;
            BroadcastLobbyPick(player, "HERO", 0, effectiveHero.ToString());
        }

        List<string> pickedUnits = new List<string>();

        // High-entropy shuffled full roster
        List<UnitMeta> pool = new List<UnitMeta>(sAllUnitMetas);
        ShuffleList(pool, rng);

        if (DisableEliteLimitsStatic || targetCount > 5)
        {
            // Fully unlimited or 10-unit mode: draw directly from shuffled full roster
            for (int i = 0; i < pool.Count && pickedUnits.Count < targetCount; i++)
            {
                string uid = pool[i].id;
                if (!pickedUnits.Contains(uid))
                {
                    pickedUnits.Add(uid);
                }
            }
        }
        else
        {
            // 5-Unit Quota Mode: Partition by tier to guarantee valid quota with maximum diversity
            List<string> legendaries = new List<string>();
            List<string> elites = new List<string>();
            List<string> cores = new List<string>();

            for (int i = 0; i < pool.Count; i++)
            {
                string uid = pool[i].id;
                UnitTier tier = GetUnitTier(uid);
                if (tier == UnitTier.Legendary) legendaries.Add(uid);
                else if (tier == UnitTier.Elite) elites.Add(uid);
                else cores.Add(uid);
            }

            ShuffleList(legendaries, rng);
            ShuffleList(elites, rng);
            ShuffleList(cores, rng);

            // Dynamic distribution: 0 or 1 Legendary, 1 or 2 Elites, remaining Cores (at least 2 Cores)
            int numLeg = rng.Next(0, 2); // 0 or 1
            int numElite = rng.Next(1, 3); // 1 or 2
            int numCore = targetCount - numLeg - numElite;
            if (numCore < 2)
            {
                numCore = 2;
                numElite = targetCount - numLeg - numCore;
            }

            for (int i = 0; i < legendaries.Count && pickedUnits.Count < numLeg; i++)
            {
                if (!pickedUnits.Contains(legendaries[i])) pickedUnits.Add(legendaries[i]);
            }
            int targetWithElite = pickedUnits.Count + numElite;
            for (int i = 0; i < elites.Count && pickedUnits.Count < targetWithElite; i++)
            {
                if (!pickedUnits.Contains(elites[i])) pickedUnits.Add(elites[i]);
            }
            for (int i = 0; i < cores.Count && pickedUnits.Count < targetCount; i++)
            {
                if (!pickedUnits.Contains(cores[i])) pickedUnits.Add(cores[i]);
            }
        }

        // Fallback fill from shuffled pool if needed
        for (int i = 0; i < pool.Count && pickedUnits.Count < targetCount; i++)
        {
            if (!pickedUnits.Contains(pool[i].id)) pickedUnits.Add(pool[i].id);
        }

        // Sort ascending by unit cost for clean hotkeys
        pickedUnits.Sort((a, b) => GetUnitCost(a).CompareTo(GetUnitCost(b)));
        string newSig = string.Join(",", pickedUnits.ToArray());
        if (player == 1) mLastP1RandomDeckSig = newSig;
        else mLastP2RandomDeckSig = newSig;

        if (player == 1)
        {
            mP1Deck = new List<string>(pickedUnits);
            if (targetCount == 10) mP1TenUnitDeck = new List<string>(pickedUnits);
            else mP1FiveUnitDeck = new List<string>(pickedUnits);
        }
        else
        {
            mP2Deck = new List<string>(pickedUnits);
            if (targetCount == 10) mP2TenUnitDeck = new List<string>(pickedUnits);
            else mP2FiveUnitDeck = new List<string>(pickedUnits);
        }

        // Randomize 2 Spells matching Hero
        List<SpellMeta> heroSpells = GetSpellsForHero(effectiveHero);
        if (heroSpells != null && heroSpells.Count >= 2)
        {
            List<SpellMeta> shuffledSpells = new List<SpellMeta>(heroSpells);
            ShuffleList(shuffledSpells, rng);
            List<string> spPicked = new List<string> { shuffledSpells[0].id, shuffledSpells[1].id };
            if (player == 1) mP1Spells = spPicked;
            else mP2Spells = spPicked;
        }

        // Randomize Perk (0..2)
        int randPerk = rng.Next(0, 3);
        if (player == 1) mP1Perk = randPerk;
        else mP2Perk = randPerk;

        // Broadcast to remote player if online
        for (int s = 0; s < pickedUnits.Count; s++)
        {
            BroadcastLobbyPick(player, "SLOT", s, pickedUnits[s]);
        }
        List<string> curSpells = (player == 1) ? mP1Spells : mP2Spells;
        for (int sp = 0; sp < curSpells.Count; sp++)
        {
            BroadcastLobbyPick(player, "SPELL", sp, curSpells[sp]);
        }
        BroadcastLobbyPick(player, "PERK", 0, randPerk.ToString());
        BroadcastFullLobbySync();

        PlaySFX("UI_Equip_01", 0.9f);
    }

    public void GenerateRandomSetsForBothSides(bool randomizeHeroes = false)
    {
        GenerateRandomSet(1, randomizeHeroes);
        GenerateRandomSet(2, randomizeHeroes);
        PlaySFX("UI_Upgrade_01", 1.0f);
        AddChatMessage("[LOBİ]", "🎲 Her iki taraf için de yepyeni ve tamamen rastgele setler (Birlikler, Büyüler, Perkler) oluşturuldu!");
    }

    public void RandomizeDeck(int player, int filterMode = 0)
    {
        GenerateRandomSet(player, false);
    }

    public static void AddChatMessage(string sender, string message)
    {
        if (sInstance != null)
        {
            string formatted = string.Format("[{0}] {1}", sender, message);
            sInstance.mChatMessages.Add(formatted);
            if (sInstance.mChatMessages.Count > 40)
            {
                sInstance.mChatMessages.RemoveAt(0);
            }
        }
    }

    public void HandleNetworkPacket(string header, string payload)
    {
        switch (header)
        {
            case "LOBBY_CONFIG":
                string[] cfg = payload.Split('|');
                if (cfg.Length >= 5)
                {
                    mSelectedArena = cfg[0];
                    mCurrentGameMode = (MultiplayerGameMode)Enum.Parse(typeof(MultiplayerGameMode), cfg[1]);
                    EnsureDeckSizeForCurrentMode();
                    float.TryParse(cfg[2], out mGateMaxHealthSetting);
                    float.TryParse(cfg[3], out mEnergySpeed);
                    bool.TryParse(cfg[4], out mBlindPick);
                    if (cfg.Length >= 6) bool.TryParse(cfg[5], out mAllowBackAttack);
                    if (cfg.Length >= 7)
                    {
                        int sLvl;
                        if (int.TryParse(cfg[6], out sLvl)) SharedTroopLevel = sLvl;
                    }
                    if (cfg.Length >= 8) bool.TryParse(cfg[7], out mDisableEliteLimits);
                    PlaySFX("UI_Click_01", 0.8f);
                    AddChatMessage("[LOBİ]", string.Format("Oda Ayarları: Harita: {0} | Birlik: Lv.{1} | Kör Seçim: {2}", mSelectedArena, SharedTroopLevel, mBlindPick ? "AÇIK (Gizli)" : "KAPALI (Görünür)"));
                }
                break;

            case "DISABLE_ELITE_LIMITS":
                bool del;
                if (bool.TryParse(payload, out del))
                {
                    mDisableEliteLimits = del;
                    PlaySFX("UI_Upgrade_01", 0.9f);
                    AddChatMessage("[LOBİ]", mDisableEliteLimits
                        ? "⚡ ELİT VE BOSS BİRLİK SINIRLAMALARI KALDIRILDI! (Sınırsız sayıda Boss ve Elit üretilebilir)"
                        : "🛡️ Elit ve Boss birlik sınırlamaları standart moda alındı.");
                }
                break;

            case "LOBBY_FULL_SYNC":
                string[] sParts = payload.Split('|');
                if (sParts.Length >= 8)
                {
                    mSelectedArena = sParts[0];
                    mCurrentGameMode = (MultiplayerGameMode)Enum.Parse(typeof(MultiplayerGameMode), sParts[1]);
                    EnsureDeckSizeForCurrentMode();
                    float.TryParse(sParts[2], out mGateMaxHealthSetting);
                    float.TryParse(sParts[3], out mEnergySpeed);
                    bool.TryParse(sParts[4], out mBlindPick);
                    mP1HeroType = (HeroType)Enum.Parse(typeof(HeroType), sParts[5]);
                    int.TryParse(sParts[6], out mP1Perk);
                    mP1Deck = new List<string>(sParts[7].Split(','));
                    if (sParts.Length >= 9 && !string.IsNullOrEmpty(sParts[8]))
                    {
                        mP1Spells = new List<string>(sParts[8].Split(','));
                    }
                    if (sParts.Length >= 10) bool.TryParse(sParts[9], out mAllowBackAttack);
                    if (sParts.Length >= 11)
                    {
                        int sLvl;
                        if (int.TryParse(sParts[10], out sLvl)) SharedTroopLevel = sLvl;
                    }
                    if (sParts.Length >= 12) bool.TryParse(sParts[11], out mDisableEliteLimits);
                    EnsureValidSpellsForPlayer(1);
                    EnsureValidSpellsForPlayer(2);

                    string reply = string.Format("{0}|{1}|{2}|{3}",
                        mP2HeroType,
                        mP2Perk,
                        string.Join(",", mP2Deck.ToArray()),
                        string.Join(",", mP2Spells.ToArray()));
                    if (SVZNetworkManager.Instance != null)
                    {
                        SVZNetworkManager.Instance.SendPacket("LOBBY_CLIENT_RESPONSE", reply);
                    }
                    AddChatMessage("[LOBİ]", "Kurucu oda bilgileri eşitlendi!");
                }
                break;

            case "TROOP_LVL_PROPOSE":
                string[] propParts = payload.Split('|');
                if (propParts.Length >= 2)
                {
                    int propLvl, propBy;
                    if (int.TryParse(propParts[0], out propLvl) && int.TryParse(propParts[1], out propBy))
                    {
                        mPendingTroopLevelProposal = Mathf.Clamp(propLvl, 1, 10);
                        mPendingTroopLevelProposer = propBy;
                        PlaySFX("UI_Window_Open_01", 0.9f);
                        AddChatMessage("[LOBİ]", string.Format("⚔️ {0}. Oyuncu Birlik Seviyesini Lv.{1} (Eşit) yapmak için teklif gönderdi! Orta panelden [ONAYLA] butonuna basabilirsiniz.", propBy, mPendingTroopLevelProposal));
                    }
                }
                break;

            case "TROOP_LVL_APPROVE":
                int appLvl;
                if (int.TryParse(payload, out appLvl))
                {
                    SharedTroopLevel = Mathf.Clamp(appLvl, 1, 10);
                    mPendingTroopLevelProposal = 0;
                    mPendingTroopLevelProposer = 0;
                    PlaySFX("UI_Upgrade_01", 0.9f);
                    AddChatMessage("[LOBİ]", string.Format("✅ Karşı taraf onayladı! Birlik Seviyeleri EŞİT: Lv.{0} olarak kilitlendi!", SharedTroopLevel));
                }
                break;

            case "TROOP_LVL_REJECT":
                int rejLvl;
                int.TryParse(payload, out rejLvl);
                mPendingTroopLevelProposal = 0;
                mPendingTroopLevelProposer = 0;
                PlaySFX("UI_Cannot_Afford_01", 0.85f);
                AddChatMessage("[LOBİ]", string.Format("❌ Karşı taraf Lv.{0} birlik seviyesi teklifini reddetti (Mevcut: Lv.{1}).", rejLvl, SharedTroopLevel));
                break;

            case "LOBBY_CLIENT_RESPONSE":
                string[] rParts = payload.Split('|');
                if (rParts.Length >= 3)
                {
                    mP2HeroType = (HeroType)Enum.Parse(typeof(HeroType), rParts[0]);
                    int.TryParse(rParts[1], out mP2Perk);
                    mP2Deck = new List<string>(rParts[2].Split(','));
                    if (rParts.Length >= 4 && !string.IsNullOrEmpty(rParts[3]))
                    {
                        mP2Spells = new List<string>(rParts[3].Split(','));
                    }
                    EnsureValidSpellsForPlayer(2);
                    AddChatMessage("[LOBİ]", "🟢 2. Oyuncu destesi ve büyüleri senkronize edildi!");
                }
                break;

            case "LOBBY_PICK":
                string[] pickParts = payload.Split('|');
                if (pickParts.Length >= 4)
                {
                    int pNum = int.Parse(pickParts[0]);
                    string pType = pickParts[1];
                    int sIdx = int.Parse(pickParts[2]);
                    string val = pickParts[3];

                    if (pNum == 1)
                    {
                        if (pType == "SLOT" && sIdx >= 0 && sIdx < mP1Deck.Count) mP1Deck[sIdx] = val;
                        else if (pType == "SPELL" && sIdx >= 0 && sIdx < mP1Spells.Count && CanHeroUseSpell(mP1HeroType, val)) mP1Spells[sIdx] = val;
                        else if (pType == "HERO")
                        {
                            mP1HeroType = (HeroType)Enum.Parse(typeof(HeroType), val);
                            EnsureValidSpellsForPlayer(1);
                        }
                        else if (pType == "PERK") int.TryParse(val, out mP1Perk);
                    }
                    else if (pNum == 2)
                    {
                        if (pType == "SLOT" && sIdx >= 0 && sIdx < mP2Deck.Count) mP2Deck[sIdx] = val;
                        else if (pType == "SPELL" && sIdx >= 0 && sIdx < mP2Spells.Count && CanHeroUseSpell(mP2HeroType, val)) mP2Spells[sIdx] = val;
                        else if (pType == "HERO")
                        {
                            mP2HeroType = (HeroType)Enum.Parse(typeof(HeroType), val);
                            EnsureValidSpellsForPlayer(2);
                        }
                        else if (pType == "PERK") int.TryParse(val, out mP2Perk);
                    }
                    PlaySFX("UI_Equip_01", 0.7f);
                }
                break;

            case "LOBBY_READY":
                bool isR;
                if (bool.TryParse(payload, out isR))
                {
                    mIsP2Ready = isR;
                    PlaySFX(isR ? "UI_Upgrade_01" : "UI_Click_01", 0.9f);
                    AddChatMessage("[LOBİ]", isR ? "🟢 2. OYUNCU HAZIR!" : "⏳ 2. Oyuncu hazırlanıyor...");
                }
                break;

            case "MATCH_START":
                string[] parts = payload.Split('|');
                if (parts.Length >= 10)
                {
                    mCurrentGameMode = (MultiplayerGameMode)Enum.Parse(typeof(MultiplayerGameMode), parts[0]);
                    int.TryParse(parts[1], out mP1TroopLevel);
                    int.TryParse(parts[2], out mP2TroopLevel);
                    mSelectedArena = parts[3];
                    float.TryParse(parts[4], out mGateMaxHealthSetting);
                    float.TryParse(parts[5], out mEnergySpeed);
                    int.TryParse(parts[6], out mP1Perk);
                    int.TryParse(parts[7], out mP2Perk);
                    mP1Deck = new List<string>(parts[8].Split(','));
                    mP2Deck = new List<string>(parts[9].Split(','));
                    if (parts.Length > 10)
                    {
                        mP1Side = (BattlefieldSide)Enum.Parse(typeof(BattlefieldSide), parts[10]);
                    }
                    if (parts.Length > 12)
                    {
                        mP1HeroType = (HeroType)Enum.Parse(typeof(HeroType), parts[11]);
                        mP2HeroType = (HeroType)Enum.Parse(typeof(HeroType), parts[12]);
                    }
                    if (parts.Length > 14)
                    {
                        if (!string.IsNullOrEmpty(parts[13])) mP1Spells = new List<string>(parts[13].Split(','));
                        if (!string.IsNullOrEmpty(parts[14])) mP2Spells = new List<string>(parts[14].Split(','));
                    }
                    if (parts.Length > 15)
                    {
                        bool.TryParse(parts[15], out mAllowBackAttack);
                    }
                }
                StartMultiplayerMatch(false);
                break;

            case "MATCH_REMATCH":
                mIsMatchOver = false;
                mWinner = 0;
                StartMultiplayerMatch(false);
                AddChatMessage("[SİSTEM]", "Rövanş başladı! İki taraf yeniden kapışıyor!");
                break;

            case "P1_SUMMON":
                ExecuteSpawnP1Unit(payload);
                mP1UnitsSpawned++;
                AddChatMessage(mPlayer1Name, "Birlik çağırdı: " + GetUnitMeta(payload).displayName);
                break;

            case "P2_SUMMON":
                ExecuteSpawnP2Unit(payload);
                mP2UnitsSpawned++;
                AddChatMessage(mPlayer2Name, "Birlik çağırdı: " + GetUnitMeta(payload).displayName);
                break;

            case "P2_FORGE":
                int fLvl;
                if (int.TryParse(payload, out fLvl))
                {
                    mP2ForgeLevel = Mathf.Clamp(fLvl, 1, 4);
                    mPlayer2MaxLeadership = GetSmithyMaxCapacityForLevel(mP2ForgeLevel);
                    AddChatMessage(mPlayer2Name, string.Format("⚡ Demirci Ocağını (Smithy) Lv.{0} seviyesine yükseltti! (Max: {1:0})", mP2ForgeLevel, mPlayer2MaxLeadership));
                }
                break;

            case "P1_SPELL":
                ExecuteSpellForPlayer(1, payload);
                mP1AbilitiesUsed++;
                AddChatMessage(mPlayer1Name, "🔮 Büyü kullandı: " + GetSpellMeta(payload).displayName);
                break;

            case "P2_SPELL":
                ExecuteSpellForPlayer(2, payload);
                mP2AbilitiesUsed++;
                AddChatMessage(mPlayer2Name, "🔮 Büyü kullandı: " + GetSpellMeta(payload).displayName);
                break;

            case "P1_HEAL":
                ExecuteSpellForPlayer(1, "DivineIntervention");
                break;

            case "P2_POISON":
                ExecuteSpellForPlayer(2, "LethargyZM");
                break;

            case "P2_BARRAGE":
                ExecuteSpellForPlayer(2, "DragonAttack");
                break;

            case "GATE_SYNC":
                string[] gateParts = payload.Split('|');
                if (gateParts.Length == 2)
                {
                    float g1, g2;
                    InGameImpl ig = WeakGlobalSceneBehavior<InGameImpl>.instance;
                    if (float.TryParse(gateParts[0], out g1))
                    {
                        mGate1Health = Mathf.Min(mGate1Health, g1);
                        if (ig != null && ig.gate != null) ig.gate.health = mGate1Health;
                    }
                    if (float.TryParse(gateParts[1], out g2))
                    {
                        mGate2Health = Mathf.Min(mGate2Health, g2);
                        if (ig != null && ig.enemyGate != null) ig.enemyGate.health = mGate2Health;
                    }
                }
                break;

            case "MATCH_OVER":
                mIsMatchOver = true;
                int.TryParse(payload, out mWinner);
                mIsActive = false;
                if (mWinner == 1) PlaySFX("Sting_Game_Win_01", 1f);
                else PlaySFX("Sting_Game_Lose_01", 1f);
                break;

            case "CHAT":
                string[] chatData = payload.Split(':');
                if (chatData.Length >= 2)
                {
                    AddChatMessage(chatData[0], chatData[1]);
                }
                break;

            case "P2_HERO_CMD":
                if (mOpponentHero != null)
                {
                    mOpponentHero.ExecuteNetworkCommand(payload);
                    mOpponentHero.RegisterClientActivity();
                }
                break;

            case "HERO_SYNC":
                ApplyHeroSync(payload);
                break;
        }
    }

    private void ApplyHeroSync(string payload)
    {
        string[] parts = payload.Split('|');
        if (parts.Length >= 6)
        {
            float p1Hp, p1Z, p2Hp, p2Z;
            int p1Facing, p2Facing;
            if (float.TryParse(parts[0], out p1Hp) && float.TryParse(parts[1], out p1Z) && int.TryParse(parts[2], out p1Facing) &&
                float.TryParse(parts[3], out p2Hp) && float.TryParse(parts[4], out p2Z) && int.TryParse(parts[5], out p2Facing))
            {
                InGameImpl inGame = WeakGlobalSceneBehavior<InGameImpl>.instance;
                if (inGame != null && inGame.hero != null && inGame.hero.controller != null)
                {
                    inGame.hero.health = p1Hp;
                    Vector3 pos = inGame.hero.controller.position;
                    float diff1 = Mathf.Abs(pos.z - p1Z);
                    if (diff1 > 250f)
                    {
                        pos.z = p1Z;
                    }
                    else if (diff1 > 0.5f)
                    {
                        pos.z = Mathf.Lerp(pos.z, p1Z, 0.45f);
                    }
                    inGame.hero.controller.position = pos;
                    inGame.hero.controller.facing = (p1Facing == 1) ? FacingType.Right : FacingType.Left;
                }

                if (mOpponentHero != null && mOpponentHero.controller != null)
                {
                    mOpponentHero.health = p2Hp;
                    Vector3 pos = mOpponentHero.controller.position;
                    float diff2 = Mathf.Abs(pos.z - p2Z);
                    if (diff2 > 250f)
                    {
                        pos.z = p2Z;
                    }
                    else if (diff2 > 0.5f)
                    {
                        pos.z = Mathf.Lerp(pos.z, p2Z, 0.45f);
                        mOpponentHero.controller.position = pos;
                    }
                    mOpponentHero.controller.facing = (p2Facing == 1) ? FacingType.Right : FacingType.Left;
                }
            }
        }
    }
}
