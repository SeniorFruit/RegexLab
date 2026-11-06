using System;
using System.Collections.Generic;

namespace RegexLab.Core;

public sealed class RegexPattern
{
    private readonly RegexNode _root;

    private RegexPattern(string source, bool ignoreCase, RegexNode root)
    {
        Source = source;
        IgnoreCase = ignoreCase;
        _root = root;
    }

    public string Source { get; }

    public bool IgnoreCase { get; }

    public static RegexPattern Compile(string source, bool ignoreCase = false)
    {
        ArgumentNullException.ThrowIfNull(source);

        var root = RegexParser.Parse(source);
        return new RegexPattern(source, ignoreCase, root);
    }

    public static bool TryCompile(
        string source,
        bool ignoreCase,
        out RegexPattern? pattern,
        out RegexSyntaxException? error)
    {
        try
        {
            pattern = Compile(source, ignoreCase);
            error = null;
            return true;
        }
        catch (RegexSyntaxException exception)
        {
            pattern = null;
            error = exception;
            return false;
        }
    }

    // Ищем непересекающиеся совпадения слева направо, берём самый длинный вариант.
    public IReadOnlyList<RegexMatch> FindMatches(string text, int maxMatches = 5_000)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (maxMatches <= 0)
        {
            return [];
        }

        var matcher = new RegexMatcher(text, IgnoreCase);
        var matches = new List<RegexMatch>();
        var searchFrom = 0;

        while (searchFrom <= text.Length && matches.Count < maxMatches)
        {
            var found = false;

            for (var start = searchFrom; start <= text.Length; start++)
            {
                var ends = matcher.Match(_root, start);

                if (ends.Count == 0)
                {
                    continue;
                }

                var end = 0;

                foreach (var candidate in ends)
                {
                    if (candidate > end)
                    {
                        end = candidate;
                    }
                }

                if (end > start)
                {
                    matches.Add(new RegexMatch(start, end - start));
                    searchFrom = end;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                break;
            }
        }

        return matches;
    }
}
