namespace BairesCalendar.Infrastructure.TimeProvider
{
    public interface ITimeProvider
    {
        DateTime UtcNow { get; }
    }
}