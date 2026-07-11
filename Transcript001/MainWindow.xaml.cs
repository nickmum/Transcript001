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
using System.Text.RegularExpressions;

namespace Transcript001
{
    public partial class MainWindow : Window
    {
        private VideoProcessor videoProcessor;
        private List<SubtitleEntry> subtitleEntries;
        private string currentVideoId;
        private readonly ClaudeApiHelper _apiHelper;
        private readonly List<ChatMessage> _chatMessages = new List<ChatMessage>();
        private List<Run> _subtitleRuns;
        private Run _currentRun;
        private bool _autoScrollEnabled = true;

        private const string SummaryPromptTemplate = "Please analyze the following video transcript and provide:\r\n\r\nA concise 2-3 sentence overview that captures the video's main theme and purpose\r\nA comprehensive analysis that includes:\r\n\r\nKey arguments and main points\r\nSupporting evidence and examples provided\r\nAny methodologies or frameworks discussed\r\nNotable quotes or statements\r\nContext and background information provided\r\n\r\n\r\nA chronological breakdown of the video's structure:\r\n\r\nHow the content is organized\r\nMajor topic transitions\r\nTime spent on each main segment (if timestamps are available)\r\n\r\n\r\nCore takeaways, including:\r\n\r\nPrimary insights and conclusions\r\nPractical applications or recommendations\r\nCritical findings or revelations\r\nAreas for further exploration mentioned\r\n\r\n\r\nAdditional considerations:\r\n\r\nAny caveats or limitations mentioned\r\nOpposing viewpoints presented\r\nQuestions raised or left unanswered\r\nResources or references cited\r\n\r\n\r\n\r\nPlease ensure the summary:\r\n\r\nMaintains objective language\r\nPreserves the original context\r\nCaptures both explicit and implicit messages\r\nReflects the relative importance of different points\r\nIncludes specific examples to support main ideas\r\n\r\nVideo Transcript: \n\n";

        public MainWindow()
        {
            InitializeComponent();
            AutoScrollToggle.IsChecked = true;
            videoProcessor = new VideoProcessor();
            videoProcessor.ProgressUpdated += VideoProcessor_ProgressUpdated;
            videoProcessor.LogUpdated += VideoProcessor_LogUpdated;
            InitializeWebView();
            this.Closing += MainWindow_Closing;
            string apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
            if (string.IsNullOrEmpty(apiKey))
            {
                MessageBox.Show("ANTHROPIC_API_KEY environment variable is not set. AI summary and chat features will not work.", "Missing API Key", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            _apiHelper = new ClaudeApiHelper(apiKey);
            UrlTextBox.Text = "https://www.youtube.com/watch?v=rbu7Zu5X1zI";
        }

        // YouTube rejects embeds from pages without a real origin (player error 153),
        // so the player page is served from a virtual host mapped to a local folder
        // instead of being loaded with NavigateToString.
        private const string PlayerHostName = "transcript001.player";

        private string PlayerDirectory => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "player");

        private async void InitializeWebView()
        {
            await VideoPlayer.EnsureCoreWebView2Async();
            Directory.CreateDirectory(PlayerDirectory);
            VideoPlayer.CoreWebView2.SetVirtualHostNameToFolderMapping(
                PlayerHostName, PlayerDirectory, CoreWebView2HostResourceAccessKind.Allow);
            VideoPlayer.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
        }

        public async Task<string> ProcessVideoAsync(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                throw new ArgumentException("Please enter a video URL.", nameof(url));
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

                    // The video and transcript are already displayed at this point, so a
                    // summary failure (no API credits, network, etc.) shouldn't read as a
                    // failure of the whole video-processing step.
                    StatusText.Text = "Generating AI summary...";
                    try
                    {
                        string transcriptText = GetTranscriptText();
                        string summary = await _apiHelper.GetResponseFromClaude(SummaryPromptTemplate + transcriptText);
                        NotesTextBox1.Text = summary;
                        StatusText.Text = "Processing complete";
                        return summary;
                    }
                    catch (Exception ex)
                    {
                        StatusText.Text = "Video loaded — AI summary failed";
                        MessageBox.Show(
                            $"The video and transcript loaded successfully, but the AI summary could not be generated:\n\n{ex.Message}",
                            "AI Summary Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return null;
                    }
                }

                StatusText.Text = "Processing complete";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error processing video: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Processing failed";
            }

            return null;
        }

        private async void ProcessVideo_Click(object sender, RoutedEventArgs e)
        {
            string url = UrlTextBox.Text.Trim();
            await ProcessVideoAsync(url);
        }

