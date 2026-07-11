using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using System.Linq;
using System.Globalization;

namespace Transcript001
{
    public class VideoProcessor
    {
        private readonly HttpClient httpClient;

        public event EventHandler<int> ProgressUpdated;
        public event EventHandler<string> LogUpdated;

        public VideoProcessor()
        {
            httpClient = new HttpClient();
        }

        public async Task<(string videoId, List<SubtitleEntry> subtitles)> ProcessVideoAsync(string url, string format)
        {
            string outputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "subs");
            Directory.CreateDirectory(outputDir);

            try
            {
                var result = await ProcessSingleVideoAsync(url, outputDir, format);
                ProgressUpdated?.Invoke(this, 1);
                return result;
            }
            catch (Exception ex)
            {
                LogMessage($"Error processing {url}: {ex.Message}");
                throw;
            }
        }

        private async Task<(string videoId, List<SubtitleEntry> subtitles)> ProcessSingleVideoAsync(string url, string outputDir, string format)
        {
            LogMessage($"Processing: {url}");

            string videoId = ExtractVideoId(url);
            if (string.IsNullOrEmpty(videoId))
            {
                throw new Exception("Invalid YouTube URL");
            }

            var videoDetails = await GetPlayerResponseAsync(videoId);
            string title = videoDetails.SelectToken("videoDetails.title")?.ToString() ?? "Unknown Title";
            string author = videoDetails.SelectToken("videoDetails.author")?.ToString() ?? "Unknown Author";

            string fileName = NormalizeFileName($"{title} - {author}");

            List<SubtitleEntry> subtitles = new List<SubtitleEntry>();

            var captionTracks = videoDetails.SelectToken("captions.playerCaptionsTracklistRenderer.captionTracks");
            if (captionTracks != null)
            {
                var captionTrack = captionTracks
                    .FirstOrDefault(t => t["languageCode"]?.ToString() == "en");

                string baseUrl = captionTrack?["baseUrl"]?.ToString();
                if (!string.IsNullOrEmpty(baseUrl))
                {
                    subtitles = await DownloadSubtitlesAsync(baseUrl, format);
                    File.WriteAllText(Path.Combine(outputDir, $"{fileName}.txt"), FormatSubtitles(subtitles, format));
                    LogMessage($"Saved subtitles for: {fileName}");
                }
                else
                {
                    LogMessage($"No English subtitles found for: {fileName}");
                }
            }
            else
            {
                LogMessage($"No subtitles available for: {fileName}");
            }

            return (videoId, subtitles);
        }

        private async Task<JObject> GetPlayerResponseAsync(string videoId)
        {
            // Caption URLs scraped from the watch-page HTML now come back with empty
            // bodies (YouTube requires a proof-of-origin token on those). The internal
            // player API used by the mobile apps still returns working caption URLs.
            var requestBody = new JObject
            {
                ["context"] = new JObject
                {
                    ["client"] = new JObject
                    {
                        ["clientName"] = "ANDROID",
                        ["clientVersion"] = "20.10.38",
                        ["androidSdkVersion"] = 30,
                        ["hl"] = "en"
                    }
                },
                ["videoId"] = videoId
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://www.youtube.com/youtubei/v1/player")
            {
                Content = new StringContent(requestBody.ToString(), Encoding.UTF8, "application/json")
            };
            request.Headers.TryAddWithoutValidation("User-Agent", "com.google.android.youtube/20.10.38 (Linux; U; Android 11) gzip");

            var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync();
            var playerResponse = JObject.Parse(json);

            string playabilityStatus = playerResponse.SelectToken("playabilityStatus.status")?.ToString();
            if (playabilityStatus != null && playabilityStatus != "OK")
            {
                string reason = playerResponse.SelectToken("playabilityStatus.reason")?.ToString() ?? playabilityStatus;
                throw new Exception($"YouTube reports this video is not playable: {reason}");
            }

            return playerResponse;
        }

        public static string ExtractVideoId(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return null;
            }

            if (uri.Host == "youtu.be")
            {
                return uri.Segments.LastOrDefault()?.Trim('/');
            }

            var query = HttpUtility.ParseQueryString(uri.Query);
            return query["v"];
        }

        private async Task<List<SubtitleEntry>> DownloadSubtitlesAsync(string url, string format)
        {
            string xmlContent = await httpClient.GetStringAsync(url);
            if (string.IsNullOrWhiteSpace(xmlContent))
            {
                throw new Exception("YouTube returned an empty subtitle response.");
            }

            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xmlContent);

            List<SubtitleEntry> subtitleEntries = new List<SubtitleEntry>();

            // Older format: <transcript><text start="1.2" dur="3.4">  (times in seconds).
            // Invariant culture: YouTube always uses "." as the decimal separator,
            // regardless of the machine's regional settings.
            XmlNodeList nodes = doc.SelectNodes("/transcript/text");
            if (nodes != null && nodes.Count > 0)
            {
                foreach (XmlNode node in nodes)
                {
                    double start = double.Parse(node.Attributes["start"].Value, CultureInfo.InvariantCulture);
                    double duration = double.Parse(node.Attributes["dur"]?.Value ?? "0", CultureInfo.InvariantCulture);
                    AddSubtitleEntry(subtitleEntries, start, duration, node.InnerText);
                }
                return subtitleEntries;
            }

            // Newer format: <timedtext format="3"><body><p t="5160" d="3194">  (times in milliseconds).
            nodes = doc.SelectNodes("/timedtext/body/p");
            if (nodes != null)
            {
                foreach (XmlNode node in nodes)
                {
                    double start = double.Parse(node.Attributes["t"].Value, CultureInfo.InvariantCulture) / 1000.0;
                    double duration = double.Parse(node.Attributes["d"]?.Value ?? "0", CultureInfo.InvariantCulture) / 1000.0;
                    AddSubtitleEntry(subtitleEntries, start, duration, node.InnerText);
                }
            }

            return subtitleEntries;
        }

        private static void AddSubtitleEntry(List<SubtitleEntry> entries, double start, double duration, string rawText)
        {
            string text = HttpUtility.HtmlDecode(rawText);
            if (!string.IsNullOrWhiteSpace(text))
            {
                entries.Add(new SubtitleEntry(start, duration, text));
            }
        }

        private string FormatSubtitles(List<SubtitleEntry> entries, string format)
        {
            switch (format)
            {
                case "Time-stamped Text":
                    return string.Join(Environment.NewLine, entries.Select(e => $"[{FormatTime(e.Start)}] {e.Text}"));
                case "YouTube Original":
                    return string.Join(Environment.NewLine + Environment.NewLine, entries.Select(e =>
                        $"{FormatTime(e.Start)} - {FormatTime(e.Start + e.Duration)}{Environment.NewLine}{e.Text}"));
                default:
                    return string.Join(Environment.NewLine, entries.Select(e => e.Text));
            }
        }

        private string FormatTime(double seconds)
        {
            TimeSpan time = TimeSpan.FromSeconds(seconds);
            return time.ToString(@"hh\:mm\:ss\.fff");
        }

        private string NormalizeFileName(string fileName)
        {
            return string.Join("_", fileName.Split(Path.GetInvalidFileNameChars()));
        }

        private void LogMessage(string message)
        {
            LogUpdated?.Invoke(this, message);
        }
    }

    public class SubtitleEntry
    {
        public double Start { get; }
        public double Duration { get; }
        public string Text { get; }

        public SubtitleEntry(double start, double duration, string text)
        {
            Start = start;
            Duration = duration;
            Text = text;
        }
    }
}