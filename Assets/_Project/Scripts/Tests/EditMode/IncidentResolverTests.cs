using System.Collections.Generic;
using AyeAye;
using NUnit.Framework;
using UnityEngine;

namespace AyeAye.Tests.EditMode
{
    /// Chu kỳ 3 — IncidentResolver: tra bảng (captain, option) + áp dụng hệ quả.
    public class IncidentResolverTests
    {
        private static CrewDefinition NewCrew(CrewId id, string name)
        {
            var c = ScriptableObject.CreateInstance<CrewDefinition>();
            c.id = id; c.displayName = name;
            c.defaultNormalLine = $"[{name}] vẫn làm đúng quy trình.";
            c.defaultQuote = $"[{name}] Aye aye, boss.";
            return c;
        }

        private static List<CrewDefinition> NewCrews()
        {
            return new List<CrewDefinition>
            {
                NewCrew(CrewId.Bob, "Bob"), NewCrew(CrewId.Mira, "Mira"),
                NewCrew(CrewId.Chen, "Chen"), NewCrew(CrewId.Dex, "Dex"),
            };
        }

        private static IncidentDef NewDef(params IncidentOption[] options)
        {
            var def = ScriptableObject.CreateInstance<IncidentDef>();
            def.incidentId = "I-T"; def.title = "Sự cố thử nghiệm";
            def.options = new List<IncidentOption>(options);
            return def;
        }

        private static ShipState NewShip(CrewId captain)
        {
            return new ShipState
            {
                Id = "S-1", DisplayName = "Tia Chớp", Captain = captain,
                Speed = SpeedClass.Normal, Condition = ShipCondition.Good,
                Status = ShipStatus.Moving, PortId = "GOC", Route = new RouteOption(),
            };
        }

        [Test]
        public void Resolve_EntryKhopDungThuyen_Thang()
        {
            var def = NewDef(new IncidentOption
            {
                label = "Đâm thẳng", baseProgressDelta = 0.3f,
                entries = new List<IncidentOutcomeEntry>
                {
                    new IncidentOutcomeEntry { crew = CrewId.Bob, outcome = DecisionOutcome.Smart, explanation = "Bob bay qua sương mù.", quote = "\"Tốc độ là tất cả!\"" },
                    new IncidentOutcomeEntry { crew = CrewId.Any, outcome = DecisionOutcome.Normal, explanation = "Bình thường.", quote = "" },
                },
            });
            var state = new GameState(NewConfig());
            var ship = NewShip(CrewId.Bob);
            var resolved = IncidentResolver.Resolve(def, def.options[0], state, ship, NewCrews());
            Assert.AreEqual(DecisionOutcome.Smart, resolved.Outcome);
            Assert.AreEqual("Bob bay qua sương mù.", resolved.Explanation);
            Assert.AreEqual("Bob", resolved.CrewName);
            // Outcome Δ = baseProgressDelta + config.OutcomeDelta(Smart) = 0.3 + 0.5
            Assert.AreEqual(0.8f, resolved.ProgressDelta, 0.0001f);
        }

        [Test]
        public void Resolve_KhongCoEntryDungThuyen_DungEntryAny()
        {
            var def = NewDef(new IncidentOption
            {
                label = "Đâm thẳng",
                entries = new List<IncidentOutcomeEntry>
                {
                    new IncidentOutcomeEntry { crew = CrewId.Mira, outcome = DecisionOutcome.Smart, explanation = "Mira.", quote = "" },
                    new IncidentOutcomeEntry { crew = CrewId.Any, outcome = DecisionOutcome.Normal, explanation = "Wildcard.", quote = "" },
                },
            });
            var state = new GameState(NewConfig());
            var resolved = IncidentResolver.Resolve(def, def.options[0], state, NewShip(CrewId.Dex), NewCrews());
            Assert.AreEqual(DecisionOutcome.Normal, resolved.Outcome);
            Assert.AreEqual("Wildcard.", resolved.Explanation);
        }

