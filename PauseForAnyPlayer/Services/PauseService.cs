using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Minigames;

namespace PauseForAnyPlayer.Services
{
    public class PauseService
    {
        private readonly IModHelper helper;
        private readonly IManifest manifest;
        private readonly PlayerStateManager playerStateManager;
        private bool isPaused;
        private int cachedGameTimeInterval;
        private bool lastNetworkClientPauseState;

        public PauseService(IModHelper helper, IManifest manifest, PlayerStateManager playerStateManager)
        {
            this.helper = helper;
            this.manifest = manifest;
            this.playerStateManager = playerStateManager;
        }

        public void HandlePauseLogic()
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
            bool networkClientShouldPause = ShouldNetworkClientPause();

            if (IsNetworkHost())
            {
                bool shouldPause = networkClientShouldPause || playerStateManager.AnyPlayerPaused();
                ControlGameTime(shouldPause);
            }
            else if (networkClientShouldPause != lastNetworkClientPauseState)
            {
                lastNetworkClientPauseState = networkClientShouldPause;
                helper.Multiplayer.SendMessage(networkClientShouldPause, "PauseState", new[] { manifest.UniqueID });
            }
        }

        private static bool ShouldNetworkClientPause()
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

        private static bool IsNetworkHost() => Context.IsMainPlayer;
    }
}