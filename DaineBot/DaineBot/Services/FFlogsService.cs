using System.Net.Http;
using System.Threading.Tasks;
using DaineBot.Models;
using Newtonsoft.Json;

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