        [Test]
        public void Resolve_KhongKhop_GanMacDinhNormalTheoCrew()
        {
            var def = NewDef(new IncidentOption
            {
                label = "Đâm thẳng",
                entries = new List<IncidentOutcomeEntry>
                {
                    new IncidentOutcomeEntry { crew = CrewId.Mira, outcome = DecisionOutcome.Smart, explanation = "Mira.", quote = "" },
                },
            });
            var state = new GameState(NewConfig());
            var resolved = IncidentResolver.Resolve(def, def.options[0], state, NewShip(CrewId.Bob), NewCrews());
            Assert.AreEqual(DecisionOutcome.Normal, resolved.Outcome);
            Assert.AreEqual("Bob", resolved.CrewName);
            Assert.AreEqual("[Bob] vẫn làm đúng quy trình.", resolved.Explanation);
            Assert.AreEqual("[Bob] Aye aye, boss.", resolved.Quote);
            // Normal: Δ = base 0
            Assert.AreEqual(0f, resolved.ProgressDelta, 0.0001f);
        }

        [Test]
        public void Apply_CargoDeltaHaMotBac_VaKhongXuongDuoiRuined()
        {
            var entry = new IncidentOutcomeEntry { crew = CrewId.Any, cargoDelta = -1 };
            var def = NewDef(new IncidentOption { label = "O", entries = new List<IncidentOutcomeEntry> { entry } });
            var state = new GameState(NewConfig());
            var ship = NewShip(CrewId.Bob);
            var order = new OrderState { Id = "O-1", CustomerName = "c", Tags = OrderTag.None, FromPortId = "GOC", ToPortId = "VANG", DeadlineDay = 3, Revenue = 100, Clue = "", Status = OrderStatus.Assigned };
            ship.OrderId = order.Id;
            state.Orders.Add(order);

            IncidentResolver.Apply(IncidentResolver.Resolve(def, def.options[0], state, ship, NewCrews()), state, ship);
            Assert.AreEqual(CargoCondition.Scratched, order.Cargo);

            IncidentResolver.Apply(IncidentResolver.Resolve(def, def.options[0], state, ship, NewCrews()), state, ship);
            IncidentResolver.Apply(IncidentResolver.Resolve(def, def.options[0], state, ship, NewCrews()), state, ship);
            Assert.AreEqual(CargoCondition.Ruined, order.Cargo);
        }

        [Test]
        public void Apply_GanFlag_TheoId()
        {
            var def = NewDef(new IncidentOption
            {
                label = "O",
                entries = new List<IncidentOutcomeEntry>
                {
                    new IncidentOutcomeEntry { crew = CrewId.Any, setFlag = FlagToSet.CargoShifted, cargoDelta = -1 },
                },
            });
            var state = new GameState(NewConfig());
            var ship = NewShip(CrewId.Bob);
            ship.OrderId = "O-9";
            IncidentResolver.Apply(IncidentResolver.Resolve(def, def.options[0], state, ship, NewCrews()), state, ship);
            Assert.That(state.CargoShiftedOrderIds.Contains("O-9"));

            // Không chở hàng → không gắn CargoShifted
            var ship2 = NewShip(CrewId.Dex);
            ship2.Id = "S-2";
            IncidentResolver.Apply(IncidentResolver.Resolve(def, def.options[0], state, ship2, NewCrews()), state, ship2);
            Assert.AreEqual(1, state.CargoShiftedOrderIds.Count);

            // TemporaryRepair gắn theo thuyền
            var def2 = NewDef(new IncidentOption
            {
                label = "O",
                entries = new List<IncidentOutcomeEntry>
                {
                    new IncidentOutcomeEntry { crew = CrewId.Any, setFlag = FlagToSet.TemporaryRepair },
                },
            });
            IncidentResolver.Apply(IncidentResolver.Resolve(def2, def2.options[0], state, ship2, NewCrews()), state, ship2);
            Assert.That(state.TemporaryRepairShipIds.Contains("S-2"));
        }

        [Test]
        public void Apply_RevenueDelta_VaKhongAm()
        {
            var def = NewDef(new IncidentOption
            {
                label = "O",
                entries = new List<IncidentOutcomeEntry>
                {
                    new IncidentOutcomeEntry { crew = CrewId.Any, revenueDelta = -20 },
                },
            });
            var state = new GameState(NewConfig());
            state.Revenue = 10;
            var ship = NewShip(CrewId.Bob);
            IncidentResolver.Apply(IncidentResolver.Resolve(def, def.options[0], state, ship, NewCrews()), state, ship);
            Assert.AreEqual(0, state.Revenue);

            var def2 = NewDef(new IncidentOption
            {
                label = "O",
                entries = new List<IncidentOutcomeEntry>
                {
                    new IncidentOutcomeEntry { crew = CrewId.Any, revenueDelta = 15 },
                },
            });
            IncidentResolver.Apply(IncidentResolver.Resolve(def2, def2.options[0], state, ship, NewCrews()), state, ship);
            Assert.AreEqual(15, state.Revenue);
        }

