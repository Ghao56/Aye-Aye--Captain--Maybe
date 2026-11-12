namespace AyeAye
{
    // FR-0302/0303: nhận/từ chối đơn trong Inbox. Thuần C#: UI chỉ gọi và render lý do từ chối.
    // Trả về null = thành công; khác null = lý do (tiếng Việt).
    public static class OrderActions
    {
        public static string Accept(GameState state, string orderId)
        {
            OrderState order = Find(state, orderId);
            if (order == null) return "Không tìm thấy đơn.";
            if (order.Status != OrderStatus.InInbox) return $"Đơn {orderId} đã được quyết định.";
            if (!CommandValidator.CanAccept(state, orderId))
                return $"Đã đủ {state.Config.maxActiveOrders} đơn đang hoạt động.";
            order.Status = OrderStatus.Accepted;
            return null;
        }

        public static string Decline(GameState state, string orderId)
        {
            OrderState order = Find(state, orderId);
            if (order == null) return "Không tìm thấy đơn.";
            if (order.Status != OrderStatus.InInbox) return $"Đơn {orderId} đã được quyết định.";
            order.Status = OrderStatus.Declined;
            return null;
        }

        static OrderState Find(GameState state, string orderId)
        {
            return state.Orders.Find(o => o.Id == orderId);
        }
    }
}
