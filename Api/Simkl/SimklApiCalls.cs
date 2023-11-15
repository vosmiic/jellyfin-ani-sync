#nullable enable
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using jellyfin_ani_sync.Configuration;
using jellyfin_ani_sync.Helpers;
using jellyfin_ani_sync.Models;
using MediaBrowser.Controller;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace jellyfin_ani_sync.Api.Simkl; 

public class SimklApiCalls {
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServerApplicationHost _serverApplicationHost;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly Dictionary<string, string> _requestHeaders;
    private readonly UserConfig _userConfig;
    private readonly ILogger<SimklApiCalls> _logger;
    private readonly AuthApiCall _authApiCall;
    public static readonly string ApiBaseUrl = "https://api.simkl.com";

    public SimklApiCalls(IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory, IServerApplicationHost serverApplicationHost, IHttpContextAccessor httpContextAccessor, Dictionary<string, string>? requestHeaders, UserConfig? userConfig = null) {
        _httpClientFactory = httpClientFactory;
        _serverApplicationHost = serverApplicationHost;
        _httpContextAccessor = httpContextAccessor;
        _requestHeaders = requestHeaders;
        _userConfig = userConfig;
        _logger = loggerFactory.CreateLogger<SimklApiCalls>();
        _authApiCall = new AuthApiCall(ApiName.Simkl, httpClientFactory, serverApplicationHost, httpContextAccessor, loggerFactory, userConfig: userConfig);
    }

    /// <summary>
    /// Get the users last activity. While this does not return the activity because this is only used to validate the token, it can later be adjusted to return the actual data if needed.
    /// </summary>
    /// <returns></returns>
    public async Task<bool> GetLastActivity() {
        UrlBuilder url = new UrlBuilder {
            Base = $"{ApiBaseUrl}/sync/activities"
        };

        HttpResponseMessage? apiCall = await _authApiCall.AuthenticatedApiCall(ApiName.Simkl, AuthApiCall.CallType.GET, url.Build(), requestHeaders: _requestHeaders);
        return apiCall is { IsSuccessStatusCode: true };
    }
}