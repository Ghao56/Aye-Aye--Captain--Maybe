using System.Collections.Generic;
using System.Linq;
using AyeAye;
using NUnit.Framework;
using UnityEngine;
namespace AyeAye.Tests.EditMode
{

    public class RoamTests
    {
        TestWorld _world;
        SimWorld _sim;
        [SetUp]

        public void SetUp()
        {
            _world = TestWorld.Create();
            _world.Config.incidentChancePerEdge = 0f; 
            _world.Config.orderFirstDelaySeconds = 100000f;
            _sim = new SimWorld
            {
                Pool = _world.OrderPool, Routes = _world.Routes,
                Incidents = _world.Incidents,
                FixedFirstDay = _world.FixedFirstDay, FixedFinale = _world.FixedFinale,
                HQPortId = TestWorld.Goc,
            };
        }
        [TearDown]

        public void TearDown()
        {
            foreach (var so in _world.SOs) Object.DestroyImmediate(so);
        }
        void Advance(GameState state, int ticks, SimWorld sim = null)
        {
            var w = sim ?? _sim;
            for (int i = 0; i < ticks; i++) Simulation.Advance(w, state, 0.1f);
        }
        static void AdvanceUntilIdle(SimWorld sim, GameState state, int maxTicks)
        {
            int guard = 0;
            while (state.Ships[0].Status == ShipStatus.Roaming && guard++ < maxTicks)
                Simulation.Advance(sim, state, 0.1f);
            Assert.That(guard, Is.LessThan(maxTicks), "tàu phải cập cảng trong giới hạn tick");
        }
        static RoamProfile Profile(float dwell = 1f, float demand = 0f, float far = 0f,
            float hub = 0f, float wander = 0f, float back = 5f) => new RoamProfile
        {
            dwellSeconds = dwell, demandWeight = demand, farBias = far,
            hubAffinity = hub, wanderWeight = wander, backtrackPenalty = back,
        };
        static ShipDef OneShip(string id, RoamProfile roam, string port)
        {
            var d = ScriptableObject.CreateInstance<ShipDef>();
            d.shipId = id; d.displayName = id; d.captain = CrewId.Bob;
            d.speedClass = SpeedClass.Normal; d.startPortId = port; d.roam = roam;
            return d;
        }
        GameState Run(ShipDef ship, float roamStart = 0f)
        {
            _world.Config.roamStartSeconds = roamStart;
            var state = GameStateBuilder.CreateRun(_world.Config, new[] { ship }, seed: 7);
            return state;
        }
        [Test]

        public void RoiCang_ChuyenRoaming_CanhTrucTiep()
        {
            var state = Run(OneShip("S-1", Profile(dwell: 1f), TestWorld.Goc));
            Advance(state, 15); 
            var s = state.Ships[0];
            Assert.AreEqual(ShipStatus.Roaming, s.Status, "hết nghỉ + dư thừa phải rời cảng");
            Assert.AreEqual(2, s.Route.portPath.Count, "roaming chỉ đi 1 cạnh trực tiếp");
            Assert.AreEqual(TestWorld.Goc, s.LastPortId, "LastPortId = cảng vừa rời");
        }
        [Test]

        public void Roaming_ToDich_VeIdle_DungTrangThai()
        {
            _world.Config.roamSpeedFactor = 7f; 
            var state = Run(OneShip("S-1", Profile(dwell: 1f), TestWorld.Goc));
            Advance(state, 15); 
            var s = state.Ships[0];
            Assert.AreEqual(ShipStatus.Roaming, s.Status, "tiền điều kiện của test");
            AdvanceUntilIdle(_sim, state, 200);
            s = state.Ships[0];
            Assert.AreEqual(ShipStatus.Idle, s.Status);
            Assert.AreEqual(TestWorld.Vang, s.PortId, "hòa điểm → lấy cảng kề đầu tiên (Vang) — xác định");
            Assert.AreEqual(TestWorld.Goc, s.LastPortId);
            Assert.AreEqual(1f, s.RoamTimerSeconds, 0.01f, "timer đặt lại = dwellSeconds");
        }
        [Test]

        public void TruocRoamStart_KhongTauNaoroiCang()
        {
            var state = Run(OneShip("S-1", Profile(dwell: 0.5f), TestWorld.Goc), roamStart: 30f);
            Advance(state, 290); 
            var s = state.Ships[0];
            Assert.AreEqual(ShipStatus.Idle, s.Status);
            Assert.AreEqual(TestWorld.Goc, s.PortId);
            Advance(state, 30); 
            Assert.AreEqual(ShipStatus.Roaming, s.Status, "qua ân hạn và dư thừa thì rời");
        }
        [Test]

