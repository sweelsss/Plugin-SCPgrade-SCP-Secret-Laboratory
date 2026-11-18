using System;

namespace SCPGrade
{
#if EXILED
    using ServerEvents = Exiled.Events.Handlers.Server;

    /// <summary>EXILED-версия (EXILED 9.15+, SCP:SL 14.x).</summary>
    public class Plugin : Exiled.API.Features.Plugin<Config>
    {
        public static Plugin Instance { get; private set; }

        public override string Name => "SCPGrade";
        public override string Prefix => "scpgrade";
        public override string Author => "sweelsss";
        public override Version Version => new Version(1, 1, 0);
        public override Version RequiredExiledVersion => new Version(9, 15, 0);

        public override void OnEnabled()
        {
            Instance = this;
            Startup();
            ServerEvents.RoundStarted += OnRoundStarted;
            ServerEvents.RestartingRound += OnRestartingRound;
            base.OnEnabled();
        }

        public override void OnDisabled()
        {
            ServerEvents.RoundStarted -= OnRoundStarted;
            ServerEvents.RestartingRound -= OnRestartingRound;
            Shutdown();
            Instance = null;
            base.OnDisabled();
        }
#else
    using LabApi.Features;
    using ServerEvents = LabApi.Events.Handlers.ServerEvents;

    /// <summary>LabAPI-версия (SCP:SL 14.x).</summary>
    public class Plugin : LabApi.Loader.Features.Plugins.Plugin<Config>
    {
        public static Plugin Instance { get; private set; }

        public override string Name => "SCPGrade";
        public override string Description => "SCPGrade — апгрейдер предметов в стиле CS: личный экран и планшет (.scpgrade).";
        public override string Author => "sweelsss";
        public override Version Version => new Version(1, 1, 0);
        public override Version RequiredApiVersion => new Version(LabApiProperties.CompiledVersion);

        public override void Enable()
        {
            Instance = this;
            Startup();
            ServerEvents.RoundStarted += OnRoundStarted;
            ServerEvents.RoundRestarted += OnRestartingRound;
        }

        public override void Disable()
        {
            ServerEvents.RoundStarted -= OnRoundStarted;
            ServerEvents.RoundRestarted -= OnRestartingRound;
            Shutdown();
            Instance = null;
        }
#endif

        private void Startup()
        {
            ItemValues.Init(Config);
            Loc.Register(Config);
            TabletManager.Init(Config);
        }

        private static void Shutdown()
        {
            TabletManager.Shutdown();
            Kiosk.Despawn();
        }

        private void OnRoundStarted()
        {
            Loc.EnsureRegistered();
            if (Config.SpawnKiosk)
                Kiosk.SpawnDefault(Config);
        }

        private static void OnRestartingRound()
        {
            TabletManager.Forget();
            Kiosk.Forget();
        }
    }
}
