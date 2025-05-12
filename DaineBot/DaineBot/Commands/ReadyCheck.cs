using DaineBot.Data;
using DaineBot.Models;
using DaineBot.Services;
using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;
using SummaryAttribute = Discord.Interactions.SummaryAttribute;

namespace DaineBot.Commands
{
    public class ReadyCheckCommand : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly DaineBotDbContext _db;
        private readonly IAdminService _adminService;
        private readonly RaidService _raidService;
        private readonly DiscordSocketClient _client;

        public ReadyCheckCommand(DaineBotDbContext db, IAdminService adminService, RaidService raidService, DiscordSocketClient client)
        {
            _db = db;
            _adminService = adminService;
            _raidService = raidService;
            _client = client;
        }

        [ComponentInteraction("readycheck_present:*")]
        public async Task ReadyCheckOK(string checkIdRaw)
        {

            int checkId = int.Parse(checkIdRaw.Replace("readycheck_absent:", ""));
            ReadyCheck? readyCheck = await _db.ReadyChecks.Include(rc => rc.Session).ThenInclude(s => s.Roster).FirstOrDefaultAsync(rc => rc.Id == checkId);
            RaidSession? session = readyCheck?.Session;
            
            if (session == null || readyCheck == null)
            {
                await RespondAsync("https://tenor.com/view/this-is-fine-gif-24177057\nErreur pendant le ready check, tu peux contacter Den pour le prévenir ¯\\_(ツ)_/¯", ephemeral: true);
                return;
            }

            var role = Context.Guild.GetRole(session.Roster.RosterRole);
            if (role == null)
            {
                await RespondAsync("https://tenor.com/view/this-is-fine-gif-24177057\nErreur pendant le ready check, tu peux contacter Den pour le prévenir ¯\\_(ツ)_/¯", ephemeral: true);
                return;
            }

            if (role.Members.ToList().Find(u => u.Id == Context.User.Id) == null)
            {
                await RespondAsync($"Bien essayé <@{Context.User.Id}>, mais tu fais pas partie du roster <:logansweet:684103798037544999>");
                return;
            }

            SocketTextChannel? rosterChannel = await _client.GetChannelAsync(session.Roster.RosterChannel) as SocketTextChannel;

            if (rosterChannel == null)
            {
                await RespondAsync("Erreur qui devrait pas être possible...", ephemeral: true);
                return;
            }

            if (readyCheck.AcceptedPlayers.Contains(Context.User.Id))
            {
                await RespondAsync("Merci, mais tu avais déjà accepté cette session petit malin.", ephemeral: true);
                return;
            }

            readyCheck.DeniedPlayers.Remove(Context.User.Id);
            readyCheck.AcceptedPlayers.Add(Context.User.Id);

            int totalPlayers = Context?.Guild?.GetRole(session.Roster.RosterRole)?.Members?.Count() ?? 0;
            int votedPlayers = readyCheck.DeniedPlayers.Count + readyCheck.AcceptedPlayers.Count;
            ReadyCheckMessage? checkMessage = await _db.ReadyCheckMessages.FirstOrDefaultAsync(rcm => rcm.CheckId == checkId);

            if (checkMessage != null)
            {
                var dmMessage = await rosterChannel.GetMessageAsync(checkMessage.MessageId) as IUserMessage;

                if (dmMessage != null)
                {
                    string content = dmMessage.Content;
                    await dmMessage.ModifyAsync(msg => msg.Content = content.Replace($"({votedPlayers - 1}/{totalPlayers})", $"({votedPlayers}/{totalPlayers})"));
                }
            }

            await _db.SaveChangesAsync();

            await RespondAsync("Merci, ta présence a bien été enregistrée !", ephemeral: true);
        }

