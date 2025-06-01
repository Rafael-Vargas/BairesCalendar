namespace BairesCalendar.Domain.Entities
{
    public class Meeting
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public DateTime StartTimeUtc { get; set; }
        public DateTime EndTimeUtc { get; set; }
        public ICollection<User> Participants { get; set; } = [];
        public Meeting(string title, DateTime startTimeUtc, DateTime endTimeUtc, IEnumerable<User> participants)
        {
            Id = Guid.NewGuid();
            Title = title;
            StartTimeUtc = startTimeUtc;
            EndTimeUtc = endTimeUtc;

            if (StartTimeUtc.Kind != DateTimeKind.Utc || EndTimeUtc.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException("StartTimeUtc and EndTimeUtc must be in UTC.");
            }

            if (startTimeUtc >= endTimeUtc)
            {
                throw new ArgumentException("StartTimeUtc must be before EndTimeUtc.");
            }

            Participants = participants?.ToList() ?? [];
        }

        private Meeting() { }

        public bool OverlapsWith(DateTime otherStartUtc, DateTime otherEndUtc)
        {
            return StartTimeUtc < otherEndUtc && EndTimeUtc > otherStartUtc;
        }
    }
}
