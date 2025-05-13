using DaineBot.Data;
using DaineBot.Models;
using DaineBot.Services;
using Discord;
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
using static System.Collections.Specialized.BitVector32;

namespace DaineBot.ScheduledService
{
    public class ReadyCheckCheckerService : BackgroundService
    {
        private readonly RaidService _raidService;
        private readonly BotReadyService _botReady;
        private readonly IServiceProvider _services;
        private readonly DiscordSocketClient _client;
        private DaineBotDbContext _db;

        public ReadyCheckCheckerService(RaidService raidService, BotReadyService botReady, IServiceProvider services, DiscordSocketClient client)
        {
            _raidService = raidService;
            _botReady = botReady;
            _services = services;
            _client = client;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await _botReady.Ready;

            while (!stoppingToken.IsCancellationRequested)
            {
                using var scope = _services.CreateScope();
                _db = scope.ServiceProvider.GetRequiredService<DaineBotDbContext>();

                try
                {
                    List<ReadyCheck> readyChecks = _db.ReadyChecks.Include(rc => rc.Messages).Include(rc => rc.Session).ThenInclude(s => s.Roster).ToList();

                    foreach (ReadyCheck check in readyChecks)
                    {
                        if (IsReadyCheckComplete(check))
                        {
                            await SendCheckCompleteNotice(check);
                            continue;
                        }

                        if (DoesReadyCheckNeedReminder(check))
                        {
                            await SendReminder(check);
                            continue;
                        }

                        await CheckIfPurgeNeeded(check);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ScheduledTaskService] Erreur : {ex.Message}");
                }

                // ⏱️ Attente de 10 minutes
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        private async Task CheckIfPurgeNeeded(ReadyCheck check)
        {
            if (DateTime.UtcNow > ((DateTime)check.Session.NextSession).AddMinutes(10))
            {
                _db.ReadyChecks.Remove(check);
                await _db.SaveChangesAsync();
            }
        }

        private async Task SendCheckCompleteNotice(ReadyCheck check)
        {
            SocketTextChannel raidChannel = (SocketTextChannel)_client.GetChannel(check.Session.Roster.RosterChannel);
            SocketGuild guild = _client.GetGuild(check.Session.Roster.Guild);
            List<SocketGuildUser>? raiders = guild?.GetRole(check.Session.Roster.RosterRole)?.Members.ToList();
            DateTime sessionTime = (DateTime)check.Session.NextSession;

            if (raidChannel != null && raiders != null)
            {
                string response = $"<@&{check.Session.Roster.RosterRole}>, ready check terminé pour la prochaine session du <t:{((DateTimeOffset)sessionTime).ToUnixTimeSeconds()}:F>.\n";
                if (check.AcceptedPlayers.Count == raiders.Count)
                {
                    response += "**Tout le monde** a confirmé sa présence !";
                }
                else
                {
                    response += $"{check.AcceptedPlayers.Count}/{raiders.Count} membres ont confirmé être présent.";
                }

                await raidChannel.SendMessageAsync(response);

                if (check.Messages.Count > 0)
                {
                    foreach (var message in check.Messages)
                    {
                        var botMessage = await raidChannel.GetMessageAsync(message.MessageId);
                        if (botMessage != null)
                        {
                            await botMessage.DeleteAsync();
                        }
                        _db.ReadyCheckMessages.Remove(message);
                    }
                }
            }

            check.Complete = true;
            await _db.SaveChangesAsync();
        }

        private async Task SendReminder(ReadyCheck check)
        {
            SocketGuild guild = _client.GetGuild(check.Session.Roster.Guild);
            List<SocketGuildUser>? raiders = guild?.GetRole(check.Session.Roster.RosterRole)?.Members.ToList();

            DateTime sessionTime = (DateTime)check.Session.NextSession;
            DateTime responseTimeLimit = sessionTime.AddHours(-1);
            var builder = new ComponentBuilder()
                .WithButton("Présent", $"readycheck_present:{check.Id}", ButtonStyle.Success)
                .WithButton("Absent", $"readycheck_absent:{check.Id}", ButtonStyle.Danger);

            List<SocketGuildUser> missingUsers = GetMissingReadyCheckUsers(check);

            if (missingUsers.Count == 0 || raiders == null)
                return;

            ReadyCheckMessage? checkMessage = await _db.ReadyCheckMessages.FirstOrDefaultAsync(rcm => rcm.CheckId == check.Id);

            if (checkMessage != null)
            {
                var dmChannel = (SocketTextChannel?)guild?.GetChannel(check.Session.Roster.RosterChannel);

                if (dmChannel != null)
                {
                    var dmMessage = await dmChannel.GetMessageAsync(checkMessage.MessageId);

                    if (dmMessage != null)
                    {
                        await dmMessage.DeleteAsync();
                    }

                    int voted = check.AcceptedPlayers.Count + check.DeniedPlayers.Count;
                    int totalPlayers = raiders.Count;
                    var dm = await dmChannel.SendMessageAsync(
                    $"<@&{check.Session.Roster.RosterRole}> Rappel : La prochaine session de raid est prévue le <t:{((DateTimeOffset)sessionTime).ToUnixTimeSeconds()}:F>.\nMerci d'indiquer la présence ici. ({voted}/{totalPlayers})\n" +
                    $"Sans réponse de tout le monde avant le <t:{((DateTimeOffset)responseTimeLimit).ToUnixTimeSeconds()}:F>, ce bot s'autodétruira.",
                    components: builder.Build());

                    ReadyCheckMessage readyChechMessage = new()
                    {
                        CheckId = check.Id,
                        MessageId = dm.Id,
                    };

                    _db.ReadyCheckMessages.Add(readyChechMessage);
                }

                _db.ReadyCheckMessages.Remove(checkMessage);
            }

            check.ReminderSent = true;
            await _db.SaveChangesAsync();
        }

        private List<SocketGuildUser> GetMissingReadyCheckUsers(ReadyCheck check)
        {
            List<ulong> readyCheckUsers = new();
            List<SocketGuildUser> missingUsers = new();
            readyCheckUsers.AddRange(check.AcceptedPlayers);
            readyCheckUsers.AddRange(check.DeniedPlayers);

            SocketGuild guild = _client.GetGuild(check.Session.Roster.Guild);
            List<SocketGuildUser>? raiders = guild?.GetRole(check.Session.Roster.RosterRole)?.Members.ToList();

            if (raiders == null)
                return missingUsers;

            foreach (SocketGuildUser raider in raiders)
            {
                if (!readyCheckUsers.Contains(raider.Id))
                    missingUsers.Add(raider);
            }

            return missingUsers;
        }

        private bool IsReadyCheckComplete(ReadyCheck check)
        {
            if (check.Complete) return false;
            SocketGuild guild = _client.GetGuild(check.Session.Roster.Guild);
            List<SocketGuildUser>? raiders = guild?.GetRole(check.Session.Roster.RosterRole)?.Members.ToList();

            if (raiders?.Count == (check.AcceptedPlayers.Count + check.DeniedPlayers.Count))
            {
                return true;
            }

            return false;
        }

        private bool DoesReadyCheckNeedReminder(ReadyCheck check)
        {
            if (check.ReminderSent || check.Complete)
                return false;

            DateTime sessionTime = (DateTime)check.Session.NextSession;

            return sessionTime - DateTime.UtcNow < TimeSpan.FromHours(6);
        }
    }
}
