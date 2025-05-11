using Discord;
using Discord.WebSocket;
using Discord.Interactions;
using System.Reflection;
using Discord.Commands;
using DaineBot.Commands;


namespace DaineBot.Core
{
    public class InteractionHandler
    {
        private readonly DiscordSocketClient _client;
        private readonly InteractionService _interactions;
        private readonly IServiceProvider _services;

        public InteractionHandler(DiscordSocketClient client, InteractionService interactions, IServiceProvider services)
        {
            _client = client;
            _interactions = interactions;
            _services = services;
        }

        public async Task InitializeAsync()
        {
            _client.InteractionCreated += async interaction =>
            {
                var ctx = new SocketInteractionContext(_client, interaction);
                await _interactions.ExecuteCommandAsync(ctx, _services);
            };

            await _interactions.AddModulesAsync(Assembly.GetEntryAssembly(), _services);

            _client.Ready += ReadyAsync;
        }

        private async Task ReadyAsync()
        {
            await _interactions.RegisterCommandsToGuildAsync(88326875641827328);

            // Supprimer les anciennes commandes
            var existingCommands = await _client.Rest.GetGlobalApplicationCommands();

            foreach (var cmd in existingCommands)
            {
                // Si une commande n'est pas dans les commandes actuelles, la supprimer
                if (!_interactions.Modules.Any(m => m.SlashCommands.Any(c => c.Name == cmd.Name)))
                {
                    await cmd.DeleteAsync();
                }
            }

            await _client.SetActivityAsync(new Game("le roster avec attention", ActivityType.Watching));
        }
    }
}

