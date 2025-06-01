namespace BairesCalendar.Domain.Exceptions
{
    public class ConflictException : Exception
    {
        public List<TimeSlot> SuggestedSlots { get; }

        public ConflictException(string message, List<TimeSlot> suggestedSlots)
            : base(message) => SuggestedSlots = suggestedSlots;
    }
}