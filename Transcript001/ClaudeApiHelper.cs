using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Transcript001
{
    public class ClaudeApiHelper
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public ClaudeApiHelper(string apiKey)
        {
            _apiKey = apiKey;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
            _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        }

        public async Task<string> GetResponseFromClaude(string prompt)
        {
            var requestBody = new
            {
                model = "claude-3-sonnet-20240229",
                max_tokens = 1000,
                messages = new[]
                {
                    new { role = "user", content = prompt }
                }
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("https://api.anthropic.com/v1/messages", content);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();
            var jsonResponse = JsonDocument.Parse(responseBody);

            var contentArray = jsonResponse.RootElement.GetProperty("content").EnumerateArray();
            if (contentArray.MoveNext())
            {
                return contentArray.Current.GetProperty("text").GetString();
            }

            throw new Exception("Unexpected response format from Claude API");
        }
    }
}