using System;
using System.Collections.Generic;

namespace AyeAye
{
    // Bốc đơn mới cho pha Planning (FR-0301/0306). Thuần C#: mọi random qua state.Rng.
    public static class OrderScheduler
    {
        public static void ScheduleNewOrders(OrderPool pool, GameState state)
        {
            if (pool == null || state == null) return;

            if (state.CurrentDay <= 1)
            {
                AddFixedOrders(pool.fixedFirstDay, state);
                return;
            }

            GameConfig config = state.Config;
            int count = PickOrderCount(config, state.Rng);
            List<OrderDef> picked = PickWeighted(pool.pool, state, count, allowRepeats: false);
            if (picked.Count < count)
            {
                // Pool cạn (hoặc không đơn nào thỏa cờ) → fallback: cho phép lặp, không bao giờ kẹt game.
                List<OrderDef> fallback = PickWeighted(pool.pool, state, count - picked.Count, allowRepeats: true);
                picked.AddRange(fallback);
            }

            foreach (OrderDef def in picked) AddOrder(def, state);
        }

        static void AddFixedOrders(List<OrderDef> fixedOrders, GameState state)
        {
            if (fixedOrders == null) return;
            foreach (OrderDef def in fixedOrders) AddOrder(def, state);
        }

        static void AddOrder(OrderDef def, GameState state)
        {
            if (def == null) return;
            if (state.UsedOrderIds.Contains(def.orderId))
            {
                // Đơn lặp (fallback): thay trạng thái cũ bằng phiên bản mới ở Inbox.
                state.Orders.RemoveAll(o => o.Id == def.orderId);
            }
            state.UsedOrderIds.Add(def.orderId);
            state.Orders.Add(OrderState.From(def));
        }

        // newOrdersPerDay bốc ngẫu nhiên trong khoảng [min, max] — qua Rng để tái lập được.
        static int PickOrderCount(GameConfig config, Random rng)
        {
            int min = Math.Max(0, config.newOrdersPerDayMin);
            int max = Math.Max(min, config.newOrdersPerDayMax);
            return rng.Next(min, max + 1);
        }

        // Thuật toán bốc theo trọng số (khuôn EventScheduler bản 2): chọn N đơn đủ điều kiện, không trùng lượt.
        static List<OrderDef> PickWeighted(List<OrderDef> pool, GameState state, int count, bool allowRepeats)
        {
            List<OrderDef> picked = new List<OrderDef>();
            if (pool == null || count <= 0) return picked;

            for (int n = 0; n < count; n++)
            {
                int total = 0;
                List<OrderDef> candidates = new List<OrderDef>();
                foreach (OrderDef def in pool)
                {
                    if (def == null) continue;
                    if (!allowRepeats && state.UsedOrderIds.Contains(def.orderId)) continue;
                    if (!FlagEvaluator.IsMet(def.spawnCondition, state)) continue;
                    candidates.Add(def);
                    total += Math.Max(1, def.weight);
                }
                if (candidates.Count == 0) break;

                int roll = state.Rng.Next(total);
                OrderDef chosen = candidates[candidates.Count - 1];
                for (int i = 0; i < candidates.Count; i++)
                {
                    roll -= Math.Max(1, candidates[i].weight);
                    if (roll < 0) { chosen = candidates[i]; break; }
                }
                picked.Add(chosen);
                // Đánh dấu đã dùng ngay để không bốc trùng trong cùng lượt.
                if (!allowRepeats) state.UsedOrderIds.Add(chosen.orderId);
            }
            return picked;
        }
    }
}
