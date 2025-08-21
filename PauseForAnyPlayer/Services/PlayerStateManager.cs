using System.Collections.Generic;
using PauseForAnyPlayer.Models;

namespace PauseForAnyPlayer.Services
{
    public class PlayerStateManager
    {
        private readonly Dictionary<long, PlayerState> playerStates;

        public PlayerStateManager()
        {
            playerStates = new Dictionary<long, PlayerState>();
        }

        public void AddPlayer(long playerId, float energy = 0f, float maxEnergy = 0f)
        {
            playerStates[playerId] = new PlayerState(playerId, energy, maxEnergy);
        }

        public void RemovePlayer(long playerId)
        {
            playerStates.Remove(playerId);
        }

        public void ClearAllPlayers()
        {
            playerStates.Clear();
        }

        public PlayerState GetPlayerState(long playerId)
        {
            return playerStates.TryGetValue(playerId, out PlayerState state) ? state : null;
        }

        public bool HasPlayer(long playerId)
        {
            return playerStates.ContainsKey(playerId);
        }

        public void UpdateEnergy(long playerId, float energy, float maxEnergy)
        {
            if (playerStates.TryGetValue(playerId, out PlayerState state))
            {
                state.Energy = energy;
                state.MaxEnergy = maxEnergy;
            }
            else
            {
                AddPlayer(playerId, energy, maxEnergy);
            }
        }

        public void UpdatePauseState(long playerId, bool isPaused)
        {
            if (playerStates.TryGetValue(playerId, out PlayerState state))
            {
                state.IsPaused = isPaused;
            }
            else
            {
                AddPlayer(playerId);
                playerStates[playerId].IsPaused = isPaused;
            }
        }

        public bool AnyPlayerPaused()
        {
            foreach (var state in playerStates.Values)
            {
                if (state.IsPaused)
                    return true;
            }
            return false;
        }
    }
}