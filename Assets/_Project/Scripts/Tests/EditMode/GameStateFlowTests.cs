using System.Collections.Generic;
using AyeAye.Tests;
using NUnit.Framework;
using UnityEngine;

namespace AyeAye
{
    // Mô phỏng đủ 3 ngày bằng logic thuần (không UI): luồng ngày, thắng/thua, Reset sạch.
    public class GameStateFlowTests
    {
        // Chạy trọn một ngày bằng logic thuần: Resolve → CargoTracker → cộng quãng đường → Evaluate.
        static void RunDay(GameState state, GameConfig config, DecisionOption option, CrewId crew, CrewDefinition crewDef)
        {
            ResolvedDecision resolved = DecisionResolver.Resolve(option, crew, state, crewDef);
            state.PendingResolved = resolved;
            CargoTracker.Apply(state, resolved);
            state.Progress += TravelProgress.GetDistance(resolved.Outcome, config);
            DeliveryEvaluator.Evaluate(state);
        }

        [Test]
        public void Flow_ThreeNormalDays_FinishesOnDayThree()
        {
            GameConfig config = TestFixtures.NewConfig();
            GameState state = TestFixtures.NewState(config);
            CrewDefinition chen = TestFixtures.NewCrew(CrewId.Chen, "chen line", "chen quote");
            DecisionOption normal = TestFixtures.Option("normal", TestFixtures.Entry(CrewId.Any, FlagCondition.Always, DecisionOutcome.Normal, "e", "q", FlagToSet.None, 0));

            RunDay(state, config, normal, CrewId.Chen, chen);
            Assert.AreEqual(2, state.CurrentDay);
            Assert.IsFalse(state.IsFinished);

            RunDay(state, config, normal, CrewId.Chen, chen);
            Assert.AreEqual(3, state.CurrentDay);
            Assert.IsFalse(state.IsFinished);

            RunDay(state, config, normal, CrewId.Chen, chen);
            Assert.IsTrue(state.IsFinished);
            Assert.AreEqual(EndingId.LateButIntact, state.FinalEnding);
        }

        [Test]
        public void Flow_TwoSmartDays_FinishesEarlyOnDayTwo()
        {
            GameConfig config = TestFixtures.NewConfig();
            GameState state = TestFixtures.NewState(config);
            CrewDefinition dex = TestFixtures.NewCrew(CrewId.Dex, "dex line", "dex quote");
            DecisionOption smart = TestFixtures.Option("smart", TestFixtures.Entry(CrewId.Any, FlagCondition.Always, DecisionOutcome.Smart, "e", "q", FlagToSet.None, 0));

            RunDay(state, config, smart, CrewId.Dex, dex);
            Assert.IsFalse(state.IsFinished);

            RunDay(state, config, smart, CrewId.Dex, dex);
            Assert.IsTrue(state.IsFinished);
            Assert.AreEqual(EndingId.PerfectDelivery, state.FinalEnding);
            Assert.IsTrue(state.ReachedEarly);
        }

        [Test]
        public void Flow_FlagChain_Day2BobCargoShifted_Day3BobOptionC_SlipsAndScratchesCargo()
        {
            GameConfig config = TestFixtures.NewConfig();
            GameState state = TestFixtures.NewState(config);
            CrewDefinition bob = TestFixtures.NewCrew(CrewId.Bob, "bob line", "bob quote");

            // Ngày 1: không entry khớp Bob → Normal mặc định (1.0).
            DecisionOption day1A = TestFixtures.Option("A", TestFixtures.Entry(CrewId.Any, FlagCondition.Always, DecisionOutcome.Normal, "e", "q", FlagToSet.None, 0));
            RunDay(state, config, day1A, CrewId.Bob, bob);
            Assert.AreEqual(2, state.CurrentDay);

            // Ngày 2, phương án B: Bob → Smart + đặt cờ CargoShifted.
            DecisionOption day2B = TestFixtures.Option("B", TestFixtures.Entry(CrewId.Bob, FlagCondition.Always, DecisionOutcome.Smart, "e", "q", FlagToSet.CargoShifted, 0));
            RunDay(state, config, day2B, CrewId.Bob, bob);
            Assert.IsTrue(state.CargoShifted);
            Assert.AreEqual(3, state.CurrentDay);

            // Ngày 3, phương án C: Bob với cờ → SlipUp, Hàng xuống một mức.
            DecisionOption day3C = TestFixtures.Option("C",
                TestFixtures.Entry(CrewId.Bob, FlagCondition.CargoShiftedForbidden, DecisionOutcome.Smart, "clean", "q", FlagToSet.None, 0),
                TestFixtures.Entry(CrewId.Bob, FlagCondition.CargoShiftedRequired, DecisionOutcome.SlipUp, "slip", "q", FlagToSet.None, -1));
            RunDay(state, config, day3C, CrewId.Bob, bob);

            Assert.AreEqual(CargoCondition.Scratched, state.Cargo);
            // 1.0 (Normal ngày 1) + 1.75 (Smart ngày 2) + 0.4 (SlipUp ngày 3) = 3.15 < 3.2 → trễ.
            Assert.AreEqual(3.15f, state.Progress, 0.0001f);
            Assert.IsTrue(state.IsFinished);
            Assert.AreEqual(EndingId.LateDelivery, state.FinalEnding);
        }

