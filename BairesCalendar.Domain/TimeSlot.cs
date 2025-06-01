namespace BairesCalendar.Domain
{
    public class TimeSlot
    {
        public DateTime StartTimeUtc { get; set; }
        public DateTime EndTimeUtc { get; set; }

        public TimeSlot(DateTime startTimeUtc, DateTime endTimeUtc)
        {
            StartTimeUtc = startTimeUtc;
            EndTimeUtc = endTimeUtc;
        }
    }
}