using Lithium.Utils;
using System;
using System.Collections.Generic;
using static Lithium.Parser.Lexer;

namespace Lithium.Parser;

internal class Lexer(string source)
{
    internal enum TokenKind
    {
        Eof, Unknown,

        Method, Struct, Class, Protocol, Defer, Return, Init, Ref, Val, And, Or, If, Elif, Else, For, In, While, Match, Default,
        DotDot, DotDotEq,

        Word, Int, Hex, Bin, Oct, Float, String, Char,

        DoubleColon, Equal, NotEqual, GreaterEq, LessEq, ShiftLeft, ShiftRight,

        Colon, Tilde, At, Semicolon, Comma, Dot, Assign, AddAssign,
        Arrow, Plus, Minus, Star, Slash, Less, Greater,
        BitAnd, BitOr, BitXor, Not,
        LParen, RParen, LBrace, RBrace, LBracket, RBracket,
        As, Bitcast
    }

    public readonly record struct Token(TokenKind Kind, int Start, int Length);

    protected IEnumerable<Token> Tokenize()
    {
        int i = 0;
        int len = source.Length;

        while (i < len)
        {
            char c = source[i];

            if (char.IsWhiteSpace(c)) { i++; continue; }

            if (c == '/' && i + 1 < len && source[i + 1] == '/')
            {
                while (i < len && source[i] != '\n') i++;
                continue;
            }

            int start = i;

            if (char.IsLetter(c) || c == '_')
            {
                while (i < len && (char.IsLetterOrDigit(source[i]) || source[i] == '_')) i++;
                var kind = source.AsSpan(start, i - start) switch
                {
                    "method" => TokenKind.Method,
                    "struct" => TokenKind.Struct,
                    "class" => TokenKind.Class,
                    "protocol" => TokenKind.Protocol,
                    "defer" => TokenKind.Defer,
                    "return" => TokenKind.Return,
                    "init" => TokenKind.Init,
                    "ref" => TokenKind.Ref,
                    "val" => TokenKind.Val,
                    "and" => TokenKind.And,
                    "or" => TokenKind.Or,
                    "as" => TokenKind.As,
                    "bitcast" => TokenKind.Bitcast,
                    "if" => TokenKind.If,
                    "elif" => TokenKind.Elif,
                    "else" => TokenKind.Else,
                    "for" => TokenKind.For,
                    "in" => TokenKind.In,
                    "while" => TokenKind.While,
                    "match" => TokenKind.Match,
                    "default" => TokenKind.Default,
                    _ => TokenKind.Word
                };
                yield return new Token(kind, start, i - start);
                continue;
            }

            if (char.IsDigit(c))
            {
                if (c == '0' && i + 1 < len && char.ToLowerInvariant(source[i + 1]) is 'x' or 'b' or 'o')
                {
                    char p = char.ToLowerInvariant(source[i + 1]);
                    i += 2;
                    int digitsStart = i;

                    while (i < len && (source[i] == '_' || (p == 'x' ? char.IsAsciiHexDigit(source[i]) : p == 'b' ? source[i] is '0' or '1' : source[i] is >= '0' and <= '7')))
                        i++;

                    if (i == digitsStart)
                        throw Error($"Empty {p}-literal", new Token(TokenKind.Unknown, start, i - start));

                    if (i < len && char.IsLetterOrDigit(source[i]))
                        throw Error($"Invalid digit '{source[i]}' for 0{p} literal", new Token(TokenKind.Unknown, start, i - start + 1));

                    yield return new Token(p == 'x' ? TokenKind.Hex : p == 'b' ? TokenKind.Bin : TokenKind.Oct, start, i - start);
                    continue;
                }

                bool isFloat = false;
                while (i < len && (char.IsDigit(source[i]) || source[i] == '_')) i++;

                if (i + 1 < len && source[i] == '.' && char.IsDigit(source[i + 1]))
                {
                    isFloat = true;
                    i++;
                    while (i < len && (char.IsDigit(source[i]) || source[i] == '_')) i++;
                }

                if (i < len && source[i] is 'e' or 'E')
                {
                    isFloat = true;
                    i++;
                    if (i < len && source[i] is '+' or '-') i++;
                    while (i < len && (char.IsDigit(source[i]) || source[i] == '_')) i++;
                }

                yield return new Token(isFloat ? TokenKind.Float : TokenKind.Int, start, i - start);
                continue;
            }

            if (c is '"' or '\'')
            {
                char quote = c;
                i++;
                while (i < len && source[i] != quote)
                {
                    if (source[i] == '\\') i++;
                    i++;
                }
                if (i < len) i++;
                yield return new Token(quote == '"' ? TokenKind.String : TokenKind.Char, start, i - start);
                continue;
            }

            if (i + 2 < len && c == '.' && source[i + 1] == '.' && source[i + 2] == '=')
            {
                i += 3;
                yield return new Token(TokenKind.DotDotEq, start, 3);
                continue;
            }

            if (i + 1 < len)
            {
                TokenKind? twoCharKind = (c, source[i + 1]) switch
                {
                    (':', ':') => TokenKind.DoubleColon,
                    ('=', '>') => TokenKind.Arrow,
                    ('+', '=') => TokenKind.AddAssign,
                    ('=', '=') => TokenKind.Equal,
                    ('!', '=') => TokenKind.NotEqual,
                    ('>', '=') => TokenKind.GreaterEq,
                    ('<', '=') => TokenKind.LessEq,
                    ('<', '<') => TokenKind.ShiftLeft,
                    ('>', '>') => TokenKind.ShiftRight,
                    ('.', '.') => TokenKind.DotDot,
                    _ => null
                }
;

                if (twoCharKind.HasValue)
                {
                    i += 2;
                    yield return new Token(twoCharKind.Value, start, 2);
                    continue;
                }
            }

            i++;
            TokenKind singleKind = c switch
            {
                ':' => TokenKind.Colon,
                '~' => TokenKind.Tilde,
                '@' => TokenKind.At,
                ';' => TokenKind.Semicolon,
                ',' => TokenKind.Comma,
                '.' => TokenKind.Dot,
                '=' => TokenKind.Assign,
                '+' => TokenKind.Plus,
                '-' => TokenKind.Minus,
                '*' => TokenKind.Star,
                '/' => TokenKind.Slash,
                '<' => TokenKind.Less,
                '>' => TokenKind.Greater,
                '&' => TokenKind.BitAnd,
                '|' => TokenKind.BitOr,
                '^' => TokenKind.BitXor,
                '!' => TokenKind.Not,
                '(' => TokenKind.LParen,
                ')' => TokenKind.RParen,
                '{' => TokenKind.LBrace,
                '}' => TokenKind.RBrace,
                '[' => TokenKind.LBracket,
                ']' => TokenKind.RBracket,
                _ => TokenKind.Unknown
            };

            yield return new Token(singleKind, start, 1);
        }

        yield return new Token(TokenKind.Eof, i, 0);
    }

