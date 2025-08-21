namespace PauseForAnyPlayer.Models
{
    public class PlayerState
    {
        public long PlayerId { get; set; }
        public bool IsPaused { get; set; }
        public float Energy { get; set; }
        public float MaxEnergy { get; set; }

        public PlayerState(long playerId, float energy, float maxEnergy)
        {
            PlayerId = playerId;
            IsPaused = false;
            Energy = energy;
            MaxEnergy = maxEnergy;
        }
    }
}