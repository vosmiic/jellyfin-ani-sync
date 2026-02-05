#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using jellyfin_ani_sync.Api.Anilist;
using jellyfin_ani_sync.Api.Annict;
using jellyfin_ani_sync.Api.Kitsu;
using jellyfin_ani_sync.Api.Shikimori;
using jellyfin_ani_sync.Api.Simkl;
using jellyfin_ani_sync.Configuration;
using jellyfin_ani_sync.Helpers;
using jellyfin_ani_sync.Extensions;
using jellyfin_ani_sync.Interfaces;
using jellyfin_ani_sync.Models;
using jellyfin_ani_sync.Enums;
using MediaBrowser.Common.Api;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Plugins;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Jellyfin.Database.Implementations.Entities;

namespace jellyfin_ani_sync.Api {
    [ApiController]
    [Route("[controller]")]
    public class AniSyncController : ControllerBase {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IServerApplicationHost _serverApplicationHost;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILibraryManager _libraryManager;
        private readonly IUserManager _userManager;
        private readonly IApplicationPaths _applicationPaths;
        private readonly IUserDataManager _userDataManager;
        private readonly IFileSystem _fileSystem;
        private readonly ILogger<AniSyncController> _logger;
        private readonly IMemoryCache _memoryCache;
        private readonly IAsyncDelayer _delayer;

        public AniSyncController(IHttpClientFactory httpClientFactory,
            ILoggerFactory loggerFactory,
            IServerApplicationHost serverApplicationHost,
            IHttpContextAccessor httpContextAccessor,
            ILibraryManager libraryManager,
            IUserManager userManager,
            IApplicationPaths applicationPaths,
            IUserDataManager userDataManager,
            IFileSystem fileSystem,
            IMemoryCache memoryCache) {
            _httpClientFactory = httpClientFactory;
            _loggerFactory = loggerFactory;
            _serverApplicationHost = serverApplicationHost;
            _httpContextAccessor = httpContextAccessor;
            _libraryManager = libraryManager;
            _userManager = userManager;
            _applicationPaths = applicationPaths;
            _userDataManager = userDataManager;
            _fileSystem = fileSystem;
            _logger = loggerFactory.CreateLogger<AniSyncController>();
            _memoryCache = memoryCache;
            _delayer = new Delayer();
        }

        private bool UserPagesEnabled()
        {
            return Plugin.Instance?.PluginConfiguration.enableUserPages == true;
        }

        [Authorize(Policy = Policies.RequiresElevation)]
        [HttpGet]
        [Route("buildAuthorizeRequestUrl")]
        public string BuildAuthorizeRequestUrl(ApiName provider, string clientId, string clientSecret, string? url, Guid user) {
            return new ApiAuthentication(provider, _httpClientFactory, _serverApplicationHost, _httpContextAccessor, _loggerFactory, _memoryCache, new ProviderApiAuth { ClientId = clientId, ClientSecret = clientSecret }, url).BuildAuthorizeRequestUrl(user);
        }

        [Authorize(Policy = Policies.RequiresElevation)]
        [HttpGet]
        [Route("testAnimeListSaveLocation")]
        public async Task<IActionResult> TestAnimeSaveLocation(string saveLocation) {
            if (String.IsNullOrEmpty(saveLocation))
                return BadRequest("Save location is empty");

            try {
                await using (System.IO.File.Create(
                                 Path.Combine(
                                     saveLocation,
                                     Path.GetRandomFileName()
                                 ),
                                 1,
                                 FileOptions.DeleteOnClose)) {
                }

                return Ok(string.Empty);
            } catch (Exception e) {
                return BadRequest(e.Message);
            }
        }

