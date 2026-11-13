using NUnit.Framework;

namespace AyeAye.Tests.EditMode
{
    // FR-0203 bản chạy liên tục: chỉ 2 trạng thái — không sự cố: cả 3 tab tương tác;
    // sự cố đang chờ: tab SỰ CỐ (index 2) tương tác, hai tab kia chỉ xem.
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
            // Chỉ xem được (không khóa tuyệt đối) → không bao giờ trả Locked.
            for (int i = 0; i < 3; i++)
                Assert.AreNotEqual(TabAccess.Locked, PhaseGate.GetAccess(incidentPending: true, i), "tab " + i);
        }
    }
}
