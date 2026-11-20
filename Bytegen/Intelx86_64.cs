using Iced.Intel;
using Lithium.iLC;
using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.IO;
using System.Linq;
using System.Text;
using static Iced.Intel.AssemblerRegisters;

namespace Lithium.Bytegen;

internal class Intelx86_64
{
    Assembler asm = new(64);
    BinaryWriter bin;
    ulong textOffset = 0x1000;
    ulong dataOffset = 0x2000;
    ulong rodataOffset = 0x3000;

    internal Intelx86_64()
    {
        //bin = new BinaryWriter(m);
    }

    internal void Assembly(MemoryStream mem, List<Literal> Literals, List<Function> Functions, MemoryStream m)
    {
        mem = m;
        


    }

    internal Label Label(Function func)
    {
        var label = asm.CreateLabel(func.Name);
        asm.Label(ref label);

        asm.push(rbp);
        asm.mov(rbp, rsp);
        asm.sub(rsp, func.StackSize);

        Body();

        asm.mov(rsp, rbp);
        asm.pop(rbp);
        asm.ret();

        return label;
    }

    internal void Body()
    {

    }

    public static void Log(byte[] machineCode, ulong rip = 0x1000)
    {
        var reader = new ByteArrayCodeReader(machineCode);
        var decoder = Iced.Intel.Decoder.Create(64, reader);
        decoder.IP = rip;

        var formatter = new IntelFormatter();
        formatter.Options.DigitSeparator = "_";
        formatter.Options.FirstOperandCharIndex = 10;

        var output = new StringOutput();
        while (reader.CanReadByte)
        {
            decoder.Decode(out var instr);
            formatter.Format(instr, output);

            string hexBytes = Convert.ToHexString(machineCode.AsSpan((int)(instr.IP - rip), instr.Length));
            Console.WriteLine($"{instr.IP:X8}:  {hexBytes,-22} {output.ToStringAndReset()}");
        }
    }

