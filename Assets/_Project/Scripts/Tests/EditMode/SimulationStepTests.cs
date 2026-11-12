using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using AyeAye;

namespace AyeAye.Tests.EditMode
{
    // FR-0103, FR-0304, FR-0409, FR-0501: chốt lệnh, chạy tàu, tới đích.
    public class SimulationStepTests
    {
        private GameConfig _config;
        private GameState _state;
        private RouteTable _routes;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<GameConfig>();
            _state = new GameState(_config);

            var s1 = ScriptableObject.CreateInstance<ShipDef>();
            s1.shipId = "S1"; s1.captain = CrewId.Bob; s1.startPortId = "A";
            var s2 = ScriptableObject.CreateInstance<ShipDef>();
            s2.shipId = "S2"; s2.captain = CrewId.Mira; s2.startPortId = "B";
            GameStateBuilder.InitializeShips(_state, new[] { s1, s2 });

            _routes = ScriptableObject.CreateInstance<RouteTable>();
            _routes.pairs.Add(new RoutePairEntry
            {
                fromPortId = "A", toPortId = "B",
                options =
                {
                    new RouteOption { label = "thẳng", portPath = { "A", "B" }, hazards = { HazardType.Clear }, length = 2f },
                    new RouteOption { label = "dài", portPath = { "A", "M", "B" }, hazards = { HazardType.Fog, HazardType.Clear }, length = 3f }
                }
            });
        }

        private OrderState Accepted(string id)
        {
            var o = new OrderState { Id = id, FromPortId = "A", ToPortId = "B", Status = OrderStatus.Accepted, DeadlineDay = 5, Revenue = 10 };
            _state.Orders.Add(o);
            return o;
        }

        // FR-0103: staged → Moving + đơn → Assigned
        [Test]
        public void CommitStaged_Assign_Tau_Chay_Don_Assigned()
        {
            var o = Accepted("O1");
            var cmd = new StagedCommand { Kind = CommandKind.Assign, ShipId = "S1", OrderId = "O1", RouteOptionIndex = 1 };
            cmd.Route = _routes.pairs[0].options[1];
            _state.StagedCommands.Add(cmd);
            SimulationStep.CommitStaged(_state);

            var ship = _state.Ships[0];
            Assert.AreEqual(ShipStatus.Moving, ship.Status);
            Assert.AreEqual("O1", ship.OrderId);
            Assert.AreEqual(0f, ship.DistanceAlongRoute);
            Assert.AreEqual(HazardType.Fog, ship.Route.hazards[0]); // option 1 = đường dài
            Assert.AreEqual(OrderStatus.Assigned, o.Status);
            Assert.AreEqual("S1", o.AssignedShipId);
            Assert.IsEmpty(_state.StagedCommands);
        }

        [Test]
        public void CommitStaged_Reposition_Tau_Chay_Khong_Don()
        {
            var cmd = new StagedCommand { Kind = CommandKind.Reposition, ShipId = "S2", RouteOptionIndex = 0 };
            cmd.Route = _routes.pairs[0].options[0]; // A→B; FromPort "A"
            _state.StagedCommands.Add(cmd);
            SimulationStep.CommitStaged(_state);

            var ship = _state.Ships[1];
            Assert.AreEqual(ShipStatus.Moving, ship.Status);
            Assert.IsEmpty(ship.OrderId);
            Assert.AreEqual("A", ship.Route.FromPort);
        }

        // FR-0304: Inbox chưa quyết định → Declined khi MAKE IT SO
        [Test]
        public void CommitStaged_Het_Han_Inbox()
        {
            Accepted("O1").Status = OrderStatus.InInbox;
            SimulationStep.CommitStaged(_state);
            Assert.AreEqual(OrderStatus.Declined, _state.Orders[0].Status);
        }

        // FR-0501
        [Test]
        public void MoveAll_Chay_Mot_Phphan_Duong()
        {
            var ship = _state.Ships[0];
            ship.Status = ShipStatus.Moving;
            ship.Route = _routes.pairs[0].options[0]; // A→B dài 2
            SimulationStep.MoveAll(_state);
            Assert.AreEqual(1f, ship.DistanceAlongRoute); // Normal 1.0
            Assert.AreEqual(ShipStatus.Moving, ship.Status);
        }

