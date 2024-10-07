using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Newtonsoft.Json.Linq;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Transcript001
{
    public partial class MainWindow : Window
    {
        private List<string> videoUrls = new List<string>();
        private VideoProcessor videoProcessor;

        public MainWindow()
        {
            InitializeComponent();
            videoProcessor = new VideoProcessor();
            videoProcessor.ProgressUpdated += VideoProcessor_ProgressUpdated;
            videoProcessor.LogUpdated += VideoProcessor_LogUpdated;
        }

        private void LoadJson_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    string jsonContent = File.ReadAllText(openFileDialog.FileName);
                    JObject jsonObject = JObject.Parse(jsonContent);
                    JArray linksArray = (JArray)jsonObject["links"];
                    videoUrls = linksArray.ToObject<List<string>>();
                    StatusText.Text = $"Loaded {videoUrls.Count} video URLs";
                    LogTextBox.AppendText($"Loaded {videoUrls.Count} video URLs from {openFileDialog.FileName}\n");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading JSON: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void ProcessVideos_Click(object sender, RoutedEventArgs e)
        {
            if (videoUrls.Count == 0)
            {
                MessageBox.Show("Please load a JSON file with video URLs first.", "No URLs", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            StatusText.Text = "Processing videos...";
            ProgressBar.Value = 0;
            ProgressBar.Maximum = videoUrls.Count;

            await videoProcessor.ProcessVideosAsync(videoUrls);

            StatusText.Text = "Processing complete";
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