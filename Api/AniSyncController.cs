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
        private readonly IServerApplicationHost _serverApplicationHost;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<AniSyncController> _logger;

        public AniSyncController(IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory, IServerApplicationHost serverApplicationHost, IHttpContextAccessor httpContextAccessor) {
            _httpClientFactory = httpClientFactory;
            _loggerFactory = loggerFactory;
            _serverApplicationHost = serverApplicationHost;
            _httpContextAccessor = httpContextAccessor;
            _logger = loggerFactory.CreateLogger<AniSyncController>();
        }

        [HttpGet]
        [Route("buildAuthorizeRequestUrl")]
        public string BuildAuthorizeRequestUrl(string clientId, string clientSecret, string? url) {
            return new MalApiAuthentication(_httpClientFactory, _serverApplicationHost, _httpContextAccessor, new ProviderApiAuth{ClientId = clientId, ClientSecret = clientSecret}, url).BuildAuthorizeRequestUrl();
        }

        [HttpGet]
        [Route("authCallback")]
        public void MalCallback(string code) {
            Guid userId = Plugin.Instance.PluginConfiguration.currentlyAuthenticatingUser;
            Console.WriteLine("plugin user id: " + userId);
            if (userId != null) {
                new MalApiAuthentication(_httpClientFactory, _serverApplicationHost, _httpContextAccessor).GetMalToken(userId, code);
                Plugin.Instance.PluginConfiguration.currentlyAuthenticatingUser = Guid.Empty;
                Plugin.Instance.SaveConfiguration();
            } else {
                _logger.LogError("Authenticated user ID could not be found in the configuration. Please regenerate the authentication URL and try again");
            }
        }

        [HttpGet]
        [Route("user")]
        public async Task<MalApiCalls.User> GetUser(string userId) {
            MalApiCalls malApiCalls = new MalApiCalls(_httpClientFactory, _loggerFactory, _serverApplicationHost, _httpContextAccessor);
            try {
                malApiCalls.UserConfig = Plugin.Instance.PluginConfiguration.UserConfig.FirstOrDefault(item => item.UserId == Guid.Parse(userId));
            } catch (ArgumentNullException) {
                _logger.LogError("User not found");
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