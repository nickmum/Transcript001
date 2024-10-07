using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;

namespace Transcript001
{
    public partial class MainWindow : Window
    {
        private VideoProcessor videoProcessor;

        public MainWindow()
        {
            InitializeComponent();
            videoProcessor = new VideoProcessor();
            videoProcessor.ProgressUpdated += VideoProcessor_ProgressUpdated;
            videoProcessor.LogUpdated += VideoProcessor_LogUpdated;
            InitializeWebView();
        }

        private async void InitializeWebView()
        {
            await VideoPlayer.EnsureCoreWebView2Async();
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
                string videoId = await videoProcessor.ProcessVideoAsync(url, selectedFormat);

                if (!string.IsNullOrEmpty(videoId))
                {
                    DisplayVideo(videoId);
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
                        <iframe width='100%' height='100%' src='https://www.youtube.com/embed/{videoId}' frameborder='0' allow='autoplay; encrypted-media' allowfullscreen></iframe>
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
    }
}