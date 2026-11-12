namespace AyeAye
{
    // FR-0701/FR-0105: kết thúc theo danh tiếng, áp phạt đơn chưa giao ở cuối ván.
    public static class EndingEvaluator
    {
        public static EndingId FromReputation(int reputation, GameConfig config)
        {
            if (reputation >= config.endingE1Threshold) return EndingId.E1_Empire;
            if (reputation >= config.endingE2Threshold) return EndingId.E2_Renowned;
            if (reputation >= config.endingE3Threshold) return EndingId.E3_Survived;
            if (reputation >= config.endingE4Threshold) return EndingId.E4_Debt;
            return EndingId.E5_Bankrupt;
        }

        public static void FinishRun(GameState state)
        {
            int penalty = 0;
            int undelivered = ScoreCalculator.UndeliveredCount(state);
            if (undelivered > 0)
                penalty = undelivered * state.Config.undeliveredReputationPenalty;
            state.Reputation -= penalty;
            state.FinalEnding = FromReputation(state.Reputation, state.Config);
            state.IsFinished = true;
        }
    }
}
