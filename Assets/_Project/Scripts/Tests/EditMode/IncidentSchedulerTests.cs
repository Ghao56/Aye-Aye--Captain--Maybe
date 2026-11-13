using System.Collections.Generic;
using AyeAye;
using NUnit.Framework;
using UnityEngine;

namespace AyeAye.Tests.EditMode
{
    /// IncidentScheduler bản CHẠY LIÊN TỤC (bỏ ngày — xem ASSUMPTIONS.md G1): TryTrigger trả về MỘT sự cố
    /// mỗi lần một tàu vào cạnh mới. I-01 bắt buộc lần đầu khớp; I-F khi DeliveredCount == winDeliveries-1
    /// và có tàu chở hàng; thường thì roll incidentChancePerEdge, ngân sách maxPendingIncidents do Simulation giữ.
    public class IncidentSchedulerTests
    {
        private static IncidentDef NewIncident(string id, float weightMultiplier = 1, HazardType hazard = HazardType.Any)
        {
            var def = ScriptableObject.CreateInstance<IncidentDef>();
            def.incidentId = id; def.title = id;
            def.weight = 1;
            def.triggers = new List<IncidentTrigger>
            {
                new IncidentTrigger { requiredHazard = hazard, weightMultiplier = weightMultiplier },
            };
            def.options = new List<IncidentOption>
            {
                new IncidentOption { label = "O", baseProgressDelta = 0f, entries = new List<IncidentOutcomeEntry> { new IncidentOutcomeEntry { crew = CrewId.Any, outcome = DecisionOutcome.Normal, explanation = "e", quote = "q" } } },
            };
            return def;
        }

        private static IncidentContext NewContext()
        {
            return new IncidentContext
            {
                Ship = new ShipState { Id = "S-0", DisplayName = "s", Captain = CrewId.Bob, Speed = SpeedClass.Normal, Condition = ShipCondition.Good, Status = ShipStatus.Moving, PortId = "GOC", Route = new RouteOption() },
                EdgeHazards = new List<HazardType> { HazardType.Clear },
            };
        }

        private static List<IncidentDef> NewPool(params IncidentDef[] defs)
        {
            return new List<IncidentDef>(defs);
        }

        // Bọc API thật 3 tham số (world, state, ctx) — test muốn truyền pool + fixed I-01/I-F rõ ràng.
        private static PendingIncident Try(List<IncidentDef> pool, GameState state, IncidentDef fixedFirst, IncidentDef fixedFinale, IncidentContext ctx)
        {
            var world = new SimWorld
            {
                Pool = new OrderPool { pool = new List<OrderDef>() },
                Incidents = pool,
                FixedFirstDay = fixedFirst,
                FixedFinale = fixedFinale,
            };
            return IncidentScheduler.TryTrigger(world, state, ctx);
        }

        [Test]
        public void I01_SuCoCoDinh_XayRaLanDauKhop_BatKeChance()
        {
            var state = NewState(3, chance: 0f); // chance 0 vẫn phải xảy ra I-01
            var fixedFirst = NewIncident("I-01");
            var pick = Try(NewPool(NewIncident("I-02")), state, fixedFirst, null, NewContext());
            Assert.IsNotNull(pick);
            Assert.AreEqual("I-01", pick.Incident.incidentId);

            // Lần thứ hai không lặp lại I-01.
            var again = Try(NewPool(NewIncident("I-02")), state, fixedFirst, null, NewContext());
            Assert.IsNull(again);
        }

        [Test]
        public void I01_KhongKhopTrigger_KhongCoSuCo()
        {
            var state = NewState(3);
            var fogOnly = NewIncident("I-FOG", hazard: HazardType.Fog);
            var pick = Try(NewPool(fogOnly), state, fogOnly, null, NewContext());
            Assert.IsNull(pick);
        }

