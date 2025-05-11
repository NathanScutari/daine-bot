using DaineBot.Data;
using DaineBot.Models;
using DaineBot.Services;
using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using SummaryAttribute = Discord.Interactions.SummaryAttribute;

namespace DaineBot.Commands
{
    public class ReadyCheckCommand : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly DaineBotDbContext _db;
        private readonly IAdminService _adminService;

        public ReadyCheckCommand(DaineBotDbContext db, IAdminService adminService)
        {
            _db = db;
            _adminService = adminService;
        }

        [ComponentInteraction("readycheck_present:*")]
        public async Task ReadyCheckOK(string sessionIdRaw)
        {
            int sessionId = int.Parse(sessionIdRaw.Replace("readycheck_present:", ""));
            RaidSession? session = await _db.RaidSessions.Include(rs => rs.Check).FirstOrDefaultAsync(rs => rs.Id == sessionId);
            ReadyCheck? readyCheck = session?.Check;

            if (session == null || readyCheck == null)
            {
                await RespondAsync("https://tenor.com/view/this-is-fine-gif-24177057\nErreur pendant le ready check, tu peux contacter Den pour le prévenir ¯\\_(ツ)_/¯");
                return;
            }

            if (readyCheck.AcceptedPlayers.Contains(Context.User.Id))
            {
                await RespondAsync("Merci, mais tu avais déjà accepté cette session petit malin.");
                return;
            }

            readyCheck.DeniedPlayers.Remove(Context.User.Id);
            readyCheck.AcceptedPlayers.Add(Context.User.Id);
            await _db.SaveChangesAsync();

            await RespondAsync("Merci, ta présence a bien été enregistrée !");
        }

        [ComponentInteraction("readycheck_absent:*")]
        public async Task ReadyCheckKO(string sessionIdRaw)
        {
            int sessionId = int.Parse(sessionIdRaw.Replace("readycheck_absent:", ""));
            RaidSession? session = await _db.RaidSessions.Include(rs => rs.Check).FirstOrDefaultAsync(rs => rs.Id == sessionId);
            ReadyCheck? readyCheck = session?.Check;

            if (session == null || readyCheck == null)
            {
                await RespondAsync("https://tenor.com/view/this-is-fine-gif-24177057\nErreur pendant le ready check, tu peux contacter Den pour le prévenir ¯\\_(ツ)_/¯");
                return;
            }

            if (readyCheck.AcceptedPlayers.Contains(Context.User.Id))
            {
                await RespondAsync("Merci, mais tu avais déjà refusé cette session petit malin.");
                return;
            }

            var embed = new EmbedBuilder()
                .WithImageUrl("https://c.tenor.com/BYZf0mMHcY4AAAAd/tenor.gif")
                .Build();

            readyCheck.AcceptedPlayers.Remove(Context.User.Id);
            readyCheck.DeniedPlayers.Add(Context.User.Id);
            await _db.SaveChangesAsync();

            await RespondAsync("Merci, ton absence a bien été enregistrée !", embed: embed);
        }
    }
}
