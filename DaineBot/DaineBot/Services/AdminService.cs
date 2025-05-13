using DaineBot.Data;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaineBot.Services
{
    public interface IAdminService
    {
        Task<bool> HasAdminRoleAsync(SocketInteractionContext context, bool noMessage = false);
    }

    public class AdminService : IAdminService
    {
        private readonly DaineBotDbContext _db;

        public AdminService(DaineBotDbContext db)
        {
            _db = db;
        }

        public async Task<bool> HasAdminRoleAsync(SocketInteractionContext context, bool noMessage = false)
        {
            if ((context.User as SocketGuildUser) == null)
            {
                if (!noMessage)
                    await context.Interaction.RespondAsync("Tu dois être dans un serveur pour utiliser la commande.", ephemeral: true);
                return false;
            }

            var roles = await _db.AdminRoles.AsNoTracking().FirstOrDefaultAsync(ar => ar.Guild == context.Guild.Id);

            if (roles == null)
            {
                if (!noMessage)
                    await context.Interaction.RespondAsync("Il n'y a pas de rôle admin configuré sur ce serveur, un admin doit d'abord utiliser admin-roles", ephemeral: true);
                return false;
            }

            SocketGuildUser user = (SocketGuildUser)context.User;
            bool hasAdminRole = false;
            foreach (ulong roleId in roles.RoleList)
            {
                var role = context.Guild.GetRole(roleId);
                if (user.Roles.Contains(role))
                    hasAdminRole = true;
            }

            if (hasAdminRole == false)
            {
                if (!noMessage)
                    await context.Interaction.RespondAsync("Tu n'as pas les droits nécessaires pour utiliser cette commande.", ephemeral: true);
                return false;
            }

            return true;
        }
    }
}
