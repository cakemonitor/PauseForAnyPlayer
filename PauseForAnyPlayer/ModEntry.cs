using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Minigames;

namespace PauseForAnyPlayer
{
    public class ModEntry : Mod
    {
        private ModConfig config;
        private bool isPaused;
        private int cachedGameTimeInterval;
        private Dictionary<long, bool> pauseStatesByPlayer;
        private bool lastLocalPauseState;
        private Dictionary<long, float> staminaByPlayer;
        private Dictionary<long, float> maxStaminaByPlayer;

        public override void Entry(IModHelper helper)
        {
            Monitor.Log($"PauseForAnyPlayer loaded - v{ModManifest.Version}", LogLevel.Info);

            config = Helper.ReadConfig<ModConfig>();

            pauseStatesByPlayer = new Dictionary<long, bool>();
            staminaByPlayer = new Dictionary<long, float>();
            maxStaminaByPlayer = new Dictionary<long, float>();

            Helper.Events.GameLoop.UpdateTicked += GameLoop_UpdateTicked;
            Helper.Events.GameLoop.SaveLoaded += GameLoop_SaveLoaded;
            Helper.Events.GameLoop.GameLaunched += GameLoop_GameLaunched;
            Helper.Events.Multiplayer.ModMessageReceived += Multiplayer_ModMessageReceived;
            Helper.Events.Multiplayer.PeerConnected += Multiplayer_PeerConnected;
            Helper.Events.Multiplayer.PeerDisconnected += Multiplayer_PeerDisconnected;
        }

        private void GameLoop_SaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            if (IsEnergyScalingHost())
            {
                pauseStatesByPlayer.Clear();
                staminaByPlayer.Clear();
                maxStaminaByPlayer.Clear();
                foreach (Farmer farmer in Game1.getAllFarmers())
                {
                    pauseStatesByPlayer[farmer.UniqueMultiplayerID] = false;
                    staminaByPlayer[farmer.UniqueMultiplayerID] = farmer.Stamina;
                    maxStaminaByPlayer[farmer.UniqueMultiplayerID] = farmer.MaxStamina;
                }
            }
        }

        private void GameLoop_UpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            HandlePauseLogic();

