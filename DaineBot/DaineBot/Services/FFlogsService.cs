using DaineBot.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DaineBot.Services
{
    public class FFLogsService
    {
        private readonly HttpClient _httpClient;
        private readonly string _token;

        public FFLogsService(string token)
        {
            _httpClient = new HttpClient();
            _token = token;
        }

        public async Task<bool> IsRaidSessionDone(RaidSession session)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"https://www.fflogs.com/api/v2/client");
            var query = @"{
                            reportData {
		                        report(code: ""{0}"") {
				                    endTime
			                    }
	                        }
                        }";
            var formattedQuery = query.Replace("{0}", session.ReportCode);
            var jsonBody = new
            {
                query = formattedQuery
            };
            var jsonContent = JsonConvert.SerializeObject(jsonBody);

            // Ajouter le token dans l'en-tête d'autorisation
            request.Headers.Add("Authorization", $"Bearer {_token}");
            request.Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.SendAsync(request);

	    Console.WriteLine(response.IsSuccessStatusCode ? "Retour OK" : "Retour KO");

	    Console.WriteLine(response.Headers);

	    Console.WriteLine(response.Content?.Headers);

            if (!response.IsSuccessStatusCode) return true;

            var responseString = await response.Content.ReadAsStringAsync();
	    Console.WriteLine(responseString.ToString());
            dynamic jsonResponse = JsonConvert.DeserializeObject(responseString);
            if (jsonResponse?.data?.reportData?.report?.endTime == null) return true;
            DateTime endTime = DateTimeOffset.FromUnixTimeMilliseconds(long.Parse((string)jsonResponse.data.reportData.report.endTime)).DateTime;

            return endTime < DateTime.UtcNow.AddMinutes(-5);
        }

        public async Task<string> RaidSessionSummary(RaidSession session)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"https://www.fflogs.com/api/v2/client");
            var query = @"{reportData {
		                    report(code: ""{0}"") {
				                fights {
                                    id,
                                    fightPercentage,
					                bossPercentage,
					                combatTime,
					                lastPhase,
					                kill,
					                name
				                }
			                }
	                    }}";
            var formattedQuery = query.Replace("{0}", session.ReportCode);
            var jsonBody = new
            {
                query = formattedQuery
            };
            var jsonContent = JsonConvert.SerializeObject(jsonBody);

            // Ajouter le token dans l'en-tête d'autorisation
            request.Headers.Add("Authorization", $"Bearer {_token}");
            request.Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode) return "";

            var responseString = await response.Content.ReadAsStringAsync();
            dynamic jsonResponse = JsonConvert.DeserializeObject(responseString);

            dynamic[] fights = ((JArray)jsonResponse.data.reportData.report.fights).ToObject<dynamic[]>();
            fights = fights.Where(f => f.fightPercentage != null && f.bossPercentage != null && f.combatTime != null && f.lastPhase != null && f.kill != null && f.name != null).ToArray();
            List<List<dynamic>> separatedFights = fights.ToList().GroupBy(o => o.name).Select(g => g.ToList()).ToList();


            string summaryResponse = "# Résumé de la soirée :\n" +
                $"- Total de **{separatedFights.Sum(l => l.Sum(o => ((bool)o.kill) ? 0 : 1))}** wipes\n";
            foreach (List<dynamic> encounters in separatedFights)
            {
                bool kill = encounters.Any(o => ((bool)o.kill) == true);
                TimeSpan maxCombatTime = TimeSpan.FromMilliseconds(encounters.Max(o => (float)o.combatTime));
                int lastPhase = encounters.Max(o => (int)o.lastPhase);
                string name = encounters.First().name;
                int wipes = encounters.Sum(o => (bool)o.kill ? 0 : 1);
                float sumDuration = encounters.Sum(o => (float)o.combatTime);
                TimeSpan averageWipe = TimeSpan.FromMilliseconds(sumDuration / wipes);
                dynamic furthestEncounter = encounters.MinBy(o => (float)o.fightPercentage);

                summaryResponse += $"\n## {name}\n";
                summaryResponse += $"- **{wipes} wipes**\n";
                if (kill)
                {
                    var killEncounter = encounters.First(o => (bool)o.kill == true);
                    TimeSpan killTime = TimeSpan.FromMilliseconds((float)killEncounter.combatTime);
                    summaryResponse += $"- Kill en {encounters.Count} essais. ({killTime.Minutes}:{killTime.Seconds:D2})\n";
                    summaryResponse += $"Lien analysis du kill: <https://xivanalysis.com/fflogs/{session.ReportCode}/{killEncounter.id}>";
                }
                else
                {
                    {
                        summaryResponse += $"- Le boss a été descendu jusqu'à {furthestEncounter.bossPercentage.ToString("0.##")}% hp";
                        if (lastPhase != 0)
                            summaryResponse += $" en phase {furthestEncounter.lastPhase.ToString()}";
                        summaryResponse += "\n" +
                            $"- L'essai le plus long a duré {maxCombatTime.Minutes}:{maxCombatTime.Seconds:D2}\n" +
                            $"- Durée moyenne des wipes : {averageWipe.Minutes}:{averageWipe.Seconds:D2}\n";
                        summaryResponse += $"Lien analysis du wipe le plus avancé: <https://xivanalysis.com/fflogs/{session.ReportCode}/{furthestEncounter.id}>";
                    }
                }
            }
            return summaryResponse;

        }

        public async Task<string> GetLogsByUserAsync(string userId, RaidSession raidSession)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"https://www.fflogs.com/api/v2/client");

            var query = @"
        {
	        reportData {
		        reports(userID: {0}, startTime: {1}) {
			        data {
				        startTime,
				        code
                    }
                }
	        }
        }";

            var formattedQuery = query.Replace("{0}", userId).Replace("{1}", ((DateTimeOffset)raidSession.NextSession.AddMinutes(-5)).ToUnixTimeMilliseconds().ToString());
            var jsonBody = new
            {
                query = formattedQuery
            };
            var jsonContent = JsonConvert.SerializeObject(jsonBody);

            // Ajouter le token dans l'en-tête d'autorisation
            request.Headers.Add("Authorization", $"Bearer {_token}");

            request.Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                return "";
            }

            var responseString = await response.Content.ReadAsStringAsync();
            dynamic jsonResponse = JsonConvert.DeserializeObject(responseString);

            var reports = jsonResponse.data.reportData.reports.data;
            var logReport = "";
            foreach (var report in reports)
            {
                string startTime = report.startTime;
                DateTime reportDate = DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(startTime)).DateTime;

                if (reportDate > raidSession.NextSession.AddMinutes(-5))
                {
                    logReport = report.code;
                    break;
                }
            }

            return logReport;
        }
    }
}
