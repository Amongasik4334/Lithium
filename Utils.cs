using System;

namespace Lithium.Utils;

public sealed class CompileException(string message, SourceSpan span) : Exception(message)
{ 
    public readonly SourceSpan Span = span;
}
public sealed class ParserException(string message, SourceSpan span) : Exception(message)
{
    public readonly SourceSpan Span = span;
}
public readonly record struct SourceSpan(int Start, int Length);