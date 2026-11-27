using System.Globalization;
using System.Text;

namespace Gearnix.Api.Modules.Catalog;

/// <summary>
/// Vietnamese text for search and URLs: one normalization shared by <c>NameNormalized</c> and slugs,
/// so "dien thoai" in a search box and <c>dien-thoai</c> in a link come from the same rule (Workflow.md §6.1, §10).
/// </summary>
public static class VietnameseText
{
    /// <summary>Used when a name has no letters or digits at all, so a slug is never empty.</summary>
    public const string FallbackSlug = "item";

    /// <summary>Lowercase, diacritics removed, <c>đ</c> → <c>d</c>, runs of whitespace collapsed to one space.</summary>
    public static string Normalize(string text)
    {
        // "đ" is a separate letter, not "d" plus a mark, so Unicode decomposition does not split it.
        var lower = text.ToLowerInvariant().Replace('đ', 'd');

        // FormD splits "ệ" into "e" + combining marks; dropping the marks leaves plain ASCII letters.
        var builder = new StringBuilder(lower.Length);
        var pendingSpace = false;
        foreach (var character in lower.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsWhiteSpace(character))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(character);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// The normalized text with every run of characters other than <c>a-z</c> and <c>0-9</c> replaced by one hyphen,
    /// e.g. "Điện thoại Nova X 5G" → <c>dien-thoai-nova-x-5g</c>.
    /// </summary>
    public static string ToSlug(string text)
    {
        var builder = new StringBuilder(text.Length);
        var pendingHyphen = false;
        foreach (var character in Normalize(text))
        {
            if (character is (>= 'a' and <= 'z') or (>= '0' and <= '9'))
            {
                if (pendingHyphen)
                {
                    builder.Append('-');
                    pendingHyphen = false;
                }

                builder.Append(character);
            }
            else
            {
                // No hyphen at the start; a trailing one is never written because nothing follows it.
                pendingHyphen = builder.Length > 0;
            }
        }

        return builder.Length > 0 ? builder.ToString() : FallbackSlug;
    }
}
