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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaineBot.Commands
{
    public class Ping : InteractionModuleBase<SocketInteractionContext>
    {
        DaineBotDbContext _db;
        DiscordSocketClient _client;

        public Ping(DaineBotDbContext db, DiscordSocketClient client)
        {
            _db = db;
            _client = client;
        }

        [SlashCommand("moi", "Donne des infos sur l'utilisateur")]
        public async Task PingAsync()
        {
            // Récupérer l'utilisateur qui a invoqué la commande
            var socketUser = Context.User;

            if (socketUser is SocketGuildUser)
            {
                var user = socketUser as SocketGuildUser;
                await ReplyAsync($"{user.GlobalName} - {user.Nickname} sur le serveur {user.Guild.Name} (ID: {user.Id}");
            }
            else if (socketUser is SocketUser)
            {
                var user = socketUser;
                await ReplyAsync($"{user.GlobalName} (ID: {user.Id}");
            }
        }

        [SlashCommand("roster-id-info", "Récupère les infos des ids des membres du roster")]
        public async Task GetUsernameSlash()
        {
            Models.Roster? roster = await _db.Rosters.FirstOrDefaultAsync(r => r.Guild == Context.Guild.Id);
            List<SocketGuildUser> users = new List<SocketGuildUser>();


            if (roster == null)
            {
                await RespondAsync("Erreur", ephemeral: true);
                return;
            }

            var role = Context.Guild.GetRole(roster.RosterRole);

            string response = "";
            foreach (SocketGuildUser user in role.Members)
            {
                response += $"\n  - {user.Nickname ?? user.GlobalName} : {user.Id}";
            }

            await RespondAsync($"{response}", ephemeral: true);
        }

        [SlashCommand("check-info", "Récupère les infos du check en cours (test)")]
        public async Task GetCheckInfo()
        {
            Models.Roster? roster = await _db.Rosters.FirstOrDefaultAsync(r => r.Guild == Context.Guild.Id);
            List<SocketGuildUser> users = new List<SocketGuildUser>();

            ReadyCheck? check = await _db.ReadyChecks.Include(rc => rc.Session).ThenInclude(s => s.Roster).FirstOrDefaultAsync(rc => rc.Session.Roster.Guild == Context.Guild.Id);

            if (check == null)
            {
                await RespondAsync("Pas de ready check", ephemeral: true);
                return;
            }

            string response = "Accepté:";
            foreach (ulong id in check.AcceptedPlayers)
            {
                response += $"\n  - {id}";
            }
            response += "\n\nRefusé:";
            foreach (ulong id in check.DeniedPlayers)
            {
                response += $"\n  - {id}";
            }

            await RespondAsync($"{response}", ephemeral: true);
        }
    }
}