            HandleEnergyScaling();
        }

        private void HandlePauseLogic()
        {
            if (!Game1.IsMultiplayer)
                return;

            if (Game1.hasLocalClientsOnly)
            {
                HandleLocalSplitscreenPause();
            }
            else
            {
                HandleNetworkMultiplayerPause();
            }
        }

        private void HandleLocalSplitscreenPause()
        {
            bool shouldPause = false;
            foreach (Farmer onlineFarmer in Game1.getOnlineFarmers())
            {
                if (onlineFarmer.requestingTimePause.Value)
                {
                    shouldPause = true;
                    break;
                }
            }

            ControlGameTime(shouldPause);
        }

        private void HandleNetworkMultiplayerPause()
        {
            bool localShouldPause = ShouldLocalPlayerPause();

            if (IsNetworkHost())
            {
                bool shouldPause = localShouldPause || pauseStatesByPlayer.Values.Any(state => state == true);
                ControlGameTime(shouldPause);
            }
            else if (localShouldPause != lastLocalPauseState)
            {
                lastLocalPauseState = localShouldPause;
                Helper.Multiplayer.SendMessage(localShouldPause, "PauseState", new[] { ModManifest.UniqueID });
            }
        }

        private static bool ShouldLocalPlayerPause()
        {
            bool playerInEvent = Game1.eventUp;

            bool playerInMenu = Game1.activeClickableMenu is not null
                and not ConfirmationDialog
                and not NumberSelectionMenu;

            bool playerInMinigame = Game1.currentMinigame is FishingGame or AbigailGame or MineCart;

            return playerInEvent || playerInMenu || playerInMinigame;
        }

        private void ControlGameTime(bool shouldPause)
        {
            if (shouldPause && !isPaused)
            {
                cachedGameTimeInterval = Game1.gameTimeInterval;
                isPaused = true;
            }
            else if (!shouldPause && isPaused)
            {
                Game1.gameTimeInterval = cachedGameTimeInterval;
                isPaused = false;
            }

            if (isPaused)
            {
                Game1.gameTimeInterval = 0;
            }
        }

        private static bool IsEnergyScalingHost() => !Game1.IsMultiplayer || Game1.hasLocalClientsOnly || Context.IsMainPlayer;
        private static bool IsNetworkHost() => Context.IsMainPlayer;
        private static bool IsNetworkMultiplayer() => Game1.IsMultiplayer && !Game1.hasLocalClientsOnly;

        private bool HasPreviousStamina(long playerId) => staminaByPlayer.ContainsKey(playerId);
        private float GetPreviousStamina(long playerId) => staminaByPlayer[playerId];
        private float GetPreviousMaxStamina(long playerId) => maxStaminaByPlayer[playerId];

        private static bool IsStardropEnergyGain(float currentMax, float previousMax) => currentMax > previousMax;

        private static bool IsOvernightRestoration() => Game1.timeOfDay >= 600 && Game1.timeOfDay <= 610;

        private static bool ShouldSkipEnergyGain(float energyGained, float currentMax, float previousMax)
        {
            if (energyGained <= 0) return true;
            if (IsStardropEnergyGain(currentMax, previousMax)) return true;
            if (IsOvernightRestoration()) return true;
            return false;
        }

        private void ProcessEnergyGainForFarmer(Farmer farmer, float energyGained, float previousStamina)
        {
            float scaledEnergyGained = energyGained * config.EnergyScaleFactor;
            float newStamina = previousStamina + scaledEnergyGained;

            farmer.Stamina = newStamina;

            if (IsNetworkMultiplayer() && !farmer.IsLocalPlayer)
            {
                Helper.Multiplayer.SendMessage(newStamina, "EnergyUpdate", new[] { ModManifest.UniqueID }, new[] { farmer.UniqueMultiplayerID });
            }
        }

        private void HandleEnergyScaling()
        {
            if (config.EnergyScaleFactor == 1.0f)
                return;

            if (!IsEnergyScalingHost())
                return;

            foreach (Farmer farmer in Game1.getAllFarmers())
            {
                if (farmer == null) continue;

                long playerId = farmer.UniqueMultiplayerID;
                float currentStamina = farmer.Stamina;
                float currentMaxStamina = farmer.MaxStamina;
                
                float previousStamina = HasPreviousStamina(playerId) 
                    ? GetPreviousStamina(playerId) 
                    : currentStamina;
                float previousMaxStamina = HasPreviousStamina(playerId) 
                    ? GetPreviousMaxStamina(playerId) 
                    : currentMaxStamina;

                float energyGained = currentStamina - previousStamina;
                
                if (!ShouldSkipEnergyGain(energyGained, currentMaxStamina, previousMaxStamina))
                {
                    ProcessEnergyGainForFarmer(farmer, energyGained, previousStamina);
                }
                
                staminaByPlayer[playerId] = farmer.Stamina;
                maxStaminaByPlayer[playerId] = farmer.MaxStamina;
            }
        }



        private void Multiplayer_ModMessageReceived(object sender, ModMessageReceivedEventArgs e)
        {
            if (e.FromModID != ModManifest.UniqueID)
                return;

            try
            {
                if (e.Type == "PauseState")
                {
                    bool pauseState = e.ReadAs<bool>();
                    pauseStatesByPlayer[e.FromPlayerID] = pauseState;
                }
                else if (e.Type == "EnergyUpdate")
                {
                    float newStamina = e.ReadAs<float>();
                    Game1.player.Stamina = newStamina;
                }
            }
            catch
            {
                // Silently ignore malformed network messages
            }
        }

        private void Multiplayer_PeerConnected(object sender, PeerConnectedEventArgs e)
        {
            if (!IsNetworkHost())
                return;

            var farmer = Game1.GetPlayer(e.Peer.PlayerID);
            if (farmer != null)
            {
                pauseStatesByPlayer[e.Peer.PlayerID] = false;
                staminaByPlayer[e.Peer.PlayerID] = farmer.Stamina;
                maxStaminaByPlayer[e.Peer.PlayerID] = farmer.MaxStamina;
            }
        }

        private void Multiplayer_PeerDisconnected(object sender, PeerDisconnectedEventArgs e)
        {
            if (!IsNetworkHost())
                return;

            pauseStatesByPlayer.Remove(e.Peer.PlayerID);
            staminaByPlayer.Remove(e.Peer.PlayerID);
            maxStaminaByPlayer.Remove(e.Peer.PlayerID);
        }

        private void GameLoop_GameLaunched(object sender, GameLaunchedEventArgs e)
        {
            var configMenu = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null)
                return;

            if (!IsEnergyScalingHost())
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
