using System;
using System.Net.Http;
using jellyfin_ani_sync.Configuration;
using jellyfin_ani_sync.Helpers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace jellyfin_ani_sync.Tests.Helpers {
    public class MemoryCacheHelperTests {
        private IMemoryCache memoryCache;
        private Mock<ILogger> mockLogger;

        [SetUp]
        public void Setup() {
            memoryCache = new MemoryCache(new MemoryCacheOptions());
            mockLogger = new Mock<ILogger>();
        }

        [TearDown]
        public void TearDown() {
            memoryCache.Dispose();
        }
        
        [Test]
        public void CheckResponseHeadersForRateLimiting_RateLimitReached_SetsTwoMinuteBackoff() {
            var responseMessage = new HttpResponseMessage();
            responseMessage.Headers.TryAddWithoutValidation(
                MemoryCacheHelper.XRateLimitRemainingHeader, 
                "0"
            );

            MemoryCacheHelper.CheckResponseHeadersForRateLimiting(
                responseMessage,
                mockLogger.Object,
                ApiName.AniList,
                memoryCache
            );

            mockLogger.Verify(x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString().Contains("Reached rate limit")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ), Times.Once);
        }

        [Test]
        public void CheckResponseHeadersForRateLimiting_ResetTimeInFuture_SetsDelayBasedOnReset() {
            var responseMessage = new HttpResponseMessage();
            var futureTime = DateTimeOffset.UtcNow.AddHours(1);
            
            responseMessage.Headers.TryAddWithoutValidation(
                MemoryCacheHelper.XRateLimitResetHeader, 
                futureTime.ToString("R")
            );

            MemoryCacheHelper.CheckResponseHeadersForRateLimiting(
                responseMessage,
                mockLogger.Object,
                ApiName.AniList,
                memoryCache
            );

            mockLogger.Verify(x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString().Contains("Rate limit until")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ), Times.Once);
        }

        [Test]
        public void CheckResponseHeadersForRateLimiting_NoRateLimitHeaders_LogsNothing() {
            var responseMessage = new HttpResponseMessage();

            MemoryCacheHelper.CheckResponseHeadersForRateLimiting(
                responseMessage,
                mockLogger.Object,
                ApiName.AniList,
                memoryCache
            );

            mockLogger.Verify(x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString().Contains("Reached rate limit")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ), Times.Never);

            mockLogger.Verify(x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString().Contains("Rate limit until")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ), Times.Never);
        }
    }
}