using System;
using System.Globalization;
using System.Linq;
using CommandSystem;
using LabApi.Features.Wrappers;
using UnityEngine;

namespace SCPGrade
{
    /// <summary>Консольная команда игрока: «.scpgrade» (консоль на ~) — открыть/закрыть свой планшет.</summary>
    [CommandHandler(typeof(ClientCommandHandler))]
    public class ScpgradeCommand : ICommand
    {
        public string Command => "scpgrade";

        public string[] Aliases => new[] { "upgrader", "upgrade" };

        public string Description => "Открыть/закрыть планшет апгрейдера / Open or close the upgrader tablet";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            Player p = Player.Get(sender);
            if (p == null)
            {
                response = "Only for players.";
                return false;
            }

            response = TabletManager.Toggle(p);
            return true;
        }
    }

    /// <summary>RA-команда для админов: upg here | tower | tp | remove | open | scale | calib | reload | values</summary>
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class UpgraderCommand : ICommand
    {
        public string Command => "upgrader";

        public string[] Aliases => new[] { "upg" };

        public string Description => "SCPGRADE: upg here | default | tower | tp | remove | open | scale <x> | calib <x> | reload | values";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (!sender.CheckPermission(PlayerPermissions.FacilityManagement, out response))
                return false;

            Config cfg = Plugin.Instance?.Config;
            if (cfg == null)
            {
                response = "Плагин не загружен.";
                return false;
            }

            Player p = Player.Get(sender);
            string sub = arguments.Count > 0 ? arguments.At(0).ToLowerInvariant() : "help";
            switch (sub)
            {
                case "here":
                {
                    if (p == null)
                    {
                        response = "Только для игрока.";
                        return false;
                    }

                    Vector3 fwd = p.Camera.forward;
                    fwd.y = 0f;
                    if (fwd.sqrMagnitude < 0.01f)
                        fwd = Vector3.forward;
                    fwd.Normalize();
                    Quaternion facing = Quaternion.LookRotation(fwd);
                    Vector3 at = p.Position + (fwd * (cfg.WorldScreen ? 2.6f : 1.6f));
                    Kiosk.SpawnAt(at, facing);
                    string where = PositionStore.Save(at, facing);
                    response = $"Поставлено перед тобой, место сохранено ({where}) — будет тут каждый раунд. upg default — вернуть в комнату из конфига (PT-00).";
                    return true;
                }

                case "default":
                    PositionStore.Clear();
                    Kiosk.Despawn();
                    Kiosk.SpawnDefault(cfg);
                    response = $"Сохранённое место сброшено, стоит в комнате из конфига ({cfg.ScreenRoom}).";
                    return true;

                case "tower":
                    response = Kiosk.SpawnAtTower(cfg) ? "Поставлено на Башню (только на этот раунд; upg here там — чтобы запомнить)." : "Не нашёл Башню.";
                    return true;

                case "tp":
                    if (p == null || !Kiosk.Spawned)
                    {
                        response = "Стойки нет или ты не игрок.";
                        return false;
                    }

                    p.Position = Kiosk.Position - (Kiosk.Facing * Vector3.forward * 1.4f) + (Vector3.up * 0.1f);
                    p.LookRotation = new Vector2(0f, Kiosk.Facing.eulerAngles.y);
                    response = "Телепортировал к стойке.";
                    return true;

                case "remove":
                    Kiosk.Despawn();
                    response = "Стойка убрана (до следующего раунда).";
                    return true;

                case "open":
                    if (p == null)
                    {
                        response = "Только для игрока.";
                        return false;
                    }

                    response = TabletManager.Toggle(p);
                    return true;

                case "scale":
                case "calib":
                    if (arguments.Count < 2 || !float.TryParse(arguments.At(1).Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out float v) || v <= 0f)
                    {
                        response = $"text_scale = {cfg.TextScale}, text_calibration = {cfg.TextCalibration}. Пример: upg {sub} 0.06";
                        return false;
                    }

                    if (sub == "scale")
                        cfg.TextScale = v;
                    else
                        cfg.TextCalibration = v;
                    response = $"{sub} = {v}. Переоткрой планшет (.scpgrade). Сохрани значение в конфиг, чтобы оно осталось после рестарта.";
                    return true;

                case "reload":
                    ItemValues.Init(cfg);
                    response = string.IsNullOrWhiteSpace(cfg.ValuesUrl)
                        ? "Ценности перечитаны из конфига (для изменений в файле сначала перезагрузи конфиг/сервер)."
                        : "Качаю ценности по values_url... результат в консоли сервера.";
                    return true;

                case "values":
                    response = $"Источник: {(ItemValues.FromWeb ? "интернет + конфиг" : "конфиг")}\n" + string.Join("\n",
                        ItemValues.All.OrderBy(kv => kv.Value).Select(kv => $"{kv.Key} = {kv.Value}"));
                    return true;

                default:
                    response = Description + $"\nПланшетов: {TabletManager.Count}, личных копий большого экрана: {TabletManager.WorldCount}";
                    return true;
            }
        }
    }
}
