namespace AyeAye
{

    public static class EndingEvaluator
    {

        public static EndingId FromOutcome(GameState state)
        {
            if (state.DeliveredCount >= state.Config.winDeliveries &&
                state.LateCount < state.Config.maxLateDeliveries)
            {
                switch (state.LateCount)
                {
                    case 0: return EndingId.E1_Empire;
                    case 1: return EndingId.E2_Renowned;
                    default: return EndingId.E3_Survived;
                }
            }
            if (state.DeliveredCount < state.Config.earlyLoseDeliveries) return EndingId.E5_Bankrupt;
            return EndingId.E4_Debt;
        }

        public static void FinishRun(GameState state)
        {
            if (state.IsFinished) return;
            state.FinalEnding = FromOutcome(state);
            state.IsFinished = true;
        }
    }
}

