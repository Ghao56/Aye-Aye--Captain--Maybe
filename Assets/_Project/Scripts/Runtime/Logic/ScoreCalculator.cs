using System.Collections.Generic;

namespace AyeAye
{
    // FR-0602/FR-0305: điểm từng lần giao + đếm đơn chịu phạt cuối ván.
    public static class ScoreCalculator
    {
        public static (int reputationDelta, int revenueDelta) ScoreDelivery(OrderState order, GameConfig config)
        {
            bool onTime = order.DeliveredDay <= order.DeadlineDay;
            int cargoFactor = order.Cargo == CargoCondition.Ruined ? 0 : 1;

            if (onTime)
                return (config.onTimeReputation + config.CargoBonus(order.Cargo),
                        order.Revenue * cargoFactor);

            // Trễ: phạt phẳng, doanh thu giảm nửa; hàng hỏng vẫn về 0.
            return (config.lateReputationPenalty,
                    (int)(order.Revenue * config.lateRevenueFactor) * cargoFactor);
        }

        // FR-0305: đơn đã nhận/đang chở mà chưa giao đến cuối ván.
        public static int UndeliveredCount(GameState state)
        {
            int count = 0;
            foreach (var o in state.Orders)
                if (o.Status == OrderStatus.Accepted || o.Status == OrderStatus.Assigned)
                    count++;
            return count;
        }
    }
}
