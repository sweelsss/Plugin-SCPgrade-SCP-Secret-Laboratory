using System.Collections.Generic;
using System.Linq;
using LabApi.Features.Wrappers;
using MEC;
using UnityEngine;
using Logger = LabApi.Features.Console.Logger;

namespace SCPGrade
{
    /// <summary>
    /// Личные экраны игроков:
    ///  • большой экран в мире — у каждого игрока рядом своя копия на одном и том же месте (видна только ему);
    ///  • планшет из .scpgrade — появляется перед игроком.
    /// Общий пул ID колонок, автосоздание/удаление, тик отрисовки.
    /// </summary>
    public static class TabletManager
    {
        private static readonly Dictionary<Player, UpgraderTablet> Tablets = new Dictionary<Player, UpgraderTablet>();
        private static readonly Dictionary<Player, UpgraderTablet> WorldScreens = new Dictionary<Player, UpgraderTablet>();
        private static Config _cfg;
        private static CoroutineHandle _loop;

        private static bool _worldOn;
        private static Vector3 _worldPos;
        private static Quaternion _worldRot;

        public static int Count => Tablets.Count;

        public static int WorldCount => WorldScreens.Count;

        public static bool WorldOn => _worldOn;

        public static void Init(Config cfg)
        {
            _cfg = cfg;
            Timing.KillCoroutines(_loop);
            _loop = Timing.RunCoroutine(Loop());
        }

        public static void Shutdown()
        {
            Timing.KillCoroutines(_loop);
            CloseAll();
            DisableWorld();
        }

        #region Большой экран в мире

        /// <param name="pos">Точка на уровне центра игрока (как Player.Position).</param>
        /// <param name="rot">Направление ОТ зрителя К экрану.</param>
        public static void SetWorldSpot(Vector3 pos, Quaternion rot)
        {
            DisableWorld();
            _worldPos = pos;
            _worldRot = rot;
            _worldOn = true;
        }

        public static void DisableWorld()
        {
            _worldOn = false;
            foreach (UpgraderTablet t in WorldScreens.Values.ToList())
                t.Despawn();
            WorldScreens.Clear();
        }

        /// <summary>origin экрана: низ экрана примерно на 0.45 м над полом.</summary>
        private static Vector3 WorldOrigin()
        {
            float floor = _worldPos.y - 0.95f;
            float k = _cfg.WorldScreenScale;
            return new Vector3(_worldPos.x, floor + 0.45f - UpgraderTablet.BottomOffset(k), _worldPos.z);
        }

        #endregion

        #region Планшет

        public static string Open(Player p)
        {
            Lang l = Loc.Of(p);
            if (p == null || !p.IsAlive)
                return Loc.T("tablet_dead", l);
            if (!p.IsHuman)
                return Loc.T("humans_only", l);
            if (!ZoneAllowed(p))
                return Loc.T("tablet_zone", l);

            if (Tablets.TryGetValue(p, out UpgraderTablet existing))
            {
                if (existing.IsSpinning)
                    return Loc.T("tablet_spinning", l);
                existing.SpawnInFront();
                return Loc.T("tablet_moved", l);
            }

            if (!TryAllocSpeaker(out byte id))
                return Loc.T("tablet_full", l);

            UpgraderTablet t = new UpgraderTablet(_cfg, p, id, _cfg.TabletScale, false);
            Tablets[p] = t;
            t.SpawnInFront();
            p.SendHint($"<size=24>{Loc.T("help", l)}</size>", 7f);
            return Loc.T("tablet_opened", l);
        }

        public static string Toggle(Player p)
        {
            if (p != null && Tablets.TryGetValue(p, out UpgraderTablet t) && !t.IsSpinning)
            {
                Close(p);
                return Loc.T("tablet_closed", Loc.Of(p));
            }

            return Open(p);
        }

        public static void Close(Player p)
        {
            if (p == null || !Tablets.TryGetValue(p, out UpgraderTablet t))
                return;
            Tablets.Remove(p);
            t.Despawn();
        }

        public static void CloseAll()
        {
            foreach (UpgraderTablet t in Tablets.Values.ToList())
                t.Despawn();
            Tablets.Clear();
        }

        #endregion

