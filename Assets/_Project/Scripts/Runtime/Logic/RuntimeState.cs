using System.Collections.Generic;

namespace AyeAye
{
    // Trạng thái runtime của một tàu (thuần C#). Lệnh staged nằm ở GameState.StagedCommands (không trùng lặp).
    public sealed class ShipState
    {
        public string Id;
        public string DisplayName;
        public CrewId Captain;
        public SpeedClass Speed;
        public ShipCondition Condition = ShipCondition.Good;
        public ShipStatus Status = ShipStatus.Idle;

        [System.ComponentModel.DefaultValue("")]
        public string PortId = "";

        public string OrderId = "";             // đơn đang chở ("" = không chở)

        public RouteOption Route;               // khác null khi đang Moving
        public float DistanceAlongRoute;        // 0..Route.Length
        public string ArrivedPortId = "";       // node giữa vừa đi qua trong lần chạy gần nhất (FR-0502)

        public bool TemporaryRepair;
    }

    // Trạng thái runtime của một đơn.
    public sealed class OrderState
    {
        public string Id;
        public string CustomerName;
        public OrderTag Tags;
        public string FromPortId;
        public string ToPortId;
        public int DeadlineDay;
        public int Revenue;
        public string Clue;
        public OrderStatus Status = OrderStatus.InInbox;
        public string AssignedShipId;
        public CargoCondition Cargo = CargoCondition.Intact;
        public int DeliveredDay;

        public static OrderState From(OrderDef def)
        {
            return new OrderState
            {
                Id = def.orderId,
                CustomerName = def.customerName,
                Tags = def.tags,
                FromPortId = def.fromPortId,
                ToPortId = def.toPortId,
                DeadlineDay = def.deadlineDay,
                Revenue = def.revenue,
                Clue = def.clue,
            };
        }
    }

    // Một lệnh staged trong pha Planning (FR-0104: sửa/hủy tự do cho đến MAKE IT SO).
    // Reposition: OrderId = null, RouteOptionIndex trỏ RouteTable từ cảng hiện tại tới cảng đích.
    public sealed class StagedCommand
    {
        public CommandKind Kind;
        public string ShipId;
        public string OrderId;
        public int RouteOptionIndex;
        // shortcut: tham chiếu RouteOption đã chọn khi staged (CommandValidator đã kiểm tra bảng tuyến) — nếu sau này cần serialize StagedCommand thì phải đổi sang RouteOptionIndex + bảng tra
        public RouteOption Route;
    }
}
