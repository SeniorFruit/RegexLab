using System;

namespace RegexLab.Core;

public sealed class RegexSyntaxException : Exception
{
    public RegexSyntaxException(string message, int position) : base(message)
    {
        Position = position;
    }

    // Позиция ошибки в исходном выражении, с нуля.
    public int Position { get; }
}
