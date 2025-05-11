using DaineBot.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaineBot.Data
{
    public class DaineBotDbContext : DbContext
    {
        public DaineBotDbContext(DbContextOptions<DaineBotDbContext> options) : base(options)
        {
        }

        public DbSet<RaidSession> RaidSessions { get; set; }
        public DbSet<ReadyCheck> ReadyChecks { get; set; }
        public DbSet<Roster> Rosters { get; set; }
        public DbSet<AdminRole> AdminRoles { get; set; }
        public DbSet<TmpRaidSession> TmpSessions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RaidSession>()
                .HasOne(rs => rs.Check)
                .WithOne(rc => rc.Session)
                .HasForeignKey<ReadyCheck>(rc => rc.SessionId);

            modelBuilder.Entity<Roster>()
                .HasMany(r => r.Sessions)
                .WithOne(rs => rs.Roster)
                .HasForeignKey(rs => rs.RosterId);
        }
    }
}
