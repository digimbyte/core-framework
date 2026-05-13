using System;

namespace Core.Registry
{
    /// <summary>
    /// Single source of truth for GUI texture registry UIDs used by editor populate and list/grid helpers.
    /// <see cref="Registry"/> lookups are case-insensitive; canonical strings use lowercase <c>type_</c> / <c>domain_</c> prefixes.
    /// </summary>
    /// <remarks>
    /// YAML leaf ids may already include <c>Type_</c> / <c>Domain_</c>; after <see cref="NormalizeSegment"/> that becomes
    /// <c>type_</c> / <c>domain_</c>. We strip one leading normalized prefix before prepending, so we never emit
    /// <c>type_type_...</c> or <c>domain_domain_...</c>.
    /// </remarks>
    public static class GuiImageRegistryUid
    {
        public static string NormalizeSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string normalized = value.Trim().ToLowerInvariant().Replace("\\", "/");
            normalized = normalized.Replace(" ", string.Empty);
            while (normalized.Contains("//", StringComparison.Ordinal))
            {
                normalized = normalized.Replace("//", "/", StringComparison.Ordinal);
            }

            return normalized.Trim('/');
        }

        /// <summary>Grid / definition-leaf GUI texture UID.</summary>
        public static string BuildTypeTextureUid(string rawLabel)
        {
            string seg = NormalizeSegment(rawLabel);
            if (string.IsNullOrEmpty(seg))
            {
                return string.Empty;
            }

            const string normalizedPrefix = "type_";
            if (seg.StartsWith(normalizedPrefix, StringComparison.Ordinal))
            {
                seg = seg.Substring(normalizedPrefix.Length);
            }

            return "type_" + seg;
        }

        /// <summary>List GUI texture UID.</summary>
        public static string BuildDomainTextureUid(string rawLabel)
        {
            string seg = NormalizeSegment(rawLabel);
            if (string.IsNullOrEmpty(seg))
            {
                return string.Empty;
            }

            const string normalizedPrefix = "domain_";
            if (seg.StartsWith(normalizedPrefix, StringComparison.Ordinal))
            {
                seg = seg.Substring(normalizedPrefix.Length);
            }

            return "domain_" + seg;
        }
    }
}