        // FR-0409
        [Test]
        public void MoveAll_Toi_Dich_Giao_Hang()
        {
            var o = Accepted("O1");
            o.Status = OrderStatus.Assigned;
            o.AssignedShipId = "S1";
            var ship = _state.Ships[0];
            ship.Status = ShipStatus.Moving;
            ship.Route = _routes.pairs[0].options[0];
            ship.OrderId = "O1";
            ship.DistanceAlongRoute = 1.5f;
            SimulationStep.MoveAll(_state);

            Assert.AreEqual(ShipStatus.Idle, ship.Status);
            Assert.AreEqual("B", ship.PortId);
            Assert.IsEmpty(ship.OrderId);
            Assert.AreEqual(OrderStatus.Delivered, o.Status);
            Assert.AreEqual(_state.CurrentDay, o.DeliveredDay);
        }

        [Test]
        public void MoveAll_Qua_Node_Giua_Van_Di_Tiep()
        {
            var ship = _state.Ships[0];
            ship.Status = ShipStatus.Moving;
            ship.Route = _routes.pairs[0].options[1]; // A→M→B dài 3
            ship.DistanceAlongRoute = 0.5f;
            SimulationStep.MoveAll(_state);
            Assert.AreEqual(1.5f, ship.DistanceAlongRoute);
            Assert.AreEqual("M", ship.ArrivedPortId); // vừa đi qua node giữa
            Assert.AreEqual(ShipStatus.Moving, ship.Status);
        }

        [Test]
        public void MovingContexts_Dung_Noi_Dung()
        {
            var o = Accepted("O1");
            o.Status = OrderStatus.Assigned; o.AssignedShipId = "S1"; o.Tags = OrderTag.Fragile;
            var ship = _state.Ships[0];
            ship.Status = ShipStatus.Moving;
            ship.Route = _routes.pairs[0].options[1];
            ship.OrderId = "O1";
            ship.DistanceAlongRoute = 1.5f;
            ship.ArrivedPortId = "M";

            var contexts = SimulationStep.MovingContexts(_state);
            Assert.AreEqual(1, contexts.Count);
            var ctx = contexts[0];
            Assert.AreEqual(ship, ctx.Ship);
            Assert.AreEqual("O1", ctx.CarriedOrder.Id);
            Assert.IsTrue(ctx.JustArrivedAtPort);
            Assert.IsTrue(ctx.EdgeHazards.Contains(HazardType.Fog));
        }

        // FR-0506: tiến độ không âm; vượt đích thì giao
        [Test]
        public void ApplyProgressDelta_Chan_Khong_Am()
        {
            var ship = _state.Ships[0];
            ship.Status = ShipStatus.Moving;
            ship.Route = _routes.pairs[0].options[0];
            ship.DistanceAlongRoute = 0.3f;
            SimulationStep.ApplyProgressDelta(_state, ship, -0.7f);
            Assert.AreEqual(0f, ship.DistanceAlongRoute);
            Assert.AreEqual(ShipStatus.Moving, ship.Status);
        }

        [Test]
        public void ApplyProgressDelta_Vuot_Dich_Giao_Hang()
        {
            var o = Accepted("O1");
            o.Status = OrderStatus.Assigned; o.AssignedShipId = "S1";
            var ship = _state.Ships[0];
            ship.Status = ShipStatus.Moving;
            ship.Route = _routes.pairs[0].options[0];
            ship.OrderId = "O1";
            ship.DistanceAlongRoute = 1.8f;
            SimulationStep.ApplyProgressDelta(_state, ship, 0.5f);
            Assert.AreEqual(ShipStatus.Idle, ship.Status);
            Assert.AreEqual(OrderStatus.Delivered, o.Status);
        }

        [Test]
        public void MoveAll_Xoa_ArrivedPortId_Cua_Ngay_Truc()
        {
            var ship = _state.Ships[0];
            ship.Status = ShipStatus.Moving;
            ship.Route = _routes.pairs[0].options[0];
            ship.ArrivedPortId = "M"; // rác từ ngày trước
            SimulationStep.MoveAll(_state);
            Assert.IsEmpty(ship.ArrivedPortId);
        }
    }
}
