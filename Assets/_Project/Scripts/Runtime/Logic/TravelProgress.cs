namespace AyeAye
{
    // Nơi duy nhất tính quãng đường theo kết quả quyết định.
    public static class TravelProgress
    {
        public static float GetDistance(DecisionOutcome outcome, GameConfig config)
        {
            if (config == null) return 0f;
            switch (outcome)
            {
                case DecisionOutcome.Smart: return config.smartDistance;
                case DecisionOutcome.SlipUp: return config.slipUpDistance;
                default: return config.normalDistance;
            }
        }
    }
}
