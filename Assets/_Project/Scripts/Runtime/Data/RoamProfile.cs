using UnityEngine;
namespace AyeAye
{
    [System.Serializable]

    public class RoamProfile
    {
        [Tooltip("Minimum dwell time at a port before considering leaving (seconds).")]

        public float dwellSeconds = 15f;
        [Tooltip("Demand attraction weight: ports with inbox/accepted-but-unassigned orders departing from it.")]

        public float demandWeight = 2f;
        [Tooltip("Prefer far (+) or near (−) ports — multiplied by edge length.")]

        public float farBias = 0f;
        [Tooltip("Bonus when the destination port is the HQ (Hub).")]

        public float hubAffinity = 0f;
        [Tooltip("Random noise weight — runs through its own RoamRng, does NOT touch state.Rng (order/incident chain unchanged).")]

        public float wanderWeight = 0f;
        [Tooltip("Penalize the port just left — ships don't bounce right back.")]

        public float backtrackPenalty = 2f;
    }
}