        [Authorize(Policy = Policies.RequiresElevation)]
        [HttpGet]
        [Route("passwordGrant")]
        public async Task<IActionResult> PasswordGrantAuthentication(ApiName provider, string userId, string username, string password) {
            try {
                new ApiAuthentication(provider, _httpClientFactory, _serverApplicationHost, _httpContextAccessor, _loggerFactory, _memoryCache, new ProviderApiAuth { ClientId = username, ClientSecret = password }).GetToken(Guid.Parse(userId));
            } catch (Exception e) {
                return StatusCode(500, $"Could not authenticate; {e.Message}");
            }

            if (provider == ApiName.Kitsu) {
                var userConfig = Plugin.Instance?.PluginConfiguration.UserConfig.FirstOrDefault(item => item.UserId == Guid.Parse(userId));

                if (userConfig != null) {
                    KitsuApiCalls kitsuApiCalls = new KitsuApiCalls(_httpClientFactory, _loggerFactory, _serverApplicationHost, _httpContextAccessor, _memoryCache, _delayer, userConfig);
                    var kitsuUserConfig = await kitsuApiCalls.GetUserInformation();
                    if (kitsuUserConfig == null)
                        return StatusCode(500, "Could not authenticate");
                    var existingKeyPair = userConfig.KeyPairs.FirstOrDefault(item => item.Key == "KitsuUserId");
                    if (existingKeyPair != null) {
                        existingKeyPair.Value = kitsuUserConfig.Id.ToString();
                    } else {
                        userConfig.KeyPairs.Add(new KeyPairs { Key = "KitsuUserId", Value = kitsuUserConfig.Id.ToString() });
                    }

                    Plugin.Instance?.SaveConfiguration();
                }
            }

            return Ok();
        }

        [Authorize(Policy = Policies.RequiresElevation)]
        [HttpGet]
        [Route("user")]
        // this only works for mal atm, needs to work for anilist as well
        public async Task<ActionResult> GetUser(ApiName apiName, string userId) {
            UserConfig? userConfig = Plugin.Instance?.PluginConfiguration.UserConfig.FirstOrDefault(item => item.UserId == Guid.Parse(userId));
            if (userConfig == null) {
                _logger.LogError("User not found in config");
                return StatusCode(500, "User not found in config");
            }

            switch (apiName) {
                case ApiName.Mal:
                    MalApiCalls malApiCalls = new MalApiCalls(_httpClientFactory, _loggerFactory, _serverApplicationHost, _httpContextAccessor, _memoryCache, _delayer, userConfig);

                    MalApiCalls.User? malUser = await malApiCalls.GetUserInformation();
                    return malUser != null ? new OkObjectResult(malUser) : StatusCode(500, "Authentication failed");
                case ApiName.AniList:
                    AniListApiCalls aniListApiCalls = new AniListApiCalls(_httpClientFactory, _loggerFactory, _serverApplicationHost, _httpContextAccessor, _memoryCache, _delayer, userConfig);

                    AniListViewer.Viewer? user = await aniListApiCalls.GetCurrentUser();
                    if (user == null) {
                        return StatusCode(500, "Authentication failed");
                    }

                    return new OkObjectResult(new MalApiCalls.User {
                        Name = user.Name
                    });
                case ApiName.Kitsu:
                    KitsuApiCalls kitsuApiCalls;
                    try {
                        kitsuApiCalls = new KitsuApiCalls(_httpClientFactory, _loggerFactory, _serverApplicationHost, _httpContextAccessor, _memoryCache, _delayer, userConfig);
                    } catch (ArgumentNullException) {
                        _logger.LogError("User could not be retrieved from API");
                        return StatusCode(500, "User could not be retrieved from API");
                    }

                    var apiCall = await kitsuApiCalls.GetUserInformation();
                    if (apiCall == null) {
                        return StatusCode(500, "Authentication failed");
                    }

                    return new OkObjectResult(new MalApiCalls.User {
                        Name = apiCall.Name
                    });
                case ApiName.Annict:
                    // sleep the thread for a short amount of time to let the user config save
                    Thread.Sleep(100);
                    AnnictApiCalls annictApiCalls;
                    try {
                        annictApiCalls = new AnnictApiCalls(_httpClientFactory, _loggerFactory, _serverApplicationHost, _httpContextAccessor, _memoryCache, _delayer, userConfig);
                    } catch (ArgumentNullException) {
                        _logger.LogError("User could not be retrieved from API");
                        return StatusCode(500, "User could not be retrieved from API");
                    }

                    var annictApiCall = await annictApiCalls.GetCurrentUser();
                    if (annictApiCall == null) {
                        return StatusCode(500, "Authentication failed");
                    }

                    return new OkObjectResult(new MalApiCalls.User {
                        Name = annictApiCall.AnnictSearchData.Viewer.username
                    });
                case ApiName.Shikimori:
                    string? shikimoriAppName = ConfigHelper.GetShikimoriAppName(_logger);
                    if (string.IsNullOrEmpty(shikimoriAppName)) {
                        return StatusCode(500, "No App Name");
                    }

                    ShikimoriApiCalls shikimoriApiCalls = new ShikimoriApiCalls(_httpClientFactory, _loggerFactory, _serverApplicationHost, _httpContextAccessor, _memoryCache, _delayer, new Dictionary<string, string> { { "User-Agent", shikimoriAppName } }, userConfig);

                    ShikimoriApiCalls.User? shikimoriUserApiCall = await shikimoriApiCalls.GetUserInformation();
                    if (shikimoriUserApiCall != null) {
                        return new OkObjectResult(new MalApiCalls.User {
                            Name = shikimoriUserApiCall.Name
                        });
                    } else {
                        _logger.LogError("User could not be retrieved from API");
                        return StatusCode(500, "User could not be retrieved from API");
                    }
                case ApiName.Simkl:
                    string? simklClientId = ConfigHelper.GetSimklClientId(_logger);
                    if (string.IsNullOrEmpty(simklClientId)) {
                        return StatusCode(500, "No Client ID");
                    }

                    var simklApiCalls = new SimklApiCalls(_httpClientFactory, _loggerFactory, _serverApplicationHost, _httpContextAccessor, _memoryCache, _delayer, new Dictionary<string, string> { { "simkl-api-key", simklClientId } }, userConfig);

                    if (await simklApiCalls.GetLastActivity()) {
                        return new OkObjectResult(new MalApiCalls.User {
                            Name = null
                        });
                    } else {
                        return StatusCode(500, "Not authenticated");
                    }
            }

            throw new Exception("Provider not supported.");
        }

