using BairesCalendar.Application.DTOs;
using BairesCalendar.Application.Services;
using BairesCalendar.Domain.Entities;
using BairesCalendar.Infrastructure;
using BairesCalendar.Infrastructure.TimeProvider;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace BairesCalendar.UnitTests.Services
{
    public class SchedulingServiceTests
    {
        private ApplicationDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new ApplicationDbContext(options);
        }

        [Fact]
        public async Task ScheduleMeetingAsync_ShouldScheduleMeeting_WhenNoConflicts()
        {
            // Arrange
            var dbContext = CreateInMemoryDbContext();
            var mockTimeProvider = new Mock<ITimeProvider>();
            mockTimeProvider.Setup(tp => tp.UtcNow).Returns(new DateTime(2025, 5, 29, 10, 0, 0, DateTimeKind.Utc));
            var mockLogger = new Mock<ILogger<SchedulingService>>();

            var user1 = new User("Rafael", "America/Sao_Paulo"); // UTC-3
            var user2 = new User("Robert", "America/New_York");    // UTC-4
            dbContext.Users.AddRange(user1, user2);
            await dbContext.SaveChangesAsync();

            var service = new SchedulingService(dbContext, mockTimeProvider.Object, mockLogger.Object);

            var request = new ScheduleMeetingRequestDTO
            {
                Title = "Test Project",
                StartTime = new DateTime(2025, 5, 30, 10, 0, 0),
                Duration = TimeSpan.FromHours(1),
                ParticipantIds = [user1.Id, user2.Id],
                UserTimeZoneId = "America/Sao_Paulo"
            };

            // Act
            var response = await service.ScheduleMeetingAsync(request);

            // Assert
            Assert.True(response.Success);
            Assert.NotNull(response.MeetingId);
            Assert.Equal("Meeting scheduled successfully.", response.Message);

            var scheduledMeeting = await dbContext.Meetings
                .Include(m => m.Participants)
                .FirstOrDefaultAsync(m => m.Id == response.MeetingId);

            Assert.NotNull(scheduledMeeting);
            Assert.Equal(new DateTime(2025, 5, 30, 13, 0, 0, DateTimeKind.Utc), scheduledMeeting.StartTimeUtc);
            Assert.Equal(new DateTime(2025, 5, 30, 14, 0, 0, DateTimeKind.Utc), scheduledMeeting.EndTimeUtc);
            Assert.Contains(user1, scheduledMeeting.Participants);
            Assert.Contains(user2, scheduledMeeting.Participants);
        }

        [Fact]
        public async Task ScheduleMeetingAsync_ShouldRejectMeeting_WhenConflictExists()
        {
            // Arrange
            var dbContext = CreateInMemoryDbContext();
            var mockTimeProvider = new Mock<ITimeProvider>();
            mockTimeProvider.Setup(tp => tp.UtcNow).Returns(new DateTime(2025, 5, 29, 10, 0, 0, DateTimeKind.Utc));
            var mockLogger = new Mock<ILogger<SchedulingService>>();

            var user1 = new User("Rafael", "America/Sao_Paulo"); // UTC-3
            var user2 = new User("Robert", "America/New_York");  // UTC-4
            dbContext.Users.AddRange(user1, user2);
            await dbContext.SaveChangesAsync();

            var existingMeeting = new Meeting(
                "Existing Meeting",
                new DateTime(2025, 5, 30, 13, 30, 0, DateTimeKind.Utc), // Overlaps with proposed 13:00-14:00
                new DateTime(2025, 5, 30, 14, 30, 0, DateTimeKind.Utc),
                [user1]);

            dbContext.Meetings.Add(existingMeeting);
            await dbContext.SaveChangesAsync();
            
            user1.Meetings.Add(existingMeeting);

            var service = new SchedulingService(dbContext, mockTimeProvider.Object, mockLogger.Object);

            var request = new ScheduleMeetingRequestDTO
            {
                Title = "Conflicting Meeting",
                StartTime = new DateTime(2025, 5, 30, 10, 0, 0),
                Duration = TimeSpan.FromHours(1),
                ParticipantIds = [user1.Id, user2.Id],
                UserTimeZoneId = "America/Sao_Paulo"
            };

            // Act
            var response = await service.ScheduleMeetingAsync(request);

            // Assert
            Assert.False(response.Success);
            Assert.Null(response.MeetingId);
            Assert.Contains("time conflict", response.Message);
            Assert.NotEmpty(response.SuggestedSlots);
            
            // Should suggest 3 slots
            Assert.Equal(3, response.SuggestedSlots.Count);

            var meetingsCount = await dbContext.Meetings.CountAsync();
            Assert.Equal(1, meetingsCount);
        }

        [Fact]
        public async Task ScheduleMeetingAsync_ShouldSuggestNextAvailableSlots_Correctly()
        {
            // Arrange
            var dbContext = CreateInMemoryDbContext();
            var mockTimeProvider = new Mock<ITimeProvider>();
            mockTimeProvider.Setup(tp => tp.UtcNow).Returns(new DateTime(2025, 5, 29, 10, 0, 0, DateTimeKind.Utc));
            var mockLogger = new Mock<ILogger<SchedulingService>>();

            var user1 = new User("Rafael", "America/Sao_Paulo");
            dbContext.Users.Add(user1);
            await dbContext.SaveChangesAsync();

            dbContext.Meetings.Add(new Meeting("Meeting1", new DateTime(2025, 5, 30, 13, 0, 0, DateTimeKind.Utc), new DateTime(2025, 5, 30, 14, 0, 0, DateTimeKind.Utc), new List<User> { user1 }));
            dbContext.Meetings.Add(new Meeting("Meeting2", new DateTime(2025, 5, 30, 15, 0, 0, DateTimeKind.Utc), new DateTime(2025, 5, 30, 16, 0, 0, DateTimeKind.Utc), new List<User> { user1 }));
            await dbContext.SaveChangesAsync();

            var service = new SchedulingService(dbContext, mockTimeProvider.Object, mockLogger.Object);

            var request = new ScheduleMeetingRequestDTO
            {
                Title = "Proposed Conflict",
                StartTime = new DateTime(2025, 5, 30, 10, 0, 0),
                Duration = TimeSpan.FromHours(1),
                ParticipantIds = [user1.Id],
                UserTimeZoneId = "America/Sao_Paulo"
            };

            // Act
            var response = await service.ScheduleMeetingAsync(request);

            // Assert
            Assert.False(response.Success);
            Assert.NotNull(response.SuggestedSlots);
            Assert.Equal(3, response.SuggestedSlots.Count);

            //14:00 - 15:00 
            Assert.Equal(new DateTime(2025, 5, 30, 14, 0, 0, DateTimeKind.Utc), response.SuggestedSlots[0].StartTimeUtc);
            Assert.Equal(new DateTime(2025, 5, 30, 15, 0, 0, DateTimeKind.Utc), response.SuggestedSlots[0].EndTimeUtc);

            //16:15 - 17:15
            Assert.Equal(new DateTime(2025, 5, 30, 16, 15, 0, DateTimeKind.Utc), response.SuggestedSlots[1].StartTimeUtc);
            Assert.Equal(new DateTime(2025, 5, 30, 17, 15, 0, DateTimeKind.Utc), response.SuggestedSlots[1].EndTimeUtc);

            //16:15 - 17:15
            Assert.Equal(new DateTime(2025, 5, 30, 17, 15, 0, DateTimeKind.Utc), response.SuggestedSlots[2].StartTimeUtc);
            Assert.Equal(new DateTime(2025, 5, 30, 18, 15, 0, DateTimeKind.Utc), response.SuggestedSlots[2].EndTimeUtc);
        }

        [Fact]
        public async Task ScheduleMeetingAsync_ShouldRejectMeeting_WhenStartTimeInPast()
        {
            // Arrange
            var dbContext = CreateInMemoryDbContext();
            var mockTimeProvider = new Mock<ITimeProvider>();
            mockTimeProvider.Setup(tp => tp.UtcNow).Returns(new DateTime(2025, 5, 29, 10, 0, 0, DateTimeKind.Utc));
            var mockLogger = new Mock<ILogger<SchedulingService>>();

            var user1 = new User("Rafael", "UTC");
            dbContext.Users.Add(user1);
            await dbContext.SaveChangesAsync();

            var service = new SchedulingService(dbContext, mockTimeProvider.Object, mockLogger.Object);

            var request = new ScheduleMeetingRequestDTO
            {
                Title = "Past Meeting",
                StartTime = new DateTime(2025, 5, 29, 9, 0, 0),
                Duration = TimeSpan.FromHours(1),
                ParticipantIds = [user1.Id],
                UserTimeZoneId = "UTC"
            };

            // Act
            var response = await service.ScheduleMeetingAsync(request);

            // Assert
            Assert.False(response.Success);
            Assert.Null(response.MeetingId);
            Assert.Equal("Cannot schedule a meeting in the past.", response.Message);
            Assert.Empty(response.SuggestedSlots);
        }

        [Fact]
        public async Task ScheduleMeetingAsync_ShouldHandleMultipleParticipantsCorrectly()
        {
            // Arrange
            var dbContext = CreateInMemoryDbContext();
            var mockTimeProvider = new Mock<ITimeProvider>();
            mockTimeProvider.Setup(tp => tp.UtcNow).Returns(new DateTime(2025, 5, 29, 10, 0, 0, DateTimeKind.Utc));
            var mockLogger = new Mock<ILogger<SchedulingService>>();

            var user1 = new User("Rafael", "UTC");
            var user2 = new User("Robert", "UTC");
            var user3 = new User("Joe", "UTC");
            dbContext.Users.AddRange(user1, user2, user3);
            await dbContext.SaveChangesAsync();

            // Meeting for user1
            dbContext.Meetings.Add(new Meeting("Meeting1", new DateTime(2025, 5, 30, 10, 0, 0, DateTimeKind.Utc), new DateTime(2025, 5, 30, 11, 0, 0, DateTimeKind.Utc), [user1]));
            
            // Meeting for user2
            dbContext.Meetings.Add(new Meeting("Meeting2", new DateTime(2025, 5, 30, 10, 30, 0, DateTimeKind.Utc), new DateTime(2025, 5, 30, 11, 30, 0, DateTimeKind.Utc), [user2]));

            await dbContext.SaveChangesAsync();

            user1.Meetings.Add(await dbContext.Meetings.SingleAsync(m => m.Title == "Meeting1"));
            user2.Meetings.Add(await dbContext.Meetings.SingleAsync(m => m.Title == "Meeting2"));

            var service = new SchedulingService(dbContext, mockTimeProvider.Object, mockLogger.Object);

            // Try to schedule 10:00-11:00 UTC for User1, User2, User3
            // there is a conflict with User1 and User2
            var request = new ScheduleMeetingRequestDTO
            {
                Title = "Team Sync",
                StartTime = new DateTime(2025, 5, 30, 10, 0, 0),
                Duration = TimeSpan.FromHours(1),
                ParticipantIds = [user1.Id, user2.Id, user3.Id],
                UserTimeZoneId = "UTC"
            };

            // Act
            var response = await service.ScheduleMeetingAsync(request);

            // Assert
            Assert.False(response.Success);
            Assert.Contains("time conflict", response.Message);
            Assert.NotEmpty(response.SuggestedSlots);
            Assert.Equal(3, response.SuggestedSlots.Count);

            // Calculate expected suggestions based on the latest end time of conflicting meetings (11:30)
            // Next available should be 11:30 - 12:30
            Assert.Equal(new DateTime(2025, 5, 30, 11, 30, 0, DateTimeKind.Utc), response.SuggestedSlots[0].StartTimeUtc);
            Assert.Equal(new DateTime(2025, 5, 30, 12, 30, 0, DateTimeKind.Utc), response.SuggestedSlots[0].EndTimeUtc);
        }

        [Fact]
        public async Task ScheduleMeetingAsync_ShouldReturnError_WhenInvalidTimeZoneIdProvided()
        {
            // Arrange
            var dbContext = CreateInMemoryDbContext();
            var mockTimeProvider = new Mock<ITimeProvider>();
            mockTimeProvider.Setup(tp => tp.UtcNow).Returns(new DateTime(2025, 5, 29, 10, 0, 0, DateTimeKind.Utc));
            var mockLogger = new Mock<ILogger<SchedulingService>>();

            var user1 = new User("Rafael", "UTC");
            dbContext.Users.Add(user1);
            await dbContext.SaveChangesAsync();

            var service = new SchedulingService(dbContext, mockTimeProvider.Object, mockLogger.Object);

            var request = new ScheduleMeetingRequestDTO
            {
                Title = "Test Meeting",
                StartTime = new DateTime(2025, 5, 30, 10, 0, 0),
                Duration = TimeSpan.FromHours(1),
                ParticipantIds = new List<Guid> { user1.Id },
                UserTimeZoneId = "America/Invalid_TZ"
            };

            // Act
            var response = await service.ScheduleMeetingAsync(request);

            // Assert
            Assert.False(response.Success);
            Assert.Contains("Invalid timezone ID provided", response.Message);
        }
    }
}