        [Test]
        public void RollXacSuot_TheoSeed_GatDi()
        {
            // Chance 0 → không bao giờ; chance 1 → luôn (khi đủ điều kiện).
            var state0 = NewState(3, chance: 0f);
            Assert.IsNull(Try(NewPool(NewIncident("I-02")), state0, null, null, NewContext()));

            var state1 = NewState(3, chance: 1f);
            Assert.IsNotNull(Try(NewPool(NewIncident("I-02")), state1, null, null, NewContext()));
        }

        [Test]
        public void XacDinhTheoSeed_ChayLaiGiongHe()
        {
            var pool = NewPool(NewIncident("I-A"), NewIncident("I-B"), NewIncident("I-C"));
            string a = SequenceString(TryAll(pool, NewState(3, seed: 42)));
            string b = SequenceString(TryAll(pool, NewState(3, seed: 42)));
            Assert.AreEqual(a, b);
        }

        [Test]
        public void TrongSoTrigger_CoLuongDatNhieuHon()
        {
            var heavy = NewIncident("I-HEAVY", weightMultiplier: 10);
            var light = NewIncident("I-LIGHT", weightMultiplier: 1);
            var pool = NewPool(heavy, light);
            int heavyCount = 0;
            for (int seed = 1; seed <= 40; seed++)
            {
                var pick = Try(pool, NewState(3, seed), null, null, NewContext());
                if (pick != null && pick.Incident.incidentId == "I-HEAVY") heavyCount++;
            }
            Assert.Greater(heavyCount, 25, "trọng số 10:1 phải thiên về I-HEAVY");
        }

        [Test]
        public void SuCoDaDung_KhongLaiChoToiKhiHet()
        {
            var pool = NewPool(NewIncident("I-A"), NewIncident("I-B"));
            var state = NewState(3);
            var p1 = Try(pool, state, null, null, NewContext());
            var p2 = Try(pool, state, null, null, NewContext());
            Assert.IsNotNull(p1);
            Assert.IsNotNull(p2);
            Assert.AreNotEqual(p1.Incident.incidentId, p2.Incident.incidentId, "còn sự cố chưa dùng thì không lặp");
            var p3 = Try(pool, state, null, null, NewContext());
            Assert.IsTrue(p3.Incident.incidentId == p1.Incident.incidentId || p3.Incident.incidentId == p2.Incident.incidentId,
                "pool cạn → fallback cho phép lặp");
        }

        [Test]
        public void TriggerKhop_HaiMoiMotKhop()
        {
            var state = NewState(3);
            // Trigger yêu cầu Fog + Fragile: context chỉ có Fog → không khớp; đủ cả hai → khớp.
            var def = ScriptableObject.CreateInstance<IncidentDef>();
            def.incidentId = "I-COMBO"; def.title = "combo"; def.weight = 1;
            def.triggers = new List<IncidentTrigger>
            {
                new IncidentTrigger { requiredHazard = HazardType.Fog, requireFragileCargo = true },
            };
            def.options = new List<IncidentOption> { new IncidentOption { label = "O", entries = new List<IncidentOutcomeEntry> { new IncidentOutcomeEntry { crew = CrewId.Any, outcome = DecisionOutcome.Normal, explanation = "e", quote = "q" } } } };

            var ctx = NewContext();
            ctx.EdgeHazards = new List<HazardType> { HazardType.Fog };
            Assert.IsNull(Try(NewPool(def), state, null, null, ctx));

            ctx.CarriedOrder = new OrderState { Id = "O-1", CustomerName = "c", Tags = OrderTag.Fragile, FromPortId = "GOC", ToPortId = "VANG", DeadlineAtSeconds = 300f, Revenue = 10, Clue = "", Status = OrderStatus.Assigned };
            Assert.IsNotNull(Try(NewPool(def), state, null, null, ctx));
        }

