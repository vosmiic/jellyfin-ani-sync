using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace jellyfin_ani_sync.Helpers; 

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
        KeyValuePair<string, string> last = Parameters.Last();
        if (Parameters.Count > 0) {
            url.Append('?');
            foreach (var parameter in Parameters) {
                url.Append($"{parameter.Key}={parameter.Value}");
                if (!parameter.Equals(last)) {
                    url.Append('&');
                }
            }
        }

        return url.ToString();
    }
}