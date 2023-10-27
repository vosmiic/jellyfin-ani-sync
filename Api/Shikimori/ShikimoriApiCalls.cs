#nullable enable
using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using jellyfin_ani_sync.Configuration;
using jellyfin_ani_sync.Helpers;
using jellyfin_ani_sync.Models;
using MediaBrowser.Controller;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace jellyfin_ani_sync.Api.Shikimori; 

public class ShikimoriApiCalls {
    private readonly ILogger<ShikimoriApiCalls> _logger;
    private readonly AuthApiCall _authApiCall;
    private readonly string _refreshTokenUrl = "https://shikimori.one/oauth/token";
    private readonly string _apiBaseUrl = "https://shikimori.one/api";
    private readonly UserConfig? _userConfig;
    
    public ShikimoriApiCalls(IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory, IServerApplicationHost serverApplicationHost, IHttpContextAccessor httpContextAccessor, UserConfig? userConfig = null) {
        _userConfig = userConfig;
        _logger = loggerFactory.CreateLogger<ShikimoriApiCalls>();
        _authApiCall = new AuthApiCall(ApiName.Shikimori, httpClientFactory, serverApplicationHost, httpContextAccessor, loggerFactory, userConfig: userConfig);
    }
    
    public class User {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("nickname")] public string Name { get; set; }
    }
    
    /// <summary>
    /// Get a users information.
    /// </summary>
    public async Task<User?> GetUserInformation() {
        UrlBuilder url = new UrlBuilder {
            Base = $"{_apiBaseUrl}/users/whoami"
        };
        var apiCall = await _authApiCall.AuthenticatedApiCall(ApiName.Shikimori, AuthApiCall.CallType.GET, url.Build());
        if (apiCall != null) {
            StreamReader streamReader = new StreamReader(await apiCall.Content.ReadAsStreamAsync());
            string streamText = await streamReader.ReadToEndAsync();

            try {
                User? user = JsonSerializer.Deserialize<User>(streamText);

                if (user != null) {
                    _userConfig?.KeyPairs.Add(new KeyPairs {
                        Key = "ShikimoriUserId",
                        Value = user.Id.ToString()
                    });

                    Plugin.Instance?.SaveConfiguration();
                }

                return user;
            } catch (Exception) {
                return null;
            }
        } else {
            return null;
        }
    }
}