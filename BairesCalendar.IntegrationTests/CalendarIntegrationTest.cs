using BairesCalendar.Application.DTOs;
using BairesCalendar.Application.Services;
using BairesCalendar.Domain.Entities;
using BairesCalendar.Infrastructure;
using BairesCalendar.Infrastructure.TimeProvider;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace BairesCalendar.IntegrationTests
{
    public class MeetingAppServiceIntegrationTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly SchedulingService _meetingAppService;

        public MeetingAppServiceIntegrationTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);
            var mockTimeProvider = new Mock<ITimeProvider>();
            mockTimeProvider.Setup(tp => tp.UtcNow).Returns(new DateTime(2025, 5, 30, 10, 0, 0, DateTimeKind.Utc));
            var mockLogger = new Mock<ILogger<SchedulingService>>();
            _meetingAppService = new SchedulingService(_context, mockTimeProvider.Object, mockLogger.Object);

            // Seed data for tests
            SeedData();
        }

        private void SeedData()
        {
            var user1 = new User("Rafael", "America/Sao_Paulo");
            var user2 = new User("Mark", "Europe/London");
            var user3 = new User("Chang", "Asia/Tokyo");

            _context.Users.Add(user1);
            _context.Users.Add(user2);
            _context.Users.Add(user3);
            _context.SaveChanges();

            // Add some existing meetings
            var meeting1 = new Meeting("Test Meeting Sync", DateTime.UtcNow.AddHours(2), DateTime.UtcNow.AddHours(3), new List<User> { user1, user2 });
            
            // Manually add meeting to user's collection for in-memory context to track
            user1.Meetings.Add(meeting1); 
            user2.Meetings.Add(meeting1);
            _context.Meetings.Add(meeting1);

            _context.SaveChanges();
        }

        [Fact]
        public async Task ScheduleMeeting_ShouldSucceed_WhenNoConflictsExist()
        {
            // Arrange
            var userIds = _context.Users.Select(u => u.Id).ToList();
            var request = new ScheduleMeetingRequestDTO
            {
                Title = "New Team Meeting",
                StartTime = DateTime.Parse("2025-06-01T10:00:00"),
                Duration = TimeSpan.FromHours(1),
                ParticipantIds = userIds,
                UserTimeZoneId = "America/Sao_Paulo"
            };

            // Act
            var response = await _meetingAppService.ScheduleMeetingAsync(request);

            // Assert
            Assert.True(response.Success);
            Assert.NotNull(response.MeetingId);
            Assert.Equal("Meeting scheduled successfully.", response.Message);

            // Check database for the scheduled meeting
            var scheduledMeeting = await _context.Meetings.Include(m => m.Participants).FirstOrDefaultAsync(m => m.Id == response.MeetingId);
            Assert.NotNull(scheduledMeeting);
            Assert.Equal("New Team Meeting", scheduledMeeting.Title);
            Assert.Equal(userIds.Count, scheduledMeeting.Participants.Count);
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }
    }
}