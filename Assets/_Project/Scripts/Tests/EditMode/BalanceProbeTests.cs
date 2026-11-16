using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using AyeAye;
namespace AyeAye.Tests.EditMode
{

    public class BalanceProbeTests
    {

        private TestWorld _world;
        [SetUp]

        public void SetUp()
        {
            _world = TestWorld.Create();
        }
        [TearDown]

        public void TearDown()
        {
            foreach (var so in _world.SOs)
                Object.DestroyImmediate(so);
        }
        [Test]

        public void PickByClue_PrefersSmartForCaptain()
        {
            var state = GameStateBuilder.CreateRun(_world.Config, _world.ShipDefs, 1);
            var mira = state.Ships.First(s => s.Captain == CrewId.Mira);
            var i01 = _world.FixedFirstDay; 
            var chosen = CluePolicy.PickByClue(i01, state, mira, _world.Crews);
            var resolved = IncidentResolver.Resolve(i01, chosen, state, mira, _world.Crews);
            Assert.AreEqual(DecisionOutcome.Smart, resolved.Outcome);
        }
        [Test]

        public void PickByClue_NeverPicksSlipUpWhenSafeExists()
        {
            var state = GameStateBuilder.CreateRun(_world.Config, _world.ShipDefs, 1);
            var bob = state.Ships.First(s => s.Captain == CrewId.Bob);
            var i01 = _world.FixedFirstDay; 
            var chosen = CluePolicy.PickByClue(i01, state, bob, _world.Crews);
            var resolved = IncidentResolver.Resolve(i01, chosen, state, bob, _world.Crews);
            Assert.AreEqual(DecisionOutcome.Normal, resolved.Outcome);
        }
        [Test]

        public void PickByClue_RespectsShipConditions()
        {
            var iF = _world.FixedFinale;
            var cleanState = GameStateBuilder.CreateRun(_world.Config, _world.ShipDefs, 1);
            var bobClean = cleanState.Ships.First(s => s.Captain == CrewId.Bob);
            var clean = CluePolicy.PickByClue(iF, cleanState, bobClean, _world.Crews);
            var resolvedClean = IncidentResolver.Resolve(iF, clean, cleanState, bobClean, _world.Crews);
            Assert.AreEqual(DecisionOutcome.Smart, resolvedClean.Outcome);
            var flaggedState = GameStateBuilder.CreateRun(_world.Config, _world.ShipDefs, 1);
            var bobFlagged = flaggedState.Ships.First(s => s.Captain == CrewId.Bob);
            flaggedState.TemporaryRepairShipIds.Add(bobFlagged.Id);
            var chosen = CluePolicy.PickByClue(iF, flaggedState, bobFlagged, _world.Crews);
            var resolved = IncidentResolver.Resolve(iF, chosen, flaggedState, bobFlagged, _world.Crews);
            Assert.AreEqual(DecisionOutcome.Normal, resolved.Outcome);
        }
        [Test]

        public void MeasureRates_ReportsBothPolicies()
        {
            float random = BalanceProbe.SimulateRuns(_world.Config, _world.OrderPool, _world.Routes,
                _world.Incidents, _world.FixedFirstDay, _world.FixedFinale, _world.Crews, _world.ShipDefs,
                runs: 400, seedBase: 3, cluePolicy: false);
            float clue = BalanceProbe.SimulateRuns(_world.Config, _world.OrderPool, _world.Routes,
                _world.Incidents, _world.FixedFirstDay, _world.FixedFinale, _world.Crews, _world.ShipDefs,
                runs: 400, seedBase: 3, cluePolicy: true);
            string line = $"BalanceProbe 400 ván, E2+: ngẫu nhiên = {random:P0}, đọc manh mối = {clue:P0}";
            System.IO.File.WriteAllText("Library/probe-rates.txt", line);
            UnityEngine.Debug.Log(line);
            Assert.GreaterOrEqual(random, 0f);
            Assert.LessOrEqual(random, 1f);
            Assert.GreaterOrEqual(clue, random, "Chính sách đọc manh mối phải không tệ hơn ngẫu nhiên");
        }
        [Test]

        public void SameSeed_Deterministic()
        {
            float a = BalanceProbe.SimulateRuns(_world.Config, _world.OrderPool, _world.Routes,
                _world.Incidents, _world.FixedFirstDay, _world.FixedFinale, _world.Crews, _world.ShipDefs,
                runs: 20, seedBase: 11);
            float b = BalanceProbe.SimulateRuns(_world.Config, _world.OrderPool, _world.Routes,
                _world.Incidents, _world.FixedFirstDay, _world.FixedFinale, _world.Crews, _world.ShipDefs,
                runs: 20, seedBase: 11);
            Assert.AreEqual(a, b, 0.0001f);
        }
        [Test]

        public void WinRate_InUnitInterval()
        {
            float rate = BalanceProbe.SimulateRuns(_world.Config, _world.OrderPool, _world.Routes,
                _world.Incidents, _world.FixedFirstDay, _world.FixedFinale, _world.Crews, _world.ShipDefs,
                runs: 30, seedBase: 7);
            Assert.GreaterOrEqual(rate, 0f);
            Assert.LessOrEqual(rate, 1f);
        }
        [Test]

        public void EveryRun_Finishes_WithEnding()
        {
            var world = new SimWorld
            {
                Pool = _world.OrderPool, Routes = _world.Routes,
                Incidents = _world.Incidents, FixedFirstDay = _world.FixedFirstDay, FixedFinale = _world.FixedFinale,
            };
            for (int i = 0; i < 10; i++)
            {
                var state = GameStateBuilder.CreateRun(_world.Config, _world.ShipDefs, seed: 100 + i);
                OrderScheduler.SpawnInitial(_world.OrderPool, state);
                while (state.CanAdvance && state.ElapsedSeconds < 1800f)
                    BalanceProbe.Step(world, state, _world.Crews, cluePolicy: false);
                if (!state.IsFinished) EndingEvaluator.FinishRun(state); 
                Assert.AreNotEqual(EndingId.None, state.FinalEnding, $"Seed {100 + i} thiếu ending");
            }
        }
    }
}

