namespace FlightCore
{
    /// <summary>Approximate representative vehicles. See docs/vehicle-specs.md for source and estimates.</summary>
    public static class RealVehiclePresets
    {
        // Starship reference is the 124 m configuration on SpaceX's published vehicle page.
        public static StageDefinition[] BuildStarshipStages() => new[]
        {
            new StageDefinition(250000, 3650000, 80.8e6, 88e6, 320, 350, .5, 63.62),
            new StageDefinition(150000, 1600000, 11e6, 15.83e6, 320, 360, .5, 63.62)
        };

        // The SRBs are not included here: they must burn in parallel with the core.
        public static StageDefinition[] BuildSlsStages() => new[]
        {
            new StageDefinition(100000, 1000000, 7.9e6, 9.3e6, 366, 452, .5, 55.42),
            new StageDefinition(30000, 27000, 0, 110000, 400, 450, .5, 20.43)
        };

        // Pair of boosters aggregated. Constant vacuum-maximum thrust is a crude approximation;
        // Isp is inferred solely to match nominal propellant mass and burn duration.
        public static SolidBoosterDefinition BuildSlsBoosterParameters() =>
            new SolidBoosterDefinition(198600, 1253400, 32.028e6,
                16.014e6 * 126 / (626700 * 9.80665));
    }
}
