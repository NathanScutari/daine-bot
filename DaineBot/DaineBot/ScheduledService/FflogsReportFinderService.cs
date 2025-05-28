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
    public class FflogsReportFinderService : BackgroundService
    {
        private readonly RaidService _raidService;
        private readonly BotReadyService _botReady;
        private readonly IServiceProvider _services;
        private readonly FFLogsService _fflogsService;
        private readonly DiscordSocketClient _client;

        public FflogsReportFinderService(RaidService raidService, BotReadyService botReady, IServiceProvider services, FFLogsService fflogsService, DiscordSocketClient client)
        {
            _raidService = raidService;
            _botReady = botReady;
            _services = services;
            _fflogsService = fflogsService;
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
                    List<RaidSession> activesSessions = await _db.RaidSessions.Include(rs => rs.Roster).Where(rs => (rs.ReportCode == null || rs.ReportCode == "") && DateTime.UtcNow > rs.NextSession.AddMinutes(-5) && DateTime.UtcNow < rs.NextSession.AddMinutes(rs.Duration.TotalMinutes)).ToListAsync();

                    foreach (RaidSession session in activesSessions)
                    {
                        string report = await _fflogsService.GetLogsByUserAsync("67302", session);
                        if (report != null && report != "")
                        {
                            session.ReportCode = report;
                            _db.SaveChanges();
                            await NotifyRosterChannelForLogUrl(session);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ScheduledTaskService] Erreur : {ex.Message}");
                }

                // ⏱️ Attente de 10 minuteS
                await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
            }
        }

        public async Task NotifyRosterChannelForLogUrl(RaidSession session)
        {
            var channel = await _client.GetChannelAsync(session.Roster.RosterChannel);

            if (channel != null && session.ReportCode != null)
            {
                var textChannel = (SocketTextChannel)channel;
                await textChannel.SendMessageAsync($"Report fflog trouvé : <https://www.fflogs.com/reports/{session.ReportCode}>");
            }
        }
    }
}
