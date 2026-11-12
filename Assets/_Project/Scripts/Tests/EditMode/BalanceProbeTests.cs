using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using AyeAye;

namespace AyeAye.Tests.EditMode
{
    /// <summary>
    /// BalanceProbe: kiểm cơ chế mô phỏng 8 ngày với chính sách ngẫu nhiên.
    /// (Ngưỡng NFR-07 với chính sách đọc manh mối → Bước 6.)
    /// </summary>
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
            // Chạy thủ công từng seed để bắt được ván bị treo (guard 2×số ngày).
            for (int i = 0; i < 10; i++)
            {
                var state = GameStateBuilder.CreateRun(_world.Config, _world.ShipDefs, seed: 100 + i);
                while (!state.IsFinished && state.CurrentDay <= 2 * _world.Config.gameLengthDays)
                    BalanceProbe.SimulateDay(_world.Config, _world.OrderPool, _world.Routes,
                        _world.Incidents, _world.FixedFirstDay, _world.FixedFinale, _world.Crews, state);
                Assert.IsTrue(state.IsFinished, $"Seed {100 + i} bị treo sau guard");
                Assert.AreNotEqual(EndingId.None, state.FinalEnding, $"Seed {100 + i} thiếu ending");
            }
        }
    }
}
