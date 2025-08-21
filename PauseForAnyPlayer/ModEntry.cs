using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using PauseForAnyPlayer.Services;

namespace PauseForAnyPlayer
{
    public class ModEntry : Mod
    {
        private ModConfig config;
        private PlayerStateManager playerStateManager;
        private PauseService pauseService;
        private EnergyService energyService;
        private NetworkMessageHandler networkMessageHandler;

        public override void Entry(IModHelper helper)
        {
            Monitor.Log($"PauseForAnyPlayer loaded - v{ModManifest.Version}", LogLevel.Info);

            config = Helper.ReadConfig<ModConfig>();

            playerStateManager = new PlayerStateManager();
            pauseService = new PauseService(Helper, ModManifest, playerStateManager);
            energyService = new EnergyService(config, Helper, ModManifest, playerStateManager);
            networkMessageHandler = new NetworkMessageHandler(ModManifest, playerStateManager);

            Helper.Events.GameLoop.UpdateTicked += GameLoop_UpdateTicked;
            Helper.Events.GameLoop.SaveLoaded += GameLoop_SaveLoaded;
            Helper.Events.GameLoop.GameLaunched += GameLoop_GameLaunched;
            Helper.Events.Multiplayer.ModMessageReceived += networkMessageHandler.HandleModMessageReceived;
            Helper.Events.Multiplayer.PeerConnected += networkMessageHandler.HandlePeerConnected;
            Helper.Events.Multiplayer.PeerDisconnected += networkMessageHandler.HandlePeerDisconnected;
        }

        private void GameLoop_SaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            if (EnergyService.IsResponsibleForEnergyScaling())
            {
                playerStateManager.ClearAllPlayers();
                foreach (Farmer farmer in Game1.getAllFarmers())
                {
                    playerStateManager.AddPlayer(farmer.UniqueMultiplayerID, farmer.Stamina, farmer.MaxStamina);
                }
            }
        }

        private void GameLoop_UpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            pauseService.HandlePauseLogic();
            energyService.HandleEnergyScaling();
        }

        private void GameLoop_GameLaunched(object sender, GameLaunchedEventArgs e)
        {
            if (!EnergyService.IsResponsibleForEnergyScaling())
                return;

            AddOptionsToGenericModConfigMenu();
        }

        private void AddOptionsToGenericModConfigMenu()
        {
            var configMenu = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null)
                return;

            configMenu.Register(
                mod: ModManifest,
                reset: () => config = new ModConfig(),
                save: () => Helper.WriteConfig(config)
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => "Energy Scale Factor",
                tooltip: () => "Multiplier for energy gains (1.0 = normal, 0.5 = half energy, etc.)",
                getValue: () => config.EnergyScaleFactor,
                setValue: value => config.EnergyScaleFactor = value,
                min: 0.1f,
                max: 2.0f,
                interval: 0.05f,
                formatValue: value => $"{value:F2}x"
            );
        }
    }
}