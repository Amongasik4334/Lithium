using Lithium.Parser;
using System;
using System.Collections.Generic;

namespace Lithium.iLC;

internal sealed record Literal(string Name, byte[] Data, bool Immutable=true);
internal sealed record Function(string Name, Typeof? Type, Operand.Local[] Params, List<Instruction> Instructions, bool Inline = false)
{
    public int StackSize { get; set; }
}

internal abstract record Typeof(int Size, int Align)
{
    internal sealed record Int(int Bytes, bool Signed) : Typeof(Bytes, Bytes);
    internal sealed record Float(int Bytes) : Typeof(Bytes, Bytes);
    internal sealed record Referal(Typeof Type) : Typeof(8, 8);
    internal sealed record Struct(int ByteSize, int Alignment) : Typeof(ByteSize, Alignment);
}

internal abstract record Operand(Typeof Type)
{
    internal sealed record Temp(int Id, Typeof Type) : Operand(Type);
    internal sealed record Local(string Name, int StackOffset, Typeof Type) : Operand(Type);
    internal sealed record ConstInt(long Value, Typeof Type) : Operand(Type);
    internal sealed record ConstFloat(double Value, Typeof Type) : Operand(Type);
    internal sealed record Referal(string Name, Typeof Type) : Operand(Type);
    internal sealed record Register(string Name, Typeof Type) : Operand(Type);
}

internal abstract record Instruction
{
    internal sealed record Move(Operand Dest, Operand Src) : Instruction;
    internal sealed record Binary(Operand Dest, Operator Op, Operand Left, Operand Right) : Instruction;
    internal sealed record Unary(Operand Dest, Operator Op, Operand Src) : Instruction;

    internal sealed record AddrOf(Operand Dest, Operand Source) : Instruction;
    internal sealed record Load(Operand Dest, Operand Ptr, int Offset = 0) : Instruction;
    internal sealed record Store(Operand Ptr, Operand Value, int Offset = 0) : Instruction;

    internal sealed record Cast(Operand Dest, Operator Kind, Operand Src) : Instruction;

    internal sealed record Label(string Name) : Instruction;
    internal sealed record Jump(string LabelName) : Instruction;
    internal sealed record Branch(Operand Cond, string TrueLabel, string FalseLabel) : Instruction;

    internal sealed record Call(Operand? Dest, string FuncName, Operand[] Args) : Instruction;
    internal sealed record Asm(string Mnemonic, Operand[] Operands) : Instruction;
    internal sealed record Return(Operand? Value) : Instruction;
}