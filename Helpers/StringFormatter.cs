using System.Text;

namespace jellyfin_ani_sync.Helpers {
    public class StringFormatter {
        public static string RemoveSpecialCharacters(string str) {
            StringBuilder stringBuilder = new StringBuilder();
            foreach (char c in str) {
                if (c is >= '0' and <= '9' || c is >= 'A' and <= 'Z' || c is >= 'a' and <= 'z') {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString();
        }
    }
}