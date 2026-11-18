using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LabApi.Features.Wrappers;
using MEC;
using UnityEngine;
using Logger = LabApi.Features.Console.Logger;
using Random = UnityEngine.Random;

namespace SCPGrade
{
    /// <summary>
    /// Личный планшет апгрейдера. Виден и слышен только владельцу, у каждого игрока свой —
    /// все могут крутить одновременно.
    /// ┌──────────────────────────── SCPGRADE ────────────────────────────┐
    /// │ [Ваш предмет]        ( 50.00% кольцо )       [Выберите предмет] │
    /// │  (подсказка)          [ Улучшить ]          [x2][x5]...[75%]    │
    /// │ ┌ Выберите предмет ───────────────────────────── 1/3  [<] [>] ┐ │
    /// │ │ [карточка] x7                                                 │ │
    /// │ │ [карточка] x7                                                 │ │
    /// └──────────────────────────────────────────────────────────────────┘
    /// Улучшается предмет в руке. Цель — E на карточку или x2/x5/x10/35%/50%/75%.
    /// Все размеры заданы для «большого» экрана 4.4 м и умножаются на TabletScale.
    /// </summary>
    public class UpgraderTablet
    {
        private enum State { Session, Spinning, Result }

        private readonly struct Target
        {
            public Target(string key, int value, float chance)
            {
                Key = key;
                Value = value;
                Chance = chance;
            }

            public string Key { get; }
            public int Value { get; }
            public float Chance { get; }
        }

        private sealed class Quick
        {
            public Quick(string label, float mult, float chance)
            {
                Label = label;
                Mult = mult;
                Chance = chance;
            }

            public string Label { get; }
            public float Mult { get; }
            public float Chance { get; }
            public PrimitiveObjectToy Plate { get; set; }
        }

        private sealed class Card
        {
            public PrimitiveObjectToy Border;
            public PrimitiveObjectToy Bg;
            public PrimitiveObjectToy Strip;
            public TextToy Top;
            public TextToy Name;
        }

        private sealed class BigCard
        {
            public PrimitiveObjectToy Border;
            public PrimitiveObjectToy Strip;
            public TextToy Title;
            public TextToy Name;
            public TextToy Sub;
            public readonly List<PrimitiveObjectToy> Chevrons = new List<PrimitiveObjectToy>();
        }

        // ── Геометрия большого экрана (метры), умножается на _k ──
        private const float ScreenW = 4.4f;
        private const float ScreenH = 2.75f;
        private const float ScreenY = 0.725f;
        private const float BigX = 1.5f;
        private const float BigY = 1.33f;
        private const float BigW = 1.2f;
        private const float BigH = 1.12f;
        private const float RingY = 1.38f;
        private const float RingR = 0.47f;
        private const int RingSegments = 40;
        private const float RowY = 0.66f;
        private const float GridTop = 0.08f;
        private const int GridCols = 7;
        private const int GridRows = 2;
        private const int PageSize = GridCols * GridRows;
        private const float CardW = 0.56f;
        private const float CardH = 0.4f;

        private static readonly Color ColFrame = Loc.Hex("17181c");
        private static readonly Color ColScreen = Loc.Hex("0d0e11");
        private static readonly Color ColPanel = Loc.Hex("1f2127");
        private static readonly Color ColCard = Loc.Hex("2a2d35");
        private static readonly Color ColAccent = Loc.Hex("f5c518");
        private static readonly Color ColZoneEdge = Loc.Hex("f5c518");
        private static readonly Color ColZoneMid = Loc.Hex("ec5a1c");
        private static readonly Color ColRing = Loc.Hex("3a3d46");
        private static readonly Color ColHot = Color.white;
        private static readonly Color ColQuick = Loc.Hex("2a2d35");
        private static readonly Color ColQuickOn = Loc.Hex("c99a0e");
        private static readonly Color ColGray = Loc.Hex("33363f");

        private readonly Config _cfg;
        private readonly float _k;
        private readonly List<AdminToy> _toys = new List<AdminToy>();
        private readonly List<(TextToy toy, Vector2 size)> _texts = new List<(TextToy, Vector2)>();
        private readonly PrimitiveObjectToy[] _segments = new PrimitiveObjectToy[RingSegments];
        private readonly List<Card> _cards = new List<Card>();
        private readonly List<Quick> _quicks = new List<Quick>
        {
            new Quick("x2", 2f, 0f),
            new Quick("x5", 5f, 0f),
            new Quick("x10", 10f, 0f),
            new Quick("35%", 0f, 35f),
            new Quick("50%", 0f, 50f),
            new Quick("75%", 0f, 75f),
        };

        private BigCard _leftCard;
        private BigCard _rightCard;
        private TextToy _header;
        private TextToy _center;
        private TextToy _leftInfo;
        private TextToy _upgradeText;
        private TextToy _gridTitle;
        private TextToy _pageText;
        private PrimitiveObjectToy _pivot;
        private SpeakerToy _speaker;

        private Vector3 _origin;
        private Quaternion _rot;
        private bool _poke;
        private int _tick;

        private State _state = State.Session;
        private string _targetKey;
        private string _lastHeldKey;
        private int _quick = -1;
        private int _page;
        private float _resultUntil;
        private bool _lastWin;
        private string _lastResultKey;
        private float _pointerAngle = 180f;