        [Authorize(Policy = Policies.RequiresElevation)]
        [HttpGet]
        [Route("parameters")]
        public IActionResult FrontendParameters(ParameterInclude[]? includes, bool onlyConfiguredProviders = false)
        {
            return Ok(GetFrontendParameters(includes, onlyConfiguredProviders, false));
        }

        [Authorize(Policy = Policies.RequiresElevation)]
        [HttpPost]
        [Route("sync")]
        public Task Sync(ApiName provider, string userId, SyncHelper.Status status, SyncAction syncAction) {
            switch (syncAction) {
                case SyncAction.UpdateProvider:
                    SyncProviderFromLocal syncProviderFromLocal = new SyncProviderFromLocal(_userManager, _libraryManager, _loggerFactory, _httpClientFactory, _applicationPaths, _fileSystem, _memoryCache, _delayer, userId);
                    return syncProviderFromLocal.SyncFromLocal();
                case SyncAction.UpdateJellyfin:
                    Sync sync = new Sync(_httpClientFactory, _loggerFactory, _serverApplicationHost, _httpContextAccessor, _userManager, _libraryManager, _applicationPaths, _userDataManager, _memoryCache, _delayer, provider, status);
                    return sync.SyncFromProvider(userId);
            }

            return Task.CompletedTask;
        }

        // The following endpoints are user-specific versions of the above endpoints (do not require elevated access)

        #region User Endpoints
        
        [Authorize]
        [HttpGet]
        [Route("buildAuthorizeRequestUrlUser")]
        public ActionResult BuildAuthorizeRequestUrlUser(ApiName provider, Guid user) {
            if (!UserPagesEnabled()) return NotFound();
            var jellyfinUser = _userManager.GetUser(User, user);
            if (jellyfinUser == null) return Forbid();

            var providerApiAuth = Plugin.Instance?.PluginConfiguration.ProviderApiAuth?
                .FirstOrDefault(providerApiAuth => providerApiAuth.Name == provider);

            if (providerApiAuth == null) {
                _logger.LogError($"User {jellyfinUser.Id} failed to build authorize request URL: Provider not configured.");
                return Forbid();
            }

            var clientId = providerApiAuth.ClientId;
            var clientSecret = providerApiAuth.ClientSecret;

            string url = !string.IsNullOrEmpty(Plugin.Instance?.PluginConfiguration.callbackUrl) ? Plugin.Instance.PluginConfiguration.callbackUrl : "local";

            return Ok(BuildAuthorizeRequestUrl(provider, clientId, clientSecret, url, jellyfinUser.Id));
        }

        [Authorize]
        [HttpGet]
        [Route("passwordGrantUser")]
        public async Task<IActionResult> PasswordGrantAuthenticationUser(ApiName provider, [FromQuery] Guid user, string username, string password) {
            if (!UserPagesEnabled()) return NotFound();
            var jellyfinUser = _userManager.GetUser(User, user);
            if (jellyfinUser == null) return Forbid();
            
            return await PasswordGrantAuthentication(provider, jellyfinUser.Id.ToString(), username, password);
        }

