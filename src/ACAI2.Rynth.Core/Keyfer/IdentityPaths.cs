#nullable disable
// Ported from Samm-AI ff44cc3; see origin/ACAI2-SOURCE.md.
using System;
using System.Text.RegularExpressions;

namespace ACAI2.Protocol
{
    public static class IdentityPaths
    {
        // Encode rather than replace invalid characters, so distinct names cannot collapse.
        public static string SafeSegment(string identity)
        {
            if (string.IsNullOrWhiteSpace(identity)) throw new ArgumentException("Known identity is required.");
            string result = Uri.EscapeDataString(identity.Trim());
            if (result == "." || result == "..") result = result.Replace(".", "%2E");
            if (result.EndsWith(".", StringComparison.Ordinal)) result = result.Substring(0, result.Length - 1) + "%2E";
            if (Regex.IsMatch(result, @"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)", RegexOptions.IgnoreCase)) result = "%" + ((int)result[0]).ToString("X2") + result.Substring(1);
            return result;
        }
    }
}
