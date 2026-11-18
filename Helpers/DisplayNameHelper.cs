using System.Text.RegularExpressions;

namespace PerfectRide.Helpers;

public static class DisplayNameHelper
{
    public static string ToDisplayName<T>(T value) where T : Enum
    {
        return Regex.Replace(
            value.ToString(),
            "([a-z0-9])([A-Z])",
            "$1 $2"
        );
    }
}