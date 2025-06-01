using BairesCalendar.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace BairesCalendar.Infrastructure
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Meeting> Meetings { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

            modelBuilder.Entity<Meeting>()
                .HasMany(m => m.Participants)
                .WithMany(u => u.Meetings)
                .UsingEntity(j => j.ToTable("MeetingParticipants")); // Create a join table

            modelBuilder.Entity<Meeting>()
                .Property(m => m.StartTimeUtc)
                .HasColumnType("timestamp with time zone");

            modelBuilder.Entity<Meeting>()
                .Property(m => m.EndTimeUtc)
                .HasColumnType("timestamp with time zone");

            base.OnModelCreating(modelBuilder);
        }
    }
}