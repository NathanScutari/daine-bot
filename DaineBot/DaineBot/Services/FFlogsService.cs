using System.Net.Http;
using System.Threading.Tasks;
using DaineBot.DTO;
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

            if (!response.IsSuccessStatusCode) return true;

            var responseString = await response.Content.ReadAsStringAsync();
            dynamic jsonResponse = JsonConvert.DeserializeObject(responseString);
            if (jsonResponse?.data?.reportData?.report?.endTime == null) return true;
            DateTime endTime = DateTimeOffset.FromUnixTimeMilliseconds(long.Parse((string)jsonResponse.data.reportData.report.endTime)).DateTime;

            return endTime < DateTime.UtcNow.AddMinutes(-15);
        }

        public async Task<string> RaidSessionSummary(RaidSession session)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"https://www.fflogs.com/api/v2/client");
            var query = @"{reportData {
		                    report(code: ""{0}"") {
				                fights {
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
            List<List<dynamic>> separatedFights = fights.ToList().GroupBy(o => o.name).Select(g => g.ToList()).ToList();

            string summaryResponse = "# Résumé de la soirée :\n" +
                $"- Total de **{separatedFights.Sum(l => l.Sum(o => ((bool)o.kill) ? 0 : 1))}** wipes\n";
            foreach (List<dynamic> encounters in separatedFights)
            {
                bool kill = encounters.Any(o => ((bool)o.kill) == true);
                float minPercentage = encounters.Min(o => ((float)o.bossPercentage));
                TimeSpan maxCombatTime = TimeSpan.FromMilliseconds(encounters.Max(o => (float)o.combatTime));
                int lastPhase = encounters.Max(o => (int)o.lastPhase);
                string name = encounters.First().name;
                int wipes = encounters.Sum(o => (bool)o.kill ? 0 : 1);
                float sumDuration = encounters.Sum(o => (float)o.combatTime);
                TimeSpan averageWipe = TimeSpan.FromMilliseconds(sumDuration / wipes);

                summaryResponse += $"\n## {name}\n";
                summaryResponse += $"- **{wipes} wipes**\n";
                if (kill)
                {
                    TimeSpan killTime = TimeSpan.FromMilliseconds((float)(encounters.First(o => (bool)o.kill == true).combatTime));
                    summaryResponse += $"- Kill en {encounters.Count} essais. ({killTime.Minutes}:{killTime.Seconds})";
                }
                else
                {
                    {
                        summaryResponse += $"- Le boss a été descendu jusqu'à {minPercentage.ToString("0.##")}% hp en phase {lastPhase + 1}\n" +
                            $"- L'essai le plus long a duré {maxCombatTime.Minutes}:{maxCombatTime.Seconds}\n" +
                            $"- Durée moyenne des wipes : {averageWipe.Minutes}:{averageWipe.Seconds}";
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
