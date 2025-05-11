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

        public async Task CreateReadyCheckForSession(RaidSession session)
        {
            var guild = _client.GetGuild(session.Roster.Guild);
            var role = guild.GetRole(session.Roster.RosterRole);

            var builder = new ComponentBuilder()
                .WithButton("Présent", $"readycheck_present:{session.Id}", ButtonStyle.Success)
                .WithButton("Absent", $"readycheck_absent:{session.Id}", ButtonStyle.Danger);

            DateTime utcNextSession = GetNextSessionDateTime(session);

            var readyCheck = new ReadyCheck()
            {
                AcceptedPlayers = new List<ulong>(),
                DeniedPlayers = new List<ulong>(),
                SessionId = session.Id,
            };

            await _db.ReadyChecks.AddAsync(readyCheck);
            await _db.SaveChangesAsync();

            foreach (var user in role.Members)
            {
                try
                {
                    var dm = await user.SendMessageAsync(
                        $"Rappel : la prochaine session de raid est prévue le <t:{((DateTimeOffset)utcNextSession).ToUnixTimeSeconds()}:F>.\nClique sur un bouton pour indiquer ta présence.",
                        components: builder.Build());
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Impossible d’envoyer un message à {user.Username}: {ex.Message}");
                }
            }
        }
    }
}
