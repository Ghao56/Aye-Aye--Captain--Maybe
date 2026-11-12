using NUnit.Framework;

namespace AyeAye.Tests.EditMode
{
    // Bảng khóa FR-0203 — kiểm đủ 4 pha × 3 tab + nút dưới cùng.
    public class PhaseGateTests
    {
        [Test]
        public void Planning_Tab12Interactive_Tab3ReadOnly()
        {
            Assert.AreEqual(TabAccess.Interactive, PhaseGate.TabAccess(GamePhase.Planning, 0));
            Assert.AreEqual(TabAccess.Interactive, PhaseGate.TabAccess(GamePhase.Planning, 1));
            Assert.AreEqual(TabAccess.ReadOnly, PhaseGate.TabAccess(GamePhase.Planning, 2));
            Assert.AreEqual(BottomAction.MakeItSo, PhaseGate.BottomButton(GamePhase.Planning));
        }

        [Test]
        public void Simulation_AllLocked()
        {
            Assert.AreEqual(TabAccess.Locked, PhaseGate.TabAccess(GamePhase.Simulation, 0));
            Assert.AreEqual(TabAccess.Locked, PhaseGate.TabAccess(GamePhase.Simulation, 2));
            Assert.AreEqual(BottomAction.Hidden, PhaseGate.BottomButton(GamePhase.Simulation));
        }

        [Test]
        public void Simulation_FleetTab_StillViewable()
        {
            // tab 2 khóa thao tác nhưng phải hiện tàu đang chạy → ReadOnly (xem được, sửa không được).
            Assert.AreEqual(TabAccess.ReadOnly, PhaseGate.TabAccess(GamePhase.Simulation, 1));
        }

        [Test]
        public void Incident_Tab3Interactive_OthersReadOnly()
        {
            Assert.AreEqual(TabAccess.ReadOnly, PhaseGate.TabAccess(GamePhase.Incident, 0));
            Assert.AreEqual(TabAccess.ReadOnly, PhaseGate.TabAccess(GamePhase.Incident, 1));
            Assert.AreEqual(TabAccess.Interactive, PhaseGate.TabAccess(GamePhase.Incident, 2));
            Assert.AreEqual(BottomAction.Continue, PhaseGate.BottomButton(GamePhase.Incident));
        }

        [Test]
        public void Summary_OverlayCoversAll()
        {
            for (int i = 0; i < 3; i++)
                Assert.AreEqual(TabAccess.Locked, PhaseGate.TabAccess(GamePhase.Summary, i), "tab " + i);
            Assert.AreEqual(BottomAction.NextDay, PhaseGate.BottomButton(GamePhase.Summary));
        }

        [Test]
        public void UnknownPhase_Locked()
        {
            Assert.AreEqual(TabAccess.Locked, PhaseGate.TabAccess((GamePhase)999, 0));
            Assert.AreEqual(BottomAction.Hidden, PhaseGate.BottomButton((GamePhase)999));
        }
    }
}