        [Test]
        public void Apply_ConditionDown_XuongWorn_VaDung()
        {
            var def = NewDef(new IncidentOption
            {
                label = "O",
                entries = new List<IncidentOutcomeEntry>
                {
                    new IncidentOutcomeEntry { crew = CrewId.Any, conditionDown = true },
                },
            });
            var state = new GameState(NewConfig());
            var ship = NewShip(CrewId.Bob);
            IncidentResolver.Apply(IncidentResolver.Resolve(def, def.options[0], state, ship, NewCrews()), state, ship);
            Assert.AreEqual(ShipCondition.Worn, ship.Condition);
            IncidentResolver.Apply(IncidentResolver.Resolve(def, def.options[0], state, ship, NewCrews()), state, ship);
            Assert.AreEqual(ShipCondition.Worn, ship.Condition);
        }

        [Test]
        public void ValidateDef_DuBanLaOk_ThieuLaBaoLoi()
        {
            var crews = NewCrews();
            // Bảng kiểu §5.5: mỗi thuyền trưởng có đúng một (option, outcome) xác định; option không khớp → Normal mặc định.
            var ok = NewDef(
                new IncidentOption
                {
                    label = "A", baseProgressDelta = 0.3f,
                    entries = new List<IncidentOutcomeEntry>
                    {
                        new IncidentOutcomeEntry { crew = CrewId.Bob, outcome = DecisionOutcome.SlipUp, explanation = "e", quote = "q" },
                    },
                },
                new IncidentOption
                {
                    label = "B",
                    entries = new List<IncidentOutcomeEntry>
                    {
                        new IncidentOutcomeEntry { crew = CrewId.Any, outcome = DecisionOutcome.Normal, explanation = "e", quote = "q" },
                        new IncidentOutcomeEntry { crew = CrewId.Chen, outcome = DecisionOutcome.Smart, explanation = "e", quote = "q" },
                    },
                },
                new IncidentOption
                {
                    label = "C",
                    entries = new List<IncidentOutcomeEntry>
                    {
                        new IncidentOutcomeEntry { crew = CrewId.Mira, outcome = DecisionOutcome.Smart, explanation = "e", quote = "q" },
                        new IncidentOutcomeEntry { crew = CrewId.Dex, outcome = DecisionOutcome.SlipUp, explanation = "e", quote = "q" },
                    },
                });
            Assert.IsEmpty(IncidentResolver.ValidateDef(ok, crews));

            var emptyOption = NewDef(new IncidentOption { label = "O", entries = new List<IncidentOutcomeEntry>() });
            Assert.IsNotEmpty(IncidentResolver.ValidateDef(emptyOption, crews));

            // Thiếu SlipUp cho toàn bộ sự cố
            var noSlipUp = NewDef(
                new IncidentOption
                {
                    label = "O",
                    entries = new List<IncidentOutcomeEntry>
                    {
                        new IncidentOutcomeEntry { crew = CrewId.Any, outcome = DecisionOutcome.Normal, explanation = "e", quote = "q" },
                        new IncidentOutcomeEntry { crew = CrewId.Any, outcome = DecisionOutcome.Smart, explanation = "e", quote = "q" },
                    },
                });
            Assert.IsNotEmpty(IncidentResolver.ValidateDef(noSlipUp, crews));

            // Thiếu hẳn một thuyền trưởng (Dex không có entry nào)
            var missingCaptain = NewDef(
                new IncidentOption
                {
                    label = "O",
                    entries = new List<IncidentOutcomeEntry>
                    {
                        new IncidentOutcomeEntry { crew = CrewId.Bob, outcome = DecisionOutcome.Smart, explanation = "e", quote = "q" },
                        new IncidentOutcomeEntry { crew = CrewId.Mira, outcome = DecisionOutcome.Normal, explanation = "e", quote = "q" },
                        new IncidentOutcomeEntry { crew = CrewId.Chen, outcome = DecisionOutcome.SlipUp, explanation = "e", quote = "q" },
                    },
                });
            Assert.IsNotEmpty(IncidentResolver.ValidateDef(missingCaptain, crews));
        }

        private static GameConfig NewConfig()
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            config.gameLengthDays = 8;
            return config;
        }
    }
}
