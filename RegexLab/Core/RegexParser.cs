using System;
using System.Collections.Generic;
using System.Globalization;

namespace RegexLab.Core;

// Парсер: строка выражения -> дерево.
internal sealed class RegexParser
{
    private const int MaxRepeat = 10_000;

    private readonly string _pattern;
    private int _position;

    private RegexParser(string pattern)
    {
        _pattern = pattern;
    }

    public static RegexNode Parse(string pattern)
    {
        var parser = new RegexParser(pattern);
        var root = parser.ParseAlternation();

        if (parser._position < pattern.Length)
        {
            throw Error("Лишняя закрывающая скобка ')'.", parser._position);
        }

        return root;
    }

    private RegexNode ParseAlternation()
    {
        var alternatives = new List<RegexNode> { ParseSequence() };

        while (Match('|'))
        {
            alternatives.Add(ParseSequence());
        }

        return alternatives.Count == 1
            ? alternatives[0]
            : new AlternationNode(alternatives);
    }

    private RegexNode ParseSequence()
    {
        var nodes = new List<RegexNode>();

        while (_position < _pattern.Length && Current is not ('|' or ')'))
        {
            var atom = ParseAtom();
            atom = ParseQuantifier(atom);
            nodes.Add(atom);
        }

        return nodes.Count switch
        {
            0 => EmptyNode.Instance,
            1 => nodes[0],
            _ => new ConcatNode(nodes)
        };
    }

    private RegexNode ParseAtom()
    {
        var current = Current;

        switch (current)
        {
            case '(':
                return ParseGroup();

            case '[':
                return ParseCharacterClass();

            case '.':
                _position++;
                return AnyCharNode.Instance;

            case '^':
                _position++;
                return new AnchorNode(AnchorKind.Start);

            case '$':
                _position++;
                return new AnchorNode(AnchorKind.End);

            case '\\':
                return ParseEscape();

            case '*' or '+' or '?':
                throw Error($"Квантификатор '{current}' не имеет предшествующего выражения.", _position);

            case '{' when IsAsciiDigit(Peek(1)):
                throw Error("Квантификатор '{...}' не имеет предшествующего выражения.", _position);

            default:
                _position++;
                return new LiteralNode(current);
        }
    }

    private RegexNode ParseGroup()
    {
        var openPosition = _position;
        _position++; // (

        if (Current == '?')
        {
            if (Peek(1) == ':')
            {
                _position += 2;
            }
            else
            {
                throw Error("Неподдерживаемая конструкция группы. Используйте (...) или (?:...).", openPosition);
            }
        }

        var inner = ParseAlternation();

        if (Current != ')')
        {
            throw Error("Не найдена закрывающая скобка ')'.", openPosition);
        }

        _position++;
        return inner;
    }

    private RegexNode ParseCharacterClass()
    {
        var openPosition = _position;
        _position++; // [

        var isNegated = Match('^');
        var set = new CharacterSet(isNegated);
        var isFirst = true;
        var isClosed = false;

        while (_position < _pattern.Length)
        {
            if (Current == ']' && !isFirst)
            {
                _position++;
                isClosed = true;
                break;
            }

            var item = ParseClassItem();

            if (item.Category is not null)
            {
                if (Current == '-' && Peek(1) != ']')
                {
                    throw Error("Диапазон не может начинаться с класса символов.", _position);
                }

                set.AddCategory(item.Category.Value);
                isFirst = false;
                continue;
            }

            var rangeStart = item.Character!.Value;

            if (Current == '-' && Peek(1) != ']' && _position + 1 < _pattern.Length)
            {
                var dashPosition = _position;
                _position++; // -
                var rangeEndItem = ParseClassItem();

                if (rangeEndItem.Category is not null)
                {
                    throw Error("Конец диапазона должен быть одним символом.", dashPosition);
                }

                var rangeEnd = rangeEndItem.Character!.Value;
                if (rangeEnd < rangeStart)
                {
                    throw Error("Начало диапазона больше его конца.", dashPosition);
                }

                set.AddRange(rangeStart, rangeEnd);
            }
            else
            {
                set.AddRange(rangeStart, rangeStart);
            }

            isFirst = false;
        }

        if (!isClosed)
        {
            throw Error("Не найдена закрывающая скобка ']'.", openPosition);
        }

        return new CharacterSetNode(set);
    }

    private (char? Character, CharacterCategory? Category) ParseClassItem()
    {
        if (_position >= _pattern.Length)
        {
            throw Error("Неожиданный конец выражения.", _position);
        }

        if (Current != '\\')
        {
            var value = Current;
            _position++;
            return (value, null);
        }

        var slashPosition = _position;
        _position++;

        if (_position >= _pattern.Length)
        {
            throw Error("Выражение не может заканчиваться символом '\\'.", slashPosition);
        }

        var escaped = Current;
        _position++;

        switch (escaped)
        {
            case 'd': return (null, CharacterCategory.Digit);
            case 'D': return (null, CharacterCategory.NotDigit);
            case 'w': return (null, CharacterCategory.Word);
            case 'W': return (null, CharacterCategory.NotWord);
            case 's': return (null, CharacterCategory.Space);
            case 'S': return (null, CharacterCategory.NotSpace);
            case 'b': return ('\b', null);
            case 'n': return ('\n', null);
            case 'r': return ('\r', null);
            case 't': return ('\t', null);
            case 'f': return ('\f', null);
            case 'v': return ('\v', null);
            case '0': return ('\0', null);
            case 'x': return (ParseHexCharacter(2, slashPosition), null);
            case 'u': return (ParseHexCharacter(4, slashPosition), null);
            case 'p' or 'P':
                throw Error("Unicode-категории (\\p{...}) не поддерживаются.", slashPosition);
            default:
                return (escaped, null);
        }
    }