        public void TauKhongDuThua_OLaiCang()
        {
            var state = Run(OneShip("S-1", Profile(dwell: 0.5f), TestWorld.Goc));
            TestWorld.AddManualOrder(state, "O-T4", TestWorld.Goc, TestWorld.Vang, 999f);
            Advance(state, 200); 
            var s = state.Ships[0];
            Assert.AreEqual(ShipStatus.Idle, s.Status, "đơn đang chờ ngay tại cảng → tàu ở lại");
            Assert.AreEqual(TestWorld.Goc, s.PortId);
        }
        [Test]

        public void Roaming_BiTuChoiLenh_CoDichVaThoiGian()
        {
            var state = Run(OneShip("S-1", Profile(dwell: 1f), TestWorld.Goc));
            Advance(state, 15); 
            var s = state.Ships[0];
            Assert.AreEqual(ShipStatus.Roaming, s.Status, "tiền điều kiện của test");
            string errAssign = CommandValidator.ValidateAssign(state, _world.Routes, s.Id, "O-KHONG-TON-TAI", 0);
            Assert.IsNotNull(errAssign, "tàu Roaming không nhận lệnh giữa cạnh");
            StringAssert.Contains("vang", errAssign, "lý do phải nêu đích đến (id cảng đích)");
            StringAssert.Contains("docks", errAssign);
            string errRepos = CommandValidator.ValidateReposition(state, _world.Routes, s.Id, TestWorld.Suong, 0);
            Assert.IsNotNull(errRepos);
            StringAssert.Contains("vang", errRepos);
        }
        [Test]

        public void KhongQuayLaiNgay_CangKeKhacLastPort()
        {
            var state = Run(OneShip("S-1", Profile(dwell: 1f, back: 5f, wander: 0f), TestWorld.Camap));
            state.Ships[0].LastPortId = TestWorld.Suong; 
            Advance(state, 15);
            var s = state.Ships[0];
            Assert.AreEqual(ShipStatus.Roaming, s.Status);
            Assert.AreEqual(TestWorld.Goc, s.Route.ToPort,
                "Suong bị trừ điểm backtrack → chọn Goc (hòa với Tram, lấy đầu theo thứ tự ổn định)");
            Assert.AreNotEqual(TestWorld.Suong, s.Route.ToPort);
        }
        [Test]

        public void CungSeed_CungChuoiRoaming()
        {
            var a = GameStateBuilder.CreateRun(_world.Config, _world.ShipDefs, seed: 7);
            var b = GameStateBuilder.CreateRun(_world.Config, _world.ShipDefs, seed: 7);
            Advance(a, 600); 
            Advance(b, 600);
            for (int i = 0; i < a.Ships.Count; i++)
            {
                Assert.AreEqual(a.Ships[i].Status, b.Ships[i].Status, "tàu {0}", i);
                Assert.AreEqual(a.Ships[i].PortId, b.Ships[i].PortId, "tàu {0}", i);
                Assert.AreEqual(a.Ships[i].LastPortId, b.Ships[i].LastPortId, "tàu {0}", i);
                Assert.AreEqual(a.Ships[i].DistanceAlongRoute, b.Ships[i].DistanceAlongRoute, 0.001f, "tàu {0}", i);
            }
        }
        [Test]

        public void RoamKhongDungRngChung_ChoiDonGiongHet()
        {
            _world.Config.orderFirstDelaySeconds = 25f; 
            _world.Config.roamStartSeconds = 0f;
            _world.Config.roamEnabled = true;
            var a = GameStateBuilder.CreateRun(_world.Config, _world.ShipDefs, seed: 7);
            _world.Config.roamEnabled = false;
            var bConfig = Object.Instantiate(_world.Config);
            _world.SOs.Add(bConfig);
            var b = GameStateBuilder.CreateRun(bConfig, _world.ShipDefs, seed: 7);
            _world.Config.roamEnabled = true;
            Advance(a, 3000, _sim); 
            Advance(b, 3000, _sim);
            Assert.AreEqual(a.OrderSpawnCount, b.OrderSpawnCount, "số đơn đã sinh phải giống hệt");
            Assert.AreEqual(a.NextOrderSpawnAtSeconds, b.NextOrderSpawnAtSeconds, 0.0001f,
                "mốc sinh kế tiếp phải giống hệt (nhịp sinh thuần đồng hồ)");
            Assert.AreEqual(a.Orders.Count, b.Orders.Count);
        }
        [Test]

