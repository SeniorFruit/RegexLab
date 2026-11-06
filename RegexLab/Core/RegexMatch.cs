using System;
using System.Collections.Generic;

namespace RegexLab.Core;

public readonly record struct RegexMatch(int Start, int Length)
{
    public int End => Start + Length;
}
