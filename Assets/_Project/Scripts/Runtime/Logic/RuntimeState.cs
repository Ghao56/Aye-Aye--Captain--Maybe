using System.Collections.Generic;
namespace AyeAye
{

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

        public string OrderId = "";             

        public RouteOption Route;               

        public float DistanceAlongRoute;        

        public string ArrivedPortId = "";       

        public bool TemporaryRepair;

        public RoamProfile Roam;

        public float RoamTimerSeconds;

        public string LastPortId = "";

        public int PatrolIndex;
    }

    public sealed class OrderState
    {

        public string Id;

        public string CustomerName;

        public OrderTag Tags;

        public string FromPortId;

        public string ToPortId;

        public float DeadlineAtSeconds;

        public bool IsLate;                     

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