        public void KhongRoam_HoacKhongCoCangKe_OLaiYen()
        {
            var shipA = OneShip("S-A", null, TestWorld.Goc);          
            var shipB = OneShip("S-B", Profile(dwell: 0.5f), "no-net"); 
            var defs = new List<ShipDef> { shipA, shipB };
            _world.SOs.Add(shipA);
            _world.SOs.Add(shipB);
            var rt = ScriptableObject.CreateInstance<RouteTable>();
            _world.SOs.Add(rt);
            rt.pairs = new List<RoutePairEntry>
            {
                new RoutePairEntry
                {
                    fromPortId = TestWorld.Goc, toPortId = TestWorld.Vang,
                    options = new List<RouteOption>
                    {
                        new RouteOption
                        {
                            label = "thẳng", length = 1f,
                            portPath = new List<string> { TestWorld.Goc, TestWorld.Vang },
                            hazards = new List<HazardType> { HazardType.Clear },
                        },
                    },
                },
            };
            var sim = new SimWorld
            {
                Pool = _world.OrderPool, Routes = rt, Incidents = _world.Incidents,
                FixedFirstDay = _world.FixedFirstDay, FixedFinale = _world.FixedFinale,
                HQPortId = TestWorld.Goc,
            };
            _world.Config.roamStartSeconds = 0f;
            var state = GameStateBuilder.CreateRun(_world.Config, defs, seed: 7);
            for (int i = 0; i < 60; i++) Simulation.Advance(sim, state, 0.1f); 
            var a = state.Ships.First(x => x.Id == "S-A");
            var b = state.Ships.First(x => x.Id == "S-B");
            Assert.AreEqual(ShipStatus.Idle, a.Status, "Roam == null → không roaming");
            Assert.AreEqual(ShipStatus.Idle, b.Status, "không có cảng kề → ở yên");
            Assert.AreEqual("no-net", b.PortId);
        }
        [Test]

        public void Roam_DungKhiSuCoCho()
        {
            _world.Config.roamSpeedFactor = 7f;
            var state = Run(OneShip("S-1", Profile(dwell: 1f), TestWorld.Goc));
            Advance(state, 15);
            var s = state.Ships[0];
            Assert.AreEqual(ShipStatus.Roaming, s.Status, "tiền điều kiện của test");
            float d0 = s.DistanceAlongRoute;
            state.PendingIncidents.Add(new PendingIncident
            {
                Incident = _world.FixedFirstDay,
                Context = new IncidentContext { Ship = s },
            });
            Advance(state, 30);
            Assert.AreEqual(d0, s.DistanceAlongRoute, 0.0001f, "đồng hồ dừng → roaming dừng");
            Assert.AreEqual(ShipStatus.Roaming, s.Status);
            state.PendingIncidents.Clear();
            AdvanceUntilIdle(_sim, state, 200);
            s = state.Ships[0];
            Assert.AreEqual(ShipStatus.Idle, s.Status, "xử lý xong → roaming chạy tiếp tới đích");
            Assert.AreEqual(TestWorld.Vang, s.PortId);
        }
        [Test]

        public void DemoDataShips_CoHoSoRoam()
        {
            foreach (var d in DemoData.BuildShips())
            {
                _world.SOs.Add(d);
                Assert.IsNotNull(d.roam, $"{d.shipId} phải có hồ sơ roam");
                Assert.Greater(d.roam.dwellSeconds, 0f, $"{d.shipId}.dwellSeconds");
            }
        }
        [Test]

        public void DwellKhac_ThoiDiemRoiKhac()
        {
            var defs = new List<ShipDef>
            {
                OneShip("S-A", Profile(dwell: 2f), TestWorld.Goc),
                OneShip("S-B", Profile(dwell: 20f), TestWorld.Goc),
            };
            _world.SOs.Add(defs[0]);
            _world.SOs.Add(defs[1]);
            _world.Config.roamStartSeconds = 0f;
            var state = GameStateBuilder.CreateRun(_world.Config, defs, seed: 7);
            Advance(state, 40); 
            var a = state.Ships.First(x => x.Id == "S-A");
            var b = state.Ships.First(x => x.Id == "S-B");
            Assert.AreEqual(ShipStatus.Roaming, a.Status);
            Assert.AreEqual(ShipStatus.Idle, b.Status, "dwell 20s → chưa rời sau 4s");
        }
    }
}

