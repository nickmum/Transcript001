using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Transcript001;

namespace Transcript001.Tests
{
    [TestClass]
    public class MainWindowTests
    {
        [TestMethod]
        public async Task ProcessVideoAsync_ShouldDisplaySummaryInNotesTextBox1()
        {
            // Arrange
            var mainWindow = new MainWindow();
            string testUrl = "https://www.youtube.com/watch?v=rbu7Zu5X1zI";

            // Act
            string summary = await mainWindow.ProcessVideoAsync(testUrl);

            // Assert
            Assert.IsNotNull(summary);
            Assert.AreEqual(summary, mainWindow.NotesTextBox1.Text);
        }
    }
}
