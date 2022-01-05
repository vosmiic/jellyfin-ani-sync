using System;
using System.Linq;
using System.Net.Http;
using jellyfin_ani_sync.Configuration;
using MediaBrowser.Common.Net;
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
    }
}