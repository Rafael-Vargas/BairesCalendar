# BairesCalendar

BairesCalendar is a modern meeting scheduling application built with .NET 8 and C# 12. 
It provides robust APIs and services for managing users, meetings, and time zone-aware scheduling, with a focus on conflict detection.

## Features

- Schedule meetings with multiple participants
- Time zone-aware scheduling and conflict detection
- Suggests alternative time slots on conflicts
- Clean architecture with separation of concerns
- Integration and unit tests

## Project Structure
<pre>
BairesCalendar/
│ ├── BairesCalendar.API/                    # ASP.NET Core Web API entry point 
│   ├── Controllers/ 
│   │   └── MeetingsController.cs 
│   ├── Program.cs 
│   └── appsettings.json 
│ ├── BairesCalendar.Application/            # Application layer (business logic) 
│   ├── DTOs/ 
│   │   ├── ScheduleMeetingRequestDTO.cs 
│   │   └── ScheduleMeetingResponseDTO.cs 
│   ├── Interfaces/ 
│   │   ├── ISchedulingService.cs 
│   │   └── IUserService.cs 
│   └── Services/ 
│       ├── SchedulingService.cs 
│       └── UserService.cs 
│ ├── BairesCalendar.Domain/                 # Domain layer (entities, exceptions) 
│   ├── Entities/ 
│   │   ├── Meeting.cs 
│   │   └── User.cs 
│   └── Exceptions/ 
│       └── ConflictException.cs 
│ ├── BairesCalendar.Infrastructure/         # Infrastructure (EF Core, providers) 
│   ├── TimeProvider/ 
│   │   ├── ITimeProvider.cs 
│   │   └── SystemTimeProvider.cs 
│   └── ApplicationDbContext.cs 
│ ├── BairesCalendar.UnitTests/              # Unit tests 
│   └── Services/ 
│       └── SchedulingServiceTests.cs 
│ ├── BairesCalendar.IntegrationTests/       # Integration tests 
│   └── CalendarIntegrationTest.cs 
│ └── README.md                              # Project documentation
</pre>

## Getting Started

1. **Clone the repository**
2. **Configure your database** in `appsettings.json`. I've used PostgreSQL, but you can use any database supported by EF Core.
   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Host=localhost;Port=5432;Database=BairesCalendar;Username=postgres;Password=yourpassword"
   }
   ```
   Make sure to replace `yourpassword` with your actual PostgreSQL password.
3. **Open Prompt and point to API folder**
4. **Run the following commands to apply migrations:**
```
dotnet ef database update -p ../BairesCalendar.Infrastructure -s .
```
5. **Run database script to create initial data:**
```sql
insert into public."Users" ("Id", "Name", "TimeZoneId")
values('6b9b1fe8-550e-442a-9626-1381e7d7e71d','TestBR1', 'America/Sao_Paulo');

insert into public."Users" ("Id", "Name", "TimeZoneId")
values('aa778827-5359-4eff-9bb7-4f16c589b054','TestBR2', 'America/Sao_Paulo');

insert into public."Users" ("Id", "Name", "TimeZoneId")
values('b062bf45-de59-44a4-98dc-1f812501162a','TestUS1', 'America/New_York');
```
6. **Run BairesCalendar.API**
7. **Use Swagger to test the API endpoints, it will open Swagger Index: https://localhost:7047/swagger/index.html**
8. **Use those payloads to test the API:**
```json
*** OK - Create first meeting for all users

{
  "title": "Test Meeting API",
  "startTime": "2025-06-01T10:00:00.000Z",
  "duration": "00:30:00",
  "participantIds": [
    "6b9b1fe8-550e-442a-9626-1381e7d7e71d"
    ,"aa778827-5359-4eff-9bb7-4f16c589b054"
	,"b062bf45-de59-44a4-98dc-1f812501162a"
  ],
  "userTimeZoneId": "UTC"
}

*** OK - Create second meeting for two users

{
  "title": "Test Meeting API",
  "startTime": "2025-06-01T10:30:00.000Z",
  "duration": "00:30:00",
  "participantIds": [
    "6b9b1fe8-550e-442a-9626-1381e7d7e71d"
    ,"b062bf45-de59-44a4-98dc-1f812501162a"
  ],
  "userTimeZoneId": "UTC"
}

*** FAIL - Only one user is free, so suggest time slots

{
  "title": "Test Meeting API",
  "startTime": "2025-06-01T10:30:00.000Z",
  "duration": "00:30:00",
  "participantIds": [
    "6b9b1fe8-550e-442a-9626-1381e7d7e71d"
    ,"aa778827-5359-4eff-9bb7-4f16c589b054"
  ],
  "userTimeZoneId": "UTC"
}

```