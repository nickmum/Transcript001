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

namespace Transcript001
{
    public partial class MainWindow : Window
    {
        private VideoProcessor videoProcessor;
        private List<SubtitleEntryViewModel> subtitleEntries;
        private DispatcherTimer timer;

        public MainWindow()
        {
            InitializeComponent();
            videoProcessor = new VideoProcessor();
            videoProcessor.ProgressUpdated += VideoProcessor_ProgressUpdated;
            videoProcessor.LogUpdated += VideoProcessor_LogUpdated;
            InitializeWebView();
            InitializeTimer();
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
                    DisplayVideo(videoId);
                    subtitleEntries = subtitles.Select(s => new SubtitleEntryViewModel(s)).ToList();
                    DisplayTranscript();
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
            if (subtitleEntries != null && subtitleEntries.Any())
            {
                TranscriptListView.ItemsSource = subtitleEntries;
            }
            else
            {
                TranscriptListView.ItemsSource = new List<string> { "No transcript available." };
            }
        }

        private void UpdateTranscriptDisplay(double currentTime)
        {
            Debug.WriteLine($"Current Time: {currentTime}");

            for (int i = 0; i < subtitleEntries.Count; i++)
            {
                var entry = subtitleEntries[i];
                var nextEntry = i < subtitleEntries.Count - 1 ? subtitleEntries[i + 1] : null;

                bool isCurrentSubtitle = (currentTime >= entry.Start && (nextEntry == null || currentTime < nextEntry.Start)) ||
                                         (i == 0 && currentTime < entry.Start);

                if (isCurrentSubtitle != entry.IsHighlighted)
                {
                    entry.IsHighlighted = isCurrentSubtitle;
                    Debug.WriteLine($"Updated subtitle {i}: Start={entry.Start}, IsHighlighted={entry.IsHighlighted}");
                }
            }

            // Force the ListView to refresh
            TranscriptListView.Items.Refresh();

            // Scroll to the highlighted item
            var highlightedItem = subtitleEntries.FirstOrDefault(s => s.IsHighlighted);
            if (highlightedItem != null)
            {
                TranscriptListView.ScrollIntoView(highlightedItem);
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
    }

    public class SubtitleEntryViewModel : INotifyPropertyChanged
    {
        public double Start { get; }
        public string Text { get; }
        private bool _isHighlighted;
        public bool IsHighlighted
        {
            get => _isHighlighted;
            set
            {
                if (_isHighlighted != value)
                {
                    _isHighlighted = value;
                    OnPropertyChanged(nameof(IsHighlighted));
                }
            }
        }

        public SubtitleEntryViewModel(SubtitleEntry entry)
        {
            Start = entry.Start;
            Text = entry.Text;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}