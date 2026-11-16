using UnityEngine;
namespace AyeAye
{

    public static class OrderScheduler
    {

        public static void SpawnInitial(OrderPool pool, GameState state)
        {
            if (state == null) return;
            state.NextOrderSpawnAtSeconds = Mathf.Max(0f, state.Config.orderFirstDelaySeconds);
            state.OrderSpawnCount = 0;
        }

        public static void SpawnOrders(SimWorld world, GameState state)
        {
            if (world == null || state == null) return;
            while (!state.IsFinished && state.ElapsedSeconds >= state.NextOrderSpawnAtSeconds)
            {
                float interval = state.Config.orderIntervalSeconds
                    * Mathf.Pow(Mathf.Max(0f, state.Config.orderIntervalDecayPerOrder), state.OrderSpawnCount);
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
                        state.OrderSpawnCount++; 
                    }
                }
                state.NextOrderSpawnAtSeconds += Mathf.Max(state.Config.orderIntervalMinSeconds, interval);
            }
        }
    }
}

