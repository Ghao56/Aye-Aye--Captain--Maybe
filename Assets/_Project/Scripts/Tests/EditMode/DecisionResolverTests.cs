using NUnit.Framework;
using UnityEngine;

namespace AyeAye.Tests
{
    // Fixture dùng chung cho các test logic.
    public static class TestFixtures
    {
        public static GameConfig NewConfig()
        {
            GameConfig config = ScriptableObject.CreateInstance<GameConfig>();
            config.totalDistance = 3.2f;
            config.deadlineDay = 3;
            config.normalDistance = 1.0f;
            config.smartDistance = 1.75f;
            config.slipUpDistance = 0.4f;
            config.startingCargo = CargoCondition.Intact;
            return config;
        }

        public static GameState NewState(GameConfig config)
        {
            return new GameState(config);
        }

        public static CrewDefinition NewCrew(CrewId id, string defaultLine, string defaultQuote)
        {
            CrewDefinition crew = ScriptableObject.CreateInstance<CrewDefinition>();
            crew.id = id;
            crew.defaultNormalLine = defaultLine;
            crew.defaultQuote = defaultQuote;
            return crew;
        }

        public static CrewOutcomeEntry Entry(CrewId crew, FlagCondition condition, DecisionOutcome outcome,
                                             string explanation, string quote, FlagToSet flag, int cargoDelta)
        {
            return new CrewOutcomeEntry
            {
                crew = crew,
                condition = condition,
                outcome = outcome,
                explanation = explanation,
                quote = quote,
                setFlag = flag,
                cargoDelta = cargoDelta,
            };
        }

        public static DecisionOption Option(string label, params CrewOutcomeEntry[] entries)
        {
            DecisionOption option = new DecisionOption();
            option.label = label;
            option.entries = new System.Collections.Generic.List<CrewOutcomeEntry>(entries);
            return option;
        }

        public static DecisionOption Option(string label)
        {
            DecisionOption option = new DecisionOption();
            option.label = label;
            option.entries = new System.Collections.Generic.List<CrewOutcomeEntry>();
            return option;
        }
    }
}

namespace AyeAye
{
    using NUnit.Framework;
    using Tests;

    public class DecisionResolverTests
    {
        [Test]
        public void Resolve_ExactMatch_ReturnsEntryOutcome()
        {
            // Ngày 1, phương án B: Dex = Smart, Bob = SlipUp.
            DecisionOption optionB = TestFixtures.Option("Kiểm tra thủ công",
                TestFixtures.Entry(CrewId.Dex, FlagCondition.Always, DecisionOutcome.Smart, "Dex phát hiện dây buộc.", "Tôi đã nói là có gì đó không ổn.", FlagToSet.None, 0),
                TestFixtures.Entry(CrewId.Bob, FlagCondition.Always, DecisionOutcome.SlipUp, "Bob siết thêm hai vòng.", "Chắc chắn là tốt!", FlagToSet.None, 0));
            GameState state = TestFixtures.NewState(TestFixtures.NewConfig());
            CrewDefinition dex = TestFixtures.NewCrew(CrewId.Dex, "default line", "default quote");

            ResolvedDecision resolved = DecisionResolver.Resolve(optionB, CrewId.Dex, state, dex);

            Assert.AreEqual(DecisionOutcome.Smart, resolved.Outcome);
            Assert.AreEqual("Dex phát hiện dây buộc.", resolved.Explanation);
            Assert.AreEqual("Tôi đã nói là có gì đó không ổn.", resolved.Quote);
            Assert.AreEqual(FlagToSet.None, resolved.FlagToSet);
            Assert.AreEqual(0, resolved.CargoDelta);
        }

        [Test]
        public void Resolve_WildcardOnlyUsedWithoutExactMatch()
        {
            DecisionOption option = TestFixtures.Option("opt",
                TestFixtures.Entry(CrewId.Bob, FlagCondition.Always, DecisionOutcome.Smart, "entry bob", "q", FlagToSet.None, 0),
                TestFixtures.Entry(CrewId.Any, FlagCondition.Always, DecisionOutcome.Normal, "entry any", "q2", FlagToSet.None, 0));
            GameState state = TestFixtures.NewState(TestFixtures.NewConfig());

            ResolvedDecision bob = DecisionResolver.Resolve(option, CrewId.Bob, state, null);
            ResolvedDecision mira = DecisionResolver.Resolve(option, CrewId.Mira, state, null);

            Assert.AreEqual(DecisionOutcome.Smart, bob.Outcome);
            Assert.AreEqual(DecisionOutcome.Normal, mira.Outcome);
            Assert.AreEqual("entry any", mira.Explanation);
        }

