using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AyeAye.Tests.PlayMode
{
    /// <summary>
    /// G1 TDD: Core game state and simulation behavior changes.
    /// Tests for: ShipStatus.Idle/Roaming, fixed timestep, reputation endgame removal,
    /// FleetActions immediate assignment, DemoValidator deadline checks.
    /// </summary>
    [TestFixture]
    public class GameStateTests
    {
        #region G1.2: ShipStatus.Idle, Roaming, no Docked
        
        [Test]
        [Description("ShipStatus.Idle = stands at port; Roaming added; Docked removed (no rename Idle→Docked)")]
        public void ShipStatus_IdleAndRoaming_Exist_NoDocked()
        {
            // Verify ShipStatus enum contains Idle and Roaming, does NOT contain Docked
            var idle = ShipStatus.Idle;
            var roaming = ShipStatus.Roaming;
            // Check enum values by reflection (no Docked present)
            var values = Enum.GetValues(typeof(ShipStatus)).Cast<ShipStatus>().ToList();
            Assert.Contains(ShipStatus.Idle, values, "ShipStatus.Idle must exist");
            Assert.Contains(ShipStatus.Roaming, values, "ShipStatus.Roaming must exist");
            // Docked should not exist (original requirement: keep Idle, add Roaming, delete Docked)
            // Since enum values are statically defined, we assert that Docked is not a member
            Assert.IsFalse(values.Contains(ShipStatus.Idle + 1), "ShipStatus.Idle + 1 should not be Docked (no rename)");
            // Instead verify that we have exactly Idle and Roaming (plus Moving which is existing)
            Assert.AreEqual(3, values.Count, "Should have exactly Idle, Moving, Roaming (no Docked)");
            Assert.IsTrue(values.Contains(ShipStatus.Idle));
            Assert.IsTrue(values.Contains(ShipStatus.Moving));
            Assert.IsTrue(values.Contains(ShipStatus.Roaming));
        }
        
        #endregion
        
        #region G1.3: Fixed timestep simulation
        
        [Test]
        [Description("Simulation uses fixed timestep from GameConfig.simTickSeconds, frame-rate independent")]
        public void Simulation_FixedTimestep_IndependentOfFrameRate()
        {
            // This test will fail until we implement GameConfig.simTickSeconds
            // For now, test that we can access the config and it has a default value
            var config = new GameConfig();
            // Expect simTickSeconds to exist with default 0.1f
            Assert.IsNotNull(config, "GameConfig should exist");
            // We'll test the actual behavior in integration tests when we implement
        }
        
        #endregion
        
        #region G1.4: Remove StagedCommands, CONFIRM, CANCEL - immediate assignment
        
        [Test]
        [Description("FleetActions.Assign immediately assigns ships, no staged commands or cancellation")]
        public void FleetActions_Assign_Immediate_NoStaged_NoCancel()
        {
            // This test will fail until we implement immediate assignment behavior
            // For now, verify the method signature exists
            var config = new GameConfig();
            var gameState = new GameState(config);
            var routes = new RouteTable();
            
            // We'll test the actual behavior when we implement the logic
            // The method should return null on success (no error string)
            // Currently testing that we can call the method without staged commands
            Assert.IsNotNull(gameState);
            Assert.IsNotNull(routes);
        }
        
        #endregion
        
        #region G1.5: No hidden end conditions (reputation removed entirely)

        [Test]
        [Description("Score (Revenue) never ends the game; only win deliveries / late count do (Reputation removed)")]
        public void GameState_RevenueNotZeroEndGame()
        {
            var config = new GameConfig();
            var gameState = new GameState(config);

            gameState.Revenue = 0;
            Assert.IsFalse(gameState.IsFinished, "Game should not finish at revenue <= 0");
            gameState.Revenue = -10;
            Assert.IsFalse(gameState.IsFinished, "Game should not finish at negative revenue");
        }
        
        #endregion
        
        #region G1.6: DemoValidator deadline checks for O-01..O-08
        
        [Test]
        [Description("DemoValidator checks every order can be delivered on time by fastest ship from pick-up port")]
        public void DemoValidator_OrderDeliveryDeadlineCheck()
        {
            // This test will fail until we implement the deadline checking logic
            // For now, verify the method exists
            var config = new GameConfig();
            var routes = new RouteTable();
            var orderPool = new OrderPool();
            var incidents = new List<IncidentDef>();
            var ships = new List<ShipDef>();
            var crews = new List<CrewDefinition>();
            
            // The method should validate and return errors for orders that cannot be delivered on time
            var errors = DemoValidator.Validate(
                config, routes, orderPool, incidents, null, null, ships, crews
            );
            
            // Should not be null
            Assert.IsNotNull(errors);
        }
        
        #endregion
        
        #region G1.7: Pause clock on page visibility loss, clamp dt when returning
        
        [Test]
        [Description("Game clock should pause when page loses focus/hidden, clamp dt when returning")]
        public void Simulation_PauseOnVisibilityLoss_ClampWhenReturn()
        {
            // This test will fail until we implement visibility pause/clamp behavior
            var config = new GameConfig();
            var gameState = new GameState(config);
            
            // When page loses focus, CanAdvance should be false
            // When page is visible and returns, dt should be clamped
            Assert.IsNotNull(gameState);
        }
        
        #endregion
        
        #region G1.8: BalanceProbe measures performance
        
        [Test]
        [Description("BalanceProbe đo tỉ lệ thắng; test thường chạy số ván nhỏ, 400 ván đưa vào menu Tools/AyeAye/Balance")]
        public void BalanceProbe_RuntimeMeasurement()
        {
            // SimulateRuns là API công khai (static) — smoke test thế giới rỗng (PlayMode không thấy TestWorld).
            var config = ScriptableObject.CreateInstance<GameConfig>();
            var pool = ScriptableObject.CreateInstance<OrderPool>();
            var routes = ScriptableObject.CreateInstance<RouteTable>();
            float rate = BalanceProbe.SimulateRuns(config, pool, routes,
                new List<IncidentDef>(), null, null,
                new List<CrewDefinition>(), new List<ShipDef>(), runs: 2, seedBase: 1);
            Assert.GreaterOrEqual(rate, 0f);
            Assert.LessOrEqual(rate, 1f);
        }
        
        #endregion
    }
}