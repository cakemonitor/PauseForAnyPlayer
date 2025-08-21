using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using PauseForAnyPlayer.Services;

namespace PauseForAnyPlayer.Services
{
    public class NetworkMessageHandler
    {
        private readonly IManifest manifest;
        private readonly PlayerStateManager playerStateManager;

        public NetworkMessageHandler(IManifest manifest, PlayerStateManager playerStateManager)
        {
            this.manifest = manifest;
            this.playerStateManager = playerStateManager;
        }

        public void HandleModMessageReceived(object sender, ModMessageReceivedEventArgs e)
        {
            if (e.FromModID != manifest.UniqueID)
                return;

            try
            {
                if (e.Type == "PauseState")
                {
                    bool pauseState = e.ReadAs<bool>();
                    playerStateManager.UpdatePauseState(e.FromPlayerID, pauseState);
                }
                else if (e.Type == "EnergyUpdate")
                {
                    float newEnergy = e.ReadAs<float>();
                    EnergyService.UpdatePlayerEnergy(newEnergy);
                }
            }
            catch
            {
                // Silently ignore malformed network messages
            }
        }

        public void HandlePeerConnected(object sender, PeerConnectedEventArgs e)
        {
            if (!IsNetworkHost())
                return;

            var farmer = Game1.GetPlayer(e.Peer.PlayerID);
            if (farmer != null)
            {
                playerStateManager.AddPlayer(e.Peer.PlayerID, farmer.Stamina, farmer.MaxStamina);
            }
        }

        public void HandlePeerDisconnected(object sender, PeerDisconnectedEventArgs e)
        {
            if (!IsNetworkHost())
                return;

            playerStateManager.RemovePlayer(e.Peer.PlayerID);
        }

        private static bool IsNetworkHost() => Context.IsMainPlayer;
    }
}