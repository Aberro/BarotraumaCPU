using System;
namespace Barotrauma.Items.Components.Signals.Controller;

[AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
public class OperandsAttribute : Attribute
{
    public OpKind[] Kinds { get; }

    public OperandsAttribute(params OpKind[] kinds)
    {
        if (kinds.Length > 3)
            throw new InvalidProgramException("Commands cannot have more than three operands!");
        Kinds = kinds;
    }
}

[Flags]
public enum ProcessorState
{
    /// <summary>
    /// Processor is stopped, no instructions are read nor executed.
    /// </summary>
    Stopped = 0,
    /// <summary>
    /// Last cycle was interrupted by memory fetching or nop.
    /// </summary>
    Underloaded = 0x2,
    /// <summary>
    /// Processor is working.
    /// </summary>
    Working = 0x1,
}

[Flags]
public enum DebugMode
{
    /// <summary>
    /// No debug mode selected.
    /// </summary>
    None = 0,
    /// <summary>
    /// Processor executes only one operation per cycle and then stops.
    /// </summary>
    StepByStep,
    /// <summary>
    /// Processor outputs every command and it's values by trying to write debug info string to 0xffffffff address.
    /// </summary>
    Verbose,
}

/// <summary>
/// Kind of operand.
/// </summary>
[Flags]
public enum OpKind : ushort
{
    /// <summary>
    /// No operand
    /// </summary>
    none = 0x0000,
    /// <summary>
    /// Integer registers, read & write
    /// </summary>
    irx = 0x0001,
    /// <summary>
    /// Floating point registers, read & write
    /// </summary>
    frx = 0x0002,
    /// <summary>
    /// String registers, read & write
    /// </summary>
    srx = 0x0004,
    /// <summary>
    /// Input channel, read only
    /// </summary>
    inx = 0x0008,
    /// <summary>
    /// Output channel, write only
    /// </summary>
    oux = 0x0010,
    /// <summary>
    /// Integer literal, read only
    /// </summary>
    i = 0x0020,
    /// <summary>
    /// Floating point literal, read only
    /// </summary>
    f = 0x0040,
    /// <summary>
    /// String literal, read only
    /// </summary>
    s = 0x0080,
    /// <summary>
    /// Integer memory address value, read & write
    /// </summary>
    im = 0x0100,
    /// <summary>
    /// Floating point memory address value, read & write
    /// </summary>
    fm = 0x0200,
    /// <summary>
    /// String memory address value, read & write
    /// </summary>
    sm = 0x0400,
    /// <summary>
    /// Special flag, used to specify that operand would be read by command
    /// </summary>
    r = 0x4000,
    /// <summary>
    /// Special flag, used to specify that operand would be written to by command
    /// </summary>
    w = 0x8000,
    /// <summary>
    /// Special flag, used to specify that operand would be both read and written by command
    /// </summary>
    rw = r | w,
    /// <summary>
    /// Any readable integer value
    /// </summary>
    anyir = irx | inx | i | im,
    /// <summary>
    /// Any readable integer value except input channels (usually used for arg1, where inx are forbidden)
    /// </summary>
    anyirni = irx | i | im,
    /// <summary>
    /// Any writeable integer value
    /// </summary>
    anyiw = irx | oux | im,
    /// <summary>
    /// Any integer value
    /// </summary>
    anyi = anyir | anyiw,
    /// <summary>
    /// Any readable and writeable integer value
    /// </summary>
    anyirw = anyir & anyiw,
    /// <summary>
    /// Any readable floating point value
    /// </summary>
    anyfr = frx | inx | f | fm,
    /// <summary>
    /// Any writeable floating point value
    /// </summary>
    anyfw = frx | oux | fm,
    /// <summary>
    /// Any floating point value
    /// </summary>
    anyf = anyfr | anyfw | fm,
    /// <summary>
    /// Any readable and writeable floating point value
    /// </summary>
    anyfrw = anyfr & anyfw,
    /// <summary>
    /// Any readable string value
    /// </summary>
    anysr = srx | inx | s | sm,
    /// <summary>
    /// Any writeable string value
    /// </summary>
    anysw = srx | oux | sm,
    /// <summary>
    /// Any string value
    /// </summary>
    anys = anysr | anysw,
    /// <summary>
    /// Any readable and writeable string value
    /// </summary>
    anysrw = anysr & anysw,
    /// <summary>
    /// Any readable value
    /// </summary>
    anyr = anyir | anyfr | anysr,
    /// <summary>
    /// Any writeable value
    /// </summary>
    anyw = anyiw | anyfw | anysw,
    /// <summary>
    /// Any readable and writeable value
    /// </summary>
    anyrw = anyr & anyw,
    /// <summary>
    /// Any readable integer or floating point value
    /// </summary>
    anynsr = anyir | anyfr,
    /// <summary>
    /// Any writeable integer or floating point value
    /// </summary>
    anynsw = anyiw | anyfw,
    /// <summary>
    /// Any integer or floating point value
    /// </summary>
    anyns = anynsr | anynsw,
    /// <summary>
    /// Any readable and writeable integer or floating point value
    /// </summary>
    anynsrw = anynsr & anynsw,
    /// <summary>
    /// Any readable integer or string value
    /// </summary>
    anynfr = anyir | anysr,
    /// <summary>
    /// Any writeable integer or string value
    /// </summary>
    anynfw = anyiw | anysw,
    /// <summary>
    /// Any integer or string value
    /// </summary>
    anynf = anyi | anys,
    /// <summary>
    /// Any readable and writeable integer or string value
    /// </summary>
    anynfrw = anyirw | anysrw,
    /// <summary>
    /// Any readable value except input channels (usually used for arg1, where inx are forbidden)
    /// </summary>
    anyrni = anynfr & ~inx,
    /// <summary>
    /// Any value
    /// </summary>
    any = 0xFFFF,
}

[Flags]
public enum ValueKind : byte
{
    Unknown = 0x00,
    Int = 0x01,
    Float = 0x02,
    String = 0x04,
    NotInt = Float | String,
    NotFloat = Int | String,
    NotString = Int | Float,
    Any = Int | Float | String,
}

/// <summary>
/// Enumeration of all processor commands with their value kinds and flags defining if operand is for read/write during performing of command.
/// </summary>
/// <remarks>
/// Every command may have up to 3 operands.
/// Every command should have Operands attribute defined.
/// Only the first operand may be writeable.
/// Only first two operands may be of reference kind.
/// First operand may never be of inx kind, second and third operands may never be of oux kind.
/// First operand may be reference kind, referenced by inx register.
/// Only one reference kind may be used for read,
/// if command supports two reference operands, first one should be used as write-only.
/// </remarks>
public enum Ops : byte
{
    // No operation:
    /// <summary>
    /// No operation.
    /// </summary>
    [Operands()]
    nop,

    // Data movement:
    /// <summary>
    /// Move value of arg2 into arg1. Also prevents any further commands fetching at current clock cycle.
    /// </summary>
    [Operands(OpKind.anyw | OpKind.w, OpKind.anyr | OpKind.r)]
    mov,
    // Arithmetic (changes flags):
    /// <summary>
    /// Set value of arg1 to sum of arg1 and arg2.
    /// </summary>
    [Operands(OpKind.anyrw | OpKind.rw, OpKind.anyr | OpKind.r)]
    add,
    /// <summary>
    /// Set value of arg1 to sum of arg1 and arg2 plus value of carry flag.
    /// </summary>
    [Operands(OpKind.anyirw | OpKind.rw, OpKind.anyir | OpKind.r)]
    adc,
    /// <summary>
    /// Set value of arg1 to subtraction of arg2 from arg1.
    /// </summary>
    [Operands(OpKind.anyrw | OpKind.rw, OpKind.anyr | OpKind.r)]
    sub,
    /// <summary>
    /// Perform subtraction of arg2 from arg1, but does not store result, only set flags register.
    /// </summary>
    // exclude inx, because here we could use any readable operand, but inx operands are forbidden to be used in arg1 position.
    [Operands(OpKind.anyrni | OpKind.r, OpKind.anyr | OpKind.r)]
    cmp,
    /// <summary>
    /// Increment value of arg1 by 1.
    /// </summary>
    [Operands(OpKind.anynsrw | OpKind.rw)]
    inc,
    /// <summary>
    /// Decrement value of arg1 by 1.
    /// </summary>
    [Operands(OpKind.anynsrw | OpKind.rw)]
    dec,
    /// <summary>
    /// set value of arg1 to multiplication of arg1 and arg2.
    /// </summary>
    [Operands(OpKind.anynsrw | OpKind.rw, OpKind.anynsr | OpKind.r)]
    mul,
    /// <summary>
    /// Set value of arg1 to division of arg1 by arg2.
    /// </summary>
    [Operands(OpKind.anynsrw | OpKind.rw, OpKind.anynsr | OpKind.r)]
    div,

    // Bitwise(changes flags):
    /// <summary>
    /// Bitwise(or char-wise for string value) shift left by arg2 bits, new bits(or chars) are set to 0.
    /// </summary>
    [Operands(OpKind.anynfrw | OpKind.rw, OpKind.anyir | OpKind.r)]
    shl,
    /// <summary>
    /// Bitwise(or char-wise for string value) shift right by arg2 bits, new bits(or chars) are set to 0.
    /// </summary>
    [Operands(OpKind.anynfrw | OpKind.rw, OpKind.anyir | OpKind.r)]
    shr,
    /// <summary>
    /// Bitwise(or char-wise for string value) shift left by arg2 bits, new bits(or chars) are set by shifted out bits(or chars).
    /// </summary>
    [Operands(OpKind.anynfrw | OpKind.rw, OpKind.anyir | OpKind.r)]
    rol,
    /// <summary>
    /// Bitwise(or char-wise for string value) shift right by arg2 bits, new bits(or chars) are set by shifted out bits(or chars).
    /// </summary>
    [Operands(OpKind.anynfrw | OpKind.rw, OpKind.anyir | OpKind.r)]
    ror,

    // Logical(changes flags):
    /// <summary>
    /// Performs bitwise logical AND between arg1 and arg2 and stores result in arg1.
    /// </summary>
    [Operands(OpKind.anyirw | OpKind.rw, OpKind.anyir | OpKind.r)]
    and,
    /// <summary>
    /// Performs bitwise logical OR between arg1 and arg2 and stores result in arg1.
    /// </summary>
    [Operands(OpKind.anyirw | OpKind.rw, OpKind.anyir | OpKind.r)]
    or,
    /// <summary>
    /// Performs bitwise logical XOR between arg1 and arg2 and stores result in arg1.
    /// </summary>
    [Operands(OpKind.anyirw | OpKind.rw, OpKind.anyir | OpKind.r)]
    xor,
    /// <summary>
    /// Inverts all bits of arg1.
    /// </summary>
    [Operands(OpKind.anyirw | OpKind.rw)]
    not,
    /// <summary>
    /// Performs bitwise logical AND between arg1 and arg2, but result is thrown away.
    /// </summary>
    [Operands(OpKind.anyirni | OpKind.r, OpKind.anyir | OpKind.r)]
    test,

    // Control flow:
    /// <summary>
    /// Break command, switched processor to stopped mode and sends debug info about previous command by writing debug info string to 0xffffffff address.
    /// </summary>
    [Operands()]
    brk,
    /// <summary>
    /// Read state of input channels and set bits of arg1 accordingly (bit 0 is set when channel 0 has value)
    /// </summary>
    [Operands(OpKind.anyiw | OpKind.w)]
    inr, 
    /// <summary>
    /// Read state of flags into arg1 in following order from most to less significant bit: OF, SF, ZF, CF.
    /// </summary>
    /// <remarks>
    /// All bits in arg1 are modified, but only 4 least significant bits have meaningful value.
    /// </remarks>
    [Operands(OpKind.anyiw | OpKind.w)]
    flr,
    /// <summary>
    /// Write state of flags from arg1 in following order from most to less significant bit: OF, SF, ZF, CF.
    /// </summary>
    /// <remarks>
    /// Only 4 least significant bits are read.
    /// </remarks>
    [Operands(OpKind.anyir | OpKind.r)]
    fls,
    /// <summary>
    /// Continues execution from given operation.
    /// </summary>
    [Operands(OpKind.anyir | OpKind.r)]
    jmp,
    /// <summary>
    /// Continues execution from given operation if ZF=1.
    /// </summary>
    [Operands(OpKind.anyir | OpKind.r)]
    je,
    /// <summary>
    /// Continues execution from given operation if ZF=0.
    /// </summary>
    [Operands(OpKind.anyir | OpKind.r)]
    jnz,
    /// <summary>
    /// Continues execution from given operation if ZF=0.
    /// </summary>
    [Operands(OpKind.anyir | OpKind.r)]
    jne,
    /// <summary>
    /// Continues execution from given operation if ZF=0 and SF=OF.
    /// </summary>
    [Operands(OpKind.anyir | OpKind.r)]
    jg,
    /// <summary>
    /// Continues execution from given operation if SF=OF.
    /// </summary>
    [Operands(OpKind.anyir | OpKind.r)]
    jge,
    /// <summary>
    /// Continues execution from given operation if SF<>OF.
    /// </summary>
    [Operands(OpKind.anyir | OpKind.r)]
    jl,
    /// <summary>
    /// Continues execution from given operation if ZF=1 or SF<>OF.
    /// </summary>
    [Operands(OpKind.anyir | OpKind.r)]
    jle,

    // Data conversion:
    /// <summary>
    /// Convert arg2 as integer value to floating point value and set result to arg1.
    /// </summary>
    [Operands(OpKind.anyfw | OpKind.w, OpKind.anyir | OpKind.r)]
    mvi2f,
    /// <summary>
    /// Converts arg2 as integer value to string value and set result to arg1.
    /// </summary>
    [Operands(OpKind.anysw | OpKind.w, OpKind.anyir | OpKind.r)]
    mvi2s,
    /// <summary>
    /// Convert arg2 as floating point value to integer value and set result to arg1.
    /// </summary>
    [Operands(OpKind.anyiw | OpKind.w, OpKind.anyfr | OpKind.r)]
    mvf2i,
    /// <summary>
    /// Converts arg2 as floating point value to string value and set result to arg1.
    /// </summary>
    [Operands(OpKind.anysw | OpKind.w, OpKind.anyfr | OpKind.r)]
    mvf2s,
    /// <summary>
    /// Convert arg2 as string value to integer value and set result to arg1.
    /// </summary>
    [Operands(OpKind.anyiw | OpKind.w, OpKind.anysr | OpKind.r)]
    mvs2i,
    /// <summary>
    /// Convert arg2 as string value to floating point value and set result to arg1.
    /// </summary>
    [Operands(OpKind.anyfw | OpKind.w, OpKind.anysr | OpKind.r)]
    mvs2f,
    /// <summary>
    /// Perform direct conversion of arg2 from integer to floating point value and stores result in arg1.
    /// </summary>
    [Operands(OpKind.anyfw | OpKind.w, OpKind.anyir | OpKind.r)]
    ldi2f,
    /// <summary>
    /// Perform direct conversion of arg2 from floating point value to integer and stores result in arg1.
    /// </summary>
    [Operands(OpKind.anyiw | OpKind.w, OpKind.anyfr | OpKind.r)]
    ldf2i,

    // String operations:
    /// <summary>
    /// Find first occurence of arg3 in arg2 and store it's index in arg1.
    /// </summary>
    [Operands(OpKind.anyiw | OpKind.w, OpKind.anysr | OpKind.r, (OpKind.anysr & ~OpKind.sm) | OpKind.r)] // arg3 can't be a memory reference.
    find,
    /// <summary>
    /// Remove any occurence of arg3 from arg2 and store resulting string in arg1.
    /// </summary>
    [Operands(OpKind.anysw | OpKind.w, OpKind.anysr | OpKind.r, (OpKind.anysr & ~OpKind.sm) | OpKind.r)]
    rmv,
    /// <summary>
    /// Store substring of string arg1 starting at index arg2 with length of arg3 in arg1.
    /// </summary>
    [Operands(OpKind.anysw | OpKind.rw, OpKind.anyir | OpKind.r, (OpKind.anyir & ~OpKind.im) | OpKind.r)]
    sbs,
    /// <summary>
    /// Replace any occurence of arg2 in arg1 by arg3 and store resulting string in arg1.
    /// </summary>
    [Operands(OpKind.anysrw | OpKind.rw, OpKind.anysr | OpKind.r, (OpKind.anysr & ~OpKind.sm) | OpKind.r)]
    rpl,
    /// <summary>
    /// Get character at index arg3 in string arg2 and store it's value as integer at arg1.
    /// </summary>
    [Operands(OpKind.anyiw | OpKind.w, OpKind.anysr | OpKind.r, (OpKind.anyir & ~OpKind.im) | OpKind.r)]
    chr,
}

public enum Reg : byte
{
    none,
    [Operands(OpKind.irx)]
    ir0,
    [Operands(OpKind.irx)]
    ir1,
    [Operands(OpKind.irx)]
    ir2,
    [Operands(OpKind.irx)]
    ir3,
    [Operands(OpKind.irx)]
    ir4,
    [Operands(OpKind.irx)]
    ir5,
    [Operands(OpKind.irx)]
    ir6,
    [Operands(OpKind.irx)]
    ir7,
    [Operands(OpKind.frx)]
    fr0,
    [Operands(OpKind.frx)]
    fr1,
    [Operands(OpKind.frx)]
    fr2,
    [Operands(OpKind.frx)]
    fr3,
    [Operands(OpKind.frx)]
    fr4,
    [Operands(OpKind.frx)]
    fr5,
    [Operands(OpKind.frx)]
    fr6,
    [Operands(OpKind.frx)]
    fr7,
    [Operands(OpKind.srx)]
    sr0,
    [Operands(OpKind.srx)]
    sr1,
    [Operands(OpKind.srx)]
    sr2,
    [Operands(OpKind.srx)]
    sr3,
    [Operands(OpKind.srx)]
    sr4,
    [Operands(OpKind.srx)]
    sr5,
    [Operands(OpKind.srx)]
    sr6,
    [Operands(OpKind.srx)]
    sr7,
    [Operands(OpKind.oux)]
    ou0,
    [Operands(OpKind.oux)]
    ou1,
    [Operands(OpKind.oux)]
    ou2,
    [Operands(OpKind.oux)]
    ou3,
    [Operands(OpKind.inx)]
    in0,
    [Operands(OpKind.inx)]
    in1,
    [Operands(OpKind.inx)]
    in2,
    [Operands(OpKind.inx)]
    in3,
}

public struct OpCode
{
    public int SourceLine;
    public Ops Operation;
    public bool IsArg1Ref;
    public bool IsArg2Ref;
    public Reg Arg1;
    public Reg Arg2;
    public Reg Arg3;
    public object Literal1;
    public object Literal2;
    public object Literal3;
}