        [Test]
        public void Reset_RestoresInitialValues()
        {
            GameConfig config = TestFixtures.NewConfig();
            GameState state = TestFixtures.NewState(config);
            CrewDefinition bob = TestFixtures.NewCrew(CrewId.Bob, "bob line", "bob quote");

            DecisionOption shift = TestFixtures.Option("shift", TestFixtures.Entry(CrewId.Any, FlagCondition.Always, DecisionOutcome.SlipUp, "e", "q", FlagToSet.CargoShifted, -1));
            RunDay(state, config, shift, CrewId.Bob, bob);

            state.Reset();

            Assert.AreEqual(1, state.CurrentDay);
            Assert.AreEqual(0f, state.Progress);
            Assert.AreEqual(CargoCondition.Intact, state.Cargo);
            Assert.IsFalse(state.CargoShifted);
            Assert.IsFalse(state.TemporaryRepair);
            Assert.IsNull(state.PendingResolved);
            Assert.IsFalse(state.IsFinished);
            Assert.IsNull(state.FinalEnding);
            Assert.IsFalse(state.ReachedEarly);
        }

        [Test]
        public void NewRun_ResetsEventStateAndSeed()
        {
            GameConfig config = TestFixtures.NewConfig();
            GameState state = new GameState(config, 42);
            state.CurrentDay = 3;
            state.Progress = 5f;
            state.CargoShifted = true;
            DayDefinition used = ScriptableObject.CreateInstance<DayDefinition>();
            used.eventId = "used1";
            state.UsedEvents.Add(used);
            state.CurrentEvent = used;

            state.NewRun(42); // cùng seed: RNG mới nhưng chuỗi xác định

            Assert.AreEqual(42, state.Seed);
            Assert.IsNotNull(state.Rng);
            Assert.AreEqual(1, state.CurrentDay);
            Assert.AreEqual(0f, state.Progress);
            Assert.IsFalse(state.CargoShifted);
            Assert.IsNull(state.CurrentEvent);
            Assert.AreEqual(0, state.UsedEvents.Count);
        }

        [Test]
        public void Flow_SevenDays_WithScheduler_FinishesDeterministically()
        {
            // Mô phỏng ván 7 ngày: pool 6 event, ngày 1 opening, ngày cuối finale cố định.
            GameConfig config = TestFixtures.NewConfig();
            config.totalDistance = 7.2f;
            config.deadlineDay = 7;
            GameState state = new GameState(config, 4242);
            CrewDefinition chen = TestFixtures.NewCrew(CrewId.Chen, "chen line", "chen quote");
            DecisionOption normal = TestFixtures.Option("normal", TestFixtures.Entry(CrewId.Any, FlagCondition.Always, DecisionOutcome.Normal, "e", "q", FlagToSet.None, 0));

            List<DayDefinition> pool = new List<DayDefinition>();
            for (int i = 0; i < 6; i++)
            {
                DayDefinition day = ScriptableObject.CreateInstance<DayDefinition>();
                day.eventId = "ev" + (i + 1);
                day.title = day.eventId;
                day.decision = ScriptableObject.CreateInstance<DecisionDefinition>();
                day.decision.options = new System.Collections.Generic.List<DecisionOption> { normal };
                pool.Add(day);
            }

            string[] sequence = new string[7];
            for (int day = 1; day <= 7; day++)
            {
                Assert.AreEqual(day, state.CurrentDay);
                DayDefinition current = EventScheduler.Pick(pool, state.UsedEvents, state, state.Rng);
                Assert.IsNotNull(current, "Ngày " + day + " không được kẹt vì thiếu event.");
                state.CurrentEvent = current;
                state.UsedEvents.Add(current);
                sequence[day - 1] = current.eventId;

                RunDay(state, config, current.decision.options[0], CrewId.Chen, chen);
            }

            Assert.IsTrue(state.IsFinished);
            // 7 ngày Normal = 7.0 < 7.2 → trễ nhưng hoàn tất (NFR-07: suýt thua).
            Assert.AreEqual(EndingId.LateButIntact, state.FinalEnding);

            // Cùng seed → cùng chuỗi 7 event.
            GameState replay = new GameState(config, 4242);
            for (int day = 1; day <= 7; day++)
            {
                DayDefinition current = EventScheduler.Pick(pool, replay.UsedEvents, replay, replay.Rng);
                replay.UsedEvents.Add(current);
                Assert.AreEqual(sequence[day - 1], current.eventId);
                RunDay(replay, config, current.decision.options[0], CrewId.Chen, chen);
            }
            Assert.IsTrue(replay.IsFinished);
        }

        [Test]
        public void Restart_RunsIdenticalWithSameChoices()
        {
            GameConfig config = TestFixtures.NewConfig();
            CrewDefinition bob = TestFixtures.NewCrew(CrewId.Bob, "bob line", "bob quote");
            DecisionOption smart = TestFixtures.Option("smart", TestFixtures.Entry(CrewId.Any, FlagCondition.Always, DecisionOutcome.Smart, "e", "q", FlagToSet.None, 0));

            GameState first = TestFixtures.NewState(config);
            RunDay(first, config, smart, CrewId.Bob, bob);
            RunDay(first, config, smart, CrewId.Bob, bob);

            GameState second = TestFixtures.NewState(config);
            RunDay(second, config, smart, CrewId.Bob, bob);
            RunDay(second, config, smart, CrewId.Bob, bob);

            Assert.AreEqual(first.CurrentDay, second.CurrentDay);
            Assert.AreEqual(first.Progress, second.Progress);
            Assert.AreEqual(first.Cargo, second.Cargo);
            Assert.AreEqual(first.IsFinished, second.IsFinished);
            Assert.AreEqual(first.FinalEnding, second.FinalEnding);
        }
    }
}
