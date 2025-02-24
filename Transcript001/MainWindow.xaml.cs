using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using System.Windows.Threading;
using System.Text.Json;
using System.Windows.Data;
using System.Diagnostics;
using System.ComponentModel;
using System.IO;
using System.Windows.Input;
using System.Windows.Documents;

namespace Transcript001
{
    public partial class MainWindow : Window
    {
        private VideoProcessor videoProcessor;
        private List<SubtitleEntry> subtitleEntries;
        private DispatcherTimer timer;
        private string currentVideoId;
        private readonly ClaudeApiHelper _apiHelper;
        private string _conversationHistory = "";
        private List<Run> _subtitleRuns;
        private Run _currentRun;
        private bool _autoScrollEnabled = true;

        public MainWindow()
        {
            InitializeComponent();
            AutoScrollToggle.IsChecked = true;
            videoProcessor = new VideoProcessor();
            videoProcessor.ProgressUpdated += VideoProcessor_ProgressUpdated;
            videoProcessor.LogUpdated += VideoProcessor_LogUpdated;
            InitializeWebView();
            InitializeTimer();
            this.Closing += MainWindow_Closing;
            _apiHelper = new ClaudeApiHelper("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");
            UrlTextBox.Text = "https://www.youtube.com/watch?v=rbu7Zu5X1zI";
        }

        private async void InitializeWebView()
        {
            await VideoPlayer.EnsureCoreWebView2Async();
            VideoPlayer.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
        }

        private void InitializeTimer()
        {
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(100);
            timer.Tick += Timer_Tick;
        }

        private async void ProcessVideo_Click(object sender, RoutedEventArgs e)
        {
            string url = UrlTextBox.Text.Trim();

            if (string.IsNullOrEmpty(url))
            {
                MessageBox.Show("Please enter a video URL.", "No URL", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            StatusText.Text = "Processing video...";
            ProgressBar.Value = 0;
            ProgressBar.Maximum = 1;

            try
            {
                string selectedFormat = (FormatComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
                var (videoId, subtitles) = await videoProcessor.ProcessVideoAsync(url, selectedFormat);

                if (!string.IsNullOrEmpty(videoId))
                {
                    currentVideoId = videoId;
                    DisplayVideo(videoId);
                    subtitleEntries = subtitles;
                    DisplayTranscript();
                    LoadNotes();

                    // Extract transcript text
                    string transcriptText = GetTranscriptText();
                    string prompt = $"Please analyze the following video transcript and provide:\r\n\r\nA concise 2-3 sentence overview that captures the video's main theme and purpose\r\nA comprehensive analysis that includes:\r\n\r\nKey arguments and main points\r\nSupporting evidence and examples provided\r\nAny methodologies or frameworks discussed\r\nNotable quotes or statements\r\nContext and background information provided\r\n\r\n\r\nA chronological breakdown of the video's structure:\r\n\r\nHow the content is organized\r\nMajor topic transitions\r\nTime spent on each main segment (if timestamps are available)\r\n\r\n\r\nCore takeaways, including:\r\n\r\nPrimary insights and conclusions\r\nPractical applications or recommendations\r\nCritical findings or revelations\r\nAreas for further exploration mentioned\r\n\r\n\r\nAdditional considerations:\r\n\r\nAny caveats or limitations mentioned\r\nOpposing viewpoints presented\r\nQuestions raised or left unanswered\r\nResources or references cited\r\n\r\n\r\n\r\nPlease ensure the summary:\r\n\r\nMaintains objective language\r\nPreserves the original context\r\nCaptures both explicit and implicit messages\r\nReflects the relative importance of different points\r\nIncludes specific examples to support main ideas\r\n\r\nVideo Transcript: \n\n{transcriptText}";

                    // Send prompt to Claude AI and get summary
                    string summary = await _apiHelper.GetResponseFromClaude(prompt);

                    // Display summary in NotesTextBox1
                    NotesTextBox1.Text = summary;
                }

                StatusText.Text = "Processing complete";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error processing video: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Processing failed";
            }
        }

        private void DisplayVideo(string videoId)
        {
            string embedHtml = $@"
                <html>
                    <body style='margin:0;padding:0;'>
                        <div id='player'></div>
                        <script src='https://www.youtube.com/iframe_api'></script>
                        <script>
                            var player;
                            function onYouTubeIframeAPIReady() {{
                                player = new YT.Player('player', {{
                                    height: '100%',
                                    width: '100%',
                                    videoId: '{videoId}',
                                    events: {{
                                        'onReady': onPlayerReady,
                                        'onStateChange': onPlayerStateChange
                                    }}
                                }});
                            }}
                            function onPlayerReady(event) {{
                                // Player is ready
                            }}
                            function onPlayerStateChange(event) {{
                                if (event.data == YT.PlayerState.PLAYING) {{
                                    window.chrome.webview.postMessage({{ type: 'playerState', state: 'playing' }});
                                }} else if (event.data == YT.PlayerState.PAUSED) {{
                                    window.chrome.webview.postMessage({{ type: 'playerState', state: 'paused' }});
                                }}
                            }}
                            function getCurrentTime() {{
                                return player.getCurrentTime();
                            }}
                            setInterval(() => {{
                                window.chrome.webview.postMessage({{ type: 'timeUpdate', currentTime: getCurrentTime() }});
                            }}, 100);
                        </script>
                    </body>
                </html>";

            VideoPlayer.NavigateToString(embedHtml);
            VideoPlayer.Visibility = Visibility.Visible;
        }

        private void VideoProcessor_ProgressUpdated(object sender, int progress)
        {
            Dispatcher.Invoke(() => ProgressBar.Value = progress);
        }

        private void VideoProcessor_LogUpdated(object sender, string message)
        {
            Dispatcher.Invoke(() => LogTextBox.AppendText(message + "\n"));
        }

        private void DisplayTranscript()
        {
            TranscriptRichTextBox.Document.Blocks.Clear();
            _subtitleRuns = new List<Run>();

            if (subtitleEntries != null && subtitleEntries.Any())
            {
                foreach (var entry in subtitleEntries)
                {
                    var run = new Run(entry.Text + " ");
                    _subtitleRuns.Add(run);
                    TranscriptRichTextBox.Document.Blocks.Add(new Paragraph(run));
                }
            }
            else
            {
                TranscriptRichTextBox.Document.Blocks.Add(new Paragraph(new Run("No transcript available.")));
            }
        }

        private void UpdateTranscriptDisplay(double currentTime)
        {
            Debug.WriteLine($"Current Time: {currentTime}");

            if (subtitleEntries == null || !subtitleEntries.Any())
                return;

            for (int i = 0; i < subtitleEntries.Count; i++)
            {
                var entry = subtitleEntries[i];
                var nextEntry = i < subtitleEntries.Count - 1 ? subtitleEntries[i + 1] : null;

                bool isCurrentSubtitle = (currentTime >= entry.Start &&
                    (nextEntry == null || currentTime < nextEntry.Start)) ||
                    (i == 0 && currentTime < entry.Start);

                if (isCurrentSubtitle)
                {
                    // Update highlighting
                    if (_currentRun != null)
                    {
                        _currentRun.Background = Brushes.Transparent;
                    }
                    _currentRun = _subtitleRuns[i];
                    _currentRun.Background = Brushes.Yellow;

                    // Only auto-scroll if enabled
                    if (_autoScrollEnabled)
                    {
                        _currentRun.BringIntoView();
                    }
                    break; // Exit loop once we've found the current subtitle
                }
            }
        }

        private void CoreWebView2_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(e.WebMessageAsJson))
                {
                    JsonElement root = doc.RootElement;

                    if (root.TryGetProperty("type", out JsonElement typeElement))
                    {
                        string messageType = typeElement.GetString();

                        if (messageType == "playerState")
                        {
                            if (root.TryGetProperty("state", out JsonElement stateElement))
                            {
                                string state = stateElement.GetString();
                                if (state == "playing")
                                {
                                    timer.Start();
                                }
                                else if (state == "paused")
                                {
                                    timer.Stop();
                                }
                            }
                        }
                        else if (messageType == "timeUpdate")
                        {
                            if (root.TryGetProperty("currentTime", out JsonElement currentTimeElement))
                            {
                                if (currentTimeElement.TryGetDouble(out double currentTime))
                                {
                                    Dispatcher.Invoke(() => UpdateTranscriptDisplay(currentTime));
                                }
                            }
                        }
                    }
                }
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"Error parsing JSON: {ex.Message}");
            }
        }

