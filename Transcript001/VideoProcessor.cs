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

        public async Task ProcessVideoAsync(string url)
        {
            string outputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "subs");
            Directory.CreateDirectory(outputDir);

            try
            {
                await ProcessSingleVideoAsync(url, outputDir);
                ProgressUpdated?.Invoke(this, 1);
            }
            catch (Exception ex)
            {
                LogMessage($"Error processing {url}: {ex.Message}");
                throw;
            }
        }

        private async Task ProcessSingleVideoAsync(string url, string outputDir)
        {
            LogMessage($"Processing: {url}");

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
                    string subtitles = await DownloadSubtitlesAsync(baseUrl);
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
        }

        private async Task<string> DownloadSubtitlesAsync(string url)
        {
            string xmlContent = await httpClient.GetStringAsync(url);
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xmlContent);
            XmlNodeList nodes = doc.SelectNodes("/transcript/text");

            List<string> subtitleLines = new List<string>();

            foreach (XmlNode node in nodes)
            {
                string text = HttpUtility.HtmlDecode(node.InnerText);
                subtitleLines.Add(text);
            }

            return string.Join(Environment.NewLine, subtitleLines);
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
}