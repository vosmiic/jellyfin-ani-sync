using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using Moq;

namespace jellyfin_ani_sync_unit_tests;

public class Helpers {
    private class DelegatingHandlerStub : DelegatingHandler {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handlerFunc;

        public DelegatingHandlerStub(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handlerFunc) {
            _handlerFunc = handlerFunc;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            return _handlerFunc(request, cancellationToken);
        }
    }

    public static void MockHttpCalls(List<HttpCall> calls, ref IHttpClientFactory _httpClientFactory) {
        var mockFactory = new Mock<IHttpClientFactory>();
        var clientHandlerStub = new DelegatingHandlerStub((request, _) => {
            request.SetConfiguration(new HttpConfiguration());
            foreach (HttpCall httpCall in calls.OrderByDescending(call => call.RequestMethod != null).ThenByDescending(call => call.RequestUrlMatch != null)) {
                if (httpCall.RequestMethod != null && request.Method != httpCall.RequestMethod) {
                    continue;
                }

                if (httpCall.RequestUrlMatch != null && request.RequestUri != null && !httpCall.RequestUrlMatch(request.RequestUri.GetLeftPart(UriPartial.Path).ToString())) {
                    continue;
                }

                return Task.FromResult(new HttpResponseMessage(httpCall.ResponseCode) { RequestMessage = request, Content = new StringContent(httpCall.ResponseContent) });
            }

            return Task.FromResult(new HttpResponseMessage());
        });
            var client = new HttpClient(clientHandlerStub);
            mockFactory.Setup(_ => _.CreateClient(It.IsAny<string>())).Returns(client);
            _httpClientFactory = mockFactory.Object;
        }

    public class HttpCall {
        /// <summary>
        /// The request method to match to. For example, this should be GET if you are expecting a GET request to be made to the external service. Null to capture all request codes.
        /// </summary>
        public HttpMethod? RequestMethod { get; set; }

        /// <summary>
        /// The request URL to match to. For example, s => s.EndsWith("/update") to match with update calls. Null to capture all URLs.
        /// </summary>
        public Func<string, bool>? RequestUrlMatch { get; set; }

        /// <summary>
        /// The response code to return from the external service.
        /// </summary>
        public HttpStatusCode ResponseCode { get; set; }

        /// <summary>
        /// The response content of the call being made.
        /// </summary>
        public string ResponseContent { get; set; }
    }
}