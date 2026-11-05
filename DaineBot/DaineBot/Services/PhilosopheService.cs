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
using DaineBot.Models;
using System;
using System.Globalization;

namespace DaineBot.Services
{
    public class PhilosopheService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _model = "gpt-6-luna";
        private readonly IServiceProvider _services;
        private readonly DiscordSocketClient _client;
        private readonly RaidService _raidService;

        private sealed record RaidToolDefinition(string Name, string Description, bool RequiresManagementRights, string ArgumentsExample);

        private static readonly RaidToolDefinition[] RaidTools =
        {
            new("raid_sessions_list", "Consulter les sessions configurées, leurs prochaines dates et leurs durées.", false, "{}"),
            new("raid_next_session_set_datetime", "Déplacer uniquement la prochaine occurrence de raid, sans changer le planning hebdomadaire. new_start_local est une date locale au format YYYY-MM-DD HH:mm dans le fuseau du roster.", true, "{ \"new_start_local\": \"YYYY-MM-DD HH:mm\" }"),
            new("raid_session_set_next_datetime", "Déplacer la prochaine occurrence d'une session identifiée par session_id, sans changer son planning hebdomadaire. new_start_local est une date locale au format YYYY-MM-DD HH:mm dans le fuseau du roster.", true, "{ \"session_id\": 123, \"new_start_local\": \"YYYY-MM-DD HH:mm\" }"),
            new("raid_session_set_duration", "Changer la durée d'une session identifiée par session_id : duration_minutes de 1 à 1440. Ce changement concerne la session récurrente et ses prochaines occurrences ; une durée différente pour une seule soirée n'est pas prise en charge.", true, "{ \"session_id\": 123, \"duration_minutes\": 120 }"),
            new("raid_next_session_skip_week", "Annuler la prochaine occurrence en la reportant à la semaine suivante, sans supprimer la session récurrente.", true, "{}"),
            new("raid_session_delete", "Supprimer définitivement une session récurrente identifiée par session_id, ainsi que son ready check associé. Ne pas utiliser pour annuler une seule soirée.", true, "{ \"session_id\": 123 }"),
            new("raid_session_create_weekly", "Créer une session hebdomadaire : day de 0 (dimanche) à 6 (samedi), hour de 0 à 23, minute de 0 à 59 et duration_minutes de 1 à 1440. L'heure est locale au fuseau du roster.", true, "{ \"day\": 1, \"hour\": 20, \"minute\": 30, \"duration_minutes\": 120 }")
        };

        private static string BuildRaidToolsContext()
        {
            return "Actions disponibles :\n" +
                string.Join("\n", RaidTools.Select(tool =>
                    $"- {tool.Name} : {tool.Description} Droits : " +
                    (tool.RequiresManagementRights
                        ? "raid leader configuré ou membre ayant un rôle admin configuré via /admin-roles (le statut administrateur Discord seul ne suffit pas)."
                        : "tous les utilisateurs du salon.")));
        }

        public PhilosopheService(IServiceProvider services, DiscordSocketClient client, RaidService raidService)
        {
            _httpClient = new HttpClient();
            _apiKey = Environment.GetEnvironmentVariable("GPT_KEY");
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            _services = services;
            _client = client;
            _raidService = raidService;
        }

