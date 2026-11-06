using System.Collections.Generic;

namespace RegexLab.Core;

internal enum AnchorKind
{
    Start,
    End,
    WordBoundary,
    NonWordBoundary
}

internal abstract class RegexNode;

internal sealed class EmptyNode : RegexNode
{
    public static readonly EmptyNode Instance = new();

    private EmptyNode()
    {
    }
}

internal sealed class LiteralNode(char character) : RegexNode
{
    public char Character { get; } = character;
}

internal sealed class CharacterSetNode(CharacterSet set) : RegexNode
{
    public CharacterSet Set { get; } = set;
}

internal sealed class AnyCharNode : RegexNode
{
    public static readonly AnyCharNode Instance = new();

    private AnyCharNode()
    {
    }
}

internal sealed class AnchorNode(AnchorKind kind) : RegexNode
{
    public AnchorKind Kind { get; } = kind;
}

internal sealed class ConcatNode(IReadOnlyList<RegexNode> children) : RegexNode
{
    public IReadOnlyList<RegexNode> Children { get; } = children;
}

internal sealed class AlternationNode(IReadOnlyList<RegexNode> alternatives) : RegexNode
{
    public IReadOnlyList<RegexNode> Alternatives { get; } = alternatives;
}

internal sealed class RepeatNode(RegexNode child, int min, int? max) : RegexNode
{
    public RegexNode Child { get; } = child;
    public int Min { get; } = min;
    public int? Max { get; } = max;
}
