using BairesCalendar.Application.Interfaces;
using BairesCalendar.Domain.Entities;
using BairesCalendar.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BairesCalendar.Application.Services
{
    public class UserService(ApplicationDbContext dbContext, ILogger<SchedulingService> logger) : IUserService
    {
        private readonly ApplicationDbContext _dbContext = dbContext;
        private readonly ILogger<SchedulingService> _logger = logger;

        public async Task<List<User>> GetUsersAsync()
        {
            _logger.LogInformation("Attempting to retrieve users.");
            return await _dbContext.Users.ToListAsync();
        }

        public async Task CreateUserAsync(string userName, string timeZoneId)
        {
            _dbContext.Users.Add(new User(userName, timeZoneId));
            await _dbContext.SaveChangesAsync();
        }
    }
}