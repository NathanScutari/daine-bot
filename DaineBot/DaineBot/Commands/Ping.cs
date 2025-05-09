using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaineBot.Commands
{
    public class Ping : InteractionModuleBase<SocketInteractionContext>
    {
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
    }
}
