using System;
using System.Net.Http;
using MediaBrowser.Common.Net;
using Microsoft.AspNetCore.Mvc;

namespace jellyfin_ani_sync.Api {
    [ApiController]
    [Route("[controller]")]
    public class AniSyncController : ControllerBase {
        private readonly IHttpClientFactory _httpClientFactory;

        public AniSyncController(IHttpClientFactory httpClientFactory) {
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet]
        [Route("buildAuthorizeRequestUrl")]
        public string BuildAuthorizeRequestUrl() {
            return new ControllerFunctions(_httpClientFactory).BuildAuthorizeRequestUrl();
        }

        [HttpGet]
        [Route("authCallback")]
        public void MalCallback(string code) {
            new ControllerFunctions(_httpClientFactory).GetMalToken(code);
        }
    }
}