        public UpgraderTablet(Config cfg, Player owner, byte speakerId, float scale, bool isWorld)
        {
            _cfg = cfg;
            Owner = owner;
            SpeakerId = speakerId;
            IsWorld = isWorld;
            _k = Mathf.Clamp(scale, 0.1f, 1.5f);
            LastActivity = Time.time;
        }

        /// <summary>true — личная копия большого экрана в мире; false — планшет из .scpgrade.</summary>
        public bool IsWorld { get; }

        /// <summary>Нижняя граница экрана относительно origin (метры, с учётом масштаба).</summary>
        public static float BottomOffset(float scale) => (ScreenY - (ScreenH / 2f) - 0.15f) * scale;

        public Player Owner { get; }

        public byte SpeakerId { get; }

        public float LastActivity { get; private set; }

        public bool IsSpinning => _state == State.Spinning;

        public Vector3 Center => World(new Vector3(0f, ScreenY, 0f));

        /// <summary>Где стоял владелец, когда планшет открылся (для автозакрытия по дистанции).</summary>
        public Vector3 OpenedAt { get; private set; }

        public bool Spawned => _header != null && !_header.IsDestroyed;

        #region Спавн

        /// <summary>Ставит планшет перед глазами владельца.</summary>
        public void SpawnInFront()
        {
            Transform cam = Owner.Camera;
            Vector3 fwd = cam.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.01f)
                fwd = Owner.Rotation * Vector3.forward;
            fwd.y = 0f;
            fwd.Normalize();

            Quaternion facing = Quaternion.LookRotation(fwd);
            OpenedAt = Owner.Position;
            Vector3 screenCenter = cam.position + (fwd * _cfg.TabletDistance) - (Vector3.up * 0.12f);
            Vector3 origin = screenCenter - (Vector3.up * (ScreenY * _k));
            Spawn(origin, facing);
        }

        public void Spawn(Vector3 origin, Quaternion facing)
        {
            Despawn();
            _origin = origin;
            _rot = facing;

            float top = ScreenY + (ScreenH / 2f);

            // ── Корпус (рамка «планшета» со скруглением-имитацией)
            Prim(new Vector3(0f, ScreenY, 0.05f), new Vector3(ScreenW + 0.3f, ScreenH + 0.3f, 0.08f), ColFrame);
            Prim(new Vector3(0f, ScreenY, -0.002f), new Vector3(ScreenW, ScreenH, 0.01f), ColScreen);
            Prim(new Vector3(0f, top + 0.15f, -0.01f), new Vector3(0.18f, 0.035f, 0.02f), Loc.Hex("2a2c33")); // «камера»

            if (_cfg.TabletLight)
            {
                LightSourceToy light = LightSourceToy.Create(World(new Vector3(0f, ScreenY, -2.2f)), _rot, null, false);
                light.Color = new Color(1f, 0.96f, 0.9f);
                light.Intensity = _cfg.TabletLightIntensity;
                light.Range = Mathf.Max(1.5f, 4.5f * _k);
                light.ShadowType = LightShadows.None;
                Own(light);
            }

            _header = Text(new Vector3(0f, top - 0.09f, -0.03f), new Vector2(ScreenW - 0.3f, 0.14f), 1f);

            // ── Большие карточки
            _leftCard = MakeBigCard(-BigX, -1);
            _rightCard = MakeBigCard(BigX, 1);

            // ── Кольцо шанса
            for (int i = 0; i < RingSegments; i++)
            {
                float a = SegmentAngle(i);
                float segLen = 2f * Mathf.PI * RingR / RingSegments * 0.86f;
                _segments[i] = Prim(RingPoint(a, RingR, -0.02f), new Vector3(segLen, 0.075f, 0.02f), ColRing, Quaternion.Euler(0f, 0f, -a));
            }

            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f;
                Prim(RingPoint(a, RingR + 0.075f, -0.015f), new Vector3(0.01f, 0.045f, 0.01f), Loc.Hex("4a4d56"), Quaternion.Euler(0f, 0f, -a));
            }

            Prim(new Vector3(0f, RingY, -0.012f), new Vector3(RingR * 1.45f, RingR * 1.45f, 0.01f), Loc.Hex("1a1c22"), Quaternion.Euler(0f, 0f, 45f));
            Label(new Vector3(0f, RingY + RingR - 0.14f, -0.03f), "<color=#777>100%</color>", 0.6f);
            Label(new Vector3(-RingR + 0.17f, RingY, -0.03f), "<color=#777>50%</color>", 0.6f);
            Label(new Vector3(RingR - 0.17f, RingY, -0.03f), "<color=#777>50%</color>", 0.6f);

            // Ось + треугольник поперёк кольца (половина внутри, половина снаружи), остриём к центру
            _pivot = PrimitiveObjectToy.Create(World(new Vector3(0f, RingY, -0.035f)), _rot, Vector3.one, null, false);
            _pivot.Type = PrimitiveType.Cube;
            _pivot.Flags = AdminToys.PrimitiveFlags.None;
            _pivot.MovementSmoothing = 90;
            Own(_pivot);

