namespace AyeAye
{

    public enum TabAccess { Interactive, ReadOnly, Locked }

    public static class PhaseGate
    {

        public static TabAccess GetAccess(bool incidentPending, int tabIndex)
        {
            return incidentPending && tabIndex != 2 ? TabAccess.ReadOnly : TabAccess.Interactive;
        }
    }
}

