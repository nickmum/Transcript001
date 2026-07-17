using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Transcript001
{
    public class ChatMessage
    {
        public string Role { get; }
        public string Content { get; }

        public ChatMessage(string role, string content)
        {
            Role = role;
            Content = content;
        }
    }

    public class ClaudeApiHelper
    {
        private readonly HttpClient _httpClient;

        public ClaudeApiHelper(string apiKey)
        {
            _httpClient = new HttpClient();
            if (!string.IsNullOrEmpty(apiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
            }
            _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        }

        public Task<string> GetResponseFromClaude(string prompt)
        {
            return GetResponseFromClaude(new List<ChatMessage> { new ChatMessage("user", prompt) });
        }

        public Task<string> GetResponseFromClaude(IReadOnlyList<ChatMessage> conversation)
        {
            return SendRequestAsync(new
            {
                model = "claude-sonnet-5",
                max_tokens = 8000,
                messages = conversation.Select(m => new { role = m.Role, content = m.Content }).ToArray()
            });
        }

        public Task<string> GetResponseFromClaude(string prompt, byte[] pngImageBytes)
        {
            return SendRequestAsync(new
            {
                model = "claude-sonnet-5",
                max_tokens = 8000,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new
                            {
                                type = "image",
                                source = new
                                {
                                    type = "base64",
                                    media_type = "image/png",
                                    data = Convert.ToBase64String(pngImageBytes)
                                }
                            },
                            new { type = "text", text = prompt }
                        }
                    }
                }
            });
        }

        private async Task<string> SendRequestAsync(object requestBody)
        {
            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("https://api.anthropic.com/v1/messages", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Claude API request failed ({(int)response.StatusCode} {response.StatusCode}): {responseBody}");
            }

            using var jsonResponse = JsonDocument.Parse(responseBody);
            var root = jsonResponse.RootElement;

            string text = null;
            foreach (var block in root.GetProperty("content").EnumerateArray())
            {
                if (block.TryGetProperty("type", out var type) && type.GetString() == "text")
                {
                    text = block.GetProperty("text").GetString();
                    break;
                }
            }

            if (text == null)
            {
                throw new Exception("Unexpected response format from Claude API");
            }

            if (root.TryGetProperty("stop_reason", out var stopReason) && stopReason.GetString() == "max_tokens")
            {
                text += "\n\n[Response was cut off at the output token limit.]";
            }

            return text;
        }
    }
}
