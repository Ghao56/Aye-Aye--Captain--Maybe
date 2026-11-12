using System.Collections.Generic;
using AyeAye;
using NUnit.Framework;
using UnityEngine;

namespace AyeAye.Tests.EditMode
{
    /// Chu kỳ 3 — IncidentScheduler: cự tô ngày, roll xác suất, ngân sách, trọng số, trigger.
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

        private static List<IncidentContext> NewContexts(int count)
        {
            var contexts = new List<IncidentContext>();
            for (int i = 0; i < count; i++)
            {
                contexts.Add(new IncidentContext
                {
                    Ship = new ShipState { Id = "S-" + i, DisplayName = "s", Captain = CrewId.Bob, Speed = SpeedClass.Normal, Condition = ShipCondition.Good, Status = ShipStatus.Moving, PortId = "GOC", Route = new RouteOption() },
                    EdgeHazards = new List<HazardType> { HazardType.Clear },
                });
            }
            return contexts;
        }

        private static List<IncidentDef> NewPool(params IncidentDef[] defs)
        {
            return new List<IncidentDef>(defs);
        }

        [Test]
        public void Ngay1_SuCoCoDinh_ChoThuyenDauTienKhop()
        {
            var state = NewState(1);
            var contexts = NewContexts(2);
            var fixedFirst = NewIncident("I-01");
            var picks = IncidentScheduler.Schedule(NewPool(NewIncident("I-02")), fixedFirst, null, state, contexts);
            Assert.AreEqual(1, picks.Count);
            Assert.AreEqual("I-01", picks[0].Incident.incidentId);
            Assert.AreEqual("S-0", picks[0].Context.Ship.Id);
        }

        [Test]
        public void Ngay1_KhongKhopTrigger_KhongCoSuCo()
        {
            var state = NewState(1);
            var fogOnly = NewIncident("I-FOG", hazard: HazardType.Fog);
            var picks = IncidentScheduler.Schedule(NewPool(fogOnly), fogOnly, null, state, NewContexts(2));
            Assert.AreEqual(0, picks.Count);
        }

        [Test]
        public void RollXacSuot_TheoSeed_GatDi()
        {
            // Chance 0 → không bao giờ; chance 1 → luôn (khi đủ điều kiện).
            var state0 = NewState(3);
            var picks0 = IncidentScheduler.Schedule(NewPool(NewIncident("I-02")), null, null, WithChance(state0, 0f), NewContexts(3));
            Assert.AreEqual(0, picks0.Count);

            var state1 = NewState(3);
            var picks1 = IncidentScheduler.Schedule(NewPool(NewIncident("I-02")), null, null, WithMax(state1, 3), NewContexts(3));
            Assert.AreEqual(3, picks1.Count);
        }

        [Test]
        public void XacDinhTheoSeed_ChayLaiGiongHe()
        {
            var pool = NewPool(NewIncident("I-A"), NewIncident("I-B"), NewIncident("I-C"));
            var a = IncidentScheduler.Schedule(pool, null, null, NewState(4, 42), NewContexts(3));
            var b = IncidentScheduler.Schedule(pool, null, null, NewState(4, 42), NewContexts(3));
            Assert.AreEqual(SequenceString(a), SequenceString(b));
        }

