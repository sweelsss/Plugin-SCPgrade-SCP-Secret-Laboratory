using System;
using System.Globalization;
using System.IO;
using System.Linq;
using LabApi.Features.Wrappers;
using MapGeneration;
using UnityEngine;
using Logger = LabApi.Features.Console.Logger;

namespace SCPGrade
{
    /// <summary>
    /// Место экрана/стойки. Хранится ОТНОСИТЕЛЬНО КОМНАТЫ (комплекс генерируется заново каждый раунд),
    /// поэтому «upg here» в PT-00 сработает в любом следующем раунде.
    /// Файл: location.txt рядом с конфигом. Формат: «room Lcz173 x y z yaw» или «world x y z yaw».
    /// </summary>
    public static class PositionStore
    {
        private static string FilePath
        {
            get
            {
#if EXILED
                return Path.Combine(Exiled.API.Features.Paths.Configs, $"scpgrade-location-{Exiled.API.Features.Server.Port}.txt");
#else
                return Path.Combine(LabApi.Loader.ConfigurationLoader.GetConfigDirectory(Plugin.Instance).FullName, "location.txt");
#endif
            }
        }

        private static string F(float v) => v.ToString(CultureInfo.InvariantCulture);

        private static float P(string s) => float.Parse(s, CultureInfo.InvariantCulture);

        /// <summary>Сохраняет место относительно комнаты, в которой стоит точка (или в мировых координатах, если комнаты нет).</summary>
        public static string Save(Vector3 pos, Quaternion facing)
        {
            try
            {
                Room room = Room.GetRoomAtPosition(pos);
                string line;
                string desc;
                if (room != null && room.Name != RoomName.Unnamed)
                {
                    Vector3 local = room.Transform.InverseTransformPoint(pos);
                    float yaw = (Quaternion.Inverse(room.Transform.rotation) * facing).eulerAngles.y;
                    line = $"room {room.Name} {F(local.x)} {F(local.y)} {F(local.z)} {F(yaw)}";
                    desc = $"комната {room.Name}";
                }
                else
                {
                    line = $"world {F(pos.x)} {F(pos.y)} {F(pos.z)} {F(facing.eulerAngles.y)}";
                    desc = "мировые координаты";
                }

                File.WriteAllText(FilePath, line);
                return desc;
            }
            catch (Exception e)
            {
                Logger.Warn($"[SCPGrade] Не удалось сохранить место: {e.Message}");
                return "ошибка сохранения";
            }
        }

        public static void Clear()
        {
            try
            {
                if (File.Exists(FilePath))
                    File.Delete(FilePath);
            }
            catch (Exception e)
            {
                Logger.Warn($"[SCPGrade] Не удалось удалить сохранённое место: {e.Message}");
            }
        }

        /// <summary>Сохранённое место → мировые координаты в текущем раунде.</summary>
        public static bool TryLoad(out Vector3 pos, out Quaternion facing)
        {
            pos = Vector3.zero;
            facing = Quaternion.identity;
            try
            {
                if (!File.Exists(FilePath))
                    return false;

                string[] p = File.ReadAllText(FilePath).Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (p.Length >= 6 && p[0] == "room" && Enum.TryParse(p[1], out RoomName name))
                    return TryRoom(name, new Vector3(P(p[2]), P(p[3]), P(p[4])), P(p[5]), out pos, out facing);

                if (p.Length >= 5 && p[0] == "world")
                {
                    pos = new Vector3(P(p[1]), P(p[2]), P(p[3]));
                    facing = Quaternion.Euler(0f, P(p[4]), 0f);
                    return true;
                }
            }
            catch (Exception e)
            {
                Logger.Warn($"[SCPGrade] Не удалось прочитать сохранённое место: {e.Message}");
            }

            return false;
        }

        /// <summary>Точка в комнате (локальные координаты комнаты) → мир.</summary>
        public static bool TryRoom(RoomName name, Vector3 local, float localYaw, out Vector3 pos, out Quaternion facing)
        {
            pos = Vector3.zero;
            facing = Quaternion.identity;
            Room room = Room.Get(name).FirstOrDefault();
            if (room == null)
            {
                Logger.Warn($"[SCPGrade] Комната {name} не найдена на этой карте.");
                return false;
            }

            pos = room.Transform.TransformPoint(local);
            Vector3 fwd = room.Transform.rotation * Quaternion.Euler(0f, localYaw, 0f) * Vector3.forward;
            fwd.y = 0f;
            facing = Quaternion.LookRotation(fwd.sqrMagnitude > 0.001f ? fwd.normalized : Vector3.forward);
            return true;
        }
    }
}
