using UnityEngine;
namespace AyeAye
{
    [CreateAssetMenu(fileName = "GameConfig", menuName = "AyeAye/Game Config")]

    public class GameConfig : ScriptableObject
    {
        [Header("Run")]

        public int maxActiveOrders = 4;
        [Header("Clock")]
        [Tooltip("Fixed simulation step (seconds). UI accumulates real dt and runs Advance in fixed steps — same seed, same result.")]

        public float simTickSeconds = 0.1f;
        [Tooltip("How many seconds one route-unit takes at Normal speed.")]

        public float secondsPerRouteUnit = 20f;
        [Header("Orders")]
        [Tooltip("Warm-up: the first order appears at this mark (seconds). A run starts with NO orders.")]

        public float orderFirstDelaySeconds = 25f;
        [Tooltip("Seconds between two order spawns.")]

        public float orderIntervalSeconds = 40f;
        [Tooltip("Interval shortened by this exponent after each spawned order.")]

        public float orderIntervalDecayPerOrder = 0.92f;
        [Tooltip("Minimum interval between two spawns.")]

        public float orderIntervalMinSeconds = 15f;
        [Tooltip("Max orders in the inbox — at the cap the spawn is skipped but the next mark still advances (anti-flood).")]

        public int maxInboxOrders = 5;
        [Range(0f, 1f), Tooltip("Chance EACH tag (Fragile/Cold/Urgent) turns on when generating an order — combinations allowed.")]

        public float orderTagChance = 0.35f;
        [Tooltip("Random deadline bonus: minimum (seconds) on top of ETA + reaction margin.")]

        public float orderDeadlineExtraMinSeconds = 5f;
        [Tooltip("Random deadline bonus: maximum (seconds). Urgent orders skip the random part.")]

        public float orderDeadlineExtraMaxSeconds = 35f;
        [Tooltip("Base revenue of each generated order.")]

        public int orderRevenueBase = 60;
        [Tooltip("Revenue added per route-unit of the shortest route.")]

        public int orderRevenuePerRouteUnit = 30;
        [Tooltip("Revenue added for EACH tag on an order.")]

        public int orderRevenueTagBonus = 25;
        [Range(0f, 1f), Tooltip("Revenue deviation ±% (jitter).")]

        public float orderRevenueJitter = 0.2f;
        [Tooltip("Countdown lower than this many seconds turns red (UI).")]

        public float deadlineWarnSeconds = 30f;
        [Tooltip("Player reaction margin: order deadline ≥ fastest-ship ETA + this (checked by DemoValidator).")]

        public float deadlineReactionMarginSeconds = 25f;
        [Tooltip("Extra margin for the FIRST order (easy rule) — new players need time to read the guide.")]

        public float tutorialDeadlineBonusSeconds = 30f;
        [Header("Win / Lose")]
        [Tooltip("Deliver this many orders → win.")]

        public int winDeliveries = 10;
        [Tooltip("Max late deliveries — reaching this loses immediately.")]

        public int maxLateDeliveries = 3;
        [Tooltip("Too-early loss: losing while fewer than this were delivered.")]

        public int earlyLoseDeliveries = 2;
        [Header("Incidents")]
        [Range(0f, 1f), Tooltip("Chance each time a ship enters a new edge.")]

        public float incidentChancePerEdge = 0.15f;
        [Tooltip("Max pending incidents at once (at the cap the clock keeps running but no more rolls).")]

        public int maxPendingIncidents = 2;
        [Tooltip("Allow incident rolls while a ship roams idle — [S].")]

        public bool incidentsWhileRoaming = false;
        [Header("Roaming ([S] — idle ships wander, G5)")]
        [Tooltip("Enable roaming in Simulation. Turn off to isolate tests or measure balance.")]

        public bool roamEnabled = true;
        [Tooltip("Roaming speed factor (multiplies the ship's normal speed).")]

        public float roamSpeedFactor = 0.7f;
        [Tooltip("Run-start grace: before this mark no ship leaves port. Must be ≥ orderFirstDelaySeconds (checked by DemoValidator).")]

        public float roamStartSeconds = 30f;
        [Header("Speed (multipliers over secondsPerRouteUnit)")]

        public float fastSpeed = 1.5f;

        public float normalSpeed = 1.0f;

        public float slowSpeed = 0.7f;
        [Header("Progress Δ per Outcome (route-units, added to the option's baseProgressDelta)")]

        public float smartProgressDelta = 0.5f;

        public float normalProgressDelta = 0f;

        public float slipUpProgressDelta = -0.7f;
        [Header("Scoring (FR-0602)")]
        [Tooltip("Revenue factor for late deliveries.")]

        public float lateRevenueFactor = 0.5f;
        [Header("Effects")]
        [Tooltip("Typing speed of the terminal effect (chars/second).")]

        public int typewriterCharsPerSecond = 90;
        [Tooltip("If on: Restart replays the same seed (same incident/order chain).")]

        public bool repeatSeedOnRestart = false;

        public float SpeedValue(SpeedClass speed)
        {
            switch (speed)
            {
                case SpeedClass.Fast: return fastSpeed;
                case SpeedClass.Slow: return slowSpeed;
                default: return normalSpeed;
            }
        }

        public float SpeedUnitsPerSecond(SpeedClass speed)
        {
            return SpeedValue(speed) / Mathf.Max(0.0001f, secondsPerRouteUnit);
        }

        public float OutcomeDelta(DecisionOutcome outcome)
        {
            switch (outcome)
            {
                case DecisionOutcome.Smart: return smartProgressDelta;
                case DecisionOutcome.SlipUp: return slipUpProgressDelta;
                default: return normalProgressDelta;
            }
        }
    }
}