        [ComponentInteraction("readycheck_absent:*")]
        public async Task ReadyCheckKO(string checkIdRaw)
        {
            int checkId = int.Parse(checkIdRaw.Replace("readycheck_absent:", ""));
            ReadyCheck? readyCheck = await _db.ReadyChecks.Include(rc => rc.Session).ThenInclude(s => s.Roster).FirstOrDefaultAsync(rc => rc.Id == checkId);
            RaidSession? session = readyCheck?.Session;

            if (session == null || readyCheck == null)
            {
                var errorEmbed = new EmbedBuilder()
                .WithImageUrl("https://c.tenor.com/BYZf0mMHcY4AAAAd/tenor.gif")
                .Build();
                await RespondAsync("Erreur pendant le ready check, tu peux contacter Den pour le prévenir ¯\\_(ツ)_/¯", embed: errorEmbed, ephemeral: true);
                return;
            }

            var role = Context.Guild.GetRole(session.Roster.RosterRole);
            if (role == null)
            {
                await RespondAsync("https://tenor.com/view/this-is-fine-gif-24177057\nErreur pendant le ready check, tu peux contacter Den pour le prévenir ¯\\_(ツ)_/¯", ephemeral: true);
                return;
            }

            if (role.Members.ToList().Find(u => u.Id == Context.User.Id) == null)
            {
                await RespondAsync($"Bien essayé <@{Context.User.Id}>, mais tu fais pas partie du roster <:logansweet:684103798037544999>");
                return;
            }

            SocketTextChannel? rosterChannel = await _client.GetChannelAsync(session.Roster.RosterChannel) as SocketTextChannel;

            if (rosterChannel == null)
            {
                await RespondAsync("Erreur qui devrait pas être possible...", ephemeral: true);
                return;
            }

            if (readyCheck.DeniedPlayers.Contains(Context.User.Id))
            {
                await RespondAsync("Merci, mais tu avais déjà refusé cette session petit malin.", ephemeral: true);
                return;
            }

            readyCheck.AcceptedPlayers.Remove(Context.User.Id);
            readyCheck.DeniedPlayers.Add(Context.User.Id);

            int totalPlayers = Context?.Guild?.GetRole(session.Roster.RosterRole)?.Members?.Count() ?? 0;
            int votedPlayers = readyCheck.DeniedPlayers.Count + readyCheck.AcceptedPlayers.Count;
            ReadyCheckMessage? checkMessage = await _db.ReadyCheckMessages.FirstOrDefaultAsync(rcm => rcm.CheckId == checkId);

            if (checkMessage != null)
            {
                var dmMessage = await rosterChannel.GetMessageAsync(checkMessage.MessageId) as IUserMessage;

                if (dmMessage != null)
                {
                    string content = dmMessage.Content;
                    await dmMessage.ModifyAsync(msg => msg.Content = content.Replace($"({votedPlayers - 1}/{totalPlayers})", $"({votedPlayers}/{totalPlayers})"));
                }
            }

            
            await _db.SaveChangesAsync();

            await _raidService.SendRefusalToRL(readyCheck, Context.User);

            await RespondWithModalAsync<ReasonModal>($"readycheck_absent_reason:{readyCheck.Id}");
        }

        [ModalInteraction("readycheck_absent_reason:*")]
        public async Task RaidSessionFinaliseCreation(string checkIdRaw, ReasonModal modal)
        {
            int checkId = int.Parse(checkIdRaw.Replace("readycheck_absent:", ""));
            ReadyCheck? readyCheck = await _db.ReadyChecks.Include(rc => rc.Session).ThenInclude(s => s.Roster).FirstOrDefaultAsync(rc => rc.Id == checkId);

            if (readyCheck == null)
            {
                var errorEmbed = new EmbedBuilder()
                .WithImageUrl("https://c.tenor.com/BYZf0mMHcY4AAAAd/tenor.gif")
                .Build();
                await RespondAsync("Erreur pendant le ready check, tu peux contacter Den pour le prévenir ¯\\_(ツ)_/¯", embed: errorEmbed, ephemeral: true);
                return;
            }

            var embed = new EmbedBuilder()
                .WithImageUrl("https://c.tenor.com/BYZf0mMHcY4AAAAd/tenor.gif")
                .Build();

            await _raidService.SendRefusalReasonToRL(readyCheck, Context.User, modal.Reason);

            await RespondAsync("Merci, ton absence a bien été enregistrée !", embed: embed, ephemeral: true);
        }

    }

    public class ReasonModal : IModal
    {
        public string Title => "Si tu veux envoyer la raison";

        [InputLabel("Raison (optionnelle)")]
        [ModalTextInput("reason", TextInputStyle.Paragraph)]
        [Required(AllowEmptyStrings = true)]
        public string Reason { get; set; } = "";
    }
}
