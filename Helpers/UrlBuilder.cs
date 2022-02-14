using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace jellyfin_ani_sync.Helpers {
    public class UrlBuilder {
        /// <summary>
        /// Gets or sets the base URL.
        /// </summary>
        public string Base { get; set; }

        /// <summary>
        /// Gets or sets the query parameters.
        /// </summary>
        public List<KeyValuePair<string, string>> Parameters { get; set; }

        public UrlBuilder() {
            Parameters = new List<KeyValuePair<string, string>>();
        }

        /// <summary>
        /// Returns the URL string.
        /// </summary>
        /// <returns></returns>
        public string Build() {
            StringBuilder url = new StringBuilder(Base);
            if (Parameters.Count > 0) {
                KeyValuePair<string, string> last = Parameters.Last();
                url.Append('?');
                foreach (var parameter in Parameters) {
                    url.Append($"{parameter.Key}={Uri.EscapeDataString(parameter.Value)}");
                    if (!parameter.Equals(last)) {
                        url.Append('&');
                    }
                }
            }

            return url.ToString();
        }
    }
}