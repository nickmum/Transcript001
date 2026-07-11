using Microsoft.VisualStudio.TestTools.UnitTesting;
using Transcript001;

namespace Transcript001.Tests
{
    [TestClass]
    public class VideoProcessorTests
    {
        [TestMethod]
        public void ExtractVideoId_ReturnsIdFromWatchUrl()
        {
            Assert.AreEqual("rbu7Zu5X1zI", VideoProcessor.ExtractVideoId("https://www.youtube.com/watch?v=rbu7Zu5X1zI"));
        }

        [TestMethod]
        public void ExtractVideoId_ReturnsIdFromWatchUrlWithExtraParameters()
        {
            Assert.AreEqual("rbu7Zu5X1zI", VideoProcessor.ExtractVideoId("https://www.youtube.com/watch?v=rbu7Zu5X1zI&t=30s"));
        }

        [TestMethod]
        public void ExtractVideoId_ReturnsIdFromShortUrl()
        {
            Assert.AreEqual("rbu7Zu5X1zI", VideoProcessor.ExtractVideoId("https://youtu.be/rbu7Zu5X1zI"));
        }

        [TestMethod]
        public void ExtractVideoId_ReturnsNullWhenUrlHasNoVideoId()
        {
            Assert.IsNull(VideoProcessor.ExtractVideoId("https://www.youtube.com/feed/subscriptions"));
        }

        [TestMethod]
        public void ExtractVideoId_ReturnsNullForMalformedUrl()
        {
            Assert.IsNull(VideoProcessor.ExtractVideoId("not a url"));
        }
    }
}
