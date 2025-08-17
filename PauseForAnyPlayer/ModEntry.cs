using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace PauseForAnyPlayer
{
    public class ModEntry : Mod
    {
        private bool isPaused;
        private int cachedGameTimeInterval;
        private ModConfig config;
        private Dictionary<long, float> previousStaminaByPlayer;
        private Dictionary<long, bool> playerPauseStates;
        private bool lastLocalPauseState;

        public override void Entry(IModHelper helper)
        {
            // Load configuration
            this.config = this.Helper.ReadConfig<ModConfig>();
            this.previousStaminaByPlayer = new Dictionary<long, float>();
            this.playerPauseStates = new Dictionary<long, bool>();

            Helper.Events.GameLoop.UpdateTicked += GameLoop_UpdateTicked;
            Helper.Events.GameLoop.SaveLoaded += GameLoop_SaveLoaded;
            Helper.Events.GameLoop.GameLaunched += GameLoop_GameLaunched;
            Helper.Events.Multiplayer.ModMessageReceived += Multiplayer_ModMessageReceived;
            Helper.Events.Multiplayer.PeerConnected += Multiplayer_PeerConnected;
            Helper.Events.Multiplayer.PeerDisconnected += Multiplayer_PeerDisconnected;
        }

        private void GameLoop_SaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            // Initialize tracking for all players when save is loaded
            this.previousStaminaByPlayer.Clear();
            this.playerPauseStates.Clear();
            foreach (Farmer farmer in Game1.getAllFarmers())
            {
                this.previousStaminaByPlayer[farmer.UniqueMultiplayerID] = farmer.Stamina;
                this.playerPauseStates[farmer.UniqueMultiplayerID] = false;
            }
        }

        private void GameLoop_UpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            // Handle pause logic
            HandlePauseLogic();

            // Handle energy scaling
            HandleEnergyScaling();
        }

        private void HandlePauseLogic()
        {
            if (!Game1.IsMultiplayer)
                return;

            if (Game1.hasLocalClientsOnly)
            {
                // Local splitscreen multiplayer
                HandleLocalSplitscreenPause();
            }
            else
            {
                // Network multiplayer
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
            // Send local player's pause state to host (only when changed)
            bool localShouldPause = ShouldLocalPlayerPause();
            if (localShouldPause != lastLocalPauseState)
            {
                this.Helper.Multiplayer.SendMessage(localShouldPause, "PauseState", new[] { this.ModManifest.UniqueID });
                lastLocalPauseState = localShouldPause;
            }

            // Only host controls time
            if (Context.IsMainPlayer)
            {
                bool shouldPause = this.playerPauseStates.Values.Any(state => state == true);
                ControlGameTime(shouldPause);
            }
        }

        private bool ShouldLocalPlayerPause()
        {
            return Game1.player.requestingTimePause.Value;
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

        private void HandleEnergyScaling()
        {
            // Only host controls energy scaling in network multiplayer
            if (Game1.IsMultiplayer && !Game1.hasLocalClientsOnly && !Context.IsMainPlayer)
                return;

            if (config.EnergyScaleFactor == 1.0f)
                return;

            foreach (Farmer farmer in Game1.getAllFarmers())
            {
                long playerId = farmer.UniqueMultiplayerID;
                float currentStamina = farmer.Stamina;
                
                if (!previousStaminaByPlayer.ContainsKey(playerId))
                {
                    previousStaminaByPlayer[playerId] = currentStamina;
                    continue;
                }

                float previousStamina = previousStaminaByPlayer[playerId];
                if (currentStamina > previousStamina)
                {
                    float energyGained = currentStamina - previousStamina;
                    float scaledEnergy = energyGained * config.EnergyScaleFactor;
                    farmer.Stamina = previousStamina + scaledEnergy;
                }
                
                previousStaminaByPlayer[playerId] = farmer.Stamina;
            }
        }

        private void Multiplayer_ModMessageReceived(object sender, ModMessageReceivedEventArgs e)
        {
            if (e.FromModID == this.ModManifest.UniqueID && e.Type == "PauseState")
            {
                bool pauseState = e.ReadAs<bool>();
                this.playerPauseStates[e.FromPlayerID] = pauseState;
            }
        }

        private void Multiplayer_PeerConnected(object sender, PeerConnectedEventArgs e)
        {
            this.playerPauseStates[e.Peer.PlayerID] = false;
        }

        private void Multiplayer_PeerDisconnected(object sender, PeerDisconnectedEventArgs e)
        {
            this.playerPauseStates.Remove(e.Peer.PlayerID);
        }

        private void GameLoop_GameLaunched(object sender, GameLaunchedEventArgs e)
        {
            // Get GMCM API
            var configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null)
                return;

            // Only show config menu to host in network multiplayer (or always in single-player/splitscreen)
            if (Game1.IsMultiplayer && !Game1.hasLocalClientsOnly && !Context.IsMainPlayer)
                return;

            // Register mod
            configMenu.Register(
                mod: this.ModManifest,
                reset: () => this.config = new ModConfig(),
                save: () => this.Helper.WriteConfig(this.config)
            );

            // Add energy scale factor slider
            configMenu.AddNumberOption(
                mod: this.ModManifest,
                name: () => "Energy Scale Factor",
                tooltip: () => "Multiplier for energy gains (1.0 = normal, 0.5 = half energy, etc.)",
                getValue: () => this.config.EnergyScaleFactor,
                setValue: value => this.config.EnergyScaleFactor = value,
                min: 0.1f,
                max: 2.0f,
                interval: 0.05f,
                formatValue: value => $"{value:F2}x"
            );
        }
    }
}
