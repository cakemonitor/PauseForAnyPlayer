using StardewModdingAPI;
using StardewValley;
using PauseForAnyPlayer.Models;

namespace PauseForAnyPlayer.Services
{
    public class EnergyService
    {
        private readonly ModConfig config;
        private readonly IModHelper helper;
        private readonly IManifest manifest;
        private readonly PlayerStateManager playerStateManager;

        public EnergyService(ModConfig config, IModHelper helper, IManifest manifest, PlayerStateManager playerStateManager)
        {
            this.config = config;
            this.helper = helper;
            this.manifest = manifest;
            this.playerStateManager = playerStateManager;
        }

        public void HandleEnergyScaling()
        {
            if (config.EnergyScaleFactor == 1.0f)
                return;

            if (!IsResponsibleForEnergyScaling())
                return;

            foreach (Farmer farmer in Game1.getAllFarmers())
            {
                if (farmer == null) continue;

                long playerId = farmer.UniqueMultiplayerID;
                float currentEnergy = farmer.Stamina;
                float currentMaxEnergy = farmer.MaxStamina;
                
                float previousEnergy = GetPreviousEnergy(playerId, currentEnergy);
                float previousMaxEnergy = GetPreviousMaxEnergy(playerId, currentMaxEnergy);

                float energyGained = currentEnergy - previousEnergy;
                
                if (ShouldScaleEnergyGain(energyGained, currentMaxEnergy, previousMaxEnergy))
                {
                    ScaleEnergyGainForFarmer(farmer, energyGained, previousEnergy);
                }
                
                playerStateManager.UpdateEnergy(playerId, farmer.Stamina, farmer.MaxStamina);
            }
        }

        public static void UpdatePlayerEnergy(float newEnergy)
        {
            Game1.player.Stamina = newEnergy;
        }

        public static bool IsResponsibleForEnergyScaling() => !Game1.IsMultiplayer || Game1.hasLocalClientsOnly || Context.IsMainPlayer;

        private float GetPreviousEnergy(long playerId, float fallback)
        {
            PlayerState state = playerStateManager.GetPlayerState(playerId);
            return state?.Energy ?? fallback;
        }

        private float GetPreviousMaxEnergy(long playerId, float fallback)
        {
            PlayerState state = playerStateManager.GetPlayerState(playerId);
            return state?.MaxEnergy ?? fallback;
        }

        private static bool IsStardropEnergyGain(float currentMax, float previousMax) => currentMax > previousMax;

        private static bool IsOvernightRestoration() => Game1.timeOfDay >= 600 && Game1.timeOfDay <= 610;

        private static bool ShouldScaleEnergyGain(float energyGained, float currentMax, float previousMax)
        {
            if (energyGained <= 0) return false;
            if (IsStardropEnergyGain(currentMax, previousMax)) return false;
            if (IsOvernightRestoration()) return false;
            return true;
        }

        private void ScaleEnergyGainForFarmer(Farmer farmer, float energyGained, float previousEnergy)
        {
            float scaledEnergyGained = energyGained * config.EnergyScaleFactor;
            float newEnergy = previousEnergy + scaledEnergyGained;

            farmer.Stamina = newEnergy;

            if (IsNetworkMultiplayer() && !farmer.IsLocalPlayer)
            {
                helper.Multiplayer.SendMessage(newEnergy, "EnergyUpdate", new[] { manifest.UniqueID }, new[] { farmer.UniqueMultiplayerID });
            }
        }

        private static bool IsNetworkMultiplayer() => Game1.IsMultiplayer && !Game1.hasLocalClientsOnly;
    }
}