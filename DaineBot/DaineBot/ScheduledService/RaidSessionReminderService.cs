using DaineBot.Data;
using DaineBot.Models;
using DaineBot.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaineBot.ScheduledService
{
    public class RaidSessionReminderService : BackgroundService
    {
        private readonly RaidService _raidService;
        private readonly BotReadyService _botReady;
        private readonly IServiceProvider _services;

        public RaidSessionReminderService(RaidService raidService, BotReadyService botReady, IServiceProvider services)
        {
            _raidService = raidService;
            _botReady = botReady;
            _services = services;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await _botReady.Ready;

            while (!stoppingToken.IsCancellationRequested)
            {
                using var scope = _services.CreateScope();
                var _db = scope.ServiceProvider.GetRequiredService<DaineBotDbContext>();

                try 
                {
                    var now = DateTime.UtcNow;
                    List<RaidSession> raidSessions = _db.RaidSessions.Include(rs => rs.Roster).Include(rs => rs.Check).Where(rs => rs.Check == null && rs.NextSession != null && DateTime.UtcNow > ((DateTime)rs.NextSession).AddHours(-12) && DateTime.UtcNow < (DateTime)rs.NextSession).ToList();

                    foreach (RaidSession raidSession in raidSessions)
                    {
                        await _raidService.CreateReadyCheckForSession(raidSession);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ScheduledTaskService] Erreur : {ex.Message}");
                }

                await FixEmptyRaidSessions(); //Temporaire le temps de migrer la base

                // ⏱️ Attente de 10 minuteS
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        private async Task FixEmptyRaidSessions()
        {
            using var scope = _services.CreateScope();
            var _db = scope.ServiceProvider.GetRequiredService<DaineBotDbContext>();

            List<RaidSession> raidSessions = await _db.RaidSessions.Where(rs => rs.NextSession == null).Include(rs => rs.Roster).ToListAsync();

            foreach (RaidSession raidSession in raidSessions)
            {
                raidSession.NextSession = _raidService.GetNextSessionDateTime(raidSession);
            }

            await _db.SaveChangesAsync();
        }
    }
}