        [Test]
        public void Finale_WhenDeliveredIsWinMinusOne_AndShipCarryingCargo()
        {
            var finale = NewIncident("I-F");
            var state = NewState(3);
            state.DeliveredCount = state.Config.winDeliveries - 1;
            var ctx = NewContext();
            ctx.CarriedOrder = new OrderState { Id = "O-1", CustomerName = "c", Tags = OrderTag.None, FromPortId = "GOC", ToPortId = "VANG", DeadlineAtSeconds = 400f, Revenue = 10, Clue = "", Status = OrderStatus.Assigned };
            var pick = Try(NewPool(NewIncident("I-2")), state, null, finale, ctx);
            Assert.IsNotNull(pick);
            Assert.AreEqual("I-F", pick.Incident.incidentId);
            Assert.IsFalse(state.IsFinished);

            // Không chở hàng → không finale (chance 0 để tách riêng luật finale khỏi roll thường); I-F chỉ một lần.
            var stateNoCargo = NewState(3, chance: 0f);
            stateNoCargo.DeliveredCount = stateNoCargo.Config.winDeliveries - 1;
            var pickNone = Try(NewPool(NewIncident("I-2")), stateNoCargo, null, finale, NewContext());
            Assert.IsNull(pickNone);
        }

        [Test]
        public void PoolRong_KhongLoi()
        {
            var state = NewState(3);
            Assert.IsNull(Try(new List<IncidentDef>(), state, null, null, NewContext()));
        }

        [Test]
        public void CapPending_InSimulation_GioiHanSoSuCoCho()
        {
            // Ngân sách sự cố nằm ở Simulation.RollIncident (maxPendingIncidents) — kiểm qua Advance.
            var config = ScriptableObject.CreateInstance<GameConfig>();
            config.incidentChancePerEdge = 1f;
            config.maxPendingIncidents = 1;
            var state = new GameState(config);
            state.NewRun(42);
            var routes = Track(ScriptableObject.CreateInstance<RouteTable>());
            routes.pairs.Add(new RoutePairEntry
            {
                fromPortId = "A", toPortId = "B",
                options = { new RouteOption { label = "t", portPath = { "A", "M", "B" }, hazards = { HazardType.Clear, HazardType.Clear }, length = 100f } }
            });
            var world = new SimWorld
            {
                Routes = routes,
                Incidents = NewPool(NewIncident("I-A"), NewIncident("I-B"), NewIncident("I-C")),
            };
            var ship = new ShipState { Id = "S1", DisplayName = "s", Captain = CrewId.Bob, Speed = SpeedClass.Fast, Status = ShipStatus.Moving, PortId = "A", Route = routes.GetOptions("A", "B")[0] };
            state.Ships.Add(ship);

            for (int i = 0; i < 50 && state.PendingIncidents.Count < 1; i++)
                Simulation.Advance(world, state, config.simTickSeconds);
            Assert.AreEqual(1, state.PendingIncidents.Count);
            // Đồng hồ vẫn chạy nhưng không roll thêm — cap không đổi.
            int before = state.PendingIncidents.Count;
            Simulation.Advance(world, state, config.simTickSeconds * 10);
            Assert.AreEqual(before, state.PendingIncidents.Count);
        }

        static List<PendingIncident> TryAll(List<IncidentDef> pool, GameState state)
        {
            var picks = new List<PendingIncident>();
            for (int i = 0; i < 3; i++)
            {
                var p = Try(pool, state, null, null, NewContext());
                if (p != null) picks.Add(p);
            }
            return picks;
        }

        static T Track<T>(T obj) where T : Object { return obj; }

        private static string SequenceString(List<PendingIncident> picks)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var p in picks) sb.Append(p.Incident.incidentId).Append(':').Append(p.Context.Ship.Id).Append(';');
            return sb.ToString();
        }

        private static GameState NewState(int unused, int seed = 1, float chance = 1f)
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            config.incidentChancePerEdge = chance;
            var state = new GameState(config);
            state.NewRun(seed);
            return state;
        }
    }
}
