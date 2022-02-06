using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using jellyfin_ani_sync.Models;
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

        public AniSyncController(IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory, IMemoryCache memoryCache) {
            _httpClientFactory = httpClientFactory;
            _loggerFactory = loggerFactory;
            _memoryCache = memoryCache;
        }

        [HttpGet]
        [Route("buildAuthorizeRequestUrl")]
        public string BuildAuthorizeRequestUrl(string userId) {
            return new MalApiAuthentication(_httpClientFactory, _memoryCache).BuildAuthorizeRequestUrl(userId);
        }

        [HttpGet]
        [Route("authCallback")]
        public void MalCallback(string code) {
            new MalApiAuthentication(_httpClientFactory, _memoryCache).GetMalToken(Guid.Parse(_memoryCache.Get<string>("userId")), code);
            _memoryCache.Remove("userId");
        }

        [HttpGet]
        [Route("user")]
        public async Task<MalApiCalls.User> GetUser() {
            return await new MalApiCalls(_httpClientFactory, _loggerFactory).GetUserInformation();
        }
    }
}