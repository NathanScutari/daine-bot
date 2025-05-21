using DaineBot.Data;
using DaineBot.Models;
using DaineBot.Services;
using Discord.WebSocket;
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
        private readonly FFLogsService _ffLogsService;
        private readonly DiscordSocketClient _client;

        public RaidSessionReminderService(RaidService raidService, BotReadyService botReady, IServiceProvider services, FFLogsService ffLogsService, DiscordSocketClient client)
        {
            _raidService = raidService;
            _botReady = botReady;
            _services = services;
            _ffLogsService = ffLogsService;
            _client = client;
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
                    Console.WriteLine("RaidSession announce check");
                    var now = DateTime.UtcNow;
                    List<RaidSession> raidSessions = _db.RaidSessions.Include(rs => rs.Roster).Where(rs => rs.Announced == false && DateTime.UtcNow > ((DateTime)rs.NextSession).AddHours(-8) && DateTime.UtcNow < (DateTime)rs.NextSession).ToList();

                    foreach (RaidSession raidSession in raidSessions)
                    {
                        await _raidService.AnnounceNextSession(raidSession);
                        raidSession.Announced = true;
                        await _db.SaveChangesAsync();
                        //await _raidService.CreateReadyCheckForSession(raidSession);
                    }

                    Console.WriteLine("RaidSession end check");
                    List<RaidSession> sessionsToUpdate = _db.RaidSessions.Include(rs => rs.Roster).Include(rs => rs.Check).Where(rs => DateTime.UtcNow > rs.NextSession.AddMinutes(rs.Duration.TotalMinutes)).ToList();

                    foreach (RaidSession session in sessionsToUpdate)
                    {
                        Console.WriteLine("Check FFLOGS");
                        if (await _ffLogsService.IsRaidSessionDone(session))
                        {
                            await SendSummaryToRosterChannel(session, await _ffLogsService.RaidSessionSummary(session));
                            await UpdateNextSession(session, _db);
                        }
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

        private async Task SendSummaryToRosterChannel(RaidSession session, string summary)
        {
            if (String.IsNullOrWhiteSpace(summary)) return;
            var rosterChannel = _client.GetChannel(session.Roster.RosterChannel);
            if (rosterChannel == null) return;

            var rosterTextChannel = (SocketTextChannel)rosterChannel;

            await rosterTextChannel.SendMessageAsync(summary);

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

            raidSession.Announced = false;
            raidSession.ReportCode = null;
            raidSession.NextSession = potentialNextSession;
            await _db.SaveChangesAsync();
        }
    }
}
