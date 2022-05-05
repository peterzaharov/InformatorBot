using Microsoft.EntityFrameworkCore;
using InformatorBot.Models;

namespace InformatorBot
{
    public class ApplicationContext : DbContext
    {
        public DbSet<Chat>? Chats { get; set; }
        public DbSet<Group>? Groups { get; set; }
        public DbSet<ChatGroup>? ChatGroups { get; set; }
        public DbSet<User>? Users { get; set; }
        public DbSet<Role>? Roles { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Group>()
                .HasMany(p => p.Users)
                .WithMany(p => p.Groups)
                .UsingEntity(j => j.ToTable("GroupUser"));
        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseNpgsql("ConnectionString");
        }
    }
}
