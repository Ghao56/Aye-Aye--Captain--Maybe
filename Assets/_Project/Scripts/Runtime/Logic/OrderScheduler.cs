using System;
using System.Collections.Generic;
using UnityEngine;

namespace AyeAye
{
    // Bốc đơn theo ĐỒNG HỒ (G1.3, bỏ ngày): 2 đơn tutorial lúc 0 giây, các đơn sau mỗi orderIntervalSeconds
    // rút ngắn dần theo decay, tối thiểu orderIntervalMinSeconds. Bốc theo trọng số + spawnCondition + không lặp.
    public static class OrderScheduler
    {
        // Đơn đầu ván (tutorial): O-01/O-02 ngay lúc 0 giây; đồng hồ bốc đơn bắt đầu đếm.
        public static void SpawnInitial(OrderPool pool, GameState state)
        {
            if (pool == null || state == null) return;
            AddFixedOrders(pool.fixedFirstDay, state, 0f);
            state.NextOrderSpawnAtSeconds = state.Config.orderIntervalSeconds;
            state.OrderSpawnCount = 0;
        }

        // Gọi mỗi Advance: while cho an toàn (dt lớn không bỏ sót mốc).
        public static void SpawnOrders(OrderPool pool, GameState state)
        {
            if (pool == null || state == null) return;
            while (!state.IsFinished && state.ElapsedSeconds >= state.NextOrderSpawnAtSeconds)
            {
                List<OrderDef> picked = PickWeighted(pool.pool, state, 1, allowRepeats: false);
                if (picked.Count == 0)
                {
                    // Pool cạn (hoặc không đơn nào thỏa cờ) → fallback: cho phép lặp, không bao giờ kẹt game.
                    picked = PickWeighted(pool.pool, state, 1, allowRepeats: true);
                }
                if (picked.Count > 0)
                    AddOrder(picked[0], state, state.NextOrderSpawnAtSeconds);

                float interval = state.Config.orderIntervalSeconds
                    * Mathf.Pow(Mathf.Max(0f, state.Config.orderIntervalDecayPerOrder), state.OrderSpawnCount);
                state.NextOrderSpawnAtSeconds += Mathf.Max(state.Config.orderIntervalMinSeconds, interval);
                state.OrderSpawnCount++;
            }
        }

        static void AddFixedOrders(List<OrderDef> fixedOrders, GameState state, float now)
        {
            if (fixedOrders == null) return;
            foreach (OrderDef def in fixedOrders) AddOrder(def, state, now);
        }

        static void AddOrder(OrderDef def, GameState state, float now)
        {
            if (def == null) return;
            if (state.UsedOrderIds.Contains(def.orderId))
            {
                // Đơn lặp (fallback): thay trạng thái cũ bằng phiên bản mới ở Inbox.
                state.Orders.RemoveAll(o => o.Id == def.orderId);
            }
            state.UsedOrderIds.Add(def.orderId);
            state.Orders.Add(OrderState.From(def, now));
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
