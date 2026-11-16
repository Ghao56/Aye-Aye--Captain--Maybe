namespace AyeAye
{

    public static class ScoreCalculator
    {

        public static int ScoreDelivery(OrderState order, GameConfig config)
        {
            int cargoFactor = order.Cargo == CargoCondition.Ruined ? 0 : 1;
            if (!order.IsLate)
                return order.Revenue * cargoFactor;
            return (int)(order.Revenue * config.lateRevenueFactor) * cargoFactor;
        }
    }
}

