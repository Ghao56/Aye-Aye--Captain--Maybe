namespace AyeAye
{
    // FR-0602/FR-0305: điểm từng lần giao. Đúng hạn = chưa từng chạm hạn khi giao (IsLate).
    // Reputation đã bỏ (chỉnh 4 của chủ dự án) — chấm điểm chỉ còn doanh thu.
    public static class ScoreCalculator
    {
        public static int ScoreDelivery(OrderState order, GameConfig config)
        {
            int cargoFactor = order.Cargo == CargoCondition.Ruined ? 0 : 1;
            if (!order.IsLate)
                return order.Revenue * cargoFactor;
            // Trễ: doanh thu giảm nửa; hàng hỏng vẫn về 0.
            return (int)(order.Revenue * config.lateRevenueFactor) * cargoFactor;
        }
    }
}