        public async Task<string> GetChatGptSummaryComment(Roster rosterSession, string summaryReport)
        {
            using var scope = _services.CreateScope();
            var _db = scope.ServiceProvider.GetRequiredService<DaineBotDbContext>();

            Roster? roster = _db.Rosters.Where(r => r.Id == rosterSession.Id).Include(r => r.Sessions).FirstOrDefault();

            if (roster == null)
                return "Le commentateur était absent, nous n'avons pas reçu de message de sa part.";

            var rosterChannel = _client.GetChannel(roster.RosterChannel) as SocketTextChannel;
            if (rosterChannel == null) return "";

            await rosterChannel.TriggerTypingAsync();

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
            " N'utilise pas de tirets de ponctuation (courts, moyens ou longs) ni de listes à puces. Préfère les points et les virgules. Conserve les traits d'union nécessaires à l'orthographe, aux dates, aux liens et aux identifiants." +
            " Une soirée de raid vient de se terminer et tu viens d'envoyer le résumé de la soirée qui est le dernier message envoyé dont tu as connaissance, fais un petit commentaire philosophique sur le résumé de la soirée pour donner ton ressenti plein de sagesse, le commentaire doit fait 3 phrases maximum." +
            " Tu peux utiliser une recherche web pour mieux comprendre le contexte du raid et les boss décris dans le résumé" +
            $" Voici tous les utilisateurs dans le salon pour contexte : {allChannelUsers}."} }
        };

            var nextSessionsList = _raidService.GetAllSessionsForRoster(roster, removeToday: true);
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
                { "content", $" Tu as également connaissance des prochaines sessions de raid et tu peux te servir de ce contexte pour le commentaire, voici les prochaines sessions (pas forcément dans l'ordre, à toi de comparer les dates) : {nextSessionsListString}." +
                $" Les heures sont données au fuseau horaire du roster : {roster.TimeZoneId}. Tu n’inventes jamais une date ou une heure absente de la liste fournie." }
            });
            }

            chatMessages.Add(new Dictionary<string, string>
            {
                { "role", "assistant" },
                { "content", summaryReport }
            });

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

                // Erreur liée aux crédits ou au quota
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
                    // On ne garde que les messages de l'assistant
                    if (outputItem.GetProperty("type").GetString() != "message")
                        continue;

                    if (outputItem.GetProperty("role").GetString() != "assistant")
                        continue;

                    // Parcours du contenu
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

        public async Task<string> GetChatGptResponse(SocketMessage message, string? extraSystemContext = null)
        {
            Console.WriteLine($"[{DateTime.UtcNow:O}] [LLM message={message.Id}] Chat : génération de la réponse avec {_model}, bilan technique présent={!string.IsNullOrWhiteSpace(extraSystemContext)}.");
            using var scope = _services.CreateScope();
            var _db = scope.ServiceProvider.GetRequiredService<DaineBotDbContext>();

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
            " Tu réponds avec humour, en donnant l'impression d'une grande sagesse, mais tes réponses contiennent quand même une vraie information. Tes phrases peuvent sembler légèrement énigmatiques ou détournées, mais elles ne doivent pas être absurdes ni inutiles. L'objectif est de faire sourire tout en instruisant." +
            " Tu es dans un salon de raid de FFXIV et tu connais le jeu, mais ce contexte ne doit pas envahir tes réponses." +
            " N'utilise pas de tirets de ponctuation (courts, moyens ou longs) ni de listes à puces. Préfère les points et les virgules. Conserve les traits d'union nécessaires à l'orthographe, aux dates, aux liens et aux identifiants." +
            " Réponds aux gens qui te parlent en texte assez court, tes réponses font idéalement entre 1 et 3 phrases courtes. Tu évites les paragraphes et les listes." +
            " Tu ne poses pas de questions en fin de réponse, tu réponds simplement à la personne qui te parle." +
            " Lorsque tu utilises la recherche internet, tu adaptes ensuite l’information trouvée à ton ton habituel avant de répondre." +
            " Même après une recherche, tu évites le ton neutre ou académique. Tu reformules toujours l'information avec ta personnalité" +
            " Les 20 derniers messages servent à comprendre le ton, les tensions éventuelles, et le sujet en cours. Tu évites de répéter une information déjà donnée récemment. Si une réponse vient d’être apportée par un joueur, tu complètes ou valides brièvement au lieu de répéter." +
            " Les messages du salon sont précédés par le nom de l'utilisateur (ex : 'Alice : Salut'). Toi, tu ne mets jamais de nom ou de format spécial, tu réponds uniquement avec le contenu de ta réponse, comme si tu étais l'intervenant principal." +
            $" Voici tous les utilisateurs dans le salon pour contexte : {allChannelUsers}."} }
        };

            chatMessages.Add(new Dictionary<string, string>
            {
                { "role", "system" },
                { "content", BuildRaidToolsContext() +
                    "\nLes actions de raid sont exécutées par le code C# avant cet appel conversationnel. Ton rôle est de formuler la réponse, pas de les exécuter à nouveau. " +
                    "Le fait que seul web_search soit disponible dans cet appel ne signifie pas que le bot ne dispose pas des outils de raid. " +
                    "\nSi l'utilisateur demande tes capacités ou les droits nécessaires, décris ces actions en langage naturel. " +
                    "Une liste numérotée concise est alors autorisée, sans puces ni tirets de ponctuation. Cette documentation n'est pas un bilan d'exécution : " +
                    "ne confirme une action que si le bilan technique indique sa réussite." }
            });

            foreach (var msg in messages.Where(m => m.Id < message.Id).OrderBy(m => m.Id))
            {
                var user = rosterChannel.Guild.GetUser(msg.Author.Id);
                var userName = user?.Nickname ?? user?.GlobalName ?? msg.Author.Username;
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
                { "role", msg.Author.Id == _client.CurrentUser.Id ? "assistant" : "user" },
                { "content", $"{(msg.Author.Id != _client.CurrentUser.Id ? (userName + " : ") : "")}{msgContent}" }
            });
            }

            chatMessages.Add(new Dictionary<string, string>
            {
                { "role", "user" },
                { "content", $"{message.Author.Username} : {NormalizeDiscordMentions(rosterChannel, message.Content)}" }
            });

            if (!string.IsNullOrWhiteSpace(extraSystemContext))
            {
                chatMessages.Add(new Dictionary<string, string>
                {
                    { "role", "system" },
                    { "content", $"Bilan technique produit par le code C# pour le message {message.Id}, après exécution des outils :\n" + extraSystemContext +
                        "\nCe bilan est la source de vérité pour les actions de ce message. Il prime sur les affirmations de l'historique concernant tes capacités ou une ancienne absence d'action. " +
                        "Les commandes marquées comme exécutées ont réellement réussi : confirme leurs résultats à l'utilisateur avec ton style habituel. " +
                        "Ne dis pas que tu n'as rien fait, que tu ne peux pas agir ou que tu manques d'outils si le bilan indique une réussite. " +
                        "Une liste de sessions consultée avant une modification est un instantané antérieur : le résultat de la modification fait foi pour la nouvelle date ou durée. " +
                        "En cas de refus ou d'échec, explique le motif sans annoncer de réussite ; si certaines actions ont réussi, distingue-les des échecs. " +
                        "N'invente aucune autre action. Si une précision manque, tu peux poser une question courte pour la demander." }
                });
                Console.WriteLine($"[{DateTime.UtcNow:O}] [LLM message={message.Id}] Chat : bilan d'exécution ajouté après l'historique et le message courant.");
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

                // Erreur liée aux crédits ou au quota
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
                    // On ne garde que les messages de l'assistant
                    if (outputItem.GetProperty("type").GetString() != "message")
                        continue;

                    if (outputItem.GetProperty("role").GetString() != "assistant")
                        continue;

                    // Parcours du contenu
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

        /// <summary>
        /// Détecte les actions de raid : consultation pour tous, modifications pour le raid leader ou les administrateurs configurés.
        /// Renvoie un bilan factuel ou un motif de refus pour le contexte de discussion, ou null pour une conversation ordinaire.
        /// </summary>
        public async Task<string?> GetRaidLeaderToolOrChatResponse(SocketMessage message)
        {
            void Trace(string details) => Console.WriteLine($"[{DateTime.UtcNow:O}] [LLM message={message.Id}] {details}");

            Trace($"Début du traitement des outils : auteur={message.Author.Id}, salon={message.Channel.Id}, modèle={_model}.");
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DaineBotDbContext>();
            var adminService = scope.ServiceProvider.GetRequiredService<IAdminService>();

            if (message.Channel is not SocketGuildChannel guildChannel)
            {
                Trace("Outils ignorés : le message ne provient pas d'un serveur.");
                return null;
            }

            var roster = await db.Rosters
                .Include(r => r.Sessions)
                .FirstOrDefaultAsync(r => r.Guild == guildChannel.Guild.Id);

            if (roster == null)
            {
                Trace("Outils ignorés : aucun roster configuré pour ce serveur.");
                return null;
            }

            // Les outils sont utilisables uniquement dans le salon du roster.
            if (guildChannel.Id != roster.RosterChannel)
            {
                Trace("Outils ignorés : le message ne provient pas du salon du roster.");
                return null;
            }

            var socketGuildUser = guildChannel.Guild.GetUser(message.Author.Id);
            if (socketGuildUser == null)
            {
                Trace("Outils ignorés : auteur introuvable parmi les membres du serveur.");
                return null;
            }

            var rosterChannel = message.Channel as SocketTextChannel;
            if (rosterChannel == null)
            {
                Trace("Outils ignorés : le salon n'est pas un salon texte compatible.");
                return null;
            }

            await rosterChannel.TriggerTypingAsync();

            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(roster.TimeZoneId);

            string normalizedMessage = NormalizeDiscordMentions(rosterChannel, message.Content);

            var systemPrompt =
                "Tu es Daine Bot, un bot Discord. Tu dois décider si le message de l'utilisateur demande une action de gestion de sessions de raid." +
                " Si oui, tu produis un plan d'actions sous forme JSON strict." +
                " Si non, renvoie kind=no_action, assistant_message vide et calls vide. Tu ne rédiges jamais de réponse conversationnelle." +
                "\n\nContraintes IMPORTANTES:" +
                "\n- Ta sortie DOIT être un UNIQUE objet JSON, sans texte autour, sans markdown." +
                "\n- Ne propose jamais d'actions qui ne sont pas explicitement demandées." +
                "\n- Pour toute question nécessitant de connaître le planning (prochaine session, dates, horaires ou durées), appelle raid_sessions_list afin de fournir les données au bot conversationnel." +
                "\n- Une question sur tes capacités ou les droits nécessaires ne demande aucune exécution : renvoie kind=no_action, assistant_message vide et calls vide, sauf si une action est aussi explicitement demandée." +
                "\n- N'invente jamais d'identifiants de session. Utilise uniquement ceux fournis dans le contexte." +
                "\n- Avant de modifier, déplacer, annuler ou supprimer une session existante, appelle uniquement raid_sessions_list. Tu recevras son résultat avant de préparer les commandes suivantes. Ne demande jamais à l'utilisateur un identifiant que cet outil permet de trouver." +
                "\n- Après consultation, prépare les actions demandées à partir des résultats. Ne relance pas la consultation si les informations sont déjà disponibles. Pour une simple question de planning, aucune autre commande n'est nécessaire après la consultation." +
                "\n- day est le jour hebdomadaire habituel (0=dimanche, 3=mercredi). next_session_local est la date réelle de la prochaine occurrence, éventuellement déplacée. Pour 'la session du mercredi', utilise le planning habituel sauf précision contraire ; si plusieurs sessions correspondent, demande une précision." +
                "\n- Pour déplacer du mercredi au jeudi, calcule la date du jeudi relativement à l'occurrence sélectionnée dans le fuseau du roster, et non relativement à une autre session. Ne change pas la récurrence hebdomadaire." +
                "\n- Une modification de durée s'applique à la session récurrente. Si l'utilisateur demande explicitement une durée exceptionnelle pour une seule occurrence, n'applique pas de changement permanent et indique cette limite." +
                "\n- Si une action est demandée mais ambiguë ou impossible avec les outils disponibles, renvoie kind=no_action, calls vide et indique uniquement la raison du refus ou les précisions manquantes dans assistant_message." +
                "\n\n" + BuildRaidToolsContext() +
                "\n\nSchéma JSON attendu:" +
                "\n{" +
                "\n  \"kind\": \"no_action\" | \"tool_calls\"," +
                "\n  \"assistant_message\": \"string\"," +
                "\n  \"calls\": [" +
                string.Join(" |", RaidTools.Select(tool => $"\n    {{ \"tool\": \"{tool.Name}\", \"args\": {tool.ArgumentsExample} }}")) +
                "\n  ]" +
                "\n}";

            var chatMessages = new List<Dictionary<string, string>>
            {
                new() { { "role", "system" }, { "content", systemPrompt } },
                new() { { "role", "system" }, { "content", $"Date/heure actuelle (UTC): {DateTime.UtcNow:O}. Fuseau horaire roster: {roster.TimeZoneId}." } },
                new() { { "role", "user" }, { "content", normalizedMessage } },
            };

            bool canExecute = message.Author.Id == roster.RaidLeader ||
                await adminService.HasAdminRoleAsync(guildChannel.Guild, socketGuildUser);
            Trace($"Contexte : serveur={guildChannel.Guild.Id}, roster={roster.Id}, fuseau={roster.TimeZoneId}, raid leader={message.Author.Id == roster.RaidLeader}, gestion autorisée={canExecute}, consultation autorisée=True.");
            var results = new List<string>();
            var executed = new List<string>();
            bool sessionsRetrieved = false;
            int remainingCalls = 5;

            // Une première analyse peut consulter le planning, puis une seconde prépare les modifications.
            for (int step = 0; step < 2; step++)
            {
                Trace($"Analyse {step + 1}/2 : envoi au modèle, planning déjà consulté={sessionsRetrieved}, appels restants={remainingCalls}.");
                var requestBody = new
                {
                    model = _model,
                    input = chatMessages,
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var analysisStarted = System.Diagnostics.Stopwatch.StartNew();
                var response = await _httpClient.PostAsync("https://api.openai.com/v1/responses", content);
                Trace($"Analyse {step + 1}/2 : réponse HTTP {(int)response.StatusCode} ({response.StatusCode}) en {analysisStarted.ElapsedMilliseconds} ms.");

                if (!response.IsSuccessStatusCode)
                {
                    results.Add($"L'analyse de la demande a échoué (OpenAI : {response.StatusCode}). Aucune action supplémentaire exécutée.");
                    Trace(results.Last());
                    break;
                }

                var responseString = await response.Content.ReadAsStringAsync();
                var planText = ExtractAssistantText(responseString);
                if (string.IsNullOrWhiteSpace(planText))
                {
                    results.Add("L'analyse de la demande n'a renvoyé aucun résultat. Aucune action supplémentaire exécutée.");
                    Trace(results.Last());
                    break;
                }

                planText = ExtractJsonObject(planText);

                if (!TryParseToolPlan(planText, out var kind, out var assistantMessage, out var calls))
                {
                    results.Add("Le plan d'actions reçu est invalide. Aucune action supplémentaire exécutée.");
                    Trace(results.Last());
                    break;
                }

                Trace($"Plan reçu : type={kind}, nombre d'appels={calls?.Count ?? 0}, motif={JsonSerializer.Serialize(assistantMessage)}.");

                if (string.Equals(kind, "no_action", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(assistantMessage))
                        results.Add($"Motif de non-exécution : {assistantMessage}");
                    Trace("Aucune nouvelle action demandée par le planificateur.");
                    break;
                }

                if (!string.Equals(kind, "tool_calls", StringComparison.OrdinalIgnoreCase) || calls == null || calls.Count == 0)
                {
                    results.Add("Le plan reçu ne contient aucune commande valide. Aucune action supplémentaire exécutée.");
                    Trace(results.Last());
                    break;
                }

                // Exécute les appels d'outils avec gestion des erreurs.
                bool hadSessionsBeforePlan = sessionsRetrieved;
                int allowedCalls = remainingCalls;
                foreach (var call in calls.Take(allowedCalls))
                {
                    remainingCalls--;
                    var tool = call.tool;
                    Trace($"Outil demandé : {tool}, arguments={JsonSerializer.Serialize(call.args)}.");
                    var definition = RaidTools.FirstOrDefault(t => t.Name == tool);
                    if (definition == null)
                    {
                        results.Add($"Outil inconnu ignoré : {tool}");
                        Trace(results.Last());
                        continue;
                    }

                    if (definition.RequiresManagementRights && !canExecute)
                    {
                        results.Add($"{tool} refusée : droits insuffisants (il faut être raid leader ou avoir un rôle admin configuré).");
                        Trace(results.Last());
                        continue;
                    }

                    if (definition.RequiresManagementRights && tool != "raid_session_create_weekly" && !hadSessionsBeforePlan)
                    {
                        results.Add($"{tool} non exécutée : le planning doit être consulté avant de préparer cette commande.");
                        Trace(results.Last());
                        continue;
                    }

                    int previousResultsCount = results.Count;
                    var toolStarted = System.Diagnostics.Stopwatch.StartNew();
                    Trace($"Exécution autorisée : {tool}.");
                    try
                    {
                        switch (tool)
                        {
                            case "raid_sessions_list":
                                results.Add(BuildSessionsContext(roster, timeZone));
                                executed.Add("raid_sessions_list");
                                sessionsRetrieved = true;
                                break;

                            case "raid_next_session_set_datetime":
                                {
                                    if (!call.args.TryGetValue("new_start_local", out var newStartLocalRaw))
                                        throw new InvalidOperationException("Argument new_start_local manquant.");

                                    var session = roster.Sessions.OrderBy(s => s.NextSession).FirstOrDefault();
                                    if (session == null)
                                        throw new InvalidOperationException("Aucune session configurée.");

                                    var utc = ParseLocalToUtc(newStartLocalRaw, timeZone);
                                    session.NextSession = utc;
                                    session.Announced = false;
                                    await db.SaveChangesAsync();
                                    results.Add($"Prochaine session déplacée au <t:{new DateTimeOffset(utc).ToUnixTimeSeconds()}:F>.");
                                    executed.Add($"raid_next_session_set_datetime(new_start_local={newStartLocalRaw})");
                                    break;
                                }

                            case "raid_session_set_next_datetime":
                                {
                                    if (!call.args.TryGetValue("session_id", out var sessionIdRaw) || !int.TryParse(sessionIdRaw, out var sessionId))
                                        throw new InvalidOperationException("Argument session_id invalide.");
                                    if (!call.args.TryGetValue("new_start_local", out var newStartLocalRaw))
                                        throw new InvalidOperationException("Argument new_start_local manquant.");

                                    var session = roster.Sessions.FirstOrDefault(s => s.Id == sessionId);
                                    if (session == null)
                                        throw new InvalidOperationException("Session introuvable.");

                                    var utc = ParseLocalToUtc(newStartLocalRaw, timeZone);
                                    session.NextSession = utc;
                                    session.Announced = false;
                                    await db.SaveChangesAsync();
                                    results.Add($"Session #{sessionId} déplacée au <t:{new DateTimeOffset(utc).ToUnixTimeSeconds()}:F>.");
                                    executed.Add($"raid_session_set_next_datetime(session_id={sessionId}, new_start_local={newStartLocalRaw})");
                                    break;
                                }

                            case "raid_session_set_duration":
                                {
                                    if (!TryGetInt(call.args, "session_id", out var sessionId))
                                        throw new InvalidOperationException("Argument session_id invalide.");
                                    if (!TryGetInt(call.args, "duration_minutes", out var durationMinutes) || durationMinutes <= 0 || durationMinutes > 1440)
                                        throw new InvalidOperationException("Argument duration_minutes invalide.");

                                    var session = roster.Sessions.FirstOrDefault(s => s.Id == sessionId);
                                    if (session == null)
                                        throw new InvalidOperationException("Session introuvable.");

                                    session.Duration = TimeSpan.FromMinutes(durationMinutes);
                                    await db.SaveChangesAsync();
                                    results.Add($"Durée de la session récurrente #{sessionId} modifiée : {durationMinutes} minutes, pour ses prochaines occurrences.");
                                    executed.Add($"raid_session_set_duration(session_id={sessionId}, duration_minutes={durationMinutes})");
                                    break;
                                }

                            case "raid_next_session_skip_week":
                                {
                                    var session = roster.Sessions.OrderBy(s => s.NextSession).FirstOrDefault();
                                    if (session == null)
                                        throw new InvalidOperationException("Aucune session configurée.");

                                    var nextSessionDateTime = _raidService.IsSessionModified(session)
                                        ? _raidService.GetNextSessionDateTime(session).AddDays(7)
                                        : session.NextSession.AddDays(7);

                                    session.NextSession = nextSessionDateTime;
                                    session.Announced = false;
                                    await db.SaveChangesAsync();
                                    results.Add($"OK, j'ai repoussé la prochaine session au <t:{new DateTimeOffset(nextSessionDateTime).ToUnixTimeSeconds()}:F>.");
                                    executed.Add("raid_next_session_skip_week");
                                    break;
                                }

                            case "raid_session_delete":
                                {
                                    if (!call.args.TryGetValue("session_id", out var sessionIdRaw) || !int.TryParse(sessionIdRaw, out var sessionId))
                                        throw new InvalidOperationException("Argument session_id invalide.");

                                    var session = await db.RaidSessions.Include(rs => rs.Check)
                                        .FirstOrDefaultAsync(rs => rs.Id == sessionId && rs.RosterId == roster.Id);

                                    if (session == null)
                                        throw new InvalidOperationException("Session introuvable.");

                                    if (session.Check != null)
                                        db.ReadyChecks.Remove(session.Check);

                                    db.RaidSessions.Remove(session);
                                    await db.SaveChangesAsync();
                                    roster.Sessions.Remove(session);
                                    results.Add($"Session #{sessionId} supprimée.");
                                    executed.Add($"raid_session_delete(session_id={sessionId})");
                                    break;
                                }

                            case "raid_session_create_weekly":
                                {
                                    if (!TryGetInt(call.args, "day", out var day) || day < 0 || day > 6)
                                        throw new InvalidOperationException("Argument day invalide.");
                                    if (!TryGetInt(call.args, "hour", out var hour) || hour < 0 || hour > 23)
                                        throw new InvalidOperationException("Argument hour invalide.");
                                    if (!TryGetInt(call.args, "minute", out var minute) || minute < 0 || minute > 59)
                                        throw new InvalidOperationException("Argument minute invalide.");
                                    if (!TryGetInt(call.args, "duration_minutes", out var durationMinutes) || durationMinutes <= 0 || durationMinutes > 1440)
                                        throw new InvalidOperationException("Argument duration_minutes invalide.");

                                    var newSession = new RaidSession
                                    {
                                        Day = day,
                                        Hour = hour,
                                        Minute = minute,
                                        Duration = TimeSpan.FromMinutes(durationMinutes),
                                        Roster = roster,
                                    };
                                    newSession.NextSession = _raidService.GetNextSessionDateTime(newSession);

                                    await db.RaidSessions.AddAsync(newSession);
                                    await db.SaveChangesAsync();
                                    results.Add($"Session hebdo créée (id: {newSession.Id}) : <t:{new DateTimeOffset(newSession.NextSession).ToUnixTimeSeconds()}:F>.");
                                    executed.Add($"raid_session_create_weekly(day={day}, hour={hour}, minute={minute}, duration_minutes={durationMinutes})");
                                    break;
                                }

                            default:
                                results.Add($"Outil inconnu ignoré: {tool}");
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        results.Add($"⛔ {tool} : {ex.Message}");
                        Trace($"Exception dans {tool} : {JsonSerializer.Serialize(ex.ToString())}");
                    }
                    finally
                    {
                        Trace($"Fin de {tool} en {toolStarted.ElapsedMilliseconds} ms : {JsonSerializer.Serialize(results.Skip(previousResultsCount).ToArray())}");
                    }
                }

                if (calls.Count > allowedCalls)
                {
                    results.Add($"{calls.Count - allowedCalls} commande(s) non exécutée(s) : limite de 5 commandes par message.");
                    Trace(results.Last());
                }

                if (hadSessionsBeforePlan || !sessionsRetrieved || remainingCalls == 0)
                {
                    Trace($"Fin de la planification : planning connu avant ce plan={hadSessionsBeforePlan}, planning consulté={sessionsRetrieved}, appels restants={remainingCalls}.");
                    break;
                }

                Trace("Transmission du planning au modèle pour préparer les actions suivantes.");
                chatMessages.Add(new() { { "role", "assistant" }, { "content", planText } });
                chatMessages.Add(new()
            {
                { "role", "system" },
                { "content", "Résultat de raid_sessions_list :\n" + BuildSessionsContext(roster, timeZone) +
                    "\nPrépare maintenant les modifications explicitement demandées dans le message initial, sans répéter la consultation. " +
                    "S'il s'agissait seulement de consulter le planning, renvoie kind=no_action avec assistant_message vide et calls vide." }
            });
            }

            if (executed.Count == 0 && results.Count == 0)
            {
                Trace("Conversation simple : aucun bilan technique transmis au chat.");
                return null;
            }

            var execSummary = executed.Count == 0
                ? "Aucune commande exécutée."
                : $"Commandes exécutées: {string.Join(", ", executed)}.";

            var resultsSummary = results.Count == 0 ? "" : ("Résultats:\n" + string.Join("\n", results.Where(r => !string.IsNullOrWhiteSpace(r))));

            Trace($"Bilan transmis au chat : {JsonSerializer.Serialize(new { commandesExecutees = executed, resultats = results })}");
            return $"{execSummary}\n{resultsSummary}";
        }

        private static string NormalizeDiscordMentions(SocketTextChannel channel, string content)
        {
            if (string.IsNullOrEmpty(content))
                return string.Empty;

            var matches = Regex.Matches(content, "<@!?([0-9]+)>");
            var msgContent = content;
            foreach (Match match in matches)
            {
                string userId = match.Groups[1].Value;
                if (ulong.TryParse(userId, out var id))
                {
                    var tagUser = channel.GetUser(id);
                    if (tagUser != null)
                        msgContent = msgContent.Replace(match.Value, "@" + (tagUser.Nickname ?? tagUser.GlobalName ?? tagUser.DisplayName));
                }
            }

            // Retire les mentions restantes du message envoyé au modèle.
            msgContent = Regex.Replace(msgContent, "<@!?\\d+>", "", RegexOptions.CultureInvariant).Trim();
            return msgContent;
        }

        private static string BuildSessionsContext(Roster roster, TimeZoneInfo timeZone)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Sessions de raid configurées pour ce roster (utilise ces ids uniquement). Fuseau : {roster.TimeZoneId}.");

            if (roster.Sessions == null || roster.Sessions.Count == 0)
            {
                sb.AppendLine("- (aucune session)");
                return sb.ToString();
            }

            foreach (var s in roster.Sessions.OrderBy(s => s.NextSession))
            {
                var local = TimeZoneInfo.ConvertTimeFromUtc(s.NextSession, timeZone);
                sb.AppendLine($"- id={s.Id} next_session_utc={s.NextSession:O} next_session_local={local:yyyy-MM-dd HH:mm} next_session_day_name={local.ToString("dddd", CultureInfo.GetCultureInfo("fr-FR"))} day={s.Day} weekly_day_name={CultureInfo.GetCultureInfo("fr-FR").DateTimeFormat.DayNames[s.Day]} hour={s.Hour} minute={s.Minute} duration_minutes={(int)s.Duration.TotalMinutes}");
            }

            return sb.ToString();
        }

        private static DateTime ParseLocalToUtc(string local, TimeZoneInfo timeZone)
        {
            var formats = new[] { "yyyy-MM-dd HH:mm", "yyyy-MM-dd H:mm", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm", "yyyy-MM-ddTHH:mm:ss" };
            if (!DateTime.TryParseExact(local.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                throw new InvalidOperationException("Date invalide. Utilise le format YYYY-MM-DD HH:mm.");

            dt = DateTime.SpecifyKind(dt, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTimeToUtc(dt, timeZone);
        }

        private static bool TryGetInt(Dictionary<string, string> args, string key, out int value)
        {
            value = default;
            return args.TryGetValue(key, out var raw) && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static string ExtractAssistantText(string responseString)
        {
            using var doc = JsonDocument.Parse(responseString);
            if (!doc.RootElement.TryGetProperty("output", out var outputArray))
                return string.Empty;

            var sb = new StringBuilder();
            foreach (var outputItem in outputArray.EnumerateArray())
            {
                if (outputItem.TryGetProperty("type", out var typeEl) && typeEl.GetString() != "message")
                    continue;
                if (!outputItem.TryGetProperty("role", out var roleEl) || roleEl.GetString() != "assistant")
                    continue;

                if (!outputItem.TryGetProperty("content", out var contentArray))
                    continue;

                foreach (var contentItem in contentArray.EnumerateArray())
                {
                    if (contentItem.TryGetProperty("type", out var cType) && cType.GetString() == "output_text")
                    {
                        if (contentItem.TryGetProperty("text", out var textEl))
                            sb.Append(textEl.GetString());
                    }
                }
            }
            return sb.ToString();
        }

        private static string ExtractJsonObject(string text)
        {
            var t = text.Trim();
            // Retire les délimiteurs de bloc de code si le modèle ne respecte pas le format demandé.
            if (t.StartsWith("```", StringComparison.Ordinal))
            {
                var firstNewline = t.IndexOf('\n');
                var lastFence = t.LastIndexOf("```", StringComparison.Ordinal);
                if (firstNewline >= 0 && lastFence > firstNewline)
                    t = t.Substring(firstNewline + 1, lastFence - firstNewline - 1).Trim();
            }

            // Isole l'objet JSON si la réponse contient du texte supplémentaire.
            var start = t.IndexOf('{');
            var end = t.LastIndexOf('}');
            if (start >= 0 && end > start)
                return t.Substring(start, end - start + 1);

            return t;
        }

        private static bool TryParseToolPlan(
            string json,
            out string kind,
            out string assistantMessage,
            out List<(string tool, Dictionary<string, string> args)>? calls)
        {
            kind = string.Empty;
            assistantMessage = string.Empty;
            calls = null;

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                kind = root.TryGetProperty("kind", out var kindEl) ? (kindEl.GetString() ?? "") : "";
                assistantMessage = root.TryGetProperty("assistant_message", out var msgEl) ? (msgEl.GetString() ?? "") : "";

                if (root.TryGetProperty("calls", out var callsEl) && callsEl.ValueKind == JsonValueKind.Array)
                {
                    var parsed = new List<(string tool, Dictionary<string, string> args)>();
                    foreach (var callEl in callsEl.EnumerateArray())
                    {
                        var tool = callEl.TryGetProperty("tool", out var toolEl) ? (toolEl.GetString() ?? "") : "";
                        if (string.IsNullOrWhiteSpace(tool))
                            continue;

                        var args = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        if (callEl.TryGetProperty("args", out var argsEl) && argsEl.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var prop in argsEl.EnumerateObject())
                            {
                                args[prop.Name] = prop.Value.ValueKind switch
                                {
                                    JsonValueKind.String => prop.Value.GetString() ?? "",
                                    JsonValueKind.Number => prop.Value.GetRawText(),
                                    JsonValueKind.True => "true",
                                    JsonValueKind.False => "false",
                                    _ => prop.Value.GetRawText()
                                };
                            }
                        }

                        parsed.Add((tool, args));
                    }

                    calls = parsed;
                }

                return !string.IsNullOrWhiteSpace(kind);
            }
            catch
            {
                return false;
            }
        }
    }
}
