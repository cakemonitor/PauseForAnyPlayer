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

        public override void Entry(IModHelper helper)
        {
            // Load configuration
            this.config = this.Helper.ReadConfig<ModConfig>();

            Helper.Events.GameLoop.UpdateTicked += GameLoop_UpdateTicked;
        }

        private void GameLoop_UpdateTicked(object sender, UpdateTickedEventArgs e)
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
    }
}
