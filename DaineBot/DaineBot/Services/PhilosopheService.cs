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

namespace DaineBot.Services
{
    public class PhilosopheService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _model = "gpt-4.1-mini"; // ou "gpt-4o-mini" si dispo
        private readonly DaineBotDbContext _db;
        private readonly DiscordSocketClient _client;

        public PhilosopheService(DaineBotDbContext db, DiscordSocketClient client)
        {
            _httpClient = new HttpClient();
            _apiKey = Environment.GetEnvironmentVariable("GPT_KEY");
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            _db = db;
            _client = client;
        }

        public async Task<string> GetChatGptResponse(SocketMessage message)
        {
            if (!(message.Channel is SocketGuildChannel))
            {
                return "Je ne réponds que sur le salon de raid du serveur. 🤌";
            }
            var guildChannel = message.Channel as SocketGuildChannel;
            if (guildChannel == null) return "";
            var roster = await _db.Rosters.FirstOrDefaultAsync(r => r.Guild == guildChannel.Guild.Id);

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
            "Tu es dans un salon de raid de ffxiv, tu as donc des connaissances sur le jeu, mais en restant philosophe." +
            "Réponds aux gens qui te parlent en texte assez court, 250 lettres max, sans dire qui tu es ou de \"en tant que\", tu réponds directement." +
            "Les messages du salon sont précédés par le nom de l'utilisateur (ex : 'Alice : Salut'). Toi, tu ne mets jamais de nom ou de format spécial, tu réponds uniquement avec le contenu de ta réponse, comme si tu étais l'intervenant principal." +
            $"Voici tous les utilisateurs dans le salon pour contexte : {allChannelUsers}."} }
        };

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

            var requestBody = new
            {
                model = _model,
                messages = chatMessages
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);

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
            var gptResponse = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            gptResponse = Regex.Replace(gptResponse, @"^\\s*\\w+\\s*:\\s*", "", RegexOptions.Singleline);
            return gptResponse;
        }
    }
}