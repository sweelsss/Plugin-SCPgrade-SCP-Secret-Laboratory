using System.Collections.Generic;
using LabApi.Features.Extensions;
using LabApi.Features.Wrappers;
using PlayerRoles;
using UnityEngine;
using Logger = LabApi.Features.Console.Logger;

namespace SCPGrade
{
    /// <summary>
    /// Стойка SCPGRADE в мире (общая, её видят все). Нажал E — открывается твой личный планшет.
    /// Место: сохранённое командой upg here, иначе перед точкой спавна Башни.
    /// </summary>
    public static class Kiosk
    {
        private static readonly List<AdminToy> Toys = new List<AdminToy>();

        public static Vector3 Position { get; private set; }

        public static Quaternion Facing { get; private set; }

        public static bool Spawned => TabletManager.WorldOn || (Toys.Count > 0 && !Toys[0].IsDestroyed);

        public static void SpawnDefault(Config cfg)
        {
            // 1) место из upg here  2) комната из конфига (по умолчанию PT-00 / Lcz173)  3) Башня
            if (PositionStore.TryLoad(out Vector3 pos, out Quaternion facing))
            {
                SpawnAt(pos, facing);
                return;
            }

            if (SpawnInRoom(cfg))
                return;

            SpawnAtTower(cfg);
        }

        /// <summary>Ставит в комнату из конфига (screen_room + screen_room_offset).</summary>
        public static bool SpawnInRoom(Config cfg)
        {
            if (!System.Enum.TryParse(cfg.ScreenRoom, true, out MapGeneration.RoomName name) || name == MapGeneration.RoomName.Unnamed)
                return false;

            Vector3 local = new Vector3(cfg.ScreenRoomOffsetX, cfg.ScreenRoomOffsetY, cfg.ScreenRoomOffsetZ);
            if (!PositionStore.TryRoom(name, local, cfg.ScreenRoomYaw, out Vector3 pos, out Quaternion facing))
                return false;

            SpawnAt(pos, facing);
            return true;
        }

        public static bool SpawnAtTower(Config cfg)
        {
            if (!RoleTypeId.Tutorial.TryGetRandomSpawnPoint(out Vector3 pos, out float yaw))
            {
                Logger.Warn("[SCPGrade] Не нашёл точку спавна Башни (Tutorial).");
                return false;
            }

            Quaternion look = Quaternion.Euler(0f, yaw, 0f);
            float forward = cfg.OffsetForward;
            foreach (RaycastHit hit in Physics.RaycastAll(pos, look * Vector3.forward, forward + 0.5f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponentInParent<AdminToys.AdminToyBase>() != null)
                    continue;
                forward = Mathf.Min(forward, Mathf.Max(0.8f, hit.distance - 0.4f));
            }

            Vector3 p = pos + (look * Vector3.forward * forward) + (look * Vector3.right * cfg.OffsetRight) + (Vector3.up * cfg.OffsetUp);
            SpawnAt(p, look);
            return true;
        }

        /// <param name="position">Точка примерно на уровне центра игрока (как Player.Position).</param>
        /// <param name="facing">Направление ОТ зрителя К стойке.</param>
        public static void SpawnAt(Vector3 position, Quaternion facing)
        {
            Despawn();
            Position = position;
            Facing = facing;

            // Режим «личный большой экран»: копии создаёт TabletManager для каждого игрока рядом
            if (Plugin.Instance.Config.WorldScreen)
            {
                TabletManager.SetWorldSpot(position, facing);
                return;
            }

            Vector3 W(float x, float y, float z) => position + (facing * new Vector3(x, y, z));

            // стойка: тумба + наклонная панель
            Prim(W(0f, -0.55f, 0f), facing, new Vector3(0.5f, 0.85f, 0.35f), Loc.Hex("17181c"), true);
            Prim(W(0f, 0.25f, 0.02f), facing * Quaternion.Euler(-15f, 0f, 0f), new Vector3(0.9f, 0.55f, 0.06f), Loc.Hex("17181c"), true);
            Prim(W(0f, 0.25f, -0.01f), facing * Quaternion.Euler(-15f, 0f, 0f), new Vector3(0.84f, 0.49f, 0.01f), Loc.Hex("0d0e11"), false);
            Prim(W(0f, 0.53f, 0.06f), facing * Quaternion.Euler(-15f, 0f, 0f), new Vector3(0.9f, 0.015f, 0.02f), Loc.Hex("f5c518"), false);

            TextToy text = TextToy.Create(W(0f, 0.25f, -0.025f), facing * Quaternion.Euler(-15f, 0f, 0f), Vector3.one * 0.035f, null, false);
            Vector2 display = new Vector2(0.8f, 0.45f) / (0.035f * Plugin.Instance.Config.TextCalibration);
            text.DisplaySize = display;
            text.TextFormat =
                "<align=center><size=180%><b><color=#f5c518>SCPGRADE</color></b></size>\n" +
                "<color=#fff>Нажми <color=#f5c518>[E]</color> — открыть планшет</color>\n" +
                "<color=#999>Press <color=#f5c518>[E]</color> to open your tablet</color>\n" +
                "<size=80%><color=#777>консоль / console: <color=#f5c518>.scpgrade</color></color></size></align>";
            text.Spawn();
            text.DisplaySize = display + new Vector2(0.01f, 0f);
            Toys.Add(text);

            if (Plugin.Instance.Config.TabletLight)
            {
                LightSourceToy light = LightSourceToy.Create(W(0f, 0.6f, -0.8f), facing, null, false);
                light.Color = new Color(1f, 0.85f, 0.4f);
                light.Intensity = 0.5f;
                light.Range = 2.5f;
                light.ShadowType = LightShadows.None;
                light.Spawn();
                Toys.Add(light);
            }

            InteractableToy it = InteractableToy.Create(W(0f, -0.1f, -0.32f), facing, new Vector3(0.95f, 1.5f, 0.25f), null, false);
            it.Shape = AdminToys.InvisibleInteractableToy.ColliderShape.Box;
            it.InteractionDuration = 0f;
            it.OnInteracted += p =>
            {
                string msg = TabletManager.Open(p);
                p.SendHint($"<size=24>{msg}</size>", 3f);
            };
            it.Spawn();
            Toys.Add(it);
        }

        public static void Despawn()
        {
            TabletManager.DisableWorld();
            foreach (AdminToy t in Toys)
            {
                try
                {
                    if (!t.IsDestroyed)
                        t.Destroy();
                }
                catch
                {
                    // уже уничтожен
                }
            }

            Toys.Clear();
        }

        public static void Forget() => Toys.Clear();

        private static void Prim(Vector3 pos, Quaternion rot, Vector3 scale, Color color, bool collide)
        {
            PrimitiveObjectToy t = PrimitiveObjectToy.Create(pos, rot, scale, null, false);
            t.Type = PrimitiveType.Cube;
            t.Color = color;
            t.Flags = collide ? AdminToys.PrimitiveFlags.Visible | AdminToys.PrimitiveFlags.Collidable : AdminToys.PrimitiveFlags.Visible;
            t.Spawn();
            Toys.Add(t);
        }
    }
}
