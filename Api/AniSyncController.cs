using System.Collections.Generic;
using System.Net.Http;
using jellyfin_ani_sync.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace jellyfin_ani_sync.Api {
    [ApiController]
    [Route("[controller]")]
    public class AniSyncController : ControllerBase {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILoggerFactory _loggerFactory;

        public AniSyncController(IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory) {
            _httpClientFactory = httpClientFactory;
            _loggerFactory = loggerFactory;
        }

        [HttpGet]
        [Route("buildAuthorizeRequestUrl")]
        public string BuildAuthorizeRequestUrl() {
            return new MalApiAuthentication(_httpClientFactory).BuildAuthorizeRequestUrl();
        }

        [HttpGet]
        [Route("authCallback")]
        public void MalCallback(string code) {
            new MalApiAuthentication(_httpClientFactory).GetMalToken(code);
        }

        [HttpGet]
        [Route("user")]
        public MalApiCalls.User GetUser() {
            return new MalApiCalls(_httpClientFactory, _loggerFactory).GetUserInformation();
        }
        
        /// <summary>
        /// Search the MAL database for anime.
        /// </summary>
        /// <param name="query">Search by title.</param>
        /// <param name="fields">The fields you would like returned.</param>
        /// <returns>List of anime.</returns>
        [HttpGet]
        [Route("searchAnime")]
        public List<Anime> SearchAnime(string query, string fields) {
            return new MalApiCalls(_httpClientFactory, _loggerFactory).SearchAnime("railgun", fields.Split(","));
        }
    }
}