    private RegexNode ParseEscape()
    {
        var slashPosition = _position;
        _position++; // \

        if (_position >= _pattern.Length)
        {
            throw Error("Выражение не может заканчиваться символом '\\'.", slashPosition);
        }

        var escaped = Current;
        _position++;

        switch (escaped)
        {
            case 'd': return CategoryNode(CharacterCategory.Digit);
            case 'D': return CategoryNode(CharacterCategory.NotDigit);
            case 'w': return CategoryNode(CharacterCategory.Word);
            case 'W': return CategoryNode(CharacterCategory.NotWord);
            case 's': return CategoryNode(CharacterCategory.Space);
            case 'S': return CategoryNode(CharacterCategory.NotSpace);

            case 'b': return new AnchorNode(AnchorKind.WordBoundary);
            case 'B': return new AnchorNode(AnchorKind.NonWordBoundary);
            case 'A': return new AnchorNode(AnchorKind.Start);
            case 'z':
            case 'Z': return new AnchorNode(AnchorKind.End);

            case 'n': return new LiteralNode('\n');
            case 'r': return new LiteralNode('\r');
            case 't': return new LiteralNode('\t');
            case 'f': return new LiteralNode('\f');
            case 'v': return new LiteralNode('\v');
            case '0': return new LiteralNode('\0');
            case 'x': return new LiteralNode(ParseHexCharacter(2, slashPosition));
            case 'u': return new LiteralNode(ParseHexCharacter(4, slashPosition));

            case 'p' or 'P':
                throw Error("Unicode-категории (\\p{...}) не поддерживаются.", slashPosition);

            case 'G' or 'k' or 'c':
                throw Error($"Конструкция \\{escaped} не поддерживается.", slashPosition);

            default:
                if (IsAsciiDigit(escaped))
                {
                    throw Error("Обратные ссылки не поддерживаются.", slashPosition);
                }

                return new LiteralNode(escaped);
        }
    }

    private RegexNode ParseQuantifier(RegexNode atom)
    {
        if (_position >= _pattern.Length)
        {
            return atom;
        }

        var current = Current;
        int min;
        int? max;

        if (current is '*' or '+' or '?')
        {
            _position++;
            (min, max) = current switch
            {
                '*' => (0, (int?)null),
                '+' => (1, (int?)null),
                _ => (0, 1)
            };
        }
        else if (current == '{' && IsAsciiDigit(Peek(1)))
        {
            var bracePosition = _position;
            _position++; // {

            min = ParseNumber(bracePosition);
            max = min;

            if (Match(','))
            {
                max = Current == '}' ? null : ParseNumber(bracePosition);
            }

            if (Current != '}')
            {
                throw Error("Не найдена закрывающая скобка '}' в квантификаторе.", bracePosition);
            }

            _position++;

            if (max is not null && max < min)
            {
                throw Error("Минимальное число повторений больше максимального.", bracePosition);
            }

            if (min > MaxRepeat || max > MaxRepeat)
            {
                throw Error($"Слишком большое число повторений (максимум {MaxRepeat}).", bracePosition);
            }
        }
        else
        {
            return atom;
        }

        // Ленивый режим принимаем, но при поиске всё равно берём самый длинный вариант.
        Match('?');

        if (_position < _pattern.Length && Current is '*' or '+' or '?' or '{')
        {
            throw Error("Два квантификатора подряд.", _position);
        }

        return new RepeatNode(atom, min, max);
    }

    private int ParseNumber(int errorPosition)
    {
        var start = _position;

        while (_position < _pattern.Length && IsAsciiDigit(Current))
        {
            _position++;
        }

        if (start == _position)
        {
            throw Error("Ожидалось число.", errorPosition);
        }

        var text = _pattern[start.._position];

        if (!int.TryParse(text, out var value))
        {
            throw Error("Слишком большое число в квантификаторе.", errorPosition);
        }

        return value;
    }

    private char ParseHexCharacter(int digits, int errorPosition)
    {
        if (_position + digits > _pattern.Length)
        {
            throw Error(
                $"После \\{(digits == 2 ? "x" : "u")} ожидаются {digits} шестнадцатеричных цифры.",
                errorPosition);
        }

        var text = _pattern.Substring(_position, digits);

        if (!ushort.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
        {
            throw Error($"Некорректная шестнадцатеричная запись '{text}'.", errorPosition);
        }

        _position += digits;
        return (char)value;
    }

    private static RegexNode CategoryNode(CharacterCategory category)
    {
        var set = new CharacterSet(isNegated: false);
        set.AddCategory(category);
        return new CharacterSetNode(set);
    }

    private bool Match(char expected)
    {
        if (_position < _pattern.Length && _pattern[_position] == expected)
        {
            _position++;
            return true;
        }

        return false;
    }

    private char Current => _position < _pattern.Length ? _pattern[_position] : '\0';

    private char Peek(int offset)
    {
        var index = _position + offset;
        return index >= 0 && index < _pattern.Length ? _pattern[index] : '\0';
    }

    private static bool IsAsciiDigit(char value) => value is >= '0' and <= '9';

    private static RegexSyntaxException Error(string message, int position) => new(message, position);
}
