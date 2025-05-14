using DaineBot.Data;
using DaineBot.Models;
using DaineBot.Services;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaineBot.ScheduledService
{
    public class BotStatusRandomizerService : BackgroundService
    {
        private readonly BotReadyService _botReady;
        private readonly DiscordSocketClient _client;


        private readonly string[] _customRaidStatuses = new string[]
        {
            "Wipe imminent, restez groupés 🔥",
            "Pas prêt mais on pull 🐢",
            "Un jour, on tombera ce boss",
            "Les strats, c’est surfait 🤷",
            "Le RL pleure en silence",
            "Need heal... et thérapie",
            "AFK cerveau depuis 2019",
            "50% strat, 50% foi",
            "Tu tanks ? Moi non plus.",
            "On loot, donc on est",
            "Full buff, zéro skill 💀",
            "La strat : ignorée",
            "Logs gris, ego brisé",
            "Le vocal sent le wipe",
            "No brain, full parse 💪",
            "Plan B : prier très fort",
            "Encore une soirée de regrets",
            "Alt+F4 est un choix tactique",
            "Pourquoi est-ce toujours le healer ?",
            "Je suis là juste pour le loot",
            "Si tu meurs, je pleure aussi",
            "La strat ? C’est une option",
            "Une pause café avant le wipe",
            "Pourquoi l'aggro toujours sur moi ?",
            "Strat secrète : on tape !",
            "Un wipe de plus, c’est normal",
            "Parfait timing sur ce wipe 🔥",
            "Un tank, 10 wipes, bravo",
            "Pas besoin de healer, juste priez",
            "Ce raid est vraiment un cauchemar",
            "Les DPS sont... comment dire ?",
            "Les wipes, c’est la vie",
            "Ce boss aime nos larmes",
            "On respawn, encore plus forts !",
            "Pour gagner, il faut mourir",
            "Strat : courir et espérer",
            "J’ai trouvé la strat... à l'envers",
            "Ce wipe était programmé depuis le début",
            "Un boss, une défaite. Repartons !",
            "Du DPS, du heal et du chaos",
            "Sur ce boss, c’est la chance",
            "Wipe, on recommence. Encore et encore.",
            "Je m’attendais à pire... mais non.",
            "Faites gaffe aux AoE, svp",
            "Les wipes sont notre spécialité",
            "Pas de strat, juste du cœur",
            "On a pas wipe, c’est une victoire",
            "Plus de stratégie, moins de chaos",
            "Je ne respire qu'en phase de wipe",
            "On avance… ou pas ?",
            "Qui a encore oublié son buff ?",
            "Un dernier wipe, puis c’est gagné",
            "Qui veut tenter le raid ?",
            "Faites-vous des amis, ignorez la strat",
            "Ça sent la victoire, enfin... presque.",
            "On peut le faire… mais plus tard",
            "Je suis là pour le fun !",
            "On pourrait finir ce raid, non ?"
        };

        public BotStatusRandomizerService(BotReadyService botReady, DiscordSocketClient client)
        {
            _botReady = botReady;
            _client = client;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await _botReady.Ready;

            while (!stoppingToken.IsCancellationRequested)
            {

                try 
                {
                    await _client.SetCustomStatusAsync(this.GetNewRandomStatus());
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ScheduledTaskService] Erreur : {ex.Message}");
                }

                // ⏱️ Attente de 15 minuteS
                await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
            }
        }

        private string GetNewRandomStatus()
        {
            Random rng = new Random();

            return this._customRaidStatuses[rng.Next(this._customRaidStatuses.Length)];
        }
    }
}
