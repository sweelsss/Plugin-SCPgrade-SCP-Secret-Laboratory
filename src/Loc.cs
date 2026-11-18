using System.Collections.Generic;
using Logger = LabApi.Features.Console.Logger;
using LabApi.Features.Wrappers;
using UnityEngine;
using UserSettings.ServerSpecific;

namespace SCPGrade
{
    public enum Lang { Ru = 0, En = 1 }

    /// <summary>
    /// Локализация. Клиент SL не сообщает серверу язык игры, поэтому язык выбирается
    /// игроком в «Настройки → Сервер» (Server-specific). По умолчанию — из конфига.
    /// </summary>
    public static class Loc
    {
        private static readonly Dictionary<string, (string ru, string en)> S = new Dictionary<string, (string, string)>
        {
            ["your_item"] = ("Ваш предмет", "Your item"),
            ["your_item_sub"] = ("Возьмите предмет в руку", "Hold an item in your hand"),
            ["target"] = ("Выберите предмет для апгрейда", "Choose an item to upgrade to"),
            ["target_sub"] = ("Нажмите [E] на карточку ниже", "Press [E] on a card below"),
            ["choose"] = ("Выберите предмет", "Choose item"),
            ["upgrade"] = ("Улучшить", "Upgrade"),
            ["chance"] = ("шанс", "chance"),
            ["avg_chance"] = ("шанс апгрейда", "upgrade chance"),
            ["online"] = ("ONLINE", "ONLINE"),
            ["author"] = ("Автор", "Author"),
            ["spinning"] = ("КРУТИМ...", "SPINNING..."),
            ["win"] = ("УСПЕХ", "SUCCESS"),
            ["lose"] = ("ПРОВАЛ", "FAILED"),
            ["burned"] = ("сгорел", "burned"),
            ["got"] = ("Ты получил", "You got"),
            ["empty_hand"] = ("Руки пусты", "Empty hand"),
            ["no_price"] = ("нельзя улучшить", "can't be upgraded"),
            ["no_targets"] = ("Нет целей — предмет уже топовый", "No targets — item is already top tier"),
            ["press_any"] = ("Нажмите [E] на любую кнопку на экране", "Press [E] on any button on the screen"),
            ["hold_e"] = ("зажми [E] на «Улучшить»", "hold [E] on «Upgrade»"),
            ["busy"] = ("Занято:", "In use by:"),
            ["humans_only"] = ("Терминал доступен только людям.", "Only humans can use the terminal."),
            ["hold_item"] = ("Возьми в руку предмет, который можно улучшить.", "Hold an item that can be upgraded."),
            ["pick_target"] = ("Выбери цель: карточка внизу или x2 / 35%.", "Pick a target: a card below or x2 / 35%."),
            ["help"] = (
                "<color=#fc3>SCPGRADE</color>\nДержи в руке предмет для апгрейда\n<color=#fa5>Карточки внизу</color> — выбор цели, <color=#fc3>x2 / 35%</color> — быстрый выбор\n<color=#fc3>Зажми E на «Улучшить»</color>  •  закрыть: <color=#fc3>.scpgrade</color>",
                "<color=#fc3>SCPGRADE</color>\nHold the item you want to upgrade\n<color=#fa5>Cards below</color> — pick a target, <color=#fc3>x2 / 35%</color> — quick pick\n<color=#fc3>Hold E on «Upgrade»</color>  •  close: <color=#fc3>.scpgrade</color>"),
            ["tablet_opened"] = ("Планшет открыт.", "Tablet opened."),
            ["tablet_closed"] = ("Планшет закрыт.", "Tablet closed."),
            ["tablet_moved"] = ("Планшет перенесён к тебе.", "Tablet moved in front of you."),
            ["tablet_spinning"] = ("Дождись конца прокрутки.", "Wait for the spin to finish."),
            ["tablet_full"] = ("Сейчас открыто слишком много планшетов, попробуй позже.", "Too many tablets are open, try later."),
            ["tablet_zone"] = ("Планшет работает только в лёгкой и тяжёлой зоне.", "The tablet works only in Light and Heavy Containment."),
            ["tablet_dead"] = ("Планшет доступен только живым.", "Only alive players can use the tablet."),
            ["world_hint"] = ("этот экран видишь только ты", "only you can see this screen"),
            ["close_hint"] = ("закрыть планшет: .scpgrade в консоли (~)", "close tablet: .scpgrade in console (~)"),
            ["broadcast"] = ("выбил {0} в апгрейдере с шансом {1}%!", "upgraded to {0} with a {1}% chance!"),
            ["values"] = ("ценности:", "values:"),
            ["src_builtin"] = ("встроенные", "built-in"),
            ["src_web"] = ("интернет", "web"),
            ["settings_header"] = ("SCPGrade", "SCPGrade"),
            ["settings_lang"] = ("Язык SCPGrade / SCPGrade language", "Язык SCPGrade / SCPGrade language"),

            // редкости (как в CS)
            ["r0"] = ("Ширпотреб", "Consumer"),
            ["r1"] = ("Промышленное", "Industrial"),
            ["r2"] = ("Армейское", "Mil-Spec"),
            ["r3"] = ("Запрещённое", "Restricted"),
            ["r4"] = ("Засекреченное", "Classified"),
            ["r5"] = ("Тайное", "Covert"),
            ["r6"] = ("Редкое", "Rare"),
        };

