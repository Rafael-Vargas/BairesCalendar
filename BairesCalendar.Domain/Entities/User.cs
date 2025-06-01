namespace BairesCalendar.Domain.Entities
{
    public class User
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string TimeZoneId { get; set; }

        public ICollection<Meeting> Meetings { get; set; } = [];

        public User(string name, string timeZoneId)
        {
            Id = Guid.NewGuid();
            Name = name;
            TimeZoneId = timeZoneId;
        }

        private User() { }
    }
}