        [Authorize]
        [HttpGet]
        [Route("userUser")]
        public async Task<ActionResult> GetUserUser(ApiName apiName, Guid user) {
            if (!UserPagesEnabled()) return NotFound();
            var jellyfinUser = _userManager.GetUser(User, user);
            if (jellyfinUser == null) return Forbid();

            return await GetUser(apiName, jellyfinUser.Id.ToString());
        }

        [Authorize]
        [HttpGet]
        [Route("configurationUser")]
        public ActionResult GetConfigurationUser(Guid user) {
            if (!UserPagesEnabled()) return NotFound();
            var jellyfinUser = _userManager.GetUser(User, user);
            if (jellyfinUser == null) return Forbid();

            var userConfig = Plugin.Instance?.PluginConfiguration.UserConfig.FirstOrDefault(userConfig => userConfig.UserId == jellyfinUser.Id);
            if (userConfig == null) {
                _logger.LogTrace("User not found in config, first time?");
                return Ok(new {});
            }

            return Ok(userConfig);
        }

        [Authorize]
        [HttpPut]
        [Route("configurationUser")]
        public ActionResult UpdateConfigurationUser(
            [FromQuery] Guid user,
            [FromBody] UserEditableConfig dto)
        {
            if (!UserPagesEnabled()) return NotFound();
            var jellyfinUser = _userManager.GetUser(User, user);
            if (jellyfinUser == null)
                return Forbid();

            var plugin = Plugin.Instance;
            if (plugin?.PluginConfiguration == null)
                return StatusCode(500, "Plugin configuration not loaded");

            var config = plugin.PluginConfiguration;
            config.UserConfig ??= Array.Empty<UserConfig>();

            var userConfig = config.UserConfig
                .FirstOrDefault(x => x.UserId == jellyfinUser.Id);

            HashSet<Guid> libraryIds = [];
            foreach (var library in dto.LibraryToCheck) {
                if (Guid.TryParse(library, out var libraryId)) {
                    libraryIds.Add(libraryId);
                }
            }

            if (!_libraryManager.UserHasAccessToLibraries(libraryIds, jellyfinUser)) {
                _logger.LogError($"User {jellyfinUser.Id} does not have access to requested libraries ({String.Join(", ", dto.LibraryToCheck)})");
                return Forbid();
            }

            if (userConfig == null)
            {
                userConfig = new UserConfig {
                    UserId = jellyfinUser.Id,
                    PlanToWatchOnly = dto.PlanToWatchOnly,
                    RewatchCompleted = dto.RewatchCompleted,
                    LibraryToCheck = dto.LibraryToCheck
                };

                config.UserConfig = config.UserConfig
                    .Append(userConfig)
                    .ToArray();
            }
            else
            {
                userConfig.PlanToWatchOnly = dto.PlanToWatchOnly;
                userConfig.RewatchCompleted = dto.RewatchCompleted;
                userConfig.LibraryToCheck = dto.LibraryToCheck;
            }

            plugin.SaveConfiguration();

            return Ok(userConfig);
        }

        [Authorize]
        [HttpGet]
        [Route("parametersUser")]
        public object GetFrontendParametersUser(ParameterInclude[]? includes) {
            if (!UserPagesEnabled()) return NotFound();
            return GetFrontendParameters(includes, true, true);
        }

        [Authorize]
        [HttpGet("{viewName}")]
        public ActionResult GetView([FromRoute] string viewName)
        {
            if (Plugin.Instance == null)
            {
                return BadRequest("No plugin instance found");
            }

            if (!UserPagesEnabled()) return NotFound();

            IEnumerable<PluginPageInfo> pages = Plugin.Instance.GetViews();

            if (pages == null)
            {
                return NotFound("Pages is null or empty");
            }

            PluginPageInfo? view = pages.FirstOrDefault(pageInfo => pageInfo?.Name == viewName, null);

            if (view == null)
            {
                return NotFound("No matching view found");
            }

            Stream? stream = Plugin.Instance.GetType().Assembly.GetManifestResourceStream(view.EmbeddedResourcePath);

            if (stream == null)
            {
                _logger.LogError("Failed to get resource {Resource}", view.EmbeddedResourcePath);
                return NotFound();
            }

            return File(stream, MimeTypes.GetMimeType(view.EmbeddedResourcePath));
        }
        
        #endregion


        // The following endpoint are allowed to be accessed anonymously (do not require any authentication)