    protected IEnumerator<Token> Tokens;
    protected readonly List<Token> Buffer = new();

    protected Token Peek(int offset = 0)
    {
        while (Buffer.Count <= offset)
        {
            if (Tokens.MoveNext())
                Buffer.Add(Tokens.Current);
            else
                return Buffer.Count > 0 ? Buffer[^1] : new Token(TokenKind.Eof, source.Length, 0);
        }
        return Buffer[offset];
    }

    protected Token Advance()
    {
        Token current = Peek(0);
        if (Buffer.Count > 0 && current.Kind != TokenKind.Eof)
            Buffer.RemoveAt(0);
        return current;
    }

    protected Token Consume(TokenKind kind) => Peek().Kind == kind ? Advance() : throw Error($"Expected {kind}, got {Peek().Kind}", Peek());

    protected string Text(Token t) => source.Substring(t.Start, t.Length);

    protected bool Match(TokenKind kind)
    {
        if (Peek().Kind != kind) return false;
        Advance();
        return true;
    }

    protected ParserException Error(string message, Token token) => new(message, new SourceSpan(token.Start, token.Length));
}

internal readonly record struct Token(TokenKind Kind, SourceSpan Span);

internal enum TokenKind
{
    EndOfFile,
    Identifier, IntegerLiteral, FloatLiteral, StringLiteral,
    Method, Struct, Protocol, If, Elif, Else, For, In, Return, Defer, Match, Default, As, Bitcast, Or, And,

    OpenParen,
    CloseParen,
    OpenBrace,
    CloseBrace,
    OpenBracket,
    CloseBracket,
    Comma,
    Colon,
    Semicolon,
    Dot,
    Range,
    RangeInclusive,
    Arrow,
    Scope,

    Assign,
    Plus,
    Minus,
    Star,
    Slash,
    Equal,
    NotEqual,
    Less,
    LessEq,
    Greater,
    GreaterEq,
    ShiftLeft,
    ShiftRight,
    BitOr,
    BitXor,
    BitAnd,
    Not,
    Tilde
}
