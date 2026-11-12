using NUnit.Framework;
using UnityEngine;
using AyeAye;

namespace AyeAye.Tests.EditMode
{
    // FR-0303, FR-0404, FR-0405, FR-0406: kiểm tra lệnh trước khi staged.
    public class CommandValidatorTests
    {
        private GameConfig _config;
        private GameState _state;
        private RouteTable _routes;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<GameConfig>();
            _state = new GameState(_config);
            _state.Reputation = _config.startReputation;

            var s1 = ScriptableObject.CreateInstance<ShipDef>();
            s1.shipId = "S1"; s1.captain = CrewId.Bob; s1.startPortId = "A";
            var s2 = ScriptableObject.CreateInstance<ShipDef>();
            s2.shipId = "S2"; s2.captain = CrewId.Mira; s2.startPortId = "B";
            GameStateBuilder.InitializeShips(_state, new[] { s1, s2 });

            _routes = ScriptableObject.CreateInstance<RouteTable>();
            _routes.pairs.Add(new RoutePairEntry
            {
                fromPortId = "A", toPortId = "B",
                options = { new RouteOption { label = "thẳng", portPath = { "A", "B" }, hazards = { HazardType.Clear }, length = 2f } }
            });
            _routes.pairs.Add(new RoutePairEntry
            {
                fromPortId = "B", toPortId = "C",
                options = { new RouteOption { label = "ven biển", portPath = { "B", "C" }, hazards = { HazardType.Clear }, length = 1f } }
            });
            _routes.pairs.Add(new RoutePairEntry
            {
                fromPortId = "A", toPortId = "C",
                options = { new RouteOption { label = "mở biển", portPath = { "A", "C" }, hazards = { HazardType.Clear }, length = 1f } }
            });
        }

        private OrderState Active(string id, string from = "A", string to = "B")
        {
            var o = new OrderState { Id = id, FromPortId = from, ToPortId = to, Status = OrderStatus.Accepted, DeadlineDay = 3, Revenue = 10 };
            _state.Orders.Add(o);
            return o;
        }

        // FR-0303
        [Test]
        public void Accept_Chan_Khi_Dat_Gioi_Han()
        {
            _config.maxActiveOrders = 1;
            Active("O1");
            Assert.IsFalse(CommandValidator.CanAccept(_state, "O2"));
        }

        [Test]
        public void Accept_Duoc_Khi_Duoi_Gioi_Han()
        {
            _config.maxActiveOrders = 4;
            Assert.IsTrue(CommandValidator.CanAccept(_state, "O9"));
        }

        // FR-0404
        [Test]
        public void Assign_Hop_Le()
        {
            var o = Active("O1");
            Assert.IsNull(CommandValidator.ValidateAssign(_state, _routes, "S1", "O1", 0));
        }

        [Test]
        public void Assign_Chan_Tau_Khong_OLLuc()
        {
            var o = Active("O1");
            _state.Ships[0].Status = ShipStatus.Moving;
            Assert.IsNotNull(CommandValidator.ValidateAssign(_state, _routes, "S1", "O1", 0));
        }

        [Test]
        public void Assign_Chan_Tau_Sai_Cang_Di()
        {
            Active("O1");
            _state.Ships[0].PortId = "C";
            Assert.IsNotNull(CommandValidator.ValidateAssign(_state, _routes, "S1", "O1", 0));
        }

        [Test]
        public void Assign_Chan_Don_Chua_Accept()
        {
            var o = Active("O1");
            o.Status = OrderStatus.InInbox;
            Assert.IsNotNull(CommandValidator.ValidateAssign(_state, _routes, "S1", "O1", 0));
        }

        [Test]
        public void Assign_Chan_Don_Da_Cho_Tau_Khac()
        {
            var o = Active("O1");
            _state.Ships[1].Status = ShipStatus.Idle;
            _state.Ships[1].PortId = "A";
            _state.StagedCommands.Add(new StagedCommand { Kind = CommandKind.Assign, ShipId = "S2", OrderId = "O1", RouteOptionIndex = 0 });
            Assert.IsNotNull(CommandValidator.ValidateAssign(_state, _routes, "S1", "O1", 0));
        }

        [Test]
        public void Assign_Chan_Tau_Da_Co_Len_Ngay()
        {
            Active("O1");
            Active("O2");
            _state.StagedCommands.Add(new StagedCommand { Kind = CommandKind.Reposition, ShipId = "S1", RouteOptionIndex = 0 });
            Assert.IsNotNull(CommandValidator.ValidateAssign(_state, _routes, "S1", "O1", 0));
        }

        [Test]
        public void Assign_Chan_Index_Route_Khong_Hop_Le()
        {
            Active("O1");
            Assert.IsNotNull(CommandValidator.ValidateAssign(_state, _routes, "S1", "O1", 5));
        }

        // FR-0405
        [Test]
        public void Reposition_Chan_Tau_Dang_Cho_Hang()
        {
            _state.Ships[0].OrderId = "O1";
            Assert.IsNotNull(CommandValidator.ValidateReposition(_state, _routes, "S1", "C", 0));
        }

        [Test]
        public void Reposition_Hop_Le()
        {
            Assert.IsNull(CommandValidator.ValidateReposition(_state, _routes, "S1", "C", 0));
        }
    }
}
