using System;
using System.Collections.Generic;
using System.Text;

namespace Lithium.Frontend;

internal sealed class Lexer
{

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
