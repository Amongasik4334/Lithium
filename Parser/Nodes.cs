using Lithium.Utils;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using static Lithium.Parser.Declaration;

namespace Lithium.Parser;

internal abstract record Node
{
    internal SourceSpan Span { get; init; }
}

internal sealed record Root(List<Declaration> Nodes) : Node;

#region Expression
internal abstract record Expression : Node
{
    internal Typeof? InferredType { get; init; }
}
internal abstract record Literal : Expression
{
    internal sealed record Int(long Value) : Literal;
    internal sealed record Float(double Value) : Literal;
    internal sealed record String(string Value) : Literal;
    internal sealed record Char(char Value) : Literal;
}
internal sealed record Var(string Name) : Expression;
internal sealed record Binary(Expression Left, Operator Op, Expression Right) : Expression;
internal sealed record Unary(Operator Op, Expression Operand) : Expression;
internal sealed record Call(Expression Target, List<Expression> Arguments) : Expression;
internal sealed record Member(Expression Target, List<Expression> Arguments) : Expression;
#endregion

#region Statement
internal abstract record Statement : Node;
internal sealed record Block(List<Statement> Statements) : Statement;
internal sealed record Variable(Local Declaration) : Statement;
internal sealed record If(Expression Condition, Block Body, Elif? Then = null, Else? Else = null) : Statement;
internal sealed record Elif(Expression Condition, Block Body, Elif? Then = null, Else? Else = null) : Statement;
internal sealed record Else(Block Body) : Statement;
internal sealed record While(Expression Condition, Block Body) : Statement;
#endregion

#region Declaration
internal abstract record Declaration : Node
{
    internal sealed record Function(
        string Name,
        List<Local> Parameters,
        Typeof ReturnType,
        AccessMode Access,
        Block? Body = null) : Declaration;
    internal sealed record Variable(
        string Name,
        Typeof Type,
        AccessMode Access,
        Expression? Init) : Declaration;
    internal sealed record Local(
        string Name,
        Typeof Type,
        Expression? Init) : Declaration;
    internal sealed record Struct(
        string Name,
        AccessMode Access,
        Declaration[] Body) : Declaration;
}
#endregion

internal abstract record Typeof : Node
{
    internal sealed record Atom(string Name, bool Mutable, int Size, bool Signed) : Typeof
    {
        internal override bool IsMutable() => Mutable;
    }
    internal sealed record Named(string Name, bool Mutable) : Typeof
    {
        internal override bool IsMutable() => Mutable;
    }
    internal sealed record Slice(Typeof Object) : Typeof
    {
        internal override bool IsMutable() => Object.IsMutable();
    }
    internal sealed record Array(Typeof Object, long Count) : Typeof
    {
        internal override bool IsMutable() => Object.IsMutable();
    }
    internal sealed record Referal(Typeof Inner) : Typeof
    {
        internal override bool IsMutable() => Inner.IsMutable();
    }

    internal abstract bool IsMutable();
}

internal enum AccessMode
{
    Local = -1,
    Public, Protected, Hidden
}
internal enum Operator
{
    Assign,         // =

    Or,             // or

    And,            // and

    BitOr,          // |
    BitXor,         // ^
    BitAnd,         // &

    Equal,          // ==
    NotEqual,       // !=

    Less,           // <
    LessEq,         // <=
    Greater,        // >
    GreaterEq,      // >=

    ShiftLeft,      // <<
    ShiftRight,     // >>

    Add,            // +
    Sub,            // -

    Mul,            // *
    Div,            // /

    As,             // as
    Bitcast,        // bitcast

    Not,            // !
    Negate          // -x
}