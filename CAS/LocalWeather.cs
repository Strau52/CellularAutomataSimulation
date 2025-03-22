namespace CAS
{
    public struct LocalWeather
    {
        public WeatherCondition Condition;
        public int Duration;

        public LocalWeather(WeatherCondition condition, int duration)
        {
            Condition = condition;
            Duration = duration;
        }
    }
}
