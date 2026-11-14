using System.Collections.Generic;

namespace AyeAye
{
    // Trạng thái runtime của một tàu (thuần C#). Lệnh áp NGAY khi MAKE IT SO trên thẻ lệnh (không staged).
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

        public RouteOption Route;               // khác null khi đang Moving/Roaming
        public float DistanceAlongRoute;        // 0..Route.length
        public string ArrivedPortId = "";       // node giữa vừa đi qua trong tick gần nhất (FR-0502)

        public bool TemporaryRepair;
        // Roaming [S]/G5 — hồ sơ tính cách; null = tàu không roaming (an toàn cho test dựng tay).
        public RoamProfile Roam;
        public float RoamTimerSeconds;
        public string LastPortId = "";
        public int PatrolIndex;
    }

    // Trạng thái runtime của một đơn. Hạn là GIÂY TUYỆT ĐỐI trên đồng hồ ván (DeadlineAt = lúc bốc + deadlineSeconds).
    public sealed class OrderState
    {
        public string Id;
        public string CustomerName;
        public OrderTag Tags;
        public string FromPortId;
        public string ToPortId;
        public float DeadlineAtSeconds;
        public bool IsLate;                     // chạm hạn mà chưa giao — chốt MỘT LẦN
        public int Revenue;
        public string Clue;
        public OrderStatus Status = OrderStatus.InInbox;
        public string AssignedShipId;
        public CargoCondition Cargo = CargoCondition.Intact;

        public static OrderState From(OrderDef def, float nowSeconds)
        {
            return new OrderState
            {
                Id = def.orderId,
                CustomerName = def.customerName,
                Tags = def.tags,
                FromPortId = def.fromPortId,
                ToPortId = def.toPortId,
                DeadlineAtSeconds = nowSeconds + def.deadlineSeconds,
                Revenue = def.revenue,
                Clue = def.clue,
            };
        }
    }
}
