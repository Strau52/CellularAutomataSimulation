namespace CAS
{
    public static class SimulationConstants
    {
        // Animal simulation constants.
        public const int INITIAL_ANIMAL_ENERGY = 10;
        public const int ENERGY_GAIN_FROM_PLANT = 5;
        public const int ENERGY_GAIN_FROM_ATTACK = 10;
        public const int ENERGY_LOSS_PER_MOVE = 1;
        public const int REPRODUCTION_THRESHOLD = 15;
        public const int REPRODUCTION_COST = 5;
        public const int HUNGER_THRESHOLD = 10;

        // Environmental effects constants.
        public const double RAIN_PLANT_BIRTH_CHANCE = 0.4;
        public const double DROUGHT_PLANT_DEATH_CHANCE = 0.5;
        public const double RAIN_PROB = 0.1;
        public const double DROUGHT_PROB = 0.05;
        public const int MIN_WEATHER_DURATION = 1;
        public const int MAX_WEATHER_DURATION = 4;
    }

    // Carnivores simulation constants.
    public static class CarnivoreConstants
    {
        public const int INITIAL_ENERGY = 20;
        public const int ENERGY_GAIN_FROM_ATTACK = 15;
        public const int ENERGY_LOSS_PER_MOVE = 1;
        public const int REPRODUCTION_THRESHOLD = 15;
        public const int REPRODUCTION_COST = 6;
        public const int HUNGER_THRESHOLD = 10;
    }
}
