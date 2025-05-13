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

                    List<RaidSession> sessionsToUpdate = _db.RaidSessions.Include(rs => rs.Roster).Include(rs => rs.Check).Where(rs => DateTime.UtcNow > rs.NextSession.AddMinutes(rs.Duration.TotalMinutes)).ToList();

                    foreach (RaidSession session in sessionsToUpdate)
                    {
                        await UpdateNextSession(session, _db);
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

        private async Task UpdateNextSession(RaidSession raidSession, DaineBotDbContext _db)
        {
            DateTime potentialNextSession = _raidService.GetNextSessionDateTime(raidSession);

            DateTime today = DateTime.Today;

            // Calcul du lundi suivant (exclut aujourd’hui si on est lundi)
            int daysUntilMonday = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
            daysUntilMonday = daysUntilMonday == 0 ? 7 : daysUntilMonday;
            DateTime nextMonday = today.AddDays(daysUntilMonday);

            // Vérification
            if (potentialNextSession < nextMonday)
            {
                potentialNextSession = potentialNextSession.AddDays(7);
            }

            if (raidSession.Check != null)
            {
                _db.Remove(raidSession.Check);
            }

            raidSession.ReportCode = null;
            raidSession.NextSession = potentialNextSession;
            await _db.SaveChangesAsync();
        }
    }
}
