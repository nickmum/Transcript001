using HtmlAgilityPack;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using System.Text.RegularExpressions;
using System.Linq;

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

        public async Task<string> ProcessVideoAsync(string url, string format)
        {
            string outputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "subs");
            Directory.CreateDirectory(outputDir);

            try
            {
                string videoId = await ProcessSingleVideoAsync(url, outputDir, format);
                ProgressUpdated?.Invoke(this, 1);
                return videoId;
            }
            catch (Exception ex)
            {
                LogMessage($"Error processing {url}: {ex.Message}");
                throw;
            }
        }

        private async Task<string> ProcessSingleVideoAsync(string url, string outputDir, string format)
        {
            LogMessage($"Processing: {url}");

            string videoId = ExtractVideoId(url);
            if (string.IsNullOrEmpty(videoId))
            {
                throw new Exception("Invalid YouTube URL");
            }

            string htmlContent = await httpClient.GetStringAsync(url);
            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(htmlContent);

            string jsonContent = htmlDocument.DocumentNode.SelectSingleNode("//script[contains(text(), 'ytInitialPlayerResponse')]").InnerHtml;

            var match = Regex.Match(jsonContent, @"ytInitialPlayerResponse\s*=\s*({.+?});");
            if (!match.Success)
            {
                throw new Exception("Could not find ytInitialPlayerResponse in the page content");
            }

            jsonContent = match.Groups[1].Value;

            var videoDetails = JObject.Parse(jsonContent);
            string title = videoDetails["videoDetails"]["title"].ToString();
            string author = videoDetails["videoDetails"]["author"].ToString();

            string fileName = NormalizeFileName($"{title} - {author}");

            if (videoDetails["captions"] != null && videoDetails["captions"]["playerCaptionsTracklistRenderer"]["captionTracks"] != null)
            {
                var captionTrack = videoDetails["captions"]["playerCaptionsTracklistRenderer"]["captionTracks"]
                    .FirstOrDefault(t => t["languageCode"].ToString() == "en");

                if (captionTrack != null)
                {
                    string baseUrl = captionTrack["baseUrl"].ToString();
                    string subtitles = await DownloadSubtitlesAsync(baseUrl, format);
                    File.WriteAllText(Path.Combine(outputDir, $"{fileName}.txt"), subtitles);
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

            return videoId;
        }

        private string ExtractVideoId(string url)
        {
            var uri = new Uri(url);
            var query = HttpUtility.ParseQueryString(uri.Query);

            if (uri.Host == "youtu.be")
            {
                return uri.Segments.LastOrDefault();
            }
            else if (query.AllKeys.Contains("v"))
            {
                return query["v"];
            }
            else
            {
                return null;
            }
        }

        private async Task<string> DownloadSubtitlesAsync(string url, string format)
        {
            string xmlContent = await httpClient.GetStringAsync(url);
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xmlContent);
            XmlNodeList nodes = doc.SelectNodes("/transcript/text");

            List<SubtitleEntry> subtitleEntries = new List<SubtitleEntry>();

            foreach (XmlNode node in nodes)
            {
                double start = Convert.ToDouble(node.Attributes["start"].Value);
                double duration = Convert.ToDouble(node.Attributes["dur"].Value);
                string text = HttpUtility.HtmlDecode(node.InnerText);
                subtitleEntries.Add(new SubtitleEntry(start, duration, text));
            }

            switch (format)
            {
                case "Time-stamped Text":
                    return FormatTimeStampedText(subtitleEntries);
                case "YouTube Original":
                    return FormatYouTubeOriginal(subtitleEntries);
                default:
                    return FormatPlainText(subtitleEntries);
            }
        }

        private string FormatPlainText(List<SubtitleEntry> entries)
        {
            return string.Join(Environment.NewLine, entries.Select(e => e.Text));
        }

        private string FormatTimeStampedText(List<SubtitleEntry> entries)
        {
            return string.Join(Environment.NewLine, entries.Select(e => $"[{FormatTime(e.Start)}] {e.Text}"));
        }

        private string FormatYouTubeOriginal(List<SubtitleEntry> entries)
        {
            return string.Join(Environment.NewLine + Environment.NewLine, entries.Select(e =>
                $"{FormatTime(e.Start)} - {FormatTime(e.Start + e.Duration)}{Environment.NewLine}{e.Text}"));
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