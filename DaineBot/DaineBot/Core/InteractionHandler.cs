using Discord;
using Discord.WebSocket;
using Discord.Interactions;
using System.Reflection;
using Discord.Commands;
using DaineBot.Commands;
using DaineBot.Services;


namespace DaineBot.Core
{
    public class InteractionHandler
    {
        private readonly DiscordSocketClient _client;
        private readonly InteractionService _interactions;
        private readonly IServiceProvider _services;
        private readonly PhilosopheService _philosopheService;

        public InteractionHandler(DiscordSocketClient client, InteractionService interactions, IServiceProvider services, PhilosopheService philosopheService)
        {
            _client = client;
            _interactions = interactions;
            _services = services;
            _philosopheService = philosopheService;
        }

        public async Task InitializeAsync()
        {
            _client.InteractionCreated += async interaction =>
            {
                var ctx = new SocketInteractionContext(_client, interaction);
                await _interactions.ExecuteCommandAsync(ctx, _services);
            };

            _client.MessageReceived += async message =>
            {
                if (message.MentionedUsers.Any(u => u.Id == _client.CurrentUser.Id))
                {
                    var response = await _philosopheService.GetChatGptResponse(message);
                    if (!String.IsNullOrEmpty(response))
                        await message.Channel.SendMessageAsync(response);
                }
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