        /// <summary>При рестарте раунда объекты уничтожаются сами — только забываем.</summary>
        public static void Forget()
        {
            Tablets.Clear();
            WorldScreens.Clear();
            _worldOn = false;
        }

        /// <summary>Планшет разрешён только в зонах из tablet_zones (по умолчанию лёгкая и тяжёлая).</summary>
        public static bool ZoneAllowed(Player p)
        {
            if (_cfg.TabletZones == null || _cfg.TabletZones.Count == 0)
                return true;
            return _cfg.TabletZones.Contains(p.Zone);
        }

        private static bool TryAllocSpeaker(out byte id)
        {
            HashSet<byte> used = new HashSet<byte>(Tablets.Values.Concat(WorldScreens.Values).Select(t => t.SpeakerId));
            int count = Mathf.Max(1, (int)_cfg.SpeakerIdCount);
            for (int i = 0; i < count; i++)
            {
                int candidate = _cfg.SpeakerIdStart + i;
                if (candidate > 255)
                    break;
                if (!used.Contains((byte)candidate))
                {
                    id = (byte)candidate;
                    return true;
                }
            }

            id = 0;
            return false;
        }

        private static IEnumerator<float> Loop()
        {
            while (true)
            {
                yield return Timing.WaitForSeconds(0.3f);

                try
                {
                    UpdateWorld();
                    UpdateTablets();
                }
                catch (System.Exception e)
                {
                    Logger.Error($"[SCPGrade] {e}");
                }
            }
        }

        private static void UpdateWorld()
        {
            // убрать лишние копии
            foreach (KeyValuePair<Player, UpgraderTablet> kv in WorldScreens.ToList())
            {
                Player p = kv.Key;
                UpgraderTablet t = kv.Value;
                if (p == null || p.IsDestroyed)
                {
                    WorldScreens.Remove(p);
                    t.Despawn();
                    continue;
                }

                if (t.IsSpinning)
                    continue;

                bool keep = _worldOn && p.IsAlive && p.IsHuman && t.Spawned
                            && Vector3.Distance(p.Position, _worldPos) <= _cfg.WorldViewDistance + 3f;
                if (!keep)
                {
                    WorldScreens.Remove(p);
                    t.Despawn();
                }
            }

            if (!_worldOn)
                return;

            // создать копии тем, кто подошёл (не больше 2 за тик, чтобы не было рывка)
            int created = 0;
            foreach (Player p in Player.ReadyList)
            {
                if (created >= 2)
                    break;
                if (p == null || p.IsDestroyed || p.IsDummy || !p.IsAlive || !p.IsHuman || WorldScreens.ContainsKey(p))
                    continue;
                if (Vector3.Distance(p.Position, _worldPos) > _cfg.WorldViewDistance)
                    continue;
                if (!TryAllocSpeaker(out byte id))
                    break;

                UpgraderTablet t = new UpgraderTablet(_cfg, p, id, _cfg.WorldScreenScale, true);
                WorldScreens[p] = t;
                t.Spawn(WorldOrigin(), _worldRot);
                created++;
            }

            foreach (UpgraderTablet t in WorldScreens.Values.ToList())
                t.Tick();
        }

        private static void UpdateTablets()
        {
            foreach (KeyValuePair<Player, UpgraderTablet> kv in Tablets.ToList())
            {
                Player p = kv.Key;
                UpgraderTablet t = kv.Value;

                if (p == null || p.IsDestroyed)
                {
                    Tablets.Remove(p);
                    t.Despawn();
                    continue;
                }

                if (t.IsSpinning)
                    continue;

                Vector3 moved = p.Position - t.OpenedAt;
                moved.y = 0f;
                bool tooFar = moved.magnitude > _cfg.TabletMaxDistance;
                bool idle = Time.time - t.LastActivity > _cfg.TabletIdleClose;
                // зона None бывает в лифтах/переходах — не закрываем из-за неё
                bool wrongZone = p.Zone != MapGeneration.FacilityZone.None && !ZoneAllowed(p);
                if (!p.IsAlive || !p.IsHuman || tooFar || idle || wrongZone || !t.Spawned)
                {
                    if (wrongZone)
                        p.SendHint($"<size=24><color=#f55>{Loc.T("tablet_zone", Loc.Of(p))}</color></size>", 3f);
                    Close(p);
                    continue;
                }

                t.Tick();
            }
        }
    }
}