    public static byte[] GenerateExe(string output)
    {
        byte[] messageBytes = Encoding.ASCII.GetBytes(output);

        const uint TextRva = 0x1000;
        const uint RdataRva = 0x2000;

        // Смещения внутри секции .rdata (RVA):
        // +0x00: IAT[0] -> адрес GetStdHandle
        // +0x08: IAT[1] -> адрес WriteFile
        // +0x10: IAT[2] -> 0 (терминатор)
        // +0x60: Строка сообщения (messageBytes)
        const uint GetStdHandleIatRva = RdataRva + 0x00;
        const uint WriteFileIatRva = RdataRva + 0x08;
        const uint MessageRva = RdataRva + 0x60;

        // 1. Генерация машинного кода x64 с помощью Iced
        var asm = new Assembler(64);
        var zeroLabel = asm.CreateLabel();
        asm.Label(ref zeroLabel);

        int GetSize()
        {
            using var s = new MemoryStream();
            asm.Assemble(new StreamCodeWriter(s), 0);
            return (int)s.Length;
        }

        asm.sub(rsp, 56);

        // call qword ptr [rip + rel32] -> GetStdHandle(-11)
        asm.mov(ecx, -11); // STD_OUTPUT_HANDLE
        asm.call(__qword_ptr[zeroLabel]);
        int dispGetStdHandleOffset = GetSize() - 4;
        int nextRipGetStdHandle = GetSize();

        // lea rdx, [rip + rel32] -> messageBytes
        asm.mov(rcx, rax);
        asm.lea(rdx, __[zeroLabel]);
        int dispMessageOffset = GetSize() - 4;
        int nextRipMessage = GetSize();

        // call qword ptr [rip + rel32] -> WriteFile(...)
        asm.mov(r8d, messageBytes.Length);
        asm.lea(r9, __[rsp + 40]);
        asm.mov(__qword_ptr[rsp + 32], 0);
        asm.call(__qword_ptr[zeroLabel]);
        int dispWriteFileOffset = GetSize() - 4;
        int nextRipWriteFile = GetSize();

        asm.add(rsp, 56);
        asm.ret();

        using var codeStream = new MemoryStream();
        asm.Assemble(new StreamCodeWriter(codeStream), 0);
        byte[] codeBytes = codeStream.ToArray();

        // Подставляем точные 32-битные RIP-смещения: TargetRva - (TextRva + NextInstructionOffset)
        int relGetStdHandle = (int)(GetStdHandleIatRva - (TextRva + (uint)nextRipGetStdHandle));
        int relMessage = (int)(MessageRva - (TextRva + (uint)nextRipMessage));
        int relWriteFile = (int)(WriteFileIatRva - (TextRva + (uint)nextRipWriteFile));

        BitConverter.GetBytes(relGetStdHandle).CopyTo(codeBytes, dispGetStdHandleOffset);
        BitConverter.GetBytes(relMessage).CopyTo(codeBytes, dispMessageOffset);
        BitConverter.GetBytes(relWriteFile).CopyTo(codeBytes, dispWriteFileOffset);

        // 2. Формирование секции .rdata (размер выровнен по 0x200)
        // Автоматически расширяем секцию, если строка длинная
        int rdataSize = Math.Max(0x200, ((0x120 + messageBytes.Length + 0x1FF) / 0x200) * 0x200);
        byte[] rdataSection = new byte[rdataSize];
        using (var rms = new MemoryStream(rdataSection))
        using (var rbw = new BinaryWriter(rms))
        {
            uint iatRva = RdataRva + 0x00;
            uint hintGetStdHandleRva = RdataRva + 0x40;
            uint hintWriteFileRva = RdataRva + 0x50;
            uint dllNameRva = RdataRva + 0x60;

            // IAT (по 8 байт на импорт)
            rms.Position = 0x00;
            rbw.Write((ulong)hintGetStdHandleRva);
            rbw.Write((ulong)hintWriteFileRva);
            rbw.Write(0ul);

            // IDT (IMAGE_IMPORT_DESCRIPTOR - 20 байт)
            rms.Position = 0x18;
            rbw.Write(iatRva);     // OriginalFirstThunk
            rbw.Write(0u);         // TimeDateStamp
            rbw.Write(0u);         // ForwarderChain
            rbw.Write(dllNameRva); // Name RVA ("kernel32.dll")
            rbw.Write(iatRva);     // FirstThunk

            // Hint/Name таблица
            rms.Position = 0x40;
            rbw.Write((ushort)0);
            rbw.Write(Encoding.ASCII.GetBytes("GetStdHandle\0"));

            rms.Position = 0x50;
            rbw.Write((ushort)0);
            rbw.Write(Encoding.ASCII.GetBytes("WriteFile\0"));

            // Имя DLL
            rms.Position = 0x60;
            rbw.Write(Encoding.ASCII.GetBytes("kernel32.dll\0"));

            // Текст сообщения (перенесём на 0x80, чтобы не пересекался с именем DLL)
        }

        // Корректируем RVA сообщения на 0x80 и обновляем смещение в коде
        const uint ActualMessageRva = RdataRva + 0x80;
        Array.Copy(messageBytes, 0, rdataSection, 0x80, messageBytes.Length);
        int actualRelMessage = (int)(ActualMessageRva - (TextRva + (uint)nextRipMessage));
        BitConverter.GetBytes(actualRelMessage).CopyTo(codeBytes, dispMessageOffset);

        // 3. Сборка PE64 файла
        uint rdataVirtualSize = (uint)((rdataSize + 0xFFF) / 0x1000) * 0x1000;
        uint sizeOfImage = RdataRva + rdataVirtualSize;

        byte[] textSection = new byte[0x200];
        Array.Copy(codeBytes, textSection, codeBytes.Length);

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        // --- DOS Header ---
        bw.Write((ushort)0x5A4D); // "MZ"
        ms.Position = 0x3C;
        bw.Write(0x40u);          // e_lfanew

        // --- PE Signature & COFF Header ---
        ms.Position = 0x40;
        bw.Write(0x00004550u);    // "PE\0\0"
        bw.Write((ushort)0x8664); // AMD64
        bw.Write((ushort)2);      // NumberOfSections
        bw.Write(0u);             // TimeDateStamp
        bw.Write(0u);             // PointerToSymbolTable
        bw.Write(0u);             // NumberOfSymbols
        bw.Write((ushort)0xF0);   // SizeOfOptionalHeader (240)
        bw.Write((ushort)0x0022); // Characteristics: EXECUTABLE_IMAGE | LARGE_ADDRESS_AWARE

        // --- Optional Header PE32+ ---
        bw.Write((ushort)0x020B);      // Magic: PE32+
        bw.Write((byte)14);            // MajorLinkerVersion
        bw.Write((byte)0);             // MinorLinkerVersion
        bw.Write(0x200u);              // SizeOfCode
        bw.Write((uint)rdataSize);     // SizeOfInitializedData
        bw.Write(0u);                  // SizeOfUninitializedData
        bw.Write(TextRva);             // AddressOfEntryPoint
        bw.Write(TextRva);             // BaseOfCode
        bw.Write(0x140000000ul);       // ImageBase
        bw.Write(0x1000u);             // SectionAlignment
        bw.Write(0x200u);              // FileAlignment
        bw.Write((ushort)6);           // MajorOperatingSystemVersion
        bw.Write((ushort)0);           // MinorOperatingSystemVersion
        bw.Write((ushort)0);           // MajorImageVersion
        bw.Write((ushort)0);           // MinorImageVersion
        bw.Write((ushort)6);           // MajorSubsystemVersion
        bw.Write((ushort)0);           // MinorSubsystemVersion
        bw.Write(0u);                  // Win32VersionValue
        bw.Write(sizeOfImage);         // SizeOfImage
        bw.Write(0x200u);              // SizeOfHeaders
        bw.Write(0u);                  // CheckSum
        bw.Write((ushort)3);           // Subsystem: WINDOWS_CUI (Console)
        bw.Write((ushort)0x8160);      // DllCharacteristics (ASLR | NX | TSA)
        bw.Write(0x100000ul);          // SizeOfStackReserve
        bw.Write(0x1000ul);            // SizeOfStackCommit
        bw.Write(0x100000ul);          // SizeOfHeapReserve
        bw.Write(0x1000ul);            // SizeOfHeapCommit
        bw.Write(0u);                  // LoaderFlags
        bw.Write(16u);                 // NumberOfRvaAndSizes

        // --- Data Directories (16 * 8 байт) ---
        bw.Write(0ul);                 // 0: Export
        bw.Write(RdataRva + 0x18);     // 1: Import Table RVA
        bw.Write(40u);                 // 1: Import Table Size
        for (int i = 2; i < 16; i++)
        {
            if (i == 12)               // 12: IAT Directory
            {
                bw.Write(RdataRva);
                bw.Write(24u);
            }
            else
            {
                bw.Write(0ul);
            }
        }

        // --- Section Headers (2 * 40 байт) ---
        // 1. .text
        bw.Write(Encoding.ASCII.GetBytes(".text\0\0\0"));
        bw.Write(0x200u);              // VirtualSize
        bw.Write(TextRva);             // VirtualAddress
        bw.Write(0x200u);              // SizeOfRawData
        bw.Write(0x200u);              // PointerToRawData
        bw.Write(0u); bw.Write(0u); bw.Write((ushort)0); bw.Write((ushort)0);
        bw.Write(0x60000020u);         // CODE | EXECUTE | READ

        // 2. .rdata
        bw.Write(Encoding.ASCII.GetBytes(".rdata\0\0"));
        bw.Write((uint)rdataSize);     // VirtualSize
        bw.Write(RdataRva);            // VirtualAddress
        bw.Write((uint)rdataSize);     // SizeOfRawData
        bw.Write(0x400u);              // PointerToRawData
        bw.Write(0u); bw.Write(0u); bw.Write((ushort)0); bw.Write((ushort)0);
        bw.Write(0x40000040u);         // INITIALIZED_DATA | READ

        // --- Raw Data секций ---
        ms.Position = 0x200;
        bw.Write(textSection);
        bw.Write(rdataSection);

        return ms.ToArray();
    }
}