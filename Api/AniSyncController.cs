#nullable enable
using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using jellyfin_ani_sync.Configuration;
using MediaBrowser.Controller;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace jellyfin_ani_sync.Api {
    [ApiController]
    [Route("[controller]")]
    public class AniSyncController : ControllerBase {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IMemoryCache _memoryCache;
        private readonly IServerApplicationHost _serverApplicationHost;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AniSyncController(IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory, IMemoryCache memoryCache, IServerApplicationHost serverApplicationHost, IHttpContextAccessor httpContextAccessor) {
            _httpClientFactory = httpClientFactory;
            _loggerFactory = loggerFactory;
            _memoryCache = memoryCache;
            _serverApplicationHost = serverApplicationHost;
            _httpContextAccessor = httpContextAccessor;
        }

        [HttpGet]
        [Route("buildAuthorizeRequestUrl")]
        public string BuildAuthorizeRequestUrl(string userId, string clientId, string clientSecret, string? url) {
            return new MalApiAuthentication(_httpClientFactory, _serverApplicationHost, _httpContextAccessor, _memoryCache, new ProviderApiAuth{ClientId = clientId, ClientSecret = clientSecret}, url).BuildAuthorizeRequestUrl(userId);
        }

        [HttpGet]
        [Route("authCallback")]
        public void MalCallback(string code) {
            new MalApiAuthentication(_httpClientFactory, _serverApplicationHost, _httpContextAccessor, _memoryCache).GetMalToken(Guid.Parse(_memoryCache.Get<string>("userId")), code);
            _memoryCache.Remove("userId");
        }

        [HttpGet]
        [Route("user")]
        public async Task<MalApiCalls.User> GetUser(string userId) {
            MalApiCalls malApiCalls = new MalApiCalls(_httpClientFactory, _loggerFactory, _serverApplicationHost, _httpContextAccessor);
            try {
                malApiCalls.UserConfig = Plugin.Instance.PluginConfiguration.UserConfig.FirstOrDefault(item => item.UserId == Guid.Parse(userId));
            } catch (ArgumentNullException) {
                _loggerFactory.CreateLogger<AniSyncController>().LogError("User not found");
                throw;
            }
            return await malApiCalls.GetUserInformation();
        }

        [HttpGet]
        [Route("localApiUrl")]
        public string GetLocalApiUrl() {
#if NET5_0
            return _serverApplicationHost.ListenWithHttps ? $"https://{Request.HttpContext.Connection.LocalIpAddress}:{_serverApplicationHost.HttpsPort}" : $"http://{Request.HttpContext.Connection.LocalIpAddress}:{_serverApplicationHost.HttpPort}";
#elif NET6_0
            return _serverApplicationHost.GetApiUrlForLocalAccess();
#endif
        }

        [HttpGet]
        [Route("apiUrlTest")]
        public string ApiUrlTest() {
            return "This is the correct URL.";
        }
    }
}