using DaineBot.Data;
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
using System.Threading.Channels;
using System.Threading.Tasks;
using SummaryAttribute = Discord.Interactions.SummaryAttribute;

namespace DaineBot.Commands
{
    public class Roster : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly DaineBotDbContext _db;
        private readonly IAdminService _adminService;

        public Roster(DaineBotDbContext db, IAdminService adminService)
        {
            _db = db;
            _adminService = adminService;
        }

        [SlashCommand("nouveau-roster", "Créer un nouveau roster sur ce serveur")]
        public async Task RegisterRosterAsync()
        {
            if (!await _adminService.HasAdminRoleAsync(Context))
                return;

            var guildId = Context.Guild.Id;
            var existingRoster = await _db.Rosters.FirstOrDefaultAsync(r => r.Guild == guildId);

            if (existingRoster != null)
            {
                await RespondAsync("Un roster est déjà enregistré pour ce serveur.", ephemeral: true);
                return;
            }

            var builder = new ComponentBuilder()
                .WithSelectMenu(new SelectMenuBuilder()
                .WithCustomId("select_roster_role")
                .WithPlaceholder("Choisis un rôle pour le roster")
                .WithMinValues(1)
                .WithMaxValues(1)
                .WithType(ComponentType.RoleSelect));

            await RespondAsync("Choisis un rôle pour le roster :", components: builder.Build(), ephemeral: true);
        }

        [ComponentInteraction("select_roster_role")]
        public async Task HandleRosterRoleSelect(string[] selected)
        {
            if (!ulong.TryParse(selected.First(), out ulong roleId))
            {
                await RespondAsync("Erreur lors de la sélection du rôle.", ephemeral: true);
                return;
            }

            var builder = new ComponentBuilder()
                .WithSelectMenu(new SelectMenuBuilder()
                    .WithCustomId($"select_roster_channel:{roleId}")
                    .WithPlaceholder("Choisis un salon pour les messages du roster")
                    .WithMinValues(1)
                    .WithMaxValues(1)
                    .WithType(ComponentType.ChannelSelect));

            await RespondAsync($"Choisis un salon texte pour le roster :", components: builder.Build(), ephemeral: true);
        }

        [ComponentInteraction("select_roster_channel:*")]
        public async Task HandleRosterChannelSelect(string roleIdRaw, string[] selected)
        {
            if (!ulong.TryParse(roleIdRaw, out ulong roleId) || !ulong.TryParse(selected.First(), out ulong channelId))
            {
                await RespondAsync("Erreur lors de la sélection.", ephemeral: true);
                return;
            }

            var timeZones = new[]
            {
                "Europe/Paris",
                "Europe/London",
                "UTC",
                "America/New_York",
                "America/Chicago",
                "America/Denver",
                "America/Los_Angeles",
                "Europe/Berlin",
                "Europe/Moscow",
                "Asia/Tokyo",
                "Asia/Shanghai",
                "Asia/Kolkata",
                "Australia/Sydney",
                "Europe/Madrid",
                "Europe/Rome",
                "Africa/Johannesburg",
                "America/Sao_Paulo",
                "America/Toronto",
                "Asia/Singapore",
                "Asia/Dubai",
                "Pacific/Auckland",
                "Europe/Amsterdam",
                "Europe/Oslo",
                "Asia/Bangkok",
                "Asia/Seoul"
            };
            var timeZoneBuilder = new ComponentBuilder()
                .WithSelectMenu(new SelectMenuBuilder()
                    .WithCustomId($"select_roster_timezone:{roleId}:{channelId}")
                    .WithPlaceholder("Choisis un fuseau horaire pour le roster")
                    .WithMinValues(1)
                    .WithMaxValues(1)
                    .WithOptions(timeZones.Select(tz => new SelectMenuOptionBuilder()
                        .WithLabel(tz)
                        .WithValue(tz)).ToList()));

            await RespondAsync($"Choisis un fuseau horaire pour le roster :", components: timeZoneBuilder.Build(), ephemeral: true);
        }

        [ComponentInteraction("select_roster_timezone:*:*")]
        public async Task HandleRosterTimezoneSelect(string roleIdRaw, string channelIdRaw, string[] selected)
        {
            if (!ulong.TryParse(roleIdRaw, out ulong roleId) || !ulong.TryParse(channelIdRaw, out ulong channelId))
            {
                await RespondAsync("Erreur lors de la sélection.", ephemeral: true);
                return;
            }

            var timeZoneId = selected.First();
            var guildId = Context.Guild.Id;

            var builder = new ComponentBuilder()
                .WithSelectMenu(new SelectMenuBuilder()
                    .WithCustomId($"select_roster_raidleader:{roleId}:{channelId}:{timeZoneId}")
                    .WithPlaceholder("Choisis un raid leader pour ce roster")
                    .WithMinValues(1)
                    .WithMaxValues(1)
                    .WithType(ComponentType.UserSelect));

            

            await RespondAsync($"Choisis un raid leader pour ce roster :", components: builder.Build(), ephemeral: true);
        }

