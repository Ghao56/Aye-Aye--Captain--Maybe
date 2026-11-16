using System.Linq;
using AyeAye;
using NUnit.Framework;
using UnityEngine;
namespace AyeAye.Tests.EditMode
{

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
            var route = Route("a", "b", "c"); 
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
            Assert.AreEqual(0, seg); 
            Assert.AreEqual(0.5f, t, 0.0001f);
            t = RouteProgress.SegmentAt(route, 2.25f, out seg);
            Assert.AreEqual(1, seg); 
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

    public class FleetActionsTests
    {

        private TestWorld _world;

        private GameState _state;
        [SetUp]

        public void SetUp()
        {
            _world = TestWorld.Create();
            _state = GameStateBuilder.CreateRun(_world.Config, _world.ShipDefs, seed: 5);
            OrderScheduler.SpawnInitial(_world.OrderPool, _state); 
            TestWorld.AddManualOrder(_state, "O-01", TestWorld.Goc, TestWorld.Vang, 70f, 120, OrderTag.Fragile);
            TestWorld.AddManualOrder(_state, "O-02", TestWorld.Goc, TestWorld.Camap, 85f, 100);
            foreach (var order in _state.Orders.Where(o => o.Status == OrderStatus.InInbox))
                order.Status = OrderStatus.Accepted;
        }
        [TearDown]

        public void TearDown()
        {
            foreach (var so in _world.SOs) Object.DestroyImmediate(so);
        }
        [Test]

        public void Assign_HopLe_ApNgay_TauXuatPhat()
        {
            var ship = _state.Ships.First(s => s.PortId == TestWorld.Goc);
            string error = FleetActions.Assign(_state, _world.Routes, ship.Id, "O-01", 0);
            Assert.IsNull(error);
            Assert.AreEqual(ShipStatus.Moving, ship.Status);
            Assert.AreEqual(_world.Routes.GetOptions(TestWorld.Goc, TestWorld.Vang)[0], ship.Route);
            Assert.AreEqual(0f, ship.DistanceAlongRoute);
            Assert.AreEqual("O-01", ship.OrderId);
            var order = _state.Orders.First(o => o.Id == "O-01");
            Assert.AreEqual(OrderStatus.Assigned, order.Status);
            Assert.AreEqual(ship.Id, order.AssignedShipId);
        }
        [Test]

        public void Assign_ShipKhongODi_BiChan()
        {
            var ship = _state.Ships.First(s => s.PortId == TestWorld.Vang); 
            string error = FleetActions.Assign(_state, _world.Routes, ship.Id, "O-01", 0);
            Assert.IsNotNull(error);
            Assert.IsTrue(error.Contains("not at the order's departure port"));
            Assert.AreEqual(ShipStatus.Idle, ship.Status); 
        }
        [Test]

        public void Assign_HaiLenhCungTau_LenhHaiBiChan()
        {
            var ship = _state.Ships.First(s => s.PortId == TestWorld.Goc);
            Assert.IsNull(FleetActions.Assign(_state, _world.Routes, ship.Id, "O-01", 0));
            string error = FleetActions.Reposition(_state, _world.Routes, ship.Id, TestWorld.Suong, 0);
            Assert.IsNotNull(error);
            Assert.IsTrue(error.Contains("busy"));
        }
        [Test]

        public void Reposition_HopLe_ApNgay_PhongRong()
        {
            var ship = _state.Ships.First(s => s.PortId == TestWorld.Goc && s.Captain == CrewId.Mira);
            string error = FleetActions.Reposition(_state, _world.Routes, ship.Id, TestWorld.Tram, 0);
            Assert.IsNull(error);
            Assert.AreEqual(ShipStatus.Moving, ship.Status);
            Assert.IsEmpty(ship.OrderId);
            Assert.IsNotNull(ship.Route);
        }
        [Test]

        public void LenhApNgay_KhongTheHuy()
        {
            var ship = _state.Ships.First(s => s.PortId == TestWorld.Goc);
            Assert.IsNull(FleetActions.Assign(_state, _world.Routes, ship.Id, "O-01", 0));
            string error = FleetActions.Assign(_state, _world.Routes, ship.Id, "O-02", 0);
            Assert.IsNotNull(error, "lệnh thứ hai trên cùng tàu phải bị chặn");
        }
    }
}

