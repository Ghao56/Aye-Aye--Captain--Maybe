namespace AyeAye
{
    // Kết quả đã giải quyết của một lần Confirm; không chứa tham chiếu dữ liệu UI.
    public class ResolvedDecision
    {
        public DecisionOutcome Outcome;
        public string Explanation;
        public string Quote;
        public FlagToSet FlagToSet;
        public int CargoDelta;
        public string CrewName;
        public string OptionLabel;
    }
}
