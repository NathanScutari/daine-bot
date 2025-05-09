using Discord.Commands;
using Discord.WebSocket;
using Discord;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using DaineBot.Core;
using Discord.Interactions;
using Microsoft.VisualBasic;

namespace DaineBot
{
    public class Bot
    {
        private readonly IServiceProvider _services;
        private DiscordSocketClient _client;

        public Bot()
        {
            _services = ConfigureServices();
        }

        public async Task RunAsync()
        {
            _client = _services.GetRequiredService<DiscordSocketClient>();
            var interactionService = _services.GetRequiredService<InteractionService>();

            _client.Log += LogAsync;
            interactionService.Log += LogAsync;

            // Login et start
            await _client.LoginAsync(TokenType.Bot, Environment.GetEnvironmentVariable("DISCORD_DAINEBOT_TOKEN"));
            await _client.StartAsync();

            // Commandes
            var interactionHandler = _services.GetRequiredService<InteractionHandler>();
            await interactionHandler.InitializeAsync();

            await Task.Delay(-1);
        }

        private IServiceProvider ConfigureServices()
        {
            var client = new DiscordSocketClient();
            var interactionService = new InteractionService(client);

            var services = new ServiceCollection()
                .AddSingleton(client)
                .AddSingleton(interactionService)
                .AddSingleton<InteractionHandler>();

            return services.BuildServiceProvider();
        }

        private Task LogAsync(LogMessage log)
        {
            Console.WriteLine(log.ToString());
            return Task.CompletedTask;
        }
    }
}