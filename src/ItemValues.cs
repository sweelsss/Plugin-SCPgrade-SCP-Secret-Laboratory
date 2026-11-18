using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using InventorySystem.Items.Usables.Scp330;
using LabApi.Features.Wrappers;
using Logger = LabApi.Features.Console.Logger;

namespace SCPGrade
{
    /// <summary>
    /// Ценности предметов. Ключи — имена ItemType строками + «PinkCandy» (розовая конфета SCP-330).
    /// Источник — конфиг (ItemValues), опционально перекрывается JSON по ссылке.
    /// </summary>
    public static class ItemValues
    {
        public const string PinkCandy = "PinkCandy";

        /// <summary>Ивентовые/служебные предметы — никогда не участвуют в апгрейде.</summary>
        public static readonly HashSet<string> EventItems = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Coal", "SpecialCoal", "Snowball", "SCP1507Tape", "Lantern", "MarshmallowItem",
            "SCP1509", "Scp021J", "DebugRagdollMover",
            "KeycardCustomTaskForce", "KeycardCustomSite02", "KeycardCustomManagement", "KeycardCustomMetalCase",
        };

        public static Dictionary<string, int> DefaultValues() => new Dictionary<string, int>
        {
            // мелочь
            ["Coin"] = 5, ["Flashlight"] = 12, ["Radio"] = 25, ["SCP330"] = 40,

            // лечение
            ["Painkillers"] = 30, ["Medkit"] = 60, ["Adrenaline"] = 75,

            // карты
            ["KeycardJanitor"] = 15, ["KeycardScientist"] = 30, ["KeycardResearchCoordinator"] = 50,
            ["KeycardZoneManager"] = 60, ["KeycardGuard"] = 75, ["KeycardMTFPrivate"] = 110,
            ["KeycardContainmentEngineer"] = 125, ["SurfaceAccessPass"] = 150, ["KeycardMTFOperative"] = 165,
            ["KeycardMTFCaptain"] = 220, ["KeycardFacilityManager"] = 250, ["KeycardChaosInsurgency"] = 280,
            ["KeycardO5"] = 400,

            // броня и гранаты
            ["ArmorLight"] = 60, ["ArmorCombat"] = 140, ["ArmorHeavy"] = 220,
            ["GrenadeFlash"] = 80, ["GrenadeHE"] = 150,

            // оружие
            ["GunCOM15"] = 80, ["GunCOM18"] = 110, ["GunFSP9"] = 160, ["GunRevolver"] = 190,
            ["GunCrossvec"] = 200, ["GunShotgun"] = 240, ["GunCom45"] = 260, ["GunAK"] = 280,
            ["GunA7"] = 300, ["GunE11SR"] = 320, ["GunLogicer"] = 400, ["GunFRMG0"] = 420,
            ["GunSCP127"] = 450, ["Jailbird"] = 500, ["ParticleDisruptor"] = 650, ["MicroHID"] = 800,

            // SCP-предметы
            ["SCP2176"] = 120, ["SCP1853"] = 140, ["SCP1576"] = 150, ["SCP207"] = 160,
            ["AntiSCP207"] = 170, ["SCP018"] = 180, ["SCP244a"] = 190, ["SCP244b"] = 190,
            ["SCP500"] = 200, ["SCP1344"] = 210, ["SCP268"] = 230,

            // розовая конфета SCP-330
            [PinkCandy] = 350,
        };

        private static volatile Dictionary<string, int> _values = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public static bool FromWeb { get; private set; }

        public static void Init(Config config)
        {
            _values = Clean(config.ItemValues ?? DefaultValues());
            FromWeb = false;
            if (!string.IsNullOrWhiteSpace(config.ValuesUrl))
                Reload(config);
        }

