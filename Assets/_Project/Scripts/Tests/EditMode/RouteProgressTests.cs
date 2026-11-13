using System.Linq;
using AyeAye;
using NUnit.Framework;
using UnityEngine;

namespace AyeAye.Tests.EditMode
{
    /// <summary>
    /// RouteProgress: hàm thuần tính vị trí theo quãng đường trên RouteOption.
    /// Quy tắc phải khớp đúng cách SimulationStep chia độ dài (các cạnh dài đều).
    /// </summary>
    public class RouteProgressTests
    {
        private static RouteOption Route(params string[] ports)
        {
            return new RouteOption
            {
                label = "test",
                portPath = new System.Collections.Generic.List<string>(ports),
                hazards = new System.Collections.Generic.List<HazardType>(
                    Enumerable.Repeat(HazardType.Clear, ports.Length - 1)),
                length = 3f,
            };
        }

        [Test]
        public void CumulativeDistance_NodeGiua_BangChiaDeu()
        {
            var route = Route("a", "b", "c"); // 2 cạnh, length 3 → node giữa tại 1.5
            Assert.AreEqual(0f, RouteProgress.CumulativeDistance(route, 0), 0.0001f);
            Assert.AreEqual(1.5f, RouteProgress.CumulativeDistance(route, 1), 0.0001f);
            Assert.AreEqual(3f, RouteProgress.CumulativeDistance(route, 2), 0.0001f);
        }

        [Test]
        public void SegmentAt_DoanDangDi_KhopCumulative()
        {
            var route = Route("a", "b", "c");
            int seg;
            float t = RouteProgress.SegmentAt(route, 0.75f, out seg);
            Assert.AreEqual(0, seg); // 0.75 < 1.5 → cạnh đầu
            Assert.AreEqual(0.5f, t, 0.0001f);
            t = RouteProgress.SegmentAt(route, 2.25f, out seg);
            Assert.AreEqual(1, seg); // 2.25 ≥ 1.5 → cạnh hai
            Assert.AreEqual(0.5f, t, 0.0001f);
        }

        [Test]
        public void SegmentAt_ToiDich_ClampVeCuoi()
        {
            var route = Route("a", "b", "c");
            int seg;
            float t = RouteProgress.SegmentAt(route, 99f, out seg);
            Assert.AreEqual(1, seg);
            Assert.AreEqual(1f, t, 0.0001f);
            t = RouteProgress.SegmentAt(route, -5f, out seg);
            Assert.AreEqual(0, seg);
            Assert.AreEqual(0f, t, 0.0001f);
        }

        [Test]
        public void CumulativeDistance_MotCanh_Node1LaDich()
        {
            var route = Route("a", "b");
            Assert.AreEqual(3f, RouteProgress.CumulativeDistance(route, 1), 0.0001f);
        }
    }

    /// <summary>
    /// FleetActions: lớp thuần cho lệnh đội tàu (giống OrderActions) — UI không chứa luật.
    /// Trả về null khi thành công; lý do lỗi là chuỗi từ CommandValidator.
    /// </summary>
    public class FleetActionsTests
    {
        private TestWorld _world;
        private GameState _state;

        [SetUp]
        public void SetUp()
        {
            _world = TestWorld.Create();
            _state = GameStateBuilder.CreateRun(_world.Config, _world.ShipDefs, seed: 5);
            OrderScheduler.ScheduleNewOrders(_world.OrderPool, _state);
            foreach (var order in _state.Orders.Where(o => o.Status == OrderStatus.InInbox))
                order.Status = OrderStatus.Accepted;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var so in _world.SOs) Object.DestroyImmediate(so);
        }

        [Test]
        public void Assign_HopLe_StageLenhVoiRouteDaChon()
        {
            var ship = _state.Ships.First(s => s.PortId == TestWorld.Goc);
            string error = FleetActions.Assign(_state, _world.Routes, ship.Id, "O-01", 0);
            Assert.IsNull(error);
            Assert.AreEqual(1, _state.StagedCommands.Count);
            var cmd = _state.StagedCommands[0];
            Assert.AreEqual(CommandKind.Assign, cmd.Kind);
            Assert.AreEqual(ship.Id, cmd.ShipId);
            Assert.AreEqual("O-01", cmd.OrderId);
            Assert.AreEqual(_world.Routes.GetOptions(TestWorld.Goc, TestWorld.Vang)[0], cmd.Route);
        }

        [Test]
        public void Assign_ShipKhongODi_BiChan()
        {
            var ship = _state.Ships.First(s => s.PortId == TestWorld.Vang); // Dex ở Vàng, O-01 từ Gốc
            string error = FleetActions.Assign(_state, _world.Routes, ship.Id, "O-01", 0);
            Assert.IsNotNull(error);
            Assert.IsTrue(error.Contains("không ở cảng đi"));
            Assert.AreEqual(0, _state.StagedCommands.Count);
        }

        [Test]
        public void Assign_HaiLenhCungTau_LenhHaiBiChan()
        {
            var ship = _state.Ships.First(s => s.PortId == TestWorld.Goc);
            Assert.IsNull(FleetActions.Assign(_state, _world.Routes, ship.Id, "O-01", 0));
            string error = FleetActions.Reposition(_state, _world.Routes, ship.Id, TestWorld.Suong, 0);
            Assert.IsNotNull(error);
            Assert.IsTrue(error.Contains("đã có lệnh hôm nay"));
        }

        [Test]
        public void Reposition_HopLe_StageLenhPhongRong()
        {
            var ship = _state.Ships.First(s => s.PortId == TestWorld.Goc && s.Captain == CrewId.Mira);
            string error = FleetActions.Reposition(_state, _world.Routes, ship.Id, TestWorld.Tram, 0);
            Assert.IsNull(error);
            var cmd = _state.StagedCommands[0];
            Assert.AreEqual(CommandKind.Reposition, cmd.Kind);
            Assert.IsNull(cmd.OrderId);
        }

        [Test]
        public void Cancel_XoaLenhStaged_RoiHetLenh_BiChan()
        {
            var ship = _state.Ships.First(s => s.PortId == TestWorld.Goc);
            Assert.IsNull(FleetActions.Assign(_state, _world.Routes, ship.Id, "O-01", 0));
            Assert.IsNull(FleetActions.Cancel(_state, ship.Id));
            Assert.AreEqual(0, _state.StagedCommands.Count);
            string error = FleetActions.Cancel(_state, ship.Id);
            Assert.IsNotNull(error, "hủy lần nữa phải báo không có lệnh");
        }
    }
}
