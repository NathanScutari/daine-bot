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
using Microsoft.Extensions.Hosting;
using DaineBot.ScheduledService;

namespace DaineBot
{
    public class Bot
    {
        private readonly IServiceProvider _services;
        private DiscordSocketClient _client;
        private IHost _host;

        public Bot(string[] args)
        {
            var socketConfig = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.All,
                AlwaysDownloadUsers = true,
            };
            _client = new DiscordSocketClient(socketConfig);
            ConfigureServices(args);
        }

        public async Task RunAsync()
        {
            var interactionService = _host.Services.GetRequiredService<InteractionService>();

            _client.Log += LogAsync;
            interactionService.Log += LogAsync;

            using (var scope = _host.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<DaineBotDbContext>();
                await db.Database.MigrateAsync();
            }

                // Login et start
                await _client.LoginAsync(TokenType.Bot, Environment.GetEnvironmentVariable("DISCORD_DAINEBOT_TOKEN"));
            await _client.StartAsync();

            // DB
            using (var scope = _host.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<DaineBotDbContext>();
                db.Database.EnsureCreated();
            }

            // Commandes
            var interactionHandler = _host.Services.GetRequiredService<InteractionHandler>();
            await interactionHandler.InitializeAsync();

            _client.Ready += () =>
            {
                var botReadyService = _host.Services.GetRequiredService<BotReadyService>();
                botReadyService.MarkReady();
                return Task.CompletedTask;
            };

            await _host.RunAsync();
        }

        private void ConfigureServices(string[] args)
        {
            var interactionService = new InteractionService(_client);

            var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
            var uri = new Uri(databaseUrl);
            var userInfo = uri.UserInfo.Split(':');

            var connectionString = $"Host={uri.Host};Port={uri.Port};Username={userInfo[0]};Password={userInfo[1]};Database={uri.AbsolutePath.TrimStart('/')};Trust Server Certificate=true;";

#if DEBUG
            connectionString += "SSL Mode=Disable;";
#else
            connectionString += "SSL Mode=Require;";
#endif

            _host = Host.CreateDefaultBuilder(args)
                .ConfigureLogging(logging =>
                {
                    logging.SetMinimumLevel(LogLevel.Warning);
                })
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton(_client);
                    services.AddSingleton(interactionService);
                    services.AddSingleton<InteractionHandler>();
                    services.AddSingleton<RaidService>();
                    services.AddSingleton<BotReadyService>();

                    services.AddDbContext<DaineBotDbContext>(options => options.UseNpgsql(connectionString));

                    services.AddScoped<IAdminService, AdminService>();
                    services.AddHostedService<TmpRaidSessionPurger>();
                    services.AddHostedService<RaidSessionReminderService>();
                    services.AddHostedService<ReadyCheckCheckerService>();
                })
                .Build();
        }

        private Task LogAsync(LogMessage log)
        {
            Console.WriteLine(log.ToString());
            return Task.CompletedTask;
        }
    }
}