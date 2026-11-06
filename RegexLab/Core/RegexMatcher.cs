using System;
using System.Collections.Generic;

namespace RegexLab.Core;

// Для каждой позиции храним все возможные конечные позиции. Так вложенные
// квантификаторы не уходят в глубокий бэктрекинг.
internal sealed class RegexMatcher
{
    private readonly string _text;
    private readonly bool _ignoreCase;
    private readonly Dictionary<(RegexNode Node, int Position), HashSet<int>> _cache = new();

    public RegexMatcher(string text, bool ignoreCase)
    {
        _text = text;
        _ignoreCase = ignoreCase;
    }

    public HashSet<int> Match(RegexNode node, int position)
    {
        var key = (node, position);

        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var result = Compute(node, position);
        _cache[key] = result;
        return result;
    }

    private HashSet<int> Compute(RegexNode node, int position)
    {
        switch (node)
        {
            case EmptyNode:
                return Positions(position);

            case LiteralNode literal:
                return position < _text.Length && CharEquals(_text[position], literal.Character)
                    ? Positions(position + 1)
                    : Positions();

            case CharacterSetNode setNode:
                return position < _text.Length && setNode.Set.Matches(_text[position], _ignoreCase)
                    ? Positions(position + 1)
                    : Positions();

            case AnyCharNode:
                return position < _text.Length && _text[position] != '\n'
                    ? Positions(position + 1)
                    : Positions();

            case AnchorNode anchor:
                return IsAnchorSatisfied(anchor.Kind, position)
                    ? Positions(position)
                    : Positions();

            case ConcatNode concat:
                return MatchConcat(concat, position);

            case AlternationNode alternation:
                var alternativesResult = Positions();

                foreach (var alternative in alternation.Alternatives)
                {
                    alternativesResult.UnionWith(Match(alternative, position));
                }

                return alternativesResult;

            case RepeatNode repeat:
                return MatchRepeat(repeat, position);

            default:
                throw new InvalidOperationException("Неизвестный узел выражения.");
        }
    }

    private HashSet<int> MatchConcat(ConcatNode concat, int position)
    {
        var current = Positions(position);

        foreach (var child in concat.Children)
        {
            if (current.Count == 0)
            {
                break;
            }

            var next = Positions();

            foreach (var currentPosition in current)
            {
                next.UnionWith(Match(child, currentPosition));
            }

            current = next;
        }

        return current;
    }

    private HashSet<int> MatchRepeat(RepeatNode repeat, int position)
    {
        var result = Positions();
        var current = Positions(position);

        for (var i = 0; i < repeat.Min; i++)
        {
            var next = Positions();

            foreach (var currentPosition in current)
            {
                next.UnionWith(Match(repeat.Child, currentPosition));
            }

            current = next;

            if (current.Count == 0)
            {
                return result;
            }
        }

        result.UnionWith(current);

        var max = repeat.Max ?? int.MaxValue;
        if (max == repeat.Min)
        {
            return result;
        }

        var frontier = new HashSet<int>(current);
        var steps = repeat.Min;

        while (frontier.Count > 0 && steps < max)
        {
            var next = Positions();

            foreach (var currentPosition in frontier)
            {
                foreach (var end in Match(repeat.Child, currentPosition))
                {
                    if (result.Add(end))
                    {
                        next.Add(end);
                    }
                }
            }

            frontier = next;
            steps++;
        }

        return result;
    }

    private bool IsAnchorSatisfied(AnchorKind kind, int position) => kind switch
    {
        AnchorKind.Start => position == 0,
        AnchorKind.End => position == _text.Length,
        AnchorKind.WordBoundary => IsWordBoundary(position),
        AnchorKind.NonWordBoundary => !IsWordBoundary(position),
        _ => false
    };

    private bool IsWordBoundary(int position)
    {
        var before = position > 0 && CharacterSet.IsWordChar(_text[position - 1]);
        var after = position < _text.Length && CharacterSet.IsWordChar(_text[position]);
        return before != after;
    }

    private bool CharEquals(char left, char right)
    {
        if (left == right)
        {
            return true;
        }

        return _ignoreCase && char.ToLowerInvariant(left) == char.ToLowerInvariant(right);
    }

    private static HashSet<int> Positions() => [];

    private static HashSet<int> Positions(int position) => [position];
}
