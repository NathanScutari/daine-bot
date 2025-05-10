using Discord.Commands;
using Discord.WebSocket;
using Discord;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using DaineBot.Core;
using Discord.Interactions;
using Microsoft.VisualBasic;
using DaineBot.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DaineBot.Services;

namespace DaineBot
{
    public class Bot
    {
        private readonly IServiceProvider _services;
        private DiscordSocketClient _client;

        public Bot()
        {
            var socketConfig = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.All
            };
            _client = new DiscordSocketClient(socketConfig);
            _services = ConfigureServices();
        }

        public async Task RunAsync()
        {
            var interactionService = _services.GetRequiredService<InteractionService>();

            _client.Log += LogAsync;
            interactionService.Log += LogAsync;

            // Login et start
            await _client.LoginAsync(TokenType.Bot, Environment.GetEnvironmentVariable("DISCORD_DAINEBOT_TOKEN"));
            await _client.StartAsync();

            // DB
            using (var scope = _services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<DaineBotDbContext>();
                db.Database.EnsureCreated();
            }

            // Commandes
            var interactionHandler = _services.GetRequiredService<InteractionHandler>();
            await interactionHandler.InitializeAsync();

            await Task.Delay(-1);
        }

        private IServiceProvider ConfigureServices()
        {
            var interactionService = new InteractionService(_client);

            var services = new ServiceCollection()
                .AddSingleton(_client)
                .AddSingleton(interactionService)
                .AddScoped<InteractionHandler>()
                .AddScoped<IAdminService, AdminService>()
                .AddDbContext<DaineBotDbContext>(options => options.UseSqlite("Data Source=dainebotdata.db").EnableSensitiveDataLogging().LogTo(Console.WriteLine, LogLevel.Information));

            return services.BuildServiceProvider();
        }

        private Task LogAsync(LogMessage log)
        {
            Console.WriteLine(log.ToString());
            return Task.CompletedTask;
        }
    }
}