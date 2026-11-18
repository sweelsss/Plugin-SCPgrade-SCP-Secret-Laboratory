using System.Collections.Generic;
using System.ComponentModel;

namespace SCPGrade
{
    public class Config
#if EXILED
        : Exiled.API.Interfaces.IConfig
#endif
    {
#if EXILED
        public bool IsEnabled { get; set; } = true;
#endif

        public bool Debug { get; set; } = false;

        [Description("Ставить в мире SCPGRADE (место — командой upg here, иначе Башня).")]
        public bool SpawnKiosk { get; set; } = true;

        [Description("true — в мире большой экран, у каждого игрока своя копия (видит только своё, все крутят одновременно). false — маленькая стойка, открывающая планшет.")]
        public bool WorldScreen { get; set; } = true;

        [Description("Размер большого экрана (1 = 4.4 x 2.75 м) и с какого расстояния у игрока появляется его копия.")]
        public float WorldScreenScale { get; set; } = 0.6f;
        public float WorldViewDistance { get; set; } = 25f;

        [Description("Комната для экрана по умолчанию (RoomName). Lcz173 = PT-00, бывшая камера SCP-173. Пусто — Башня. Точное место лучше задать командой upg here, стоя в комнате (сохраняется относительно комнаты).")]
        public string ScreenRoom { get; set; } = "Lcz173";

        [Description("Смещение внутри комнаты (локальные координаты комнаты, метры) и поворот экрана в градусах.")]
        public float ScreenRoomOffsetX { get; set; } = 0f;
        public float ScreenRoomOffsetY { get; set; } = 1f;
        public float ScreenRoomOffsetZ { get; set; } = 0f;
        public float ScreenRoomYaw { get; set; } = 0f;

        [Description("Планшет (.scpgrade) работает только в этих зонах: LightContainment, HeavyContainment, Entrance, Surface.")]
        public List<MapGeneration.FacilityZone> TabletZones { get; set; } = new List<MapGeneration.FacilityZone>
        {
            MapGeneration.FacilityZone.LightContainment,
            MapGeneration.FacilityZone.HeavyContainment,
        };

        [Description("Смещение стойки от точки спавна Башни (если комната не найдена): вперёд / вправо / вверх (метры).")]
        public float OffsetForward { get; set; } = 1.8f;
        public float OffsetRight { get; set; } = 0f;
        public float OffsetUp { get; set; } = 0f;

        [Description("Размер планшета (1 = большой экран 4.4 м, 0.32 = ~1.4 м) и на каком расстоянии от игрока он появляется.")]
        public float TabletScale { get; set; } = 0.32f;
        public float TabletDistance { get; set; } = 0.95f;

        [Description("Планшет закрывается сам: если отойти от места, где его открыл, дальше N метров, или ничего не нажимать N секунд.")]
        public float TabletMaxDistance { get; set; } = 2f;
        public float TabletIdleClose { get; set; } = 90f;

        [Description("Подсветка планшета (видна только владельцу).")]
        public bool TabletLight { get; set; } = true;
        public float TabletLightIntensity { get; set; } = 0.6f;

        [Description("Минимальный и максимальный шанс апгрейда (в процентах).")]
        public float MinChance { get; set; } = 1f;
        public float MaxChance { get; set; } = 80f;

        [Description("Комиссия — на сколько процентов урезается честный шанс (0.10 = 10%).")]
        public float HouseEdge { get; set; } = 0.10f;

        [Description("Длительность прокрутки (сек), полных оборотов стрелки, сколько держать E на «Улучшить».")]
        public float SpinDuration { get; set; } = 6f;
        public int SpinTurns { get; set; } = 5;
        public float UpgradeHoldTime { get; set; } = 0.6f;

        [Description("Ценность предметов. Ключ — название ItemType (или PinkCandy — розовая конфета). 0 или отсутствие — предмет нельзя улучшить/получить. Ивентовые предметы (уголь, снежок, диктофон, фонарь и т.п.) всегда исключены.")]
        public Dictionary<string, int> ItemValues { get; set; } = SCPGrade.ItemValues.DefaultValues();

        [Description("Необязательно: ссылка на JSON {\"GunE11SR\": 320, ...} — перекрывает значения выше. Пусто = только конфиг.")]
        public string ValuesUrl { get; set; } = "";

        [Description("Что нельзя получить как цель / нельзя улучшать.")]
        public List<string> TargetBlacklist { get; set; } = new List<string>();
        public List<string> InputBlacklist { get; set; } = new List<string>();

        [Description("Звуки (слышит только владелец планшета). Диапазон ID колонок: у каждого планшета своя.")]
        public bool EnableSounds { get; set; } = true;
        public float Volume { get; set; } = 1f;
        public byte SpeakerIdStart { get; set; } = 120;
        public byte SpeakerIdCount { get; set; } = 60;

        [Description("Язык по умолчанию (Ru / En). Игрок меняет его в Настройки → Сервер.")]
        public Lang DefaultLanguage { get; set; } = Lang.Ru;
        public int LanguageSettingId { get; set; } = 7301;

        [Description("Масштаб и калибровка текста (команды upg scale / upg calib).")]
        public float TextScale { get; set; } = 0.065f;
        public float TextCalibration { get; set; } = 0.051f;

        [Description("Подпись в шапке.")]
        public string Author { get; set; } = "sweelsss";
        public string Discord { get; set; } = "@sweeelsss";
    }
}