        [ComponentInteraction("select_roster_raidleader:*:*:*")]
        public async Task HandleRosterTimezoneSelect(string roleIdRaw, string channelIdRaw, string timeZoneId, string[] selected)
        {
            if (!ulong.TryParse(roleIdRaw, out ulong roleId) || !ulong.TryParse(channelIdRaw, out ulong channelId) || !ulong.TryParse(selected.First(), out ulong userId))
            {
                await RespondAsync("Erreur lors de la sélection.", ephemeral: true);
                return;
            }

            var guildId = Context.Guild.Id;

            var roster = new Models.Roster
            {
                Guild = guildId,
                RosterRole = roleId,
                RosterChannel = channelId,
                TimeZoneId = timeZoneId,
                RaidLeader = userId
            };

            _db.Rosters.Add(roster);
            await _db.SaveChangesAsync();

            await RespondAsync($"Roster enregistré avec succès pour le rôle <@&{roleId}> dans le salon <#{channelId}> avec le fuseau horaire '{timeZoneId}' et le raid lead <@{userId}>.", ephemeral: true);
        }

        [SlashCommand("supprimer-roster", "Supprime le roster enregistré pour ce serveur")]
        public async Task DeleteRosterAsync()
        {
            if (!await _adminService.HasAdminRoleAsync(Context))
                return;

            var guildId = Context.Guild.Id;
            var existingRoster = await _db.Rosters.FirstOrDefaultAsync(r => r.Guild == guildId);

            if (existingRoster == null)
            {
                await RespondAsync("Aucun roster enregistré pour ce serveur.", ephemeral: true);
                return;
            }

            var builder = new ComponentBuilder()
                .WithButton("Confirmer la suppression", customId: "confirm_delete_roster", ButtonStyle.Danger)
                .WithButton("Annuler", customId: "cancel_delete_roster", ButtonStyle.Secondary);

            await RespondAsync("Es-tu sûr de vouloir supprimer le roster ? Cette action est irréversible.", components: builder.Build(), ephemeral: true);
        }

        [ComponentInteraction("confirm_delete_roster")]
        public async Task ConfirmDeleteRosterAsync()
        {
            var guildId = Context.Guild.Id;
            var existingRoster = await _db.Rosters.FirstOrDefaultAsync(r => r.Guild == guildId);

            if (existingRoster == null)
            {
                await RespondAsync("Aucun roster trouvé.", ephemeral: true);
                return;
            }

            _db.Rosters.Remove(existingRoster);
            await _db.SaveChangesAsync();

            await RespondAsync("Roster supprimé avec succès.", ephemeral: true);
        }

        [ComponentInteraction("cancel_delete_roster")]
        public async Task CancelDeleteRosterAsync()
        {
            await RespondAsync("Suppression annulée.", ephemeral: true);
        }

        [SlashCommand("info-roster", "Donne des informations sur le roster de ce serveur")]
        public async Task InfoRoster()
        {
            var guildId = Context.Guild.Id;
            var existingRoster = await _db.Rosters.FirstOrDefaultAsync(r => r.Guild == guildId);

            if (existingRoster == null)
            {
                await RespondAsync("Aucun roster n'a encore été créé sur ce serveur.", ephemeral: true);
                return;
            }

            string response = $"Il existe un roster sur ce serveur :\n\n";

            var role = Context.Guild.GetRole(existingRoster.RosterRole);
            var salon = Context.Guild.GetTextChannel(existingRoster.RosterChannel);

            if (role == null)
            {
                await RespondAsync("Erreur en récupérant le rôle du roster, il a sûrement été supprimé sur le serveur après son enregistrement sur le bot.", ephemeral: true);
                return;
            }

            if (salon == null)
            {
                await RespondAsync("Erreur en récupérant le salon du roster, il a sûrement été supprimé sur le serveur après son enregistrement sur le bot.", ephemeral: true);
                return;
            }

            response += $"- Le **rôle** utilisé pour les membres du roster est <@&{role.Id}>\n\n";
            response += $"- Le **salon** utilisé pour le roster est <#{salon.Id}>\n\n";

            if (!Context.Guild.HasAllMembers)
            {
                await DeferAsync(ephemeral: true);
                await Context.Guild.DownloadUsersAsync();
            }

            int roleMembersNbr = Context.Guild.Users.Count(u => u.Roles.Contains(role));
            List<SocketGuildUser> users = role.Members.ToList();
            users.Sort(delegate (SocketGuildUser memberA, SocketGuildUser memberB) { return (memberA.Nickname != null) ? memberA.Nickname.CompareTo(memberB.Nickname ?? memberB.GlobalName) : memberA.GlobalName.CompareTo(memberB.Nickname ?? memberB.GlobalName); });

            response += $"- Il y a actuellement **{roleMembersNbr} membres** dans le roster :\n";

            foreach (SocketGuildUser user in users)
            {
                response += $"  * {user.Nickname ?? user.GlobalName}\n";
            }

            await RespondAsync(response, ephemeral: true);
        }
    }
}
