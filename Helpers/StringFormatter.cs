using System.Text;

namespace jellyfin_ani_sync.Helpers; 

public class StringFormatter {
    public static string RemoveSpecialCharacters(string str) {
        StringBuilder stringBuilder = new StringBuilder();
        foreach (char c in str) {
            if (c is not ((< '0' or > '9') and (< 'A' or > 'Z') and (< 'a' or > 'z') and ' ')) {
                stringBuilder.Append(c);
            }
        }

        return stringBuilder.ToString();
    }
}