using BairesCalendar.Domain.Entities;

namespace BairesCalendar.Application.Interfaces
{
    public interface IUserService
    {
        Task<List<User>> GetUsersAsync();
        Task CreateUserAsync(string name, string timeZoneId);
    }
}