using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;
using Discord.WebSocket;
using Discord;
using DaineBot.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Serialization;
using System.Net;
using Microsoft.Extensions.DependencyInjection;

namespace DaineBot.Services
{
    public class PhilosopheService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _model = "gpt-5-mini"; // ou "gpt-4o-mini" si dispo
        private readonly IServiceProvider _services;
        private readonly DiscordSocketClient _client;
        private readonly RaidService _raidService;

        public PhilosopheService(IServiceProvider services, DiscordSocketClient client, RaidService raidService)
        {
            _httpClient = new HttpClient();
            _apiKey = Environment.GetEnvironmentVariable("GPT_KEY");
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            _services = services;
            _client = client;
            _raidService = raidService;
        }

        public async Task<string> GetChatGptResponse(SocketMessage message)
        {
            using var scope = _services.CreateScope();
            var _db = scope.ServiceProvider.GetRequiredService<DaineBotDbContext>();

            if (!(message.Channel is SocketGuildChannel))
            {
                return "Je ne réponds que sur le salon de raid du serveur. 🤌";
            }
            var guildChannel = message.Channel as SocketGuildChannel;
            if (guildChannel == null) return "";
            var roster = await _db.Rosters.Include(r => r.Sessions).FirstOrDefaultAsync(r => r.Guild == guildChannel.Guild.Id);

            if (roster == null) return "";
            if (guildChannel.Id != roster.RosterChannel) return "";

            var rosterChannel = guildChannel as SocketTextChannel;
            if (rosterChannel == null) return "";

            await rosterChannel.TriggerTypingAsync();

            var messages = await rosterChannel.GetMessagesAsync(limit: 20).FlattenAsync();
            var allChannelUsers = "";
            foreach (var user in rosterChannel.Users)
            {
                var userName = user.Nickname ?? user.GlobalName;
                if (user.IsBot)
                    continue;

                if (!String.IsNullOrEmpty(allChannelUsers)) allChannelUsers += ", ";

                allChannelUsers += userName;
            }
            var chatMessages = new List<Dictionary<string, string>>
        {
            new() { { "role", "system" }, { "content", "Tu es Daine Bot, un bot humoristique intégré dans discord." +
            "Tu réponds avec humour, en donnant l'impression d'une grande sagesse, mais tes réponses contiennent quand même une vraie information. Tes phrases peuvent sembler légèrement énigmatiques ou détournées, mais elles ne doivent pas être absurdes ni inutiles. L'objectif est de faire sourire tout en instruisant." +
            " Tu es dans un salon de raid de ffxiv, tu as donc des connaissances sur le jeu." +
            " Réponds aux gens qui te parlent en texte assez court, tes réponses font idéalement entre 1 et 3 phrases courtes. Tu évites les paragraphes et les listes." +
            " Tu ne poses pas de questions en fin de réponse, tu réponds simplement à la personne qui te parle." +
            " Lorsque tu utilises la recherche internet, tu adaptes ensuite l’information trouvée à ton ton habituel avant de répondre." +
            " Même après une recherche, tu évites le ton neutre ou académique. Tu reformules toujours l'information avec ta personnalité" +
            " Les 20 derniers messages servent à comprendre le ton, les tensions éventuelles, et le sujet en cours. Tu évites de répéter une information déjà donnée récemment. Si une réponse vient d’être apportée par un joueur, tu complètes ou valides brièvement au lieu de répéter." +
            " Les messages du salon sont précédés par le nom de l'utilisateur (ex : 'Alice : Salut'). Toi, tu ne mets jamais de nom ou de format spécial, tu réponds uniquement avec le contenu de ta réponse, comme si tu étais l'intervenant principal." +
            $" Voici tous les utilisateurs dans le salon pour contexte : {allChannelUsers}."} }
        };

            var nextSessionsList = _raidService.GetAllSessionsForRoster(roster);
            if (nextSessionsList.Count > 0)
            {
                var nextSessionsListString = "";
                foreach (var session in nextSessionsList)
                {
                    nextSessionsListString += $"\n- {session.sessionStr}";
                }
                chatMessages.Add(new Dictionary<string, string>
            {
                { "role", "system" },
                { "content", $" Tu as également connaissance des prochaines sessions de raid et tu peux aider les personnes qui te posent des questions par rapport à ça, voici les prochaines sessions (pas forcément dans l'ordre, à toi de comparer les dates) : {nextSessionsListString}." +
                $" Les heures sont données au fuseau horaire du roster : {roster.TimeZoneId}. Tu n’inventes jamais une date ou une heure absente de la liste fournie. Si une question concerne une session de raid et que l’information est incertaine ou discutée, tu invites l’utilisateur à utiliser la commande /raid-session plutôt que d’affirmer quelque chose." }
            });
            }

            foreach (var msg in messages.Reverse())
            {
                var user = rosterChannel.Guild.GetUser(msg.Author.Id);
                var userName = user.Nickname ?? user.GlobalName;
                var matches = Regex.Matches(msg.Content, "<@!?([0-9]+)>");
                var msgContent = msg.Content;


                foreach (Match match in matches)
                {
                    string userId = match.Groups[1].Value;
                    var tagUser = rosterChannel.GetUser(ulong.Parse(userId));
                    if (tagUser != null)
                    {
                        msgContent = msgContent.Replace(match.Value, "@" + (tagUser.Nickname ?? tagUser.GlobalName ?? tagUser.DisplayName));
                    }
                }

                chatMessages.Add(new Dictionary<string, string>
            {
                { "role", msg.Author.IsBot ? "assistant" : "user" },
                { "content", $"{(!(msg.Author.IsBot) ? (userName + " : ") : "")}{msgContent}" }
            });
            }

            var toolsList = new List<Dictionary<string, string>>();
            toolsList.Add(new Dictionary<string, string> { { "type", "web_search" } });

            var requestBody = new
            {
                model = _model,
                input = chatMessages,
                tools = toolsList
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("https://api.openai.com/v1/responses", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();

                // Cas typique d'erreur liée aux crédits/quota
                if (response.StatusCode == HttpStatusCode.PaymentRequired || errorContent.Contains("insufficient_quota"))
                {
                    return "Je n'ai plus les ressources pour produire ma sagesse, il faut demander à Den d'ouvrir son porte monnaie.";
                }

                return $"Erreur OpenAI: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}";

            }

            var responseString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseString);
            string finalText = "";

            if (doc.RootElement.TryGetProperty("output", out var outputArray))
            {
                foreach (var outputItem in outputArray.EnumerateArray())
                {
                    // On ne garde que les messages assistant
                    if (outputItem.GetProperty("type").GetString() != "message")
                        continue;

                    if (outputItem.GetProperty("role").GetString() != "assistant")
                        continue;

                    // Parcours du content
                    foreach (var contentItem in outputItem.GetProperty("content").EnumerateArray())
                    {
                        if (contentItem.GetProperty("type").GetString() == "output_text")
                        {
                            finalText += contentItem.GetProperty("text").GetString();
                        }
                    }
                }
            }
            finalText = Regex.Replace(finalText, @"^\\s*\\w+\\s*:\\s*", "", RegexOptions.Singleline);
            return finalText;
        }
    }
}