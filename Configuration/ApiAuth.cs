namespace jellyfin_ani_sync.Configuration;

public enum ApiName {
    Mal
}

public class ApiAuth {
    public ApiName Name { get; set; }
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
}