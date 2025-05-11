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
                    List<RaidSession> raidSessions = _db.RaidSessions.Include(rs => rs.Roster).Include(rs => rs.Check).Where(rs => ((rs.Day - ((int)DateTime.UtcNow.DayOfWeek) + 7) % 7) <= 1).AsEnumerable().Where(rs =>
                    {
                        if (rs.Check != null)
                            return false;

                        DateTime nextSession = _raidService.GetNextSessionDateTime(rs);

                        if (nextSession - DateTime.UtcNow < TimeSpan.FromHours(24)) { return true; }

                        return false;
                    }).ToList();

                    foreach (RaidSession raidSession in raidSessions)
                    {
                        await _raidService.CreateReadyCheckForSession(raidSession);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ScheduledTaskService] Erreur : {ex.Message}");
                }

                // ⏱️ Attente de 10 minuteS
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        private List<RaidSession> GetSessionsNeedingReadyCheck()
        {
            return new List<RaidSession>();
        }
    }
}
