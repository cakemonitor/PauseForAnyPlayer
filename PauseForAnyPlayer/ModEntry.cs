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
        private bool isPaused;
        private int cachedGameTimeInterval;
        private ModConfig config;
        private Dictionary<long, float> previousStaminaByPlayer;
        private Dictionary<long, bool> playerPauseStates;
        private bool lastLocalPauseState;
        private Dictionary<long, float> previousMaxStaminaByPlayer;

        public override void Entry(IModHelper helper)
        {
            config = Helper.ReadConfig<ModConfig>();

            playerPauseStates = new Dictionary<long, bool>();
            previousStaminaByPlayer = new Dictionary<long, float>();
            previousMaxStaminaByPlayer = new Dictionary<long, float>();

            Helper.Events.GameLoop.UpdateTicked += GameLoop_UpdateTicked;
            Helper.Events.GameLoop.SaveLoaded += GameLoop_SaveLoaded;
            Helper.Events.GameLoop.GameLaunched += GameLoop_GameLaunched;
            Helper.Events.Multiplayer.ModMessageReceived += Multiplayer_ModMessageReceived;
            Helper.Events.Multiplayer.PeerConnected += Multiplayer_PeerConnected;
            Helper.Events.Multiplayer.PeerDisconnected += Multiplayer_PeerDisconnected;
        }

        private void GameLoop_SaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            if (!Game1.IsMultiplayer || Game1.hasLocalClientsOnly || Context.IsMainPlayer)
            {
                playerPauseStates.Clear();
                previousStaminaByPlayer.Clear();
                previousMaxStaminaByPlayer.Clear();
                foreach (Farmer farmer in Game1.getAllFarmers())
                {
                    playerPauseStates[farmer.UniqueMultiplayerID] = false;
                    previousStaminaByPlayer[farmer.UniqueMultiplayerID] = farmer.Stamina;
                    previousMaxStaminaByPlayer[farmer.UniqueMultiplayerID] = farmer.MaxStamina;
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
                    if (!isPaused)
                    {
                        Monitor.Log($"Pause requested by {onlineFarmer.Name}", LogLevel.Debug);
                    }
                    break;
                }
            }

            ControlGameTime(shouldPause);
        }

        private void HandleNetworkMultiplayerPause()
        {
            bool localShouldPause = ShouldLocalPlayerPause();

            if (Context.IsMainPlayer)
            {
                bool shouldPause = localShouldPause || playerPauseStates.Values.Any(state => state == true);
                if (!isPaused && localShouldPause)
                {
                    Monitor.Log($"Pause requested by host", LogLevel.Debug);
                }
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

        private void HandleEnergyScaling()
        {
            if (config.EnergyScaleFactor == 1.0f)
                return;

            if (Game1.IsMultiplayer && !Game1.hasLocalClientsOnly && !Context.IsMainPlayer)
                return;

            foreach (Farmer farmer in Game1.getAllFarmers())
            {
                long playerId = farmer.UniqueMultiplayerID;
                float currentStamina = farmer.Stamina;
                float currentMaxStamina = farmer.MaxStamina;
                
                float previousStamina = previousStaminaByPlayer.ContainsKey(playerId)
                    ? previousStaminaByPlayer[playerId]
                    : currentStamina;

                float previousMaxStamina = previousMaxStaminaByPlayer.ContainsKey(playerId)
                    ? previousMaxStaminaByPlayer[playerId]
                    : currentMaxStamina;

                float energyGained = currentStamina - previousStamina;
                if (energyGained > 0)
                {
                    if (currentMaxStamina > previousMaxStamina)
                    {
                        Monitor.Log($"Max stamina increased for {farmer.Name} ({previousMaxStamina} -> {currentMaxStamina}) - skipping scaling (Stardrop?)", LogLevel.Debug);
                    }
                    else if (Game1.timeOfDay >= 600 && Game1.timeOfDay <= 610)
                    {
                        Monitor.Log($"Energy gain at {Game1.timeOfDay} for {farmer.Name}: {energyGained} - skipping scaling (overnight restoration)", LogLevel.Debug);
                    }
                    else
                    {
                        float scaledEnergyGained = energyGained * config.EnergyScaleFactor;
                        float newStamina = previousStamina + scaledEnergyGained;

                        Monitor.Log($"Scaling energy gain for {farmer.Name}: {energyGained} -> {scaledEnergyGained}, setting stamina to {newStamina}", LogLevel.Debug);
                        farmer.Stamina = newStamina;

                        if (Game1.IsMultiplayer && !Game1.hasLocalClientsOnly && !farmer.IsLocalPlayer)
                        {
                            Helper.Multiplayer.SendMessage(newStamina, "EnergyUpdate", new[] { ModManifest.UniqueID }, new[] { playerId });
                        }
                    }
                }
                
                previousStaminaByPlayer[playerId] = farmer.Stamina;
                previousMaxStaminaByPlayer[playerId] = farmer.MaxStamina;
            }
        }



        private void Multiplayer_ModMessageReceived(object sender, ModMessageReceivedEventArgs e)
        {
            if (e.FromModID != ModManifest.UniqueID)
                return;

            if (e.Type == "PauseState")
            {
                bool pauseState = e.ReadAs<bool>();
                playerPauseStates[e.FromPlayerID] = pauseState;

                var farmer = Game1.GetPlayer(e.FromPlayerID);
                if (farmer != null)
                {
                    Monitor.Log($"Received update from {farmer.Name}: pause state = {pauseState}", LogLevel.Debug);
                }
            }
            else if (e.Type == "EnergyUpdate")
            {
                float newStamina = e.ReadAs<float>();
                Game1.player.Stamina = newStamina;

                Monitor.Log($"Received energy update: setting stamina to {newStamina}", LogLevel.Debug);
            }

        }

        private void Multiplayer_PeerConnected(object sender, PeerConnectedEventArgs e)
        {
            if (Context.IsMainPlayer)
            {
                var farmer = Game1.GetPlayer(e.Peer.PlayerID);
                if (farmer != null)
                {
                    playerPauseStates[e.Peer.PlayerID] = false;
                    previousStaminaByPlayer[e.Peer.PlayerID] = farmer.Stamina;
                    previousMaxStaminaByPlayer[e.Peer.PlayerID] = farmer.MaxStamina;
                }
            }
        }

        private void Multiplayer_PeerDisconnected(object sender, PeerDisconnectedEventArgs e)
        {
            if (Context.IsMainPlayer)
            {
                playerPauseStates.Remove(e.Peer.PlayerID);
                previousStaminaByPlayer.Remove(e.Peer.PlayerID);
                previousMaxStaminaByPlayer.Remove(e.Peer.PlayerID);
            }
        }

        private void GameLoop_GameLaunched(object sender, GameLaunchedEventArgs e)
        {
            var configMenu = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null)
                return;

            if (Game1.IsMultiplayer && !Game1.hasLocalClientsOnly && !Context.IsMainPlayer)
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
