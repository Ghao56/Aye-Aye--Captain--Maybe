using AyeAye.Tests;
using NUnit.Framework;
using UnityEngine;

namespace AyeAye
{
    public class CargoTrackerTests
    {
        static GameState NewStateWithCargo(CargoCondition cargo)
        {
            GameConfig config = TestFixtures.NewConfig();
            config.startingCargo = cargo;
            return TestFixtures.NewState(config);
        }

        static ResolvedDecision Resolved(FlagToSet flag, int cargoDelta)
        {
            return new ResolvedDecision
            {
                Outcome = DecisionOutcome.Normal,
                Explanation = "e",
                Quote = "q",
                FlagToSet = flag,
                CargoDelta = cargoDelta,
                CrewName = "BOB",
                OptionLabel = "opt",
            };
        }

        [Test]
        public void Apply_CargoDeltaMinusOne_LowersOneLevel()
        {
            GameState intact = NewStateWithCargo(CargoCondition.Intact);
            CargoTracker.Apply(intact, Resolved(FlagToSet.None, -1));
            Assert.AreEqual(CargoCondition.Scratched, intact.Cargo);

            GameState scratched = NewStateWithCargo(CargoCondition.Scratched);
            CargoTracker.Apply(scratched, Resolved(FlagToSet.None, -1));
            Assert.AreEqual(CargoCondition.Ruined, scratched.Cargo);
        }

        [Test]
        public void Apply_NeverGoesBelowRuined()
        {
            GameState state = NewStateWithCargo(CargoCondition.Ruined);
            CargoTracker.Apply(state, Resolved(FlagToSet.None, -1));
            Assert.AreEqual(CargoCondition.Ruined, state.Cargo);

            CargoTracker.Apply(state, Resolved(FlagToSet.None, -5));
            Assert.AreEqual(CargoCondition.Ruined, state.Cargo);
        }

        [Test]
        public void Apply_ZeroDelta_KeepsCargo()
        {
            GameState state = NewStateWithCargo(CargoCondition.Scratched);
            CargoTracker.Apply(state, Resolved(FlagToSet.None, 0));
            Assert.AreEqual(CargoCondition.Scratched, state.Cargo);
        }

        [Test]
        public void Apply_SetsFlags()
        {
            GameState state = TestFixtures.NewState(TestFixtures.NewConfig());
            CargoTracker.Apply(state, Resolved(FlagToSet.CargoShifted, 0));
            Assert.IsTrue(state.CargoShifted);
            Assert.IsFalse(state.TemporaryRepair);

            CargoTracker.Apply(state, Resolved(FlagToSet.TemporaryRepair, 0));
            Assert.IsTrue(state.TemporaryRepair);
        }

        [Test]
        public void Apply_FlagsAreNeverCleared()
        {
            GameState state = TestFixtures.NewState(TestFixtures.NewConfig());
            CargoTracker.Apply(state, Resolved(FlagToSet.CargoShifted, 0));
            CargoTracker.Apply(state, Resolved(FlagToSet.None, 0));
            Assert.IsTrue(state.CargoShifted);
        }
    }
}
