#if DEBUG
using System;

namespace DeveloperTools.Common;

/// <summary>
/// Lightweight fuzzy subsequence matcher for editor search boxes. Returns whether
/// the query is a (case-insensitive) subsequence of the candidate and a relevance
/// score where higher means a closer fit — consecutive runs, word-boundary hits,
/// early matches, and exact/prefix matches all score higher.
/// </summary>
public static class FuzzyMatch
{
    /// <summary>
    /// Attempts to match <paramref name="query"/> against <paramref name="text"/>.
    /// Returns true with a relevance <paramref name="score"/> when every query char
    /// appears in order. An empty query matches everything with score 0.
    /// </summary>
    public static bool TryMatch(string query, string text, out int score)
    {
        score = 0;
        if (string.IsNullOrEmpty(query)) return true;
        if (string.IsNullOrEmpty(text)) return false;

        int qi = 0;
        int run = 0;
        int prevMatchIdx = -2;
        for (int ti = 0; ti < text.Length && qi < query.Length; ti++)
        {
            if (char.ToLowerInvariant(text[ti]) != char.ToLowerInvariant(query[qi]))
                continue;

            score += 1; // base point per matched char
            if (ti == prevMatchIdx + 1) { run++; score += run * 3; } // consecutive-run bonus
            else run = 0;
            if (ti == 0 || text[ti - 1] is '_' or ' ' or '-') score += 5; // word-boundary bonus
            if (ti < 4) score += 4 - ti; // earlier matches rank slightly higher
            prevMatchIdx = ti;
            qi++;
        }

        if (qi < query.Length) { score = 0; return false; }

        if (string.Equals(query, text, StringComparison.OrdinalIgnoreCase)) score += 50;
        else if (text.StartsWith(query, StringComparison.OrdinalIgnoreCase)) score += 20;
        return true;
    }
}
#endif
