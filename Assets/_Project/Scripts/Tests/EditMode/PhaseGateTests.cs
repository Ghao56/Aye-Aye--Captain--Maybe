using NUnit.Framework;
namespace AyeAye.Tests.EditMode
{

    public class PhaseGateTests
    {
        [Test]

        public void Normal_AllTabsInteractive()
        {
            for (int i = 0; i < 3; i++)
                Assert.AreEqual(TabAccess.Interactive, PhaseGate.GetAccess(incidentPending: false, i), "tab " + i);
        }
        [Test]

        public void IncidentPending_OthersReadOnly_FleetTabInteractive()
        {
            Assert.AreEqual(TabAccess.ReadOnly, PhaseGate.GetAccess(incidentPending: true, 0));
            Assert.AreEqual(TabAccess.ReadOnly, PhaseGate.GetAccess(incidentPending: true, 1));
            Assert.AreEqual(TabAccess.Interactive, PhaseGate.GetAccess(incidentPending: true, 2));
        }
        [Test]

        public void NeverLocked_ReadOnlyTabsStillViewable()
        {
            for (int i = 0; i < 3; i++)
                Assert.AreNotEqual(TabAccess.Locked, PhaseGate.GetAccess(incidentPending: true, i), "tab " + i);
        }
    }
}

