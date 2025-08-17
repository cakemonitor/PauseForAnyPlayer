namespace PauseForAnyPlayer
{
    public class ModConfig
    {
        /// <summary>
        /// Factor to scale energy gains (1.0 = normal, 0.5 = half energy, etc.).
        /// Set to 1.0 to disable energy scaling.
        /// </summary>
        public float EnergyScaleFactor { get; set; } = 0.75f;
    }
}