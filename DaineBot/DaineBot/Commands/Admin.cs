using DaineBot.Data;
using DaineBot.Models;
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
using SummaryAttribute = Discord.Interactions.SummaryAttribute;

namespace DaineBot.Commands
{
    public class Admin : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly DaineBotDbContext _db;

        public Admin(DaineBotDbContext db)
        {
            _db = db;
        }

        [SlashCommand("admin-roles", "Configure les rôles qui ont le droit d'utiliser toutes les commandes de gestion du bot")]
        public async Task RegisterRosterAsync()
        {
            var guildId = Context.Guild.Id;
            var user = (SocketGuildUser)Context.User;
            if (user == null)
            {
                await RespondAsync("Vous devez être dans un serveur pour utiliser cette commande.", ephemeral: true);
                return;
            }

            if (!user.GuildPermissions.Administrator)
            {
                await RespondAsync("Il faut être admin sur le serveur pour utiliser cette commande.", ephemeral: true);
                return;
            }

            var builder = new ComponentBuilder()
                .WithSelectMenu(new SelectMenuBuilder()
                .WithCustomId("select_admin_roles")
                .WithPlaceholder("Choisis les rôles admins")
                .WithMinValues(1)
                .WithMaxValues(10)
                .WithType(ComponentType.RoleSelect));

            await RespondAsync("Choisis les rôles admins :", components: builder.Build(), ephemeral: true);
        }

        [ComponentInteraction("select_admin_roles")]
        public async Task HandleRosterRoleSelect(string[] selected)
        {
            List<ulong> roleIds = new List<ulong>();

            foreach (var id in selected)
            {
                if (ulong.TryParse(id, out ulong roleId))
                {
                    roleIds.Add(roleId);
                }
            }

            var adminRoles = await _db.AdminRoles.FirstOrDefaultAsync(ar => ar.Guild == Context.Guild.Id);
            if (adminRoles == null)
            {
                adminRoles = new AdminRole();
                _db.AdminRoles.Add(adminRoles);
            }
            adminRoles.Guild = Context.Guild.Id;
            adminRoles.RoleList = roleIds;

            await _db.SaveChangesAsync();

            await RespondAsync("La liste des rôles admin a été configurée", ephemeral: true);
        }

        [SlashCommand("admin-roles-info", "Liste les rôles autorisés à gérer le roster avec le bot")]
        public async Task DisplayAdminRolesInfo()
        {
            var guildId = Context.Guild.Id;
            var user = (SocketGuildUser)Context.User;
            var adminRoles = await _db.AdminRoles.FirstOrDefaultAsync(ar => ar.Guild ==  guildId);

            if (adminRoles == null)
            {
                await RespondAsync("Il n'y a pas encore de rôles admin configurés sur ce serveur.");
                return;
            }

            string plural = adminRoles.RoleList.Count > 1 ? "s" : "";
            string response = $"Il y a **{adminRoles.RoleList.Count} rôle{plural} configuré{plural} sur ce serveur :**";

            foreach (ulong roleId in adminRoles.RoleList)
            {
                var role = Context.Guild.GetRole(roleId);
                response += $"\n  - {role.Name}";
            }

            await RespondAsync(response, ephemeral: true);
        }
    }
}
