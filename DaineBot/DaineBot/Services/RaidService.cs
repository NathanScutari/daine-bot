using DaineBot.Data;
using DaineBot.Models;
using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaineBot.Services
{
    public class RaidService
    {
        private readonly DaineBotDbContext _db;
        private readonly DiscordSocketClient _client;

        public RaidService(DaineBotDbContext db, DiscordSocketClient client)
        {
            _db = db;
            _client = client;
        }

        public DateTime GetNextSessionDateTime(RaidSession session)
        {
            var timezone = TimeZoneInfo.FindSystemTimeZoneById(session.Roster.TimeZoneId);
            int currentDay = (int)DateTime.UtcNow.DayOfWeek;
            int sessionDay = session.Day;
            int daysUntilNext = ((sessionDay - currentDay + 7) % 7);

            DateTime nextSessionDt = DateTime.UtcNow.Date.AddDays(daysUntilNext).AddHours(session.Hour).AddMinutes(session.Minute);
            nextSessionDt = DateTime.SpecifyKind(nextSessionDt, DateTimeKind.Unspecified);
            DateTime nextSessionConverted = TimeZoneInfo.ConvertTimeToUtc(nextSessionDt, timezone);

            if (nextSessionConverted < DateTime.UtcNow)
            {
                nextSessionConverted.AddDays(7);
            }

            return nextSessionConverted;
        }

        public async Task SendRefusalToRL(ReadyCheck check, SocketUser user)
        {
            var guild = _client.GetGuild(check.Session.Roster.Guild);
            var socketGuildUser = guild?.GetUser(user.Id);
            var RL = _client.GetUser(check.Session.Roster.RaidLeader);
            DateTime nextSession = GetNextSessionDateTime(check.Session);

            if (guild == null || socketGuildUser == null) return;

            string response = $"<:beuuuuuh:1024757080235712512> {socketGuildUser.Nickname ?? socketGuildUser.GlobalName} a refusé le ready check pour la prochaine session de raid de {guild.Name} le <t:{((DateTimeOffset)nextSession).ToUnixTimeSeconds()}:F>";

            await RL.SendMessageAsync(response);
        }

        public async Task SendRefusalReasonToRL(ReadyCheck check, SocketUser user, string reason)
        {
            var guild = _client.GetGuild(check.Session.Roster.Guild);
            var socketGuildUser = guild?.GetUser(user.Id);
            var RL = _client.GetUser(check.Session.Roster.RaidLeader);
            DateTime nextSession = GetNextSessionDateTime(check.Session);

            if (guild == null || socketGuildUser == null) return;

            string response = $"Aucune raison donnée";
            if (!String.IsNullOrWhiteSpace(response))
            {
                response = "Raison du refus: " + reason;
            }

            await RL.SendMessageAsync(response);
        }

        public List<(string sessionStr, int id)> GetAllSessionsForRoster(Roster roster)
        {
            List<(string, int)> sessions = new();

            foreach (RaidSession session in roster.Sessions)
            {
                DateTime sessionDT = GetNextSessionDateTime(session);
                (string, int) sessionTuple = ($"<t:{((DateTimeOffset)sessionDT).ToUnixTimeSeconds()}:F>", session.Id);
                sessions.Add(sessionTuple);
            }

            return sessions;
        }

        public async Task CreateReadyCheckForSession(RaidSession session)
        {
            var guild = _client.GetGuild(session.Roster.Guild);
            var role = guild.GetRole(session.Roster.RosterRole);

            DateTime utcNextSession = GetNextSessionDateTime(session);

            var readyCheck = new ReadyCheck()
            {
                AcceptedPlayers = new List<ulong>(),
                DeniedPlayers = new List<ulong>(),
                SessionId = session.Id,
            };

            await _db.ReadyChecks.AddAsync(readyCheck);
            await _db.SaveChangesAsync();

            var builder = new ComponentBuilder()
                .WithButton("Présent", $"readycheck_present:{readyCheck.Id}", ButtonStyle.Success)
                .WithButton("Absent", $"readycheck_absent:{readyCheck.Id}", ButtonStyle.Danger);

            var rosterChannel = guild.GetChannel(session.Roster.RosterChannel) as SocketTextChannel;

            if (rosterChannel == null)
                return;

            var message = await rosterChannel.SendMessageAsync(
                $"<@&{session.Roster.RosterRole}> Ready check pour la prochaine session de raid qui est prévue le <t:{((DateTimeOffset)utcNextSession).ToUnixTimeSeconds()}:F>. (0/{role.Members.Count()})",
                components: builder.Build());

            ReadyCheckMessage readyChechMessage = new()
            {
                CheckId = readyCheck.Id,
                MessageId = message.Id
            };

            _db.ReadyCheckMessages.Add(readyChechMessage);
            await _db.SaveChangesAsync();
        }
    }
}