        private async void Timer_Tick(object sender, EventArgs e)
        {
            try
            {
                var result = await VideoPlayer.CoreWebView2.ExecuteScriptAsync("getCurrentTime()");
                if (double.TryParse(result, out double currentTime))
                {
                    UpdateTranscriptDisplay(currentTime);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting current time: {ex.Message}");
            }
        }

        private void LoadNotes()
        {
            if (!string.IsNullOrEmpty(currentVideoId))
            {
                string notesFilePath1 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{currentVideoId}_notes1.txt");
                string notesFilePath2 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{currentVideoId}_notes2.txt");

                if (File.Exists(notesFilePath1))
                {
                    NotesTextBox1.Text = File.ReadAllText(notesFilePath1);
                }
                else
                {
                    NotesTextBox1.Text = string.Empty;
                }

                if (File.Exists(notesFilePath2))
                {
                    NotesTextBox2.Text = File.ReadAllText(notesFilePath2);
                }
                else
                {
                    NotesTextBox2.Text = string.Empty;
                }
            }
        }

        private void SaveNotes()
        {
            if (!string.IsNullOrEmpty(currentVideoId))
            {
                string notesFilePath1 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{currentVideoId}_notes1.txt");
                string notesFilePath2 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{currentVideoId}_notes2.txt");

                File.WriteAllText(notesFilePath1, NotesTextBox1.Text);
                File.WriteAllText(notesFilePath2, NotesTextBox2.Text);
            }
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            SaveNotes();
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            string userInput = UserInputTextBox.Text.Trim();
            if (string.IsNullOrEmpty(userInput)) return;

            AppendToConversation("User: " + userInput);
            UserInputTextBox.Clear();

            try
            {
                string response = await _apiHelper.GetResponseFromClaude(_conversationHistory + "\nHuman: " + userInput + "\nAssistant:");
                AppendToConversation("Assistant: " + response);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UserInputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SendButton_Click(sender, e);
                e.Handled = true;
            }
        }

        private void AppendToConversation(string message)
        {
            _conversationHistory += message + "\n";
            ConversationTextBox.Text = _conversationHistory;
            ConversationTextBox.ScrollToEnd();
        }

        private void CopyMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (TranscriptRichTextBox.Selection.Text.Length > 0)
            {
                Clipboard.SetText(TranscriptRichTextBox.Selection.Text);
            }
        }

        private void AutoScrollToggle_Checked(object sender, RoutedEventArgs e)
        {
            _autoScrollEnabled = true;
            if (_currentRun != null)
            {
                _currentRun.BringIntoView();
            }
        }

        private void AutoScrollToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            _autoScrollEnabled = false;
        }
        private string GetTranscriptText()
        {
            if (subtitleEntries == null || !subtitleEntries.Any())
            {
                return string.Empty;
            }

            return string.Join(" ", subtitleEntries.Select(entry => entry.Text));
        }
    }
}