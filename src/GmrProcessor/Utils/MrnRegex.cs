using System.Text.RegularExpressions;

namespace GmrProcessor.Utils;

public static partial class MrnRegex
{
    [GeneratedRegex(@"^\d{2}[A-Z]{2}[A-Za-z0-9]{14}$", RegexOptions.IgnoreCase, "en-GB")]
    public static partial Regex Value();
}