            const int slices = 10;
            const float triH = 0.17f;
            const float triW = 0.13f;
            const float outH = triH + 0.04f;
            const float outW = triW + 0.05f;
            for (int k = 0; k < slices; k++)
            {
                float f = (k + 0.5f) / slices;
                Child(_pivot, new Vector3(0f, RingR + (outH / 2f) - (f * outH), -0.006f), new Vector3(outW * (1f - f), outH / slices * 1.08f, 0.006f), Loc.Hex("111214"));
                Child(_pivot, new Vector3(0f, RingR + (triH / 2f) - (f * triH), -0.012f), new Vector3(triW * (1f - f), triH / slices * 1.08f, 0.012f), ColAccent);
            }

            _center = Text(new Vector3(0f, RingY + 0.02f, -0.04f), new Vector2(0.7f, 0.36f), 1.2f);

            // ── Улучшить + быстрый выбор + подсказка
            ScreenButton(new Vector2(0f, RowY), new Vector2(1.25f, 0.2f), Loc.Hex("8f7210"), null, 0, Mathf.Max(0f, _cfg.UpgradeHoldTime), OnUpgrade);
            _upgradeText = Text(new Vector3(0f, RowY, -0.04f), new Vector2(1.2f, 0.18f), 1.15f);
            Chevron(new Vector2(-0.38f, RowY + 0.018f), 1, 0.6f, ColAccent);
            Chevron(new Vector2(-0.38f, RowY - 0.018f), 1, 0.6f, ColAccent);

            for (int i = 0; i < _quicks.Count; i++)
            {
                int index = i;
                _quicks[i].Plate = ScreenButton(new Vector2(BigX - 0.5f + (i * 0.2f), RowY), new Vector2(0.18f, 0.15f), ColQuick, $"<color=#ddd>{_quicks[i].Label}</color>", 0, 0f, () => OnQuick(index));
            }

            _leftInfo = Text(new Vector3(-BigX, RowY, -0.03f), new Vector2(BigW, 0.15f), 0.75f);

            // ── Сетка целей
            Prim(new Vector3(0f, -0.08f, -0.008f), new Vector3(ScreenW - 0.2f, 1.05f, 0.01f), ColPanel);
            Chevron(new Vector2(-1.98f, 0.37f), 1, 0.55f, ColAccent);
            _gridTitle = Text(new Vector3(-0.85f, 0.37f, -0.03f), new Vector2(2.1f, 0.12f), 0.9f);
            _pageText = Text(new Vector3(1.3f, 0.37f, -0.03f), new Vector2(0.4f, 0.12f), 0.8f);
            ScreenButton(new Vector2(1.62f, 0.37f), new Vector2(0.22f, 0.11f), ColQuick, null, 2, 0f, () => OnPage(-1));
            ScreenButton(new Vector2(1.88f, 0.37f), new Vector2(0.22f, 0.11f), ColQuick, null, -2, 0f, () => OnPage(1));

            for (int r = 0; r < GridRows; r++)
            {
                for (int c = 0; c < GridCols; c++)
                {
                    int slot = _cards.Count;
                    Vector2 pos = new Vector2((c - ((GridCols - 1) / 2f)) * (CardW + 0.035f), GridTop - (r * (CardH + 0.035f)));
                    _cards.Add(MakeCard(pos, slot));
                }
            }

            // ── Колонка (своя у каждого планшета, слышит только владелец)
            _speaker = SpeakerToy.Create(World(new Vector3(0f, RingY, -0.3f)), _rot, null, false);
            _speaker.ControllerId = SpeakerId;
            _speaker.IsSpatial = true;
            _speaker.Volume = _cfg.Volume;
            _speaker.MinDistance = 1.5f;
            _speaker.MaxDistance = 12f;
            Own(_speaker);
            SpeakerToy.GetTransmitter(SpeakerId).ValidPlayers = pl => pl == Owner;

