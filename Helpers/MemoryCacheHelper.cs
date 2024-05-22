using jellyfin_ani_sync.Configuration;

namespace jellyfin_ani_sync.Helpers;

public class MemoryCacheHelper
{
    public static string GetLastCallDateTimeKey(ApiName provider) => $"{provider}LastCallDateTime";
}