        private void DisplayVideo(string videoId)
        {
            // The ID is interpolated into HTML/JS below, so only accept the exact
            // 11-character YouTube ID format to prevent script injection.
            if (!Regex.IsMatch(videoId, "^[A-Za-z0-9_-]{11}$"))
            {
                MessageBox.Show("The video ID extracted from the URL is not valid.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (VideoPlayer.CoreWebView2 == null)
            {
                MessageBox.Show("The video player is still initializing. Please try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

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
                                    playerVars: {{ 'origin': 'https://{PlayerHostName}' }}
                                }});
                            }}
                            setInterval(() => {{
                                if (player && player.getPlayerState && player.getPlayerState() === YT.PlayerState.PLAYING) {{
                                    window.chrome.webview.postMessage({{ type: 'timeUpdate', currentTime: player.getCurrentTime() }});
                                }}
                            }}, 100);
                        </script>
                    </body>
                </html>";

            Directory.CreateDirectory(PlayerDirectory);
            File.WriteAllText(Path.Combine(PlayerDirectory, "player.html"), embedHtml);
            VideoPlayer.CoreWebView2.Navigate($"https://{PlayerHostName}/player.html");
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

                        if (messageType == "timeUpdate")
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

        private string GetNotesFilePath(int noteNumber)
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{currentVideoId}_notes{noteNumber}.txt");
        }

        private List<TextBox> GetNoteTextBoxes()
        {
            var textBoxes = new List<TextBox>();
            foreach (var item in NotesTabControl.Items)
            {
                if (item is TabItem tab && tab.Content is TextBox textBox)
                {
                    textBoxes.Add(textBox);
                }
            }
            return textBoxes;
        }

        private void LoadNotes()
        {
            if (string.IsNullOrEmpty(currentVideoId)) return;

            var textBoxes = GetNoteTextBoxes();

            // Recreate tabs for any saved note files beyond the tabs that already exist
            while (File.Exists(GetNotesFilePath(textBoxes.Count + 1)))
            {
                textBoxes.Add(CreateNoteTab());
            }

            for (int i = 0; i < textBoxes.Count; i++)
            {
                string path = GetNotesFilePath(i + 1);
                textBoxes[i].Text = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
            }
        }

        private void SaveNotes()
        {
            if (string.IsNullOrEmpty(currentVideoId)) return;

            int noteNumber = 1;
            foreach (var textBox in GetNoteTextBoxes())
            {
                File.WriteAllText(GetNotesFilePath(noteNumber), textBox.Text);
                noteNumber++;
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
            _chatMessages.Add(new ChatMessage("user", userInput));

            try
            {
                string response = await _apiHelper.GetResponseFromClaude(_chatMessages);
                _chatMessages.Add(new ChatMessage("assistant", response));
                AppendToConversation("Assistant: " + response);
            }
            catch (Exception ex)
            {
                // Remove the unanswered user message so the history stays valid for the next attempt
                _chatMessages.RemoveAt(_chatMessages.Count - 1);
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
            ConversationTextBox.AppendText(message + "\n");
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

        private TextBox CreateNoteTab()
        {
            var textBox = new TextBox
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 14
            };

            var newTab = new TabItem
            {
                Header = $"Note {NotesTabControl.Items.Count + 1}",
                Content = textBox
            };

            NotesTabControl.Items.Add(newTab);
            return textBox;
        }

        private void TabItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Only the header visuals live inside the TabItem, so this fires for
            // header double-clicks, not clicks on the tab's content
            if (!(sender is TabItem tab) || !(tab.Header is string currentName)) return;

            var editBox = new TextBox
            {
                Text = currentName,
                MinWidth = 60
            };

            void Commit()
            {
                if (!ReferenceEquals(tab.Header, editBox)) return;
                string newName = editBox.Text.Trim();
                tab.Header = string.IsNullOrEmpty(newName) ? currentName : newName;
            }

            editBox.KeyDown += (s, args) =>
            {
                if (args.Key == Key.Enter)
                {
                    Commit();
                    args.Handled = true;
                }
                else if (args.Key == Key.Escape)
                {
                    tab.Header = currentName;
                    args.Handled = true;
                }
            };
            editBox.LostFocus += (s, args) => Commit();
            editBox.Loaded += (s, args) =>
            {
                editBox.Focus();
                editBox.SelectAll();
            };

            tab.Header = editBox;
            e.Handled = true;
        }

        private void AddTabButton_Click(object sender, RoutedEventArgs e)
        {
            CreateNoteTab();
            NotesTabControl.SelectedIndex = NotesTabControl.Items.Count - 1;
        }

        private void AddSketchTabButton_Click(object sender, RoutedEventArgs e)
        {
            var toolbar = new SketchToolbar();
            var canvas = new SketchCanvas();
            toolbar.TargetCanvas = canvas;

            var grid = new Grid
            {
                RowDefinitions =
                {
                    new RowDefinition { Height = new GridLength(50) },
                    new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }
                },
                Children = { toolbar, canvas }
            };
            Grid.SetRow(toolbar, 0);
            Grid.SetRow(canvas, 1);

            TabItem newSketchTab = new TabItem
            {
                Header = $"Sketch {NotesTabControl.Items.Count + 1}",
                Content = grid
            };

            NotesTabControl.Items.Add(newSketchTab);
            NotesTabControl.SelectedItem = newSketchTab;
        }
    }
}