        [AllowAnonymous]
        [HttpGet]
        [Route("authCallback")]
        public IActionResult AuthCallback(string code, string? state) {
            if (state == null) return BadRequest("State is empty");
            StoredState? storedState = MemoryCacheHelper.ConsumeState(_memoryCache, state);
            if (storedState == null) return BadRequest("User not found or link already used/expired, try again");
            new ApiAuthentication(storedState.ApiName, _httpClientFactory, _serverApplicationHost, _httpContextAccessor, _loggerFactory, _memoryCache).GetToken(storedState.UserId, code);
            if (!string.IsNullOrEmpty(Plugin.Instance?.PluginConfiguration.callbackRedirectUrl)) {
                string replacedCallbackRedirectUrl = Plugin.Instance.PluginConfiguration.callbackRedirectUrl.Replace("{{LocalIpAddress}}", Request.HttpContext.Connection.LocalIpAddress != null ? Request.HttpContext.Connection.LocalIpAddress.ToString() : "localhost")
                    .Replace("{{LocalPort}}", _serverApplicationHost.ListenWithHttps ? _serverApplicationHost.HttpsPort.ToString() : _serverApplicationHost.HttpPort.ToString());

                if (Uri.TryCreate(replacedCallbackRedirectUrl, UriKind.Absolute, out _)) {
                    return Redirect(replacedCallbackRedirectUrl);
                } else {
                    _logger.LogWarning($"Invalid redirect URL ({replacedCallbackRedirectUrl}), skipping redirect.");
                }
            }

            return new ObjectResult("Success! Received access token! You can test your authentication in Ani-Sync Configuration!") { StatusCode = 200 };
        }

        [AllowAnonymous]
        [HttpGet]
        [Route("apiUrlTest")]
        public string ApiUrlTest() {
            return "This is the correct URL.";
        }

        private Parameters GetFrontendParameters(ParameterInclude[]? includes, bool onlyConfiguredProviders, bool onlyLibrariesUserHasAccessTo) {
            Parameters toReturn = new Parameters();

            if (includes == null || includes.Contains(ParameterInclude.ProviderList))
            {
                var configured = Plugin.Instance?
                    .PluginConfiguration.ProviderApiAuth?
                    .Where(x => !string.IsNullOrWhiteSpace(x.ClientId))
                    .Select(x => x.Name)
                    .ToHashSet();

                toReturn.providerList = new List<ExpandoObject>();

                foreach (ApiName apiName in Enum.GetValues<ApiName>())
                {
                    if (onlyConfiguredProviders && (configured == null || !configured.Contains(apiName)))
                        continue;

                    dynamic provider = new ExpandoObject();
                    provider.Name = apiName.GetType()
                        .GetMember(apiName.ToString())
                        .First()
                        .GetCustomAttribute<DisplayAttribute>()
                        ?.GetName();
                    provider.Key = apiName;

                    toReturn.providerList.Add(provider);
                }
            }

            if (includes == null || includes.Contains(ParameterInclude.LocalIpAddress))
                toReturn.localIpAddress = Request.HttpContext.Connection.LocalIpAddress?.ToString() ?? "localhost";

            if (includes == null || includes.Contains(ParameterInclude.LocalPort))
                toReturn.localPort = _serverApplicationHost.ListenWithHttps
                    ? _serverApplicationHost.HttpsPort
                    : _serverApplicationHost.HttpPort;

            if (includes == null || includes.Contains(ParameterInclude.Https))
                toReturn.https = _serverApplicationHost.ListenWithHttps;

            if (includes == null || includes.Contains(ParameterInclude.Libraries))
            {
                Dictionary<Guid, string> libraries = new Dictionary<Guid, string>();
                if (onlyLibrariesUserHasAccessTo) {
                    User? jellyfinUser = _userManager.GetUser(User, null);
                    if (jellyfinUser != null) {
                        libraries = _libraryManager.GetLibrariesUserHasAccessTo(jellyfinUser);
                    }
                } else {
                    libraries = _libraryManager.GetVirtualFolders().ToDictionary(virtualFolderInfo => Guid.Parse(virtualFolderInfo.ItemId), virtualFolderInfo => virtualFolderInfo.Name);
                }
                toReturn.libraries = new List<ExpandoObject>();
                foreach (var library in libraries)
                {
                    dynamic lib = new ExpandoObject();
                    lib.Name = library.Value;
                    lib.Id = library.Key;
                    toReturn.libraries.Add(lib);
                }
            }

            return toReturn;
        }
    }
}
