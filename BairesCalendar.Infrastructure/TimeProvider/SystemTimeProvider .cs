namespace BairesCalendar.Infrastructure.TimeProvider
{
    public class SystemTimeProvider : ITimeProvider
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }
}