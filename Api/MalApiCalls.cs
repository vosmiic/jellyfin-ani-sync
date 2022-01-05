using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using jellyfin_ani_sync.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Authentication;
using Microsoft.Extensions.Logging;

namespace jellyfin_ani_sync.Api;

public class MalApiCalls {
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<MalApiCalls> _logger;
    private readonly string _refreshTokenUrl = "https://myanimelist.net/v1/oauth2/token";

    public MalApiCalls(IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory) {
        _httpClientFactory = httpClientFactory;
        _logger = loggerFactory.CreateLogger<MalApiCalls>();
    }

    /// <summary>
    /// Get a users information.
    /// </summary>
    public User GetUserInformation() {
        StreamReader streamReader = new StreamReader(MalApiCall(CallType.GET, "https://api.myanimelist.net/v2/users/@me").Content.ReadAsStream());
        string streamText = streamReader.ReadToEnd();

        return JsonSerializer.Deserialize<User>(streamText);
    }

    public class User {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; }
        [JsonPropertyName("location")] public string Location { get; set; }
        [JsonPropertyName("joined_at")] public DateTime JoinedAt { get; set; }
        [JsonPropertyName("picture")] public string Picture { get; set; }
    }

    public enum CallType {
        GET,
        POST,
        PATCH,
        DELETE
    }

    private HttpResponseMessage MalApiCall(CallType callType, string url, FormUrlEncodedContent formUrlEncodedContent = null) {
        int attempts = 0;
        var auth = Plugin.Instance.PluginConfiguration.ApiAuth.FirstOrDefault(item => item.Name == ApiName.Mal);
        while (attempts < 2) {
            var client = _httpClientFactory.CreateClient(NamedClient.Default);

            if (auth == null) {
                _logger.LogError("Could not find authentication details, please authenticate the plugin first");
                throw new NullReferenceException();
            }

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
            HttpResponseMessage responseMessage;
            switch (callType) {
                case CallType.GET:
                    responseMessage = client.GetAsync(url).Result;
                    break;
                case CallType.POST:
                    responseMessage = client.PostAsync(url, formUrlEncodedContent).Result;
                    break;
                case CallType.PATCH:
                    responseMessage = client.PatchAsync(url, formUrlEncodedContent).Result;
                    break;
                case CallType.DELETE:
                    responseMessage = client.DeleteAsync(url).Result;
                    break;
                default:
                    responseMessage = client.GetAsync(url).Result;
                    break;
            }

            if (responseMessage.IsSuccessStatusCode) {
                return responseMessage;
            } else {
                if (responseMessage.StatusCode == HttpStatusCode.Unauthorized) {
                    // token has probably expired; try refreshing it
                    var newAuth = new MalApiAuthentication(_httpClientFactory).GetMalToken(refreshToken: auth.RefreshToken);
                    // and then make the call again, using the new auth details
                    auth = newAuth;
                    attempts++;
                } else {
                    _logger.LogError($"Unable to complete MAL API call, reason: {responseMessage.StatusCode}; {responseMessage.ReasonPhrase}");
                    throw new Exception();
                }
            }
        }

        _logger.LogError("Unable to authenticate the MAL API call, re-authenticate the plugin");
        throw new AuthenticationException();
    }
}