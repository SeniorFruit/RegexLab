using System;
using System.Collections.Generic;

namespace RegexLab.Core;

internal enum CharacterCategory
{
    Digit,
    Word,
    Space,
    NotDigit,
    NotWord,
    NotSpace
}

// Набор символов: диапазоны и классы вроде \d.
internal sealed class CharacterSet
{
    private readonly List<(char Min, char Max)> _ranges = [];
    private readonly List<CharacterCategory> _categories = [];

    public CharacterSet(bool isNegated)
    {
        IsNegated = isNegated;
    }

    public bool IsNegated { get; }

    public void AddRange(char min, char max)
    {
        _ranges.Add((min, max));
    }

    public void AddCategory(CharacterCategory category)
    {
        _categories.Add(category);
    }

    public bool Matches(char value, bool ignoreCase)
    {
        var matched = MatchesCore(value);

        if (!matched && ignoreCase)
        {
            matched = MatchesCore(char.ToLowerInvariant(value))
                      || MatchesCore(char.ToUpperInvariant(value));
        }

        return IsNegated ? !matched : matched;
    }

    private bool MatchesCore(char value)
    {
        foreach (var (min, max) in _ranges)
        {
            if (value >= min && value <= max)
            {
                return true;
            }
        }

        foreach (var category in _categories)
        {
            if (MatchesCategory(category, value))
            {
                return true;
            }
        }

        return false;
    }

    public static bool MatchesCategory(CharacterCategory category, char value) => category switch
    {
        CharacterCategory.Digit => char.IsDigit(value),
        CharacterCategory.Word => IsWordChar(value),
        CharacterCategory.Space => char.IsWhiteSpace(value),
        CharacterCategory.NotDigit => !char.IsDigit(value),
        CharacterCategory.NotWord => !IsWordChar(value),
        CharacterCategory.NotSpace => !char.IsWhiteSpace(value),
        _ => false
    };

    public static bool IsWordChar(char value) => char.IsLetterOrDigit(value) || value == '_';
}
