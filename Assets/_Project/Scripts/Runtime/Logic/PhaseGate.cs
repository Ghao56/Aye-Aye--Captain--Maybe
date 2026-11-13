namespace AyeAye
{
    // FR-0203 bản chạy liên tục: chỉ 2 trạng thái — bình thường cả 3 tab dùng được;
    // khi có sự cố đang chờ → tab SỰ CỐ (index 2) tương tác, hai tab kia chỉ xem. UI chỉ đọc kết quả.
    public enum TabAccess { Interactive, ReadOnly, Locked }

    public static class PhaseGate
    {
        public static TabAccess GetAccess(bool incidentPending, int tabIndex)
        {
            return incidentPending && tabIndex != 2 ? TabAccess.ReadOnly : TabAccess.Interactive;
        }
    }
}
