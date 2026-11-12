using System.Collections.Generic;
using System.Linq;
using AyeAye;
using NUnit.Framework;
using UnityEngine;

namespace AyeAye.Tests.EditMode
{
    /// <summary>
    /// Coverage theo yêu cầu boss sau Bước 1:
    /// (1) ngày 1, I-01 rơi vào tàu đầu tiên đi qua tuyến Sương mù (FR-0502);
    /// (2) tàu chọn tuyến O-02 hai chặng (Gốc→Sương Mù→Cá Mập) đi qua node giữa rồi giao đúng hạn.
    /// (Giữ nguyên hành vi hiện tại của scheduler/simulation — test chốt_regression.)
    /// </summary>
    public class Day1FogRouteTests
    {
        private TestWorld _world;
        private GameState _state;

        [SetUp]
        public void SetUp()
        {
            _world = TestWorld.Create();
            _state = GameStateBuilder.CreateRun(_world.Config, _world.ShipDefs, seed: 5);
            OrderScheduler.ScheduleNewOrders(_world.OrderPool, _state);
            // ValidateAssign chỉ nhận đơn đã Accept (FR-0302) — đơn ngày 1 phải vào "Đã nhận" trước khi gán.
            foreach (var order in _state.Orders.Where(o => o.Status == OrderStatus.InInbox))
                order.Status = OrderStatus.Accepted;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var so in _world.SOs) Object.DestroyImmediate(so);
        }

        private ShipState Stage(ShipState ship, string orderId, int optionIndex)
        {
            var order = _state.Orders.First(o => o.Id == orderId);
            var route = _world.Routes.GetOptions(order.FromPortId, order.ToPortId)[optionIndex];
            _state.StagedCommands.Add(new StagedCommand
            {
                Kind = CommandKind.Assign,
                ShipId = ship.Id,
                OrderId = orderId,
                Route = route,
            });
            return ship;
        }

        [Test]
        public void Ngay1_I01_RoiVaoTauDiTuyenSuongMu()
        {
            var tiaChop = _state.Ships.First(s => s.Captain == CrewId.Bob);   // O-01: Gốc→Vàng (Thông thoáng)
            var ruaVang = _state.Ships.First(s => s.Captain == CrewId.Chen);  // O-02: Gốc→Cá Mập (qua Sương Mù)
            Assert.IsNull(CommandValidator.ValidateAssign(_state, _world.Routes, tiaChop.Id, "O-01", 0));
            Assert.IsNull(CommandValidator.ValidateAssign(_state, _world.Routes, ruaVang.Id, "O-02", 0));
            Stage(tiaChop, "O-01", 0);
            Stage(ruaVang, "O-02", 0);

            SimulationStep.CommitStaged(_state);
            var pendings = IncidentScheduler.Schedule(_world.Incidents, _world.FixedFirstDay, _world.FixedFinale,
                _state, SimulationStep.MovingContexts(_state));

            Assert.AreEqual(1, pendings.Count, "ngày 1 đúng 1 sự cố cố định I-01");
            Assert.AreEqual("I-01", pendings[0].Incident.incidentId);
            Assert.AreEqual(ruaVang.Id, pendings[0].Context.Ship.Id, "I-01 rơi vào tàu đầu tiên đi qua Sương mù");
        }

        [Test]
        public void TuyenO02_HaiChang_QuaNodeGiua_RoiGiaoDungHan()
        {
            var mira = _state.Ships.First(s => s.Captain == CrewId.Mira); // Normal 1.0 units/ngày, đang ở Gốc
            Assert.IsNull(CommandValidator.ValidateAssign(_state, _world.Routes, mira.Id, "O-02", 0));
            Stage(mira, "O-02", 0);

            SimulationStep.CommitStaged(_state);
            Assert.AreEqual(ShipStatus.Moving, mira.Status);
            Assert.AreEqual(3, mira.Route.portPath.Count, "tuyến O-02 phải hai chặng (3 node): Gốc → Sương Mù → Cá Mập");
            Assert.IsTrue(mira.Route.hazards.Contains(HazardType.Fog));

            // Ngày 1: tới node giữa Sương Mù (1/2 route-units), chưa giao
            SimulationStep.MoveAll(_state);
            Assert.AreEqual(ShipStatus.Moving, mira.Status);
            Assert.AreEqual(TestWorld.Suong, mira.ArrivedPortId);

            // Ngày 2: tới Cá Mập → giao (hạn N4)
            _state.CurrentDay = 2;
            SimulationStep.MoveAll(_state);
            Assert.AreEqual(ShipStatus.Idle, mira.Status);
            var order = _state.Orders.First(o => o.Id == "O-02");
            Assert.AreEqual(OrderStatus.Delivered, order.Status);
            Assert.AreEqual(2, order.DeliveredDay);
            Assert.LessOrEqual(order.DeliveredDay, order.DeadlineDay);
        }
    }
}
