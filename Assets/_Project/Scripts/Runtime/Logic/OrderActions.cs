namespace AyeAye
{

    public static class OrderActions
    {

        public static string Accept(GameState state, string orderId)
        {
            OrderState order = Find(state, orderId);
            if (order == null) return "Order not found.";
            if (order.Status != OrderStatus.InInbox) return $"Order {orderId} was already decided.";
            if (!CommandValidator.CanAccept(state, orderId))
                return $"Already have {state.Config.maxActiveOrders} active orders.";
            order.Status = OrderStatus.Accepted;
            return null;
        }

        public static string Decline(GameState state, string orderId)
        {
            OrderState order = Find(state, orderId);
            if (order == null) return "Order not found.";
            if (order.Status != OrderStatus.InInbox) return $"Order {orderId} was already decided.";
            order.Status = OrderStatus.Declined;
            return null;
        }
        static OrderState Find(GameState state, string orderId)
        {
            return state.Orders.Find(o => o.Id == orderId);
        }
    }
}

