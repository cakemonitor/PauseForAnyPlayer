using System.Collections.Generic;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace PauseForAnySplitscreenPlayer
{
    public class ModEntry : Mod
    {
        private bool isPaused;
        private int cachedGameTimeInterval;
        private ModConfig config;
        private Dictionary<long, float> previousStaminaByPlayer;

        public override void Entry(IModHelper helper)
        {
            // Load configuration
            this.config = this.Helper.ReadConfig<ModConfig>();
            this.previousStaminaByPlayer = new Dictionary<long, float>();

            Helper.Events.GameLoop.UpdateTicked += GameLoop_UpdateTicked;
            Helper.Events.GameLoop.SaveLoaded += GameLoop_SaveLoaded;
        }

        private void GameLoop_SaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            // Initialize stamina tracking for all players when save is loaded
            this.previousStaminaByPlayer.Clear();
            foreach (Farmer farmer in Game1.getAllFarmers())
            {
                this.previousStaminaByPlayer[farmer.UniqueMultiplayerID] = farmer.Stamina;
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
            if (!Game1.IsMultiplayer || !Game1.hasLocalClientsOnly)
            {
                return;
            }

            bool shouldPause = false;
            foreach (Farmer onlineFarmer in Game1.getOnlineFarmers())
            {
                if (onlineFarmer.requestingTimePause.Value)
                {
                    shouldPause = true;
                    break;
                }
            }

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
    }
}