            _state = State.Session;
            SetPointer(_pointerAngle);
            Render();
        }

        public void Despawn()
        {
            for (int i = _toys.Count - 1; i >= 0; i--)
            {
                try
                {
                    if (!_toys[i].IsDestroyed)
                        _toys[i].Destroy();
                }
                catch
                {
                    // уже уничтожен сменой сцены
                }
            }

            _toys.Clear();
            _texts.Clear();
            _cards.Clear();
            Array.Clear(_segments, 0, _segments.Length);
            foreach (Quick q in _quicks)
                q.Plate = null;
            _header = null;
        }

        private Vector3 World(Vector3 local) => _origin + (_rot * (local * _k));

        private static float SegmentAngle(int i) => 360f * (i + 0.5f) / RingSegments;

        private static Vector3 RingPoint(float angleDeg, float radius, float z)
        {
            float r = angleDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(r) * radius, RingY + (Mathf.Cos(r) * radius), z);
        }

        private void Own(AdminToy toy)
        {
            OwnerOnly.Spawn(toy, Owner);
            _toys.Add(toy);
        }

        private PrimitiveObjectToy Prim(Vector3 local, Vector3 scale, Color color, Quaternion? localRot = null)
        {
            PrimitiveObjectToy toy = PrimitiveObjectToy.Create(World(local), _rot * (localRot ?? Quaternion.identity), scale * _k, null, false);
            toy.Type = PrimitiveType.Cube;
            toy.Color = color;
            toy.Flags = AdminToys.PrimitiveFlags.Visible;
            Own(toy);
            return toy;
        }

        private void Child(AdminToy parent, Vector3 localPos, Vector3 scale, Color color)
        {
            PrimitiveObjectToy toy = PrimitiveObjectToy.Create(localPos * _k, Quaternion.identity, scale * _k, parent.Transform, false);
            toy.Type = PrimitiveType.Cube;
            toy.Color = color;
            toy.Flags = AdminToys.PrimitiveFlags.Visible;
            Own(toy);
        }

        private TextToy Text(Vector3 local, Vector2 worldSize, float sizeMul)
        {
            float scale = _cfg.TextScale * sizeMul * _k;
            Vector2 display = worldSize * _k / (scale * _cfg.TextCalibration);
            TextToy t = TextToy.Create(World(local), _rot, Vector3.one * scale, null, false);
            t.DisplaySize = display;
            t.TextFormat = " ";
            Own(t);
            t.DisplaySize = display + new Vector2(0.01f, 0f);
            _texts.Add((t, display));
            return t;
        }

        private void Label(Vector3 local, string text, float sizeMul) =>
            Text(local, new Vector2(0.4f, 0.1f), sizeMul).TextFormat = $"<align=center>{text}</align>";

        /// <summary>«^» (1), «v» (-1), «&lt;» (2), «&gt;» (-2) из двух палочек.</summary>
        private List<PrimitiveObjectToy> Chevron(Vector2 pos, int dir, float scale, Color color, float z = -0.04f)
        {
            float half = 0.032f * scale;
            Vector3 bar = new Vector3(0.085f * scale, 0.022f * scale, 0.01f);
            List<PrimitiveObjectToy> list = new List<PrimitiveObjectToy>();
            float a = dir > 0 ? 45f : -45f;
            if (Mathf.Abs(dir) == 1)
            {
                list.Add(Prim(new Vector3(pos.x - half, pos.y, z), bar, color, Quaternion.Euler(0f, 0f, a)));
                list.Add(Prim(new Vector3(pos.x + half, pos.y, z), bar, color, Quaternion.Euler(0f, 0f, -a)));
            }
            else
            {
                list.Add(Prim(new Vector3(pos.x, pos.y + half, z), bar, color, Quaternion.Euler(0f, 0f, a)));
                list.Add(Prim(new Vector3(pos.x, pos.y - half, z), bar, color, Quaternion.Euler(0f, 0f, -a)));
            }

            return list;
        }

        private PrimitiveObjectToy ScreenButton(Vector2 pos, Vector2 size, Color color, string caption, int chevron, float holdTime, Action onPress)
        {
            PrimitiveObjectToy plate = Prim(new Vector3(pos.x, pos.y, -0.025f), new Vector3(size.x, size.y, 0.02f), color);
            if (caption != null)
                Text(new Vector3(pos.x, pos.y, -0.04f), size, 0.8f).TextFormat = $"<align=center><b>{caption}</b></align>";
            if (chevron != 0)
                Chevron(pos, chevron, 1f, Color.white);
            Hit(pos, size, holdTime, onPress);
            return plate;
        }

        private void Hit(Vector2 pos, Vector2 size, float holdTime, Action onPress)
        {
            InteractableToy it = InteractableToy.Create(World(new Vector3(pos.x, pos.y, -0.05f)), _rot, new Vector3(size.x + 0.02f, size.y + 0.02f, 0.08f) * _k, null, false);
            it.Shape = AdminToys.InvisibleInteractableToy.ColliderShape.Box;
            it.InteractionDuration = holdTime;

            void Handler(Player p)
            {
                if (p != Owner)
                    return;
                LastActivity = Time.time;
                try
                {
                    onPress();
                }
                catch (Exception e)
                {
                    Logger.Error($"[SCPGrade] {e}");
                }
            }

            if (holdTime > 0f)
                it.OnSearched += Handler;
            else
                it.OnInteracted += Handler;

            Own(it);
        }

        private BigCard MakeBigCard(float x, int chevronDir)
        {
            BigCard bc = new BigCard();
            float cardBottom = BigY - (BigH / 2f);
            bc.Border = Prim(new Vector3(x, BigY, -0.01f), new Vector3(BigW + 0.02f, BigH + 0.02f, 0.01f), ColGray);
            Prim(new Vector3(x, BigY, -0.016f), new Vector3(BigW, BigH, 0.01f), ColPanel);
            bc.Strip = Prim(new Vector3(x, cardBottom + 0.015f, -0.022f), new Vector3(BigW, 0.03f, 0.01f), ColGray);
            bc.Title = Text(new Vector3(x, BigY + (BigH / 2f) - 0.09f, -0.03f), new Vector2(BigW - 0.1f, 0.12f), 0.85f);
            bc.Name = Text(new Vector3(x, BigY + 0.07f, -0.03f), new Vector2(BigW - 0.1f, 0.3f), 1.7f);
            bc.Sub = Text(new Vector3(x, BigY - 0.2f, -0.03f), new Vector2(BigW - 0.1f, 0.12f), 0.95f);

            for (int i = 0; i < 3; i++)
            {
                float dy = (i - 1) * 0.085f * chevronDir;
                Color c = i == 1 ? ColAccent : ColAccent * 0.55f;
                c.a = 1f;
                bc.Chevrons.AddRange(Chevron(new Vector2(x, BigY + 0.04f + dy), chevronDir, 2.6f, c, -0.025f));
            }

            return bc;
        }

        private Card MakeCard(Vector2 pos, int slot)
        {
            Card c = new Card();
            c.Border = Prim(new Vector3(pos.x, pos.y, -0.012f), new Vector3(CardW, CardH, 0.01f), ColGray);
            c.Bg = Prim(new Vector3(pos.x, pos.y, -0.018f), new Vector3(CardW - 0.02f, CardH - 0.02f, 0.01f), ColCard);
            c.Strip = Prim(new Vector3(pos.x, pos.y - (CardH / 2f) + 0.02f, -0.024f), new Vector3(CardW - 0.02f, 0.02f, 0.01f), ColGray);
            c.Top = Text(new Vector3(pos.x, pos.y + (CardH / 2f) - 0.05f, -0.03f), new Vector2(CardW - 0.06f, 0.07f), 0.65f);
            c.Name = Text(new Vector3(pos.x, pos.y - 0.02f, -0.03f), new Vector2(CardW - 0.05f, 0.2f), 0.85f);
            Hit(pos, new Vector2(CardW, CardH), 0f, () => OnCard(slot));
            return c;
        }

        private void Play(float[] clip)
        {
            if (!_cfg.EnableSounds || _speaker == null || _speaker.IsDestroyed)
                return;
            try
            {
                SpeakerToy.GetTransmitter(SpeakerId).ValidPlayers = pl => pl == Owner;
                _speaker.Play(clip, false);
            }
            catch (Exception e)
            {
                if (_cfg.Debug)
                    Logger.Warn($"[SCPGrade] звук: {e.Message}");
            }
        }

        #endregion

        #region Логика

        private Lang L => Loc.Of(Owner);

        private void OnCard(int slot)
        {
            if (_state == State.Spinning)
                return;
            _state = State.Session;
            Play(Sounds.Click);

            List<Target> targets = Targets(HeldValue(out _));
            int i = (_page * PageSize) + slot;
            if (i < targets.Count)
            {
                _targetKey = targets[i].Key;
                _quick = -1;
            }

            Render();
        }

        private void OnPage(int dir)
        {
            if (_state == State.Spinning)
                return;
            _state = State.Session;
            Play(Sounds.Click);
            int pages = Mathf.Max(1, Mathf.CeilToInt(Targets(HeldValue(out _)).Count / (float)PageSize));
            _page = Wrap(_page + dir, pages);
            Render();
        }

        private void OnQuick(int index)
        {
            if (_state == State.Spinning)
                return;
            _state = State.Session;
            Play(Sounds.Click);
            _quick = index;
            int baseValue = HeldValue(out _);
            ApplyQuick(Targets(baseValue), baseValue);
            Render();
        }

        private void ApplyQuick(List<Target> targets, int baseValue)
        {
            if (_quick < 0 || targets.Count == 0 || baseValue <= 0)
                return;

            Quick q = _quicks[_quick];
            Target best = q.Mult > 0f
                ? targets.OrderBy(t => Mathf.Abs(t.Value - (baseValue * q.Mult))).First()
                : targets.OrderBy(t => Mathf.Abs(t.Chance - q.Chance)).ThenByDescending(t => t.Value).First();
            _targetKey = best.Key;
            _page = targets.FindIndex(t => t.Key == best.Key) / PageSize;
        }

        private void OnUpgrade()
        {
            if (_state == State.Spinning)
                return;
            _state = State.Session;

            Lang pl = L;
            if (!Owner.IsHuman)
            {
                Owner.SendHint($"<color=#f55>{Loc.T("humans_only", pl)}</color>", 3f);
                return;
            }

            Item item = Owner.CurrentItem;
            int baseValue = HeldValue(out string fromKey);
            if (baseValue <= 0 || item == null)
            {
                Owner.SendHint($"<color=#f55>{Loc.T("hold_item", pl)}</color>", 3f);
                return;
            }

            List<Target> targets = Targets(baseValue);
            int idx = targets.FindIndex(t => t.Key == _targetKey);
            if (idx < 0)
            {
                Owner.SendHint($"<color=#f55>{Loc.T("pick_target", pl)}</color>", 3f);
                return;
            }

            Target target = targets[idx];
            Owner.RemoveItem(item);

            float roll = Random.Range(0f, 100f);
            bool win = roll < target.Chance;

            if (_cfg.Debug)
                Logger.Info($"[SCPGrade] {Owner.Nickname}: {fromKey}({baseValue}) -> {target.Key}, шанс {target.Chance:0.0}, ролл {roll:0.0}, {(win ? "WIN" : "LOSE")}");

            _state = State.Spinning;
            Timing.RunCoroutine(Spin(fromKey, baseValue, target, roll, win));
        }

        private IEnumerator<float> Spin(string fromKey, int baseValue, Target target, float roll, bool win)
        {
            Lang pl = L;
            float duration = Mathf.Max(2f, _cfg.SpinDuration);
            float startAngle = Mathf.Repeat(_pointerAngle, 360f);

            // зона выигрыша — по центру снизу: [180 - chance*1.8 ; 180 + chance*1.8]
            float zoneStart = 180f - (target.Chance * 1.8f);
            float stopAngle = Mathf.Repeat(zoneStart + (roll * 3.6f), 360f);
            float turns = 360f * Mathf.Max(2, _cfg.SpinTurns);
            float endAngle = stopAngle;
            while (endAngle - startAngle < turns)
                endAngle += 360f;

            Color[] ring = RingColors(target.Chance);
            ApplyRing(ring);
            foreach (Quick q in _quicks)
                q.Plate.Color = ColQuick;
            _header.TextFormat = HeaderText($"<color=#fc3>{Loc.T("spinning", pl)}</color>", pl);
            _upgradeText.TextFormat = $"<align=center><b><color=#777>{Loc.T("spinning", pl)}</color></b></align>";
            Play(Sounds.Start);

            float start = Time.time;
            float lastTick = 0f;
            int lit = -1;
            while (true)
            {
                if (!Spawned)
                    yield break;

                float t = Mathf.Clamp01((Time.time - start) / duration);
                float eased = 1f - Mathf.Pow(1f - t, 4f);
                float angle = Mathf.Lerp(startAngle, endAngle, eased);
                SetPointer(angle);

                int seg = Mathf.Clamp(Mathf.FloorToInt(Mathf.Repeat(angle, 360f) / 360f * RingSegments), 0, RingSegments - 1);
                if (seg != lit)
                {
                    if (lit >= 0)
                        _segments[lit].Color = ring[lit];
                    _segments[seg].Color = ColHot;
                    lit = seg;

                    if (Time.time - start > 0.3f && Time.time - lastTick > 0.035f)
                    {
                        Play(Sounds.Tick);
                        lastTick = Time.time;
                    }
                }

                if (t >= 1f)
                    break;
                yield return Timing.WaitForSeconds(0.04f);
            }

            if (lit >= 0)
                _segments[lit].Color = ring[lit];

            Play(win ? Sounds.Win : Sounds.Lose);

            Color flash = win ? Loc.Hex("39d353") : Loc.Hex("e5322d");
            for (int k = 0; k < 3 && Spawned; k++)
            {
                foreach (PrimitiveObjectToy s in _segments)
                    s.Color = flash;
                _center.TextFormat = win
                    ? $"<align=center><color=#3f6><size=170%><b>{Loc.T("win", pl)}</b></size></color></align>"
                    : $"<align=center><color=#f44><size=170%><b>{Loc.T("lose", pl)}</b></size></color></align>";
                yield return Timing.WaitForSeconds(0.25f);
                ApplyRing(ring);
                yield return Timing.WaitForSeconds(0.2f);
            }

            if (!Spawned)
                yield break;

            // Выдача
            string name = ItemValues.Name(target.Key, pl);
            if (win)
            {
                ItemValues.Give(Owner, target.Key, World(new Vector3(0f, -0.3f, -1.5f)));
                Owner.SendHint($"<size=40><color=#3f6>{Loc.T("win", pl)}!</color></size>\n{Loc.T("got", pl)} <b>{name}</b>", 5f);

                if (target.Chance <= 10f)
                {
                    foreach (Player pp in Player.ReadyList)
                    {
                        Lang l = Loc.Of(pp);
                        pp.SendBroadcast($"<color=#3f6>{Safe(Owner.Nickname)}</color> " + string.Format(Loc.T("broadcast", l), $"<b>{ItemValues.Name(target.Key, l)}</b>", target.Chance.ToString("0.0", CultureInfo.InvariantCulture)), 5);
                    }
                }
            }
            else
            {
                Owner.SendHint($"<size=40><color=#f44>{Loc.T("lose", pl)}</color></size>\n{ItemValues.Name(fromKey, pl)} {Loc.T("burned", pl)}", 5f);
            }

            _pointerAngle = stopAngle;
            _lastWin = win;
            _lastResultKey = win ? target.Key : fromKey;
            _state = State.Result;
            _resultUntil = Time.time + 3f;
            _targetKey = null;
            LastActivity = Time.time;
            Render();
        }

        /// <summary>Вызывается менеджером раз в ~0.3 с.</summary>
        public void Tick()
        {
            if (!Spawned || _state == State.Spinning)
                return;

            _tick++;
            if (_state == State.Result && Time.time >= _resultUntil)
                _state = State.Session;

            Render();

            if (_tick % 16 == 0)
            {
                _poke = !_poke;
                foreach ((TextToy toy, Vector2 size) in _texts)
                {
                    if (toy != null && !toy.IsDestroyed)
                        toy.DisplaySize = size + new Vector2(_poke ? 0.02f : 0.01f, 0f);
                }
            }
        }

        private void SetPointer(float angle)
        {
            _pointerAngle = angle;
            if (_pivot != null && !_pivot.IsDestroyed)
                _pivot.Rotation = _rot * Quaternion.Euler(0f, 0f, -angle);
        }

        private static Color[] RingColors(float chance)
        {
            Color[] result = new Color[RingSegments];
            float half = Mathf.Max(0f, chance) * 1.8f;
            float segHalf = 180f / RingSegments;
            for (int i = 0; i < RingSegments; i++)
            {
                float d = Mathf.Abs(SegmentAngle(i) - 180f);
                if (chance <= 0f || d - segHalf >= half)
                {
                    result[i] = ColRing;
                    continue;
                }

                Color zone = Color.Lerp(ColZoneMid, ColZoneEdge, half > 0f ? Mathf.Clamp01(d / half) : 0f);
                float cover = Mathf.Clamp01((half - (d - segHalf)) / (2f * segHalf));
                result[i] = Color.Lerp(ColRing, zone, cover);
            }

            return result;
        }

        private void ApplyRing(Color[] colors)
        {
            for (int i = 0; i < RingSegments; i++)
            {
                if (_segments[i] != null && !_segments[i].IsDestroyed)
                    _segments[i].Color = colors[i];
            }
        }

        private int HeldValue(out string key)
        {
            key = null;
            Item item = Owner?.CurrentItem;
            if (item == null || _cfg.InputBlacklist.Contains(item.Type.ToString()))
                return 0;
            return ItemValues.TryGetHeld(item, out key, out int value) ? value : 0;
        }

        private List<Target> Targets(int baseValue)
        {
            List<Target> list = new List<Target>();
            if (baseValue <= 0)
                return list;

            foreach (KeyValuePair<string, int> kv in ItemValues.All)
            {
                if (kv.Value <= baseValue || _cfg.TargetBlacklist.Contains(kv.Key))
                    continue;

                float raw = (float)baseValue / kv.Value * (1f - _cfg.HouseEdge) * 100f;
                if (raw < _cfg.MinChance)
                    continue;

                list.Add(new Target(kv.Key, kv.Value, Mathf.Min(raw, _cfg.MaxChance)));
            }

            return list.OrderBy(t => t.Value).ThenBy(t => t.Key).ToList();
        }

        private static int Wrap(int i, int count) => count <= 0 ? 0 : ((i % count) + count) % count;

        #endregion

        #region Отрисовка

        private string HeaderText(string left, Lang lang) =>
            $"<align=left>{left}<line-height=0>\n" +
            $"<align=center><size=125%><b><color=#f5c518>SCPGRADE</color></b></size><line-height=0>\n" +
            $"<align=right><color=#888>{Loc.T("author", lang)}:</color> <color=#fff>{Safe(_cfg.Author)}</color>   <color=#888>ds:</color> <color=#7289da>{Safe(_cfg.Discord)}</color><line-height=1em>";

        private void Render()
        {
            if (!Spawned || _state == State.Spinning)
                return;

            Lang lang = L;
            _header.TextFormat = HeaderText($"<color=#7f7>{Safe(Owner.Nickname)}</color>", lang);
            _upgradeText.TextFormat = $"<align=center><b><color=#fff>{Loc.T("upgrade", lang)}</color></b></align>";
            _gridTitle.TextFormat = $"<align=left><b><color=#fff>{Loc.T("choose", lang)}</color></b></align>";

            Item held = Owner.CurrentItem;
            int baseValue = HeldValue(out string heldKey);
            List<Target> targets = Targets(baseValue);

            // сменил предмет в руке — переподбираем цель по быстрому выбору
            string heldId = held == null ? null : held.Serial + ":" + heldKey;
            if (heldId != _lastHeldKey)
            {
                _lastHeldKey = heldId;
                if (_quick >= 0)
                    ApplyQuick(targets, baseValue);
            }

            int idx = targets.FindIndex(t => t.Key == _targetKey);
            if (idx < 0 && _quick >= 0 && targets.Count > 0)
            {
                ApplyQuick(targets, baseValue);
                idx = targets.FindIndex(t => t.Key == _targetKey);
            }

            int pages = Mathf.Max(1, Mathf.CeilToInt(targets.Count / (float)PageSize));
            _page = Mathf.Clamp(_page, 0, pages - 1);

            // ── Левая карточка — предмет в руке
            if (_state == State.Result && !_lastWin)
            {
                FillBig(_leftCard, Loc.T("your_item", lang), null, 0, lang, $"<color=#f44>{ItemValues.Name(_lastResultKey, lang)} {Loc.T("burned", lang)}</color>");
            }
            else if (baseValue > 0)
            {
                FillBig(_leftCard, Loc.T("your_item", lang), heldKey, baseValue, lang, null);
            }
            else
            {
                string sub = held != null
                    ? $"<color=#f55>{ItemValues.Name(held.Type.ToString(), lang)} — {Loc.T("no_price", lang)}</color>"
                    : $"<color=#888>{Loc.T("your_item_sub", lang)}</color>";
                FillBig(_leftCard, Loc.T("your_item", lang), null, 0, lang, sub);
            }

            // ── Правая карточка — цель
            Target? target = idx >= 0 ? targets[idx] : (Target?)null;
            if (_state == State.Result && _lastWin)
                FillBig(_rightCard, Loc.T("target", lang), _lastResultKey, ItemValues.Get(_lastResultKey), lang, null);
            else if (target.HasValue)
                FillBig(_rightCard, Loc.T("target", lang), target.Value.Key, target.Value.Value, lang, null);
            else
                FillBig(_rightCard, Loc.T("target", lang), null, 0, lang, $"<color=#888>{(baseValue > 0 && targets.Count == 0 ? Loc.T("no_targets", lang) : Loc.T("target_sub", lang))}</color>");

            // ── Сетка
            for (int s = 0; s < _cards.Count; s++)
            {
                int i = (_page * PageSize) + s;
                if (i < targets.Count)
                    FillCard(_cards[s], targets[i], i == idx, lang);
                else
                    HideCard(_cards[s]);
            }

            _pageText.TextFormat = targets.Count > 0 ? $"<align=right><color=#888>{_page + 1}/{pages}</color></align>" : " ";
            for (int i = 0; i < _quicks.Count; i++)
                _quicks[i].Plate.Color = i == _quick ? ColQuickOn : ColQuick;

            // ── Центр
            if (_state == State.Result)
            {
                ApplyRing(RingColors(-1f));
                _center.TextFormat = _lastWin
                    ? $"<align=center><color=#3f6><size=160%><b>{Loc.T("win", lang)}</b></size></color></align>"
                    : $"<align=center><color=#f44><size=160%><b>{Loc.T("lose", lang)}</b></size></color></align>";
            }
            else if (target.HasValue)
            {
                ApplyRing(RingColors(target.Value.Chance));
                _center.TextFormat = $"<align=center><color={ChanceColor(target.Value.Chance)}><size=190%><b>{target.Value.Chance.ToString("0.00", CultureInfo.InvariantCulture)}%</b></size></color>\n<color=#9be21c><size=80%>{Loc.T("avg_chance", lang)}</size></color></align>";
            }
            else
            {
                ApplyRing(RingColors(-1f));
                _center.TextFormat = $"<align=center><color=#555><size=190%><b>0.00%</b></size></color>\n<color=#666><size=80%>{Loc.T("avg_chance", lang)}</size></color></align>";
            }

            _leftInfo.TextFormat = target.HasValue && baseValue > 0
                ? $"<align=center><color=#fc3>x{((float)target.Value.Value / baseValue).ToString("0.00", CultureInfo.InvariantCulture)}</color>  <color=#888>{Loc.T("hold_e", lang)}</color></align>"
                : $"<align=center><color=#567>{Loc.T(IsWorld ? "world_hint" : "close_hint", lang)}</color></align>";
        }

        private static void FillBig(BigCard bc, string title, string key, int value, Lang lang, string emptySub)
        {
            bc.Title.TextFormat = $"<align=center><b><color=#fff>{title}</color></b></align>";
            bool has = key != null && value > 0;

            foreach (PrimitiveObjectToy ch in bc.Chevrons)
                ch.Flags = has ? AdminToys.PrimitiveFlags.None : AdminToys.PrimitiveFlags.Visible;

            if (!has)
            {
                bc.Name.TextFormat = " ";
                bc.Sub.TextFormat = $"<align=center>{emptySub}</align>";
                bc.Border.Color = ColGray;
                bc.Strip.Color = ColGray;
                return;
            }

            Color rc = Loc.RarityColor(value);
            bc.Border.Color = rc;
            bc.Strip.Color = rc;
            bc.Name.TextFormat = $"<align=center><b><color=#fff>{ItemValues.Name(key, lang)}</color></b></align>";
            bc.Sub.TextFormat = $"<align=center><color=#f5c518>{value}</color> <color=#555>•</color> <color=#{ColorUtility.ToHtmlStringRGB(rc)}>{Loc.RarityName(value, lang)}</color></align>";
        }

        private void FillCard(Card c, Target t, bool selected, Lang lang)
        {
            c.Border.Flags = c.Bg.Flags = c.Strip.Flags = AdminToys.PrimitiveFlags.Visible;

            Color rc = Loc.RarityColor(t.Value);
            Color border = rc * 0.75f;
            border.a = 1f;
            c.Border.Color = selected ? ColAccent : border;
            c.Border.Scale = (selected ? new Vector3(CardW + 0.03f, CardH + 0.03f, 0.01f) : new Vector3(CardW, CardH, 0.01f)) * _k;
            c.Bg.Color = selected ? Loc.Hex("3a3420") : ColCard;
            c.Strip.Color = rc;
            c.Top.TextFormat = $"<align=left><color=#f5c518><b>{t.Value}</b></color><line-height=0>\n<align=right><color={ChanceColor(t.Chance)}>{t.Chance.ToString("0.#", CultureInfo.InvariantCulture)}%</color><line-height=1em>";
            c.Name.TextFormat = $"<align=center><b><color=#fff>{ItemValues.Name(t.Key, lang)}</color></b></align>";
        }

        private static void HideCard(Card c)
        {
            c.Border.Flags = c.Bg.Flags = c.Strip.Flags = AdminToys.PrimitiveFlags.None;
            c.Top.TextFormat = " ";
            c.Name.TextFormat = " ";
        }

        private static string ChanceColor(float c) => c >= 50f ? "#9be21c" : c >= 20f ? "#f5c518" : "#f0603a";

        private static string Safe(string s) =>
            string.IsNullOrEmpty(s) ? "-" : s.Replace("{", "(").Replace("}", ")").Replace("<", "").Replace(">", "");

        #endregion
    }
}