        /// <summary>Скачивает JSON {"GunE11SR": 320, ...} в фоне и перекрывает значения из конфига.</summary>
        public static void Reload(Config config)
        {
            string url = config.ValuesUrl;
            Task.Run(() =>
            {
                try
                {
                    ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                    string json;
                    using (WebClient wc = new WebClient())
                    {
                        wc.Encoding = System.Text.Encoding.UTF8;
                        wc.Headers[HttpRequestHeader.UserAgent] = "SCPGrade";
                        json = wc.DownloadString(url);
                    }

                    Dictionary<string, int> merged = new Dictionary<string, int>(config.ItemValues ?? DefaultValues(), StringComparer.OrdinalIgnoreCase);
                    int count = 0;
                    foreach (Match m in Regex.Matches(json, "\"([A-Za-z0-9_]+)\"\\s*:\\s*(-?\\d+(?:\\.\\d+)?)"))
                    {
                        merged[m.Groups[1].Value] = (int)Math.Round(double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture));
                        count++;
                    }

                    _values = Clean(merged);
                    FromWeb = true;
                    Logger.Info($"[SCPGrade] Загружено {count} ценностей с {url}");
                }
                catch (Exception e)
                {
                    Logger.Warn($"[SCPGrade] Не удалось скачать ценности ({url}): {e.Message}. Использую конфиг.");
                }
            });
        }

        private static Dictionary<string, int> Clean(Dictionary<string, int> raw)
        {
            Dictionary<string, int> result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, int> kv in raw)
            {
                if (kv.Value <= 0 || EventItems.Contains(kv.Key) || kv.Key.StartsWith("Ammo", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (string.Equals(kv.Key, PinkCandy, StringComparison.OrdinalIgnoreCase))
                {
                    result[PinkCandy] = kv.Value;
                    continue;
                }

                if (Enum.TryParse(kv.Key, true, out ItemType type) && type != ItemType.None)
                    result[type.ToString()] = kv.Value;
                else
                    Logger.Warn($"[SCPGrade] Неизвестный предмет в item_values: {kv.Key}");
            }

            return result;
        }

        public static bool TryGet(string key, out int value) => _values.TryGetValue(key, out value);

        public static int Get(string key) => _values.TryGetValue(key, out int v) ? v : 0;

        public static IEnumerable<KeyValuePair<string, int>> All => _values;

        /// <summary>
        /// Ценность предмета в руке. Мешок SCP-330 оценивается по конфетам: розовая = PinkCandy, остальные = SCP330 за штуку.
        /// </summary>
        public static bool TryGetHeld(Item item, out string key, out int value)
        {
            key = null;
            value = 0;
            if (item == null)
                return false;

            if (item is Scp330Item bag)
            {
                IReadOnlyList<CandyKindID> candies = bag.Candies;
                if (candies == null || candies.Count == 0)
                    return false;

                int pink = candies.Count(c => c == CandyKindID.Pink);
                int other = candies.Count - pink;
                value = (pink * Get(PinkCandy)) + (other * Get("SCP330"));
                key = pink == candies.Count && pink == 1 ? PinkCandy : "SCP330";
                return value > 0;
            }

            key = item.Type.ToString();
            return _values.TryGetValue(key, out value) && value > 0;
        }

        /// <summary>Выдаёт игроку предмет по ключу. false — инвентарь полон (выброшено на пол).</summary>
        public static void Give(Player p, string key, UnityEngine.Vector3 fallbackPos)
        {
            bool alive = p != null && !p.IsDestroyed && p.IsAlive;

            if (key == PinkCandy)
            {
                if (alive)
                {
                    Scp330Item bag = p.Items.OfType<Scp330Item>().FirstOrDefault(b => b.Candies.Count < Scp330Item.MaxCandies);
                    if (bag != null)
                    {
                        bag.AddCandies(new[] { CandyKindID.Pink });
                        p.CurrentItem = bag;
                        return;
                    }

                    if (!p.IsInventoryFull && p.AddItem(ItemType.SCP330) is Scp330Item newBag)
                    {
                        newBag.SetCandies(new[] { CandyKindID.Pink });
                        p.CurrentItem = newBag;
                        return;
                    }
                }

                Pickup pk = Pickup.Create(ItemType.SCP330, alive ? p.Position : fallbackPos);
                if (pk is LabApi.Features.Wrappers.Scp330Pickup sp)
                {
                    sp.Candies.Clear();
                    sp.Candies.Add(CandyKindID.Pink);
                    sp.ExposedCandy = CandyKindID.Pink;
                }

                return;
            }

            if (!Enum.TryParse(key, out ItemType type))
                return;

            if (alive && !p.IsInventoryFull)
            {
                Item given = p.AddItem(type);
                if (given != null)
                    p.CurrentItem = given;
                return;
            }

            Pickup.Create(type, alive ? p.Position : fallbackPos);
        }

        #region Названия

        private static readonly Dictionary<string, string> Names = new Dictionary<string, string>
        {
            ["Coin"] = "Монетка", ["Flashlight"] = "Фонарик", ["Radio"] = "Рация", ["SCP330"] = "Конфеты SCP-330",
            [PinkCandy] = "Розовая конфета",
            ["Painkillers"] = "Обезбол", ["Medkit"] = "Аптечка", ["Adrenaline"] = "Адреналин",
            ["KeycardJanitor"] = "Карта уборщика", ["KeycardScientist"] = "Карта учёного",
            ["KeycardResearchCoordinator"] = "Карта координатора", ["KeycardZoneManager"] = "Карта мен. зоны",
            ["KeycardGuard"] = "Карта охраны", ["KeycardMTFPrivate"] = "Карта ряд. МОГ",
            ["KeycardContainmentEngineer"] = "Карта инженера", ["SurfaceAccessPass"] = "Пропуск наружу",
            ["KeycardMTFOperative"] = "Карта опер. МОГ", ["KeycardMTFCaptain"] = "Карта кап. МОГ",
            ["KeycardFacilityManager"] = "Карта директора", ["KeycardChaosInsurgency"] = "Девайс ПХ",
            ["KeycardO5"] = "Карта O5",
            ["ArmorLight"] = "Лёгкая броня", ["ArmorCombat"] = "Боевая броня", ["ArmorHeavy"] = "Тяжёлая броня",
            ["GrenadeFlash"] = "Светошумовая", ["GrenadeHE"] = "Осколочная",
            ["GunCOM15"] = "COM-15", ["GunCOM18"] = "COM-18", ["GunFSP9"] = "FSP-9", ["GunRevolver"] = ".44 Револьвер",
            ["GunCrossvec"] = "Crossvec", ["GunShotgun"] = "Дробовик", ["GunCom45"] = "COM-45", ["GunAK"] = "AK",
            ["GunA7"] = "A7", ["GunE11SR"] = "MTF-E11-SR", ["GunLogicer"] = "Logicer", ["GunFRMG0"] = "FR-MG-0",
            ["GunSCP127"] = "SCP-127", ["Jailbird"] = "Jailbird", ["ParticleDisruptor"] = "3-X Дисраптор",
            ["MicroHID"] = "Micro H.I.D.",
            ["SCP2176"] = "SCP-2176", ["SCP1853"] = "SCP-1853", ["SCP1576"] = "SCP-1576", ["SCP207"] = "SCP-207",
            ["AntiSCP207"] = "Anti SCP-207", ["SCP018"] = "SCP-018", ["SCP244a"] = "SCP-244-A", ["SCP244b"] = "SCP-244-B",
            ["SCP500"] = "SCP-500", ["SCP1344"] = "SCP-1344", ["SCP268"] = "SCP-268",
        };

        private static readonly Dictionary<string, string> NamesEn = new Dictionary<string, string>
        {
            ["Coin"] = "Coin", ["Flashlight"] = "Flashlight", ["Radio"] = "Radio", ["SCP330"] = "SCP-330 Candy",
            [PinkCandy] = "Pink Candy",
            ["Painkillers"] = "Painkillers", ["Medkit"] = "First Aid Kit", ["Adrenaline"] = "Adrenaline",
            ["KeycardJanitor"] = "Janitor Card", ["KeycardScientist"] = "Scientist Card",
            ["KeycardResearchCoordinator"] = "Research Coord.", ["KeycardZoneManager"] = "Zone Manager",
            ["KeycardGuard"] = "Guard Card", ["KeycardMTFPrivate"] = "MTF Private",
            ["KeycardContainmentEngineer"] = "Cont. Engineer", ["SurfaceAccessPass"] = "Surface Pass",
            ["KeycardMTFOperative"] = "MTF Operative", ["KeycardMTFCaptain"] = "MTF Captain",
            ["KeycardFacilityManager"] = "Facility Manager", ["KeycardChaosInsurgency"] = "Chaos Device",
            ["KeycardO5"] = "O5 Card",
            ["ArmorLight"] = "Light Armor", ["ArmorCombat"] = "Combat Armor", ["ArmorHeavy"] = "Heavy Armor",
            ["GrenadeFlash"] = "Flashbang", ["GrenadeHE"] = "HE Grenade",
            ["GunRevolver"] = ".44 Revolver", ["GunShotgun"] = "Shotgun", ["ParticleDisruptor"] = "3-X Disruptor",
        };

        public static string Name(string key, Lang lang)
        {
            if (string.IsNullOrEmpty(key))
                return "-";
            if (lang == Lang.En && NamesEn.TryGetValue(key, out string en))
                return en;
            return Names.TryGetValue(key, out string n) ? n : key;
        }

        #endregion
    }
}
