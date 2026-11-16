using UnityEngine;

namespace AyeAye
{
    // Bốc đơn theo ĐỒNG HỒ (G1.3 + G7): ván BẮT ĐẦU KHÔNG CÓ ĐƠN — đơn đầu tiên xuất hiện sau
    // orderFirstDelaySeconds (thời gian làm quen), các đơn sau mỗi orderIntervalSeconds rút ngắn
    // theo decay, sàn orderIntervalMinSeconds. Đơn sinh NGẪU NHIÊN qua OrderGenerator (state.Rng),
    // Id "O-G{n}" duy nhất — KHÔNG còn bốc từ OrderPool (pool chỉ còn là dữ liệu mẫu tuỳ chọn).
    public static class OrderScheduler
    {
        // Ván mới: KHÔNG sinh đơn lúc 0 giây — chỉ đặt mốc bốc đầu tiên sau thời gian làm quen.
        public static void SpawnInitial(OrderPool pool, GameState state)
        {
            if (state == null) return;
            state.NextOrderSpawnAtSeconds = Mathf.Max(0f, state.Config.orderFirstDelaySeconds);
            state.OrderSpawnCount = 0;
        }

        // Gọi mỗi Advance: while cho an toàn (dt lớn không bỏ sót mốc).
        public static void SpawnOrders(SimWorld world, GameState state)
        {
            if (world == null || state == null) return;
            while (!state.IsFinished && state.ElapsedSeconds >= state.NextOrderSpawnAtSeconds)
            {
                // Chống tràn hộp thư: đủ maxInboxOrders thì bỏ lượt sinh, vẫn dời mốc kế tiếp.
                int inbox = 0;
                foreach (var o in state.Orders)
                    if (o.Status == OrderStatus.InInbox) inbox++;
                if (inbox < Mathf.Max(1, state.Config.maxInboxOrders))
                {
                    var order = OrderGenerator.Generate(state, world.Routes, state.NextOrderSpawnAtSeconds);
                    if (order != null)
                    {
                        state.UsedOrderIds.Add(order.Id);
                        state.Orders.Add(order);
                        state.OrderSpawnCount++; // decay + id đếm theo đơn ĐÃ SINH thật
                    }
                }

                // Decay theo số đơn đã sinh TRƯỚC lượt này (đơn đầu giữ nguyên khoảng 40s).
                float interval = state.Config.orderIntervalSeconds
                    * Mathf.Pow(Mathf.Max(0f, state.Config.orderIntervalDecayPerOrder), state.OrderSpawnCount);
                state.NextOrderSpawnAtSeconds += Mathf.Max(state.Config.orderIntervalMinSeconds, interval);
            }
        }
    }
}