        [Test]
        public void NganSach_ToiDaMoiNgay()
        {
            var state = WithMax(NewState(5, 42), 1);
            var picks = IncidentScheduler.Schedule(NewPool(NewIncident("I-A"), NewIncident("I-B")), null, null, state, NewContexts(3));
            Assert.AreEqual(1, picks.Count);
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
                var picks = IncidentScheduler.Schedule(pool, null, null, NewState(2, seed), NewContexts(1));
                if (picks.Count > 0 && picks[0].Incident.incidentId == "I-HEAVY") heavyCount++;
            }
            Assert.Greater(heavyCount, 25, "trọng số 10:1 phải thiên về I-HEAVY");
        }

        [Test]
        public void SuCoDaDung_KhongLaiChoToiKhiHet()
        {
            var pool = NewPool(NewIncident("I-A"), NewIncident("I-B"));
            var state = NewState(7, 42);
            var picks = new List<PendingIncident>();
            for (int i = 0; i < 2; i++) picks.AddRange(IncidentScheduler.Schedule(pool, null, null, state, NewContexts(1)));
            Assert.AreEqual(2, picks.Count);
            Assert.AreNotEqual(picks[0].Incident.incidentId, picks[1].Incident.incidentId, "còn sự cố chưa dùng thì không lặp");
            picks.AddRange(IncidentScheduler.Schedule(pool, null, null, state, NewContexts(1)));
            Assert.That(picks[2].Incident.incidentId == picks[0].Incident.incidentId || picks[2].Incident.incidentId == picks[1].Incident.incidentId,
                "pool cạn → fallback cho phép lặp");
        }

        [Test]
        public void TriggerKhop_HaiMoiMotKhop()
        {
            var state = NewState(2, 42);
            // Trigger yêu cầu Fog + Fragile: context chỉ có Fog → không khớp; đủ cả hai → khớp.
            var def = ScriptableObject.CreateInstance<IncidentDef>();
            def.incidentId = "I-COMBO"; def.title = "combo"; def.weight = 1;
            def.triggers = new List<IncidentTrigger>
            {
                new IncidentTrigger { requiredHazard = HazardType.Fog, requireFragileCargo = true },
            };
            def.options = new List<IncidentOption> { new IncidentOption { label = "O", entries = new List<IncidentOutcomeEntry> { new IncidentOutcomeEntry { crew = CrewId.Any, outcome = DecisionOutcome.Normal, explanation = "e", quote = "q" } } } };

            var contexts = NewContexts(1);
            contexts[0].EdgeHazards = new List<HazardType> { HazardType.Fog };
            Assert.AreEqual(0, IncidentScheduler.Schedule(NewPool(def), null, null, state, contexts).Count);

            contexts[0].CarriedOrder = new OrderState { Id = "O-1", CustomerName = "c", Tags = OrderTag.Fragile, FromPortId = "GOC", ToPortId = "VANG", DeadlineDay = 3, Revenue = 10, Clue = "", Status = OrderStatus.Assigned };
            Assert.AreEqual(1, IncidentScheduler.Schedule(NewPool(def), null, null, state, contexts).Count);
        }

        [Test]
        public void FinaleNgayCuoi_NeuCoTauDangChoHang()
        {
            var finale = NewIncident("I-F");
            var state = NewState(8, 42);
            var contexts = NewContexts(1);
            contexts[0].CarriedOrder = new OrderState { Id = "O-1", CustomerName = "c", Tags = OrderTag.None, FromPortId = "GOC", ToPortId = "VANG", DeadlineDay = 8, Revenue = 10, Clue = "", Status = OrderStatus.Assigned };
            var picks = IncidentScheduler.Schedule(NewPool(NewIncident("I-2")), null, finale, state, contexts);
            Assert.AreEqual(1, picks.Count);
            Assert.AreEqual("I-F", picks[0].Incident.incidentId);
            Assert.IsFalse(state.IsFinished);

            // Không chở hàng → không finale, không sự cố nào khác trong ngày cuối
            var picksNone = IncidentScheduler.Schedule(NewPool(NewIncident("I-2")), null, finale, state, NewContexts(1));
            Assert.AreEqual(0, picksNone.Count);
        }

        [Test]
        public void PoolRong_HoacKhongTauChuyenDong_KhongLoi()
        {
            var state = NewState(2, 42);
            Assert.AreEqual(0, IncidentScheduler.Schedule(new List<IncidentDef>(), null, null, state, NewContexts(2)).Count);
            Assert.AreEqual(0, IncidentScheduler.Schedule(NewPool(NewIncident("I-A")), null, null, state, new List<IncidentContext>()).Count);
        }

        private static string SequenceString(List<PendingIncident> picks)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var p in picks) sb.Append(p.Incident.incidentId).Append(':').Append(p.Context.Ship.Id).Append(';');
            return sb.ToString();
        }

        private static GameState NewState(int day, int seed = 1)
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            config.gameLengthDays = 8;
            config.incidentChancePerMovingShip = 1f;
            config.maxIncidentsPerDay = 2;
            var state = new GameState(config);
            state.NewRun(seed);
            state.CurrentDay = day;
            return state;
        }

        private static GameState WithChance(GameState state, float chance)
        {
            state.Config.incidentChancePerMovingShip = chance;
            return state;
        }

        private static GameState WithMax(GameState state, int max)
        {
            state.Config.maxIncidentsPerDay = max;
            return state;
        }
    }
}