        public static string T(string key, Lang lang)
        {
            if (!S.TryGetValue(key, out (string ru, string en) v))
                return key;
            return lang == Lang.En ? v.en : v.ru;
        }

        #region Редкость

        private static readonly int[] RarityBounds = { 40, 100, 180, 260, 400, 600 };

        private static readonly Color[] RarityColors =
        {
            Hex("b0c3d9"), Hex("5e98d9"), Hex("4b69ff"), Hex("8847ff"), Hex("d32ce6"), Hex("eb4b4b"), Hex("e4ae39"),
        };

        public static int Rarity(int value)
        {
            for (int i = 0; i < RarityBounds.Length; i++)
            {
                if (value < RarityBounds[i])
                    return i;
            }

            return RarityBounds.Length;
        }

        public static Color RarityColor(int value) => RarityColors[Rarity(value)];

        public static string RarityName(int value, Lang lang) => T("r" + Rarity(value), lang);

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out Color c);
            return c;
        }

        #endregion

        #region Настройка языка у игрока (Server-specific settings)

        private static int _settingId;
        private static SSDropdownSetting _dropdown;

        public static void Register(Config cfg)
        {
            _settingId = cfg.LanguageSettingId;
            _dropdown = new SSDropdownSetting(
                _settingId,
                T("settings_lang", Lang.Ru),
                new[] { "Русский", "English" },
                cfg.DefaultLanguage == Lang.En ? 1 : 0);

            EnsureRegistered();
        }

        /// <summary>Добавляет нашу настройку к уже существующим (не затирая настройки других плагинов).</summary>
        public static void EnsureRegistered()
        {
            if (_dropdown == null)
                return;

            ServerSpecificSettingBase[] current = ServerSpecificSettingsSync.DefinedSettings ?? new ServerSpecificSettingBase[0];
            foreach (ServerSpecificSettingBase s in current)
            {
                if (s.SettingId == _settingId && s is SSDropdownSetting)
                    return;
            }

            List<ServerSpecificSettingBase> list = new List<ServerSpecificSettingBase>(current)
            {
                new SSGroupHeader(T("settings_header", Lang.Ru)),
                _dropdown,
            };
            ServerSpecificSettingsSync.DefinedSettings = list.ToArray();
            ServerSpecificSettingsSync.SendToAll();
            Logger.Info("[SCPGrade] Настройка языка добавлена во вкладку настроек сервера.");
        }

        public static Lang Of(Player p)
        {
            Lang def = Plugin.Instance?.Config.DefaultLanguage ?? Lang.Ru;
            if (p == null || p.IsDestroyed)
                return def;

            try
            {
                if (ServerSpecificSettingsSync.TryGetSettingOfUser(p.ReferenceHub, _settingId, out SSDropdownSetting s))
                    return s.SyncSelectionIndexValidated == 1 ? Lang.En : Lang.Ru;
            }
            catch
            {
                // настройка ещё не пришла от клиента
            }

            return def;
        }

        #endregion
    }
}