        [Test]
        public void Resolve_WildcardWithFailedCondition_FallsToDefault()
        {
            DecisionOption option = TestFixtures.Option("opt",
                TestFixtures.Entry(CrewId.Any, FlagCondition.CargoShiftedRequired, DecisionOutcome.SlipUp, "wild slip", "q", FlagToSet.None, 0));
            GameState state = TestFixtures.NewState(TestFixtures.NewConfig());
            CrewDefinition chen = TestFixtures.NewCrew(CrewId.Chen, "chen line", "chen quote");

            ResolvedDecision resolved = DecisionResolver.Resolve(option, CrewId.Chen, state, chen);

            Assert.AreEqual(DecisionOutcome.Normal, resolved.Outcome);
            Assert.AreEqual("chen line", resolved.Explanation);
        }

        [Test]
        public void Resolve_FlagCondition_SelectsByState()
        {
            // Ngày 3, phương án C: Bob khi không có cờ = Smart; khi có cờ = SlipUp + cargoDelta -1.
            DecisionOption optionC = TestFixtures.Option("Trả lời ngắn gọn",
                TestFixtures.Entry(CrewId.Bob, FlagCondition.CargoShiftedForbidden, DecisionOutcome.Smart, "Bob trả lời ngắn gọn.", "Có! Không! Cảm ơn!", FlagToSet.None, 0),
                TestFixtures.Entry(CrewId.Bob, FlagCondition.CargoShiftedRequired, DecisionOutcome.SlipUp, "Bob làm rơi bánh.", "Tôi giúp được mà!", FlagToSet.None, -1));
            GameState state = TestFixtures.NewState(TestFixtures.NewConfig());
            CrewDefinition bob = TestFixtures.NewCrew(CrewId.Bob, "bob line", "bob quote");

            ResolvedDecision clean = DecisionResolver.Resolve(optionC, CrewId.Bob, state, bob);
            Assert.AreEqual(DecisionOutcome.Smart, clean.Outcome);
            Assert.AreEqual(0, clean.CargoDelta);

            state.CargoShifted = true;
            ResolvedDecision shifted = DecisionResolver.Resolve(optionC, CrewId.Bob, state, bob);
            Assert.AreEqual(DecisionOutcome.SlipUp, shifted.Outcome);
            Assert.AreEqual(-1, shifted.CargoDelta);
        }

        [Test]
        public void Resolve_MissingCombination_UsesCrewDefault()
        {
            GameState state = TestFixtures.NewState(TestFixtures.NewConfig());
            CrewDefinition dex = TestFixtures.NewCrew(CrewId.Dex, "Dex kiểm tra mọi thứ hai lần.", "Tôi vẫn thấy có gì đó lạ.");

            ResolvedDecision resolved = DecisionResolver.Resolve(TestFixtures.Option("empty option"), CrewId.Dex, state, dex);

            Assert.AreEqual(DecisionOutcome.Normal, resolved.Outcome);
            Assert.AreEqual("Dex kiểm tra mọi thứ hai lần.", resolved.Explanation);
            Assert.AreEqual("Tôi vẫn thấy có gì đó lạ.", resolved.Quote);
            Assert.AreEqual(FlagToSet.None, resolved.FlagToSet);
            Assert.AreEqual(0, resolved.CargoDelta);
        }

        [Test]
        public void Resolve_DoesNotMutateState()
        {
            GameState state = TestFixtures.NewState(TestFixtures.NewConfig());
            state.CargoShifted = true;
            state.TemporaryRepair = true;
            state.Progress = 5f;
            state.CurrentDay = 2;
            state.Cargo = CargoCondition.Scratched;
            DecisionOption option = TestFixtures.Option("opt",
                TestFixtures.Entry(CrewId.Bob, FlagCondition.Always, DecisionOutcome.SlipUp, "e", "q", FlagToSet.CargoShifted, -1));

            DecisionResolver.Resolve(option, CrewId.Bob, state, null);

            Assert.IsTrue(state.CargoShifted);
            Assert.IsTrue(state.TemporaryRepair);
            Assert.AreEqual(5f, state.Progress);
            Assert.AreEqual(2, state.CurrentDay);
            Assert.AreEqual(CargoCondition.Scratched, state.Cargo);
            Assert.IsNull(state.PendingResolved);
        }

        [Test]
        public void Resolve_TwoMatchingEntries_FirstInListWins()
        {
            DecisionOption option = TestFixtures.Option("opt",
                TestFixtures.Entry(CrewId.Chen, FlagCondition.Always, DecisionOutcome.SlipUp, "first", "q1", FlagToSet.None, 0),
                TestFixtures.Entry(CrewId.Chen, FlagCondition.Always, DecisionOutcome.Smart, "second", "q2", FlagToSet.None, 0));
            GameState state = TestFixtures.NewState(TestFixtures.NewConfig());

            ResolvedDecision resolved = DecisionResolver.Resolve(option, CrewId.Chen, state, null);

            Assert.AreEqual(DecisionOutcome.SlipUp, resolved.Outcome);
            Assert.AreEqual("first", resolved.Explanation);
        }
    }
}
