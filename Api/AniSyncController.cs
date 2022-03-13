#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Dynamic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using jellyfin_ani_sync.Api.Anilist;
using jellyfin_ani_sync.Configuration;
using MediaBrowser.Controller;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
        public string BuildAuthorizeRequestUrl(ApiName provider, string clientId, string clientSecret, string? url) {
            return new ApiAuthentication(provider, _httpClientFactory, _serverApplicationHost, _httpContextAccessor, new ProviderApiAuth { ClientId = clientId, ClientSecret = clientSecret }, url).BuildAuthorizeRequestUrl();
        }

        [HttpGet]
        [Route("authCallback")]
        public void MalCallback(string code) {
            Guid userId = Plugin.Instance.PluginConfiguration.currentlyAuthenticatingUser;
            ApiName provider = Plugin.Instance.PluginConfiguration.currentlyAuthenticatingProvider;
            if (userId != null && provider != null) {
                new ApiAuthentication(provider, _httpClientFactory, _serverApplicationHost, _httpContextAccessor).GetToken(userId, code);
                Plugin.Instance.PluginConfiguration.currentlyAuthenticatingUser = Guid.Empty;
                Plugin.Instance.SaveConfiguration();
            } else {
                _logger.LogError("Authenticated user ID could not be found in the configuration. Please regenerate the authentication URL and try again");
            }
        }

        [HttpGet]
        [Route("user")]
        // this only works for mal atm, needs to work for anilist as well
        public async Task<MalApiCalls.User> GetUser(ApiName apiName, string userId) {
            if (apiName == ApiName.Mal) {
                MalApiCalls malApiCalls;
                try {
                    malApiCalls = new MalApiCalls(_httpClientFactory, _loggerFactory, _serverApplicationHost, _httpContextAccessor, Plugin.Instance.PluginConfiguration.UserConfig.FirstOrDefault(item => item.UserId == Guid.Parse(userId)));
                } catch (ArgumentNullException) {
                    _logger.LogError("User not found");
                    throw;
                }

                return await malApiCalls.GetUserInformation();
            } else if (apiName == ApiName.AniList) {
                AniListApiCalls aniListApiCalls;
                try {
                    aniListApiCalls = new AniListApiCalls(_httpClientFactory, _loggerFactory, _serverApplicationHost, _httpContextAccessor, Plugin.Instance.PluginConfiguration.UserConfig.FirstOrDefault(item => item.UserId == Guid.Parse(userId)));
                } catch (ArgumentNullException) {
                    _logger.LogError("User not found");
                    throw;
                }

                return new MalApiCalls.User {
                    Name = await aniListApiCalls.GetCurrentUser()
                };
            }

            throw new Exception("Provider not supported.");
        }

        [HttpGet]
        [Route("parameters")]
        public object GetFrontendParameters() {
            Parameters toReturn = new Parameters();
            toReturn.providerList = new List<ExpandoObject>();
            foreach (ApiName apiName in Enum.GetValues<ApiName>()) {
                dynamic provider = new ExpandoObject();
                provider.Name = apiName.GetType()
                    .GetMember(apiName.ToString())
                    .First()
                    .GetCustomAttribute<DisplayAttribute>()
                    ?.GetName();
                provider.Key = apiName;
                toReturn.providerList.Add(provider);
            }

#if NET5_0
            toReturn.localApiUrl = _serverApplicationHost.ListenWithHttps ? $"https://{Request.HttpContext.Connection.LocalIpAddress}:{_serverApplicationHost.HttpsPort}" : $"http://{Request.HttpContext.Connection.LocalIpAddress}:{_serverApplicationHost.HttpPort}";
#elif NET6_0
            toReturn.localApiUrl = _serverApplicationHost.GetApiUrlForLocalAccess();
#endif
            return toReturn;
        }

        private class Parameters {
            public string localApiUrl { get; set; }
            public List<ExpandoObject> providerList { get; set; }
        }

        [HttpGet]
        [Route("apiUrlTest")]
        public string ApiUrlTest() {
            return "This is the correct URL.";
        }
    }
}