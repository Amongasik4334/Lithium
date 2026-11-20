using Lithium.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using static Lithium.Parser.Lexer;

namespace Lithium.Parser;

internal sealed class Parser(string source) : Lexer(source)
{
    internal Root Parse()
    {
        Tokens = Tokenize().GetEnumerator();
        List<Declaration> nodes = [];

        /*while (Peek().Kind != TokenKind.Eof)
        {
            Match(TokenKind.Semicolon);
            if (Peek().Kind == TokenKind.Eof) break;
            nodes.Add(NamespaceNode());
        }*/

        return new Root(nodes);
    }

    /*private Declaration NamespaceNode()
    {
        var start = Peek();

        switch()
        if (Match(TokenKind.Method))
        {
            var name = Consume(TokenKind.Word);
            Consume(TokenKind.LParen);
            List<Local> parameters = [];
            while (Peek().Kind != TokenKind.RParen)
            {
                var pType = ParseTypeof();
                var pName = Consume(TokenKind.Word);
                Expression? initializer = Match(TokenKind.Assign) ? ParseExpression() : null;
                parameters.Add(new Local(Text(pName), pType));
                if (!Match(TokenKind.Comma)) break;
            }
            Consume(TokenKind.RParen);
            Typeof? returnType = Match(TokenKind.Colon) ? ParseTypeof() : null;
            Block? body = Peek().Kind == TokenKind.LBrace ? ParseBlock() : null;
            return WithSpan(new Method(Text(name), parameters, returnType, AccessMode.Public, body), start);
        }

        if (Match(TokenKind.Struct))
        {
            var name = Consume(TokenKind.Word);
            List<string> protocols = [];
            if (Match(TokenKind.Colon))
            {
                do protocols.Add(Text(Consume(TokenKind.Word)));
                while (Match(TokenKind.Comma));
            }
            Consume(TokenKind.LBrace);
            List<Declaration> members = [];
            while (Peek().Kind != TokenKind.RBrace)
                members.Add(ParseNamespaceDeclaration());
            Consume(TokenKind.RBrace);
            return WithSpan(new Struct(Text(name), members, protocols, AccessMode.Public), start);
        }

        if (Peek().Kind == TokenKind.Word || Peek().Kind == TokenKind.Ref)
        {
            var type = ParseTypeof();
            var name = Consume(TokenKind.Word);
            Expression? initializer = Match(TokenKind.Assign) ? ParseExpression() : null;
            Match(TokenKind.Semicolon);
            return WithSpan(new Variable(Text(name), type, AccessMode.Public, initializer), start);
        }

        throw Error($"Unexpected token {Peek().Kind} in declaration", Peek());
    }

    private Block ParseBlock()
    {
        var start = Peek();
        Consume(TokenKind.LBrace);
        List<Statement> statements = [];
        while (Peek().Kind != TokenKind.RBrace && Peek().Kind != TokenKind.Eof)
        {
            statements.Add(ParseStatement());
        }
        Consume(TokenKind.RBrace);
        return WithSpan(new Block(statements), start);
    }

    private Statement ParseStatement()
    {
        var start = Peek();

        if (Match(TokenKind.If))
        {
            var condition = ParseExpression();
            var then = ParseBlock();
            Block? otherwise = null;
            if (Match(TokenKind.Elif))
            {
                var elifCondition = ParseExpression();
                var elifBody = ParseBlock();
                otherwise = WithSpan(new Block([WithSpan(new IfStatement(elifCondition, elifBody, null), start)]), start);
            }
            else if (Match(TokenKind.Else))
            {
                otherwise = ParseBlock();
            }
            return WithSpan(new IfStatement(condition, then, otherwise), start);
        }

        if (Match(TokenKind.For))
        {
            Typeof? type = null;
            if (Peek().Kind is TokenKind.Word or TokenKind.Ref)
                type = Peek().Kind == TokenKind.Ref ? null : PeekTypeaheadIsNameThenWord() ? ParseTypeof() : null;
            var name = Consume(TokenKind.Word);
            Consume(TokenKind.In);
            var iterable = ParseExpression();
            var body = ParseBlock();
            return WithSpan(new ForStatement(type, Text(name), iterable, body), start);
        }

        if (Match(TokenKind.Return))
        {
            Expression? value = Peek().Kind is TokenKind.Semicolon or TokenKind.RBrace ? null : ParseExpression();
            Match(TokenKind.Semicolon);
            return WithSpan(new ReturnStatement(value), start);
        }

        if (Match(TokenKind.Defer))
        {
            var expression = ParseExpression();
            Match(TokenKind.Semicolon);
            return WithSpan(new DeferStatement(expression), start);
        }

        if (Peek().Kind == TokenKind.Match)
        {
            var match = ParseMatch();
            Match(TokenKind.Semicolon);
            if (match is MatchStatement statement) return statement;
            return WithSpan(new ExpressionStatement((MatchExpression)match), start);
        }

        if (Peek().Kind == TokenKind.Word || Peek().Kind == TokenKind.Ref)
        {
            var type = PeekTypeaheadIsNameThenWord() ? ParseTypeof() : null;
            if (type != null)
            {
                var name = Consume(TokenKind.Word);
                Expression? initializer = Match(TokenKind.Assign) ? ParseExpression() : null;
                Match(TokenKind.Semicolon);
                return WithSpan(new LocalDeclaration(type, Text(name), initializer), start);
            }
        }

        var expression = ParseExpression();
        Match(TokenKind.Semicolon);
        return WithSpan(new ExpressionStatement(expression), start);
    }

    private bool PeekTypeaheadIsNameThenWord()
    {
        if (Peek().Kind is not (TokenKind.Word or TokenKind.Ref)) return false;
        return Peek(1).Kind == TokenKind.Word;
    }

    private Node ParseMatch()
    {
        var start = Peek();
        Consume(TokenKind.Match);
        var target = ParseExpression();
        Consume(TokenKind.LBrace);
        List<MatchArm> arms = [];

        while (Peek().Kind != TokenKind.RBrace && Peek().Kind != TokenKind.Eof)
        {
            Expression? pattern = Match(TokenKind.Default) ? null : ParseExpression();
            Consume(TokenKind.Arrow);
            Node body;
            if (Peek().Kind == TokenKind.LBrace)
                body = ParseBlock();
            else
            {
                body = ParseExpression();
                Match(TokenKind.Semicolon);
            }
            arms.Add(WithSpan(new MatchArm(pattern, body), start));
            Match(TokenKind.Comma);
        }

        Consume(TokenKind.RBrace);
        if (Peek().Kind is TokenKind.Semicolon or TokenKind.Eof or TokenKind.RBrace)
            return WithSpan(new MatchStatement(target, arms), start);
        return WithSpan(new MatchExpression(target, arms), start);
    }

    private Expression ParseExpression() => ParseAssignment();

    private Expression ParseAssignment()
    {
        var left = Peek().Kind == TokenKind.Match ? (Expression)ParseMatchExpression() : ParseOr();
        if (Match(TokenKind.Assign))
            return new Binary(left, Operator.Assign, ParseAssignment()) { Span = Span(left) };
        if (Match(TokenKind.AddAssign))
            return new Binary(left, Operator.Add, ParseAssignment()) { Span = Span(left) };
        return left;
    }

    private MatchExpression ParseMatchExpression()
    {
        var start = Peek();
        Consume(TokenKind.Match);
        var target = ParseOr();
        Consume(TokenKind.LBrace);
        List<MatchArm> arms = [];
        while (Peek().Kind != TokenKind.RBrace && Peek().Kind != TokenKind.Eof)
        {
            Expression? pattern = Match(TokenKind.Default) ? null : ParseExpression();
            Consume(TokenKind.Arrow);
            Node body = Peek().Kind == TokenKind.LBrace ? ParseBlock() : ParseExpression();
            arms.Add(WithSpan(new MatchArm(pattern, body), start));
            Match(TokenKind.Comma);
        }
        Consume(TokenKind.RBrace);
        return WithSpan(new MatchExpression(target, arms), start);
    }

    private Expression ParseOr()
    {
        var left = ParseAnd();
        while (Match(TokenKind.Or)) left = new Binary(left, Operator.Or, ParseAnd());
        return left;
    }

    private Expression ParseAnd()
    {
        var left = ParseEquality();
        while (Match(TokenKind.And)) left = new Binary(left, Operator.And, ParseEquality());
        return left;
    }

    private Expression ParseEquality()
    {
        var left = ParseComparison();
        while (Peek().Kind is TokenKind.Equal or TokenKind.NotEqual)
        {
            var op = Advance();
            left = new Binary(left, op.Kind == TokenKind.Equal ? Operator.Equal : Operator.NotEqual, ParseComparison());
        }
        return left;
    }

    private Expression ParseComparison()
    {
        var left = ParseRange();
        while (Peek().Kind is TokenKind.Less or TokenKind.LessEq or TokenKind.Greater or TokenKind.GreaterEq)
        {
            var op = Advance();
            Operator kind = op.Kind switch
            {
                TokenKind.Less => Operator.Less,
                TokenKind.LessEq => Operator.LessEq,
                TokenKind.Greater => Operator.Greater,
                _ => Operator.GreaterEq
            };
            left = new Binary(left, kind, ParseRange());
        }
        return left;
    }

    private Expression ParseRange()
    {
        var start = ParseAdditive();
        if (Peek().Kind is TokenKind.DotDot or TokenKind.DotDotEq)
        {
            bool inclusive = Advance().Kind == TokenKind.DotDotEq;
            return new Range(start, ParseAdditive(), inclusive);
        }
        return start;
    }

    private Expression ParseAdditive()
    {
        var left = ParseMultiplicative();
        while (Peek().Kind is TokenKind.Plus or TokenKind.Minus)
        {
            var op = Advance();
            left = new Binary(left, op.Kind == TokenKind.Plus ? Operator.Add : Operator.Sub, ParseMultiplicative());
        }
        return left;
    }

    private Expression ParseMultiplicative()
    {
        var left = ParseUnary();
        while (Peek().Kind is TokenKind.Star or TokenKind.Slash)
        {
            var op = Advance();
            left = new Binary(left, op.Kind == TokenKind.Star ? Operator.Mul : Operator.Div, ParseUnary());
        }
        return left;
    }

    private Expression ParseUnary()
    {
        if (Peek().Kind is TokenKind.Minus or TokenKind.Not)
        {
            var op = Advance();
            return new Unary(op.Kind == TokenKind.Minus ? Operator.Negate : Operator.Not, ParseUnary());
        }
        return ParsePostfix();
    }

    private Expression ParsePostfix()
    {
        var expression = ParsePrimary();
        for (;;)
        {
            if (Match(TokenKind.Dot) || Match(TokenKind.DoubleColon))
            {
                var name = Consume(TokenKind.Word);
                expression = new Member(expression, Text(name));
            }
            else if (Match(TokenKind.LParen))
            {
                List<Expression> arguments = [];
                while (Peek().Kind != TokenKind.RParen)
                {
                    arguments.Add(ParseExpression());
                    if (!Match(TokenKind.Comma)) break;
                }
                Consume(TokenKind.RParen);
                expression = new Call(expression, arguments);
            }
            else break;
        }
        return expression;
    }

    private Expression ParsePrimary()
    {
        var start = Peek();
        if (Match(TokenKind.Word)) return WithSpan(new Var(Text(start)), start);
        if (Peek().Kind is TokenKind.Int or TokenKind.Hex or TokenKind.Bin or TokenKind.Oct)
        {
            var token = Advance();
            string text = Text(token).Replace("_", "");
            BigInteger value = token.Kind switch
            {
                TokenKind.Hex => BigInteger.Parse(text.Substring(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture),
                TokenKind.Bin => ParseBinary(text.Substring(2)),
                TokenKind.Oct => ParseOctal(text.Substring(2)),
                _ => BigInteger.Parse(text, CultureInfo.InvariantCulture)
            };
            return WithSpan(new Literal.Int(value), start);
        }
        if (Peek().Kind == TokenKind.Float)
        {
            var token = Advance();
            return WithSpan(new Literal.Float(BigInteger.Parse(Text(token).Replace("_", "").Split('.')[0]), 0), start);
        }
        if (Peek().Kind == TokenKind.String)
        {
            var token = Advance();
            var value = Text(token);
            return WithSpan(new Literal.String(value.Substring(1, value.Length - 2)), start);
        }
        if (Peek().Kind == TokenKind.Char)
        {
            var token = Advance();
            var value = Text(token);
            return WithSpan(new Literal.Char(value.Length >= 3 ? value[1] : '\0'), start);
        }
        if (Peek().Kind == TokenKind.Match)
            return ParseMatchExpression();
        if (Match(TokenKind.LParen))
        {
            var expression = ParseExpression();
            Consume(TokenKind.RParen);
            return expression;
        }
        if (Match(TokenKind.LBracket))
        {
            List<Expression> elements = [];
            while (Peek().Kind != TokenKind.RBracket)
            {
                elements.Add(ParseExpression());
                if (!Match(TokenKind.Comma)) break;
            }
            Consume(TokenKind.RBracket);
            return WithSpan(new ArrayLiteral(elements), start);
        }
        throw Error($"Expected expression, got {Peek().Kind}", Peek());
    }

    private static BigInteger ParseBinary(string text)
    {
        BigInteger value = 0;
        foreach (char c in text) value = (value << 1) | (c - '0');
        return value;
    }

    private static BigInteger ParseOctal(string text)
    {
        BigInteger value = 0;
        foreach (char c in text) value = (value << 3) | (c - '0');
        return value;
    }

    private Typeof ParseTypeof()
    {
        var start = Peek();
        if (Match(TokenKind.Ref))
            return WithSpan(new Typeof.Referal(ParseTypeof()), start);
        if (Match(TokenKind.LParen))
        {
            List<Typeof> elements = [];
            while (Peek().Kind != TokenKind.RParen)
            {
                elements.Add(ParseTypeof());
                if (!Match(TokenKind.Comma)) break;
            }
            Consume(TokenKind.RParen);
            return WithSpan(new Typeof.Tuple(elements), start);
        }

        string name = Text(Consume(TokenKind.Word));
        bool mutable = Match(TokenKind.Tilde);
        Typeof type = name is "i8" or "u8" or "i16" or "u16" or "i32" or "u32" or "i64" or "u64" or "int" or "uint" or "f32" or "f64" or "bool" or "char" or "void"
            ? new Typeof.Primitive(name, mutable)
            : new Typeof.Named(name, mutable);
        while (Match(TokenKind.LBracket))
        {
            if (Match(TokenKind.RBracket)) type = new Typeof.Slice(type);
            else
            {
                var countToken = Consume(TokenKind.Int);
                long count = long.Parse(Text(countToken), CultureInfo.InvariantCulture);
                Consume(TokenKind.RBracket);
                type = new Typeof.Array(type, count);
            }
            if (Match(TokenKind.Tilde))
                type = type switch
                {
                    Typeof.Primitive p => new Typeof.Primitive(p.Name, true),
                    Typeof.Named n => new Typeof.Named(n.Name, true),
                    var other => other
                };
        }
        return WithSpan(type, start);
    }

    private T WithSpan<T>(T node, Token start) where T : Node
    {
        int end = Peek().Start > start.Start ? Peek().Start : start.Start + start.Length;
        return node with { Span = new SourceSpan(start.Start, Math.Max(1, end - start.Start)) };
    }

    private SourceSpan Span(Node node) => node.Span;*/
}
