using System;
using System.ComponentModel;
using System.Linq;
using System.Resources;
using System.Runtime.InteropServices;
using RestSharp.Extensions;

namespace Barotrauma.Items.Components.Signals.Controller;

/// <summary>
/// Commands processor, a state machine implementation.
/// </summary>
/// <remarks>
/// Processor performs multiple operation per one clock cycle, defined by Multiplier.
/// If any operation during cycle requires reading from memory, cycle ends at such
/// operation, and processor stops execution until it receives requested memory value.
/// Each operation may have only one memory reading.
/// Processor only able to send out signal once per channel per cycle, so if there's
/// multiple commands that writes to same output channel, processor will only perform
/// one such command in one cycle, ending cycle once it receives another command
/// writing to same channel. It's ok to send signals to different channel though.
/// Processor is unable to read previously sent signals from output channel.
/// Processor is able to receive and store values from 4 input channel, so it's not
/// necessary to read channel values every cycle (though, it's possible that new
/// channel input could override previously received and unread value).
/// Reading from any input channel resets it's value, so it's only valid to
/// read from input channel once per cycle. Next readings in same cycle would result
/// in fetching failure and stall processor until value is received and fetched.
/// One significant difference with x86 processor is that most commands modifies
/// flags, the only ones that doesn't is the ones that do no arithmetics nor write any
/// value into arg1. So keep that in mind when writing jxx commands.
/// Processor is able to output debug info by writing it as a string to 0xffffffff
/// memory address.
/// </remarks>
public class Processor
{
    // I prefer to use different naming style here at least internally, just for more like C style.
    // Because I can and because it's neat to write processor in C like style)
    private struct Operation
    {
        public Ops operation; // operation to perform
        public bool fetched; // is operation fetched
        public ValueKind out_t; // type of output value
        public ulong out_i; // integer value result of operation
        public float out_f; // floating point result of operation
        public string out_s; // string result of operation
        public ValueKind arg1_t; // type of output value
        public uint arg1_i; // arg1 as integer value
        public float arg1_f; // arg1 as floating point value
        public string arg1_s; // arg1 as string value
        public ValueKind arg2_t; // type of output value
        public uint arg2_i; // arg2 as integer value
        public float arg2_f; // arg2 as floating point value
        public string arg2_s; // arg2 as string value
        public ValueKind arg3_t; // type of output value
        public uint arg3_i; // arg3 as integer value
        public float arg3_f; // arg3 as floating point value
        public string arg3_s; // arg3 as string value
    }
    [StructLayout(LayoutKind.Explicit)]
    struct Converter
    {
        [FieldOffset(0)] private float f;
        [FieldOffset(0)] private uint u;
        [FieldOffset(0)] private int i;
        [FieldOffset(0)] private char c;

        public static int ToInt32(uint value) => new Converter { u = value }.i;
        public static int ToInt32(float value) => new Converter { f = value }.i;
        public static int ToInt32(char value) => new Converter { c = value }.i;

        public static uint ToUInt32(float value) => new Converter { f = value }.u;
        public static uint ToUInt32(int value) => new Converter { i = value }.u;
        public static uint ToUInt32(char value) => new Converter { c = value }.u;

        public static float ToSingle(uint value) => new Converter { u = value }.f;
        public static float ToSingle(int value) => new Converter { i = value }.f;
        public static float ToSingle(char value) => new Converter { c = value }.f;
    }
    private delegate void OpAction(ref Operation op);

    public const int RegistersCount = 8;
    public const int InputChannelsCount = 4;
    public const int OutputChannelsCount = 4;

    private static readonly Int32Converter IntConverter = new();
    private static readonly SingleConverter FloatConverter = new();
    private static readonly byte[] OperandNumbers; // cache for number of operands for each command
    private static readonly OpKind[][] OperandKinds; // cache for operand kinds and readability/writeability for each command
    private static readonly int[] RegistersIndexMapping; // cache for register/channel index in registers/channels arrays.
    private static readonly OpKind[] RegistersKindMapping; // cache for register/channel kinds.
    private static readonly OpCode Nop = new OpCode
    {
        Operation = Ops.nop,
        SourceLine = -1,
    };

    /// <summary>
    /// Carry flag
    /// </summary>
    private bool CF;
    /// <summary>
    /// Zero flag
    /// </summary>
    private bool ZF;
    /// <summary>
    /// Sign flag
    /// </summary>
    private bool SF;
    /// <summary>
    /// Overflow flag
    /// </summary>
    private bool OF;
    private int[] ir_registers = new int[RegistersCount];
    private float[] fr_registers = new float[RegistersCount];
    private string[] sr_registers = new string[RegistersCount];
    private int[] in_int = new int[InputChannelsCount];
    private float[] in_float = new float[InputChannelsCount];
    private string[] in_str = new string[InputChannelsCount];
    private bool[] in_set = new bool[InputChannelsCount];
    private int[] out_int = new int[OutputChannelsCount];
    private float[] out_float = new float[OutputChannelsCount];
    private string[] out_str = new string[OutputChannelsCount];
    private ValueKind[] out_kind = new ValueKind[OutputChannelsCount];
    private bool[] out_set = new bool[OutputChannelsCount];
    private OpCode[] program = Array.Empty<OpCode>();
    private uint ip_register;
    private int fetched;
    private string memory_val;
    private bool memory_read;
    private uint memory_requested;

    /// <summary>
    /// State of integer registers.
    /// </summary>
    public ReadOnlySpan<int> IRX => ir_registers;
    /// <summary>
    /// State of floating point registers.
    /// </summary>
    public ReadOnlySpan<float> FRX => fr_registers;
    /// <summary>
    /// State of string registers.
    /// </summary>
    public ReadOnlySpan<string> SRX => sr_registers;
    /// <summary>
    /// State of input channels interpreted as integer values.
    /// </summary>
    public ReadOnlySpan<int> IINX => in_int;
    /// <summary>
    /// State of input channels interpreted as floating point values.
    /// </summary>
    public ReadOnlySpan<float> FINX => in_float;
    /// <summary>
    /// State of input channels uninterpreted.
    /// </summary>
    public ReadOnlySpan<string> SINX => in_str;
    /// <summary>
    /// State of input channels (whether there are unread signals).
    /// </summary>
    public ReadOnlySpan<bool> INX => in_set;
    /// <summary>
    /// Last integer values sent by output channels.
    /// </summary>
    public ReadOnlySpan<int> IOUX => out_int;
    /// <summary>
    /// Last floating point values sent by output channels.
    /// </summary>
    public ReadOnlySpan<float> FOUX => out_float;
    /// <summary>
    /// Last string values sent by output channels.
    /// </summary>
    public ReadOnlySpan<string> SOUX => out_str;
    /// <summary>
    /// Kind of last values sent by output channels.
    /// </summary>
    public ReadOnlySpan<ValueKind> KOUX => out_kind;
    /// <summary>
    /// Frequency multiplier, amount of instructions processor tries to do in one clock cycle.
    /// </summary>
    public byte Multiplier { get; set; } = 8;
    public DebugMode Debug { get; set; }
    public ProcessorState State { get; private set; }
    /// <summary>
    /// Raised when processor writes given value to a memory cell at given address.
    /// </summary>
    public event Action<uint, string> MemoryWrite;
    /// <summary>
    /// Raised when processor tries to acquire value from a given memory cell.
    /// Handler should always invoke <see cref="Memory"/> method to pass memory data, even if memory address was invalid.
    /// Otherwise, processor would hang, trying to acquire memory data at each new cycle.
    /// </summary>
    public event Action<uint> MemoryRead;
    /// <summary>
    /// Raised when processor tries to write given value at given output channel.
    /// </summary>
    public event Action<uint, string> ChannelWrite;

    static Processor()
    {
        OperandNumbers = typeof(Ops).GetEnumNames()
            .Select(x => (byte)(typeof(Ops).GetMember(x).First().GetAttribute<OperandsAttribute>()?.Kinds.Length ?? 0)).ToArray();
        OperandKinds = typeof(Ops).GetEnumNames()
            .Select(x => typeof(Ops).GetMember(x).First().GetAttribute<OperandsAttribute>()!.Kinds).ToArray();

        // This array is filled manually and should always be in sync with Reg enumeration.
        RegistersIndexMapping = new[]
        {
                -1 /*none*/,
                0 /*ir0*/, 1 /*ir1*/, 2 /*ir2*/, 3 /*ir3*/, 4 /*ir4*/, 5 /*ir5*/, 6 /*ir6*/, 7 /*ir7*/,
                0 /*fr0*/, 1 /*fr1*/, 2 /*fr2*/, 3 /*fr3*/, 4 /*fr4*/, 5 /*fr5*/, 6 /*fr6*/, 7 /*fr7*/,
                0 /*sr0*/, 1 /*sr1*/, 2 /*sr2*/, 3 /*sr3*/, 4 /*sr4*/, 5 /*sr5*/, 6 /*sr6*/, 7 /*sr7*/,
                0 /*ou0*/, 1 /*ou1*/, 2 /*ou2*/, 3 /*ou3*/,
                0 /*in0*/, 1 /*in1*/, 2 /*in2*/, 3 /*in3*/,
            };
        RegistersKindMapping = new[]
        {
                OpKind.none /*none*/,
                OpKind.irx /*ir0*/, OpKind.irx /*ir1*/, OpKind.irx /*ir2*/, OpKind.irx /*ir3*/, OpKind.irx /*ir4*/, OpKind.irx /*ir5*/, OpKind.irx /*ir6*/, OpKind.irx /*ir7*/,
                OpKind.frx /*fr0*/, OpKind.frx /*fr1*/, OpKind.frx /*fr2*/, OpKind.frx /*fr3*/, OpKind.frx /*fr4*/, OpKind.frx /*fr5*/, OpKind.frx /*fr6*/, OpKind.frx /*fr7*/,
                OpKind.srx /*sr0*/, OpKind.srx /*sr1*/, OpKind.srx /*sr2*/, OpKind.srx /*sr3*/, OpKind.srx /*sr4*/, OpKind.srx /*sr5*/, OpKind.srx /*sr6*/, OpKind.srx /*sr7*/,
                OpKind.oux /*ou0*/, OpKind.oux /*ou1*/, OpKind.oux /*ou2*/, OpKind.oux /*ou3*/,
                OpKind.inx /*in0*/, OpKind.inx /*in1*/, OpKind.inx /*in2*/, OpKind.inx /*in3*/,
            };
    }

    public Processor()
    {
        program = Array.Empty<OpCode>();
        Reset();
    }

    /// <summary>
    /// This method used to pass value of the last requested memory cell.
    /// </summary>
    /// <param name="value"></param>
    public void Memory(string value)
    {
        memory_val = value;
        memory_read = true;
    }
    /// <summary>
    /// This method used to pass value of input channel at given index.
    /// </summary>
    /// <param name="channel_number"></param>
    /// <param name="value"></param>
    public void Channel(uint channel_number, string value)
    {
        if (channel_number > 4)
            throw new ArgumentOutOfRangeException(nameof(channel_number));
        in_int[channel_number] = (int)((value != null && IntConverter.IsValid(value) ? IntConverter.ConvertFrom(value) : 0) ?? 0);
        in_float[channel_number] = (float)((value != null && FloatConverter.IsValid(value) ? FloatConverter.ConvertFrom(value) : 0) ?? 0);
        in_str[channel_number] = value;
        in_set[channel_number] = true;
    }
    /// <summary>
    /// Load commands set into processor's memory.
    /// </summary>
    /// <param name="program"></param>
    public void Load(OpCode[] program)
    {
        this.program = program;
        Reset();
    }

    /// <summary>
    /// Reset processor to it's initial state: nullify all registers, clear all flags, set ip_register to first operation.
    /// </summary>
    public void Reset()
    {
        ir_registers = new int[RegistersCount];
        fr_registers = new float[RegistersCount];
        sr_registers = new string[RegistersCount];
        in_int = new int[4];
        in_float = new float[4];
        in_str = new string[4];
        in_set = new bool[4];
        out_int = new int[4];
        out_float = new float[4];
        out_str = new string[4];
        out_kind = new ValueKind[4];
        out_set = new bool[4];
        ip_register = 0;
        CF = false;
        ZF = false;
        SF = false;
        OF = false;
    }

    /// <summary>
    /// Start execution.
    /// </summary>
    public void Start()
    {
        State = ProcessorState.Working;
    }

    /// <summary>
    /// Stop execution.
    /// </summary>
    public void Stop()
    {
        State &= ~ProcessorState.Working;
    }

    /// <summary>
    /// Initiate clock cycle.
    /// </summary>
    public void Cycle()
    {
        if (State is ProcessorState.Stopped)
            return;
        for (var i = 0; i < Multiplier; i++)
        {
            // ensure we're still in program scope
            OpCode code = ip_register < program.Length ? program[ip_register++] : Nop;
            if (code.Operation is not Ops.nop)
            {
                if (Fetch(ref code, out var operation))
                {
                    Execute(ref operation, ref code);
                    if (Write(ref code, ref operation))
                        break; // Memory write
                }
                else
                {
                    ip_register--;
                    break;
                }
            }
            else break;
            if ((Debug & DebugMode.StepByStep) != 0)
            {
                Stop();
                break;
            }
        }
        SendChannels();
    }

    private void SendChannels()
    {
        for (uint i = 0; i < 4; i++)
            if (out_set[i])
            {
                var kind = out_kind[i];
                ChannelWrite?.Invoke(i, (kind & ValueKind.Float) != ValueKind.Unknown
                    ? out_float[i].ToString()
                    : (kind & ValueKind.Int) != ValueKind.Unknown
                        ? out_int[i].ToString()
                        : out_str[i]);
                out_set[i] = false;
            }
    }

    private bool Fetch(ref OpCode op_code, out Operation result)
    {
        // This may seem like a lot of code to execute in very performance critical section,
        // but in actuality, it's mostly switch cases and just a bunch of lines executing each time.
        // And most of checking are optimized by cached arrays.
        var op = (int)op_code.Operation;
        var operands = OperandNumbers[op];
        result = new()
        {
            operation = op_code.Operation,
            fetched = true,
        };
        if (operands >= 1)
        {
            // Most commands both read and write to arg1, but some only write, so ensure we actually need to read arg1.
            if ((OperandKinds[op][0] & OpKind.r) != OpKind.none)
            {
                if (op_code is { IsArg1Ref: true })
                {
                    var mem = op_code.Literal1 != null ? (int)op_code.Literal1 : ir_registers[RegistersIndexMapping[(int)op_code.Arg1]];
                    if (mem == memory_requested && memory_read)
                    {
                        if (IntConverter.ConvertFromString(memory_val) is int i)
                        {
                            result.arg1_t |= ValueKind.Int;
                            result.arg1_i = Converter.ToUInt32(i);
                        }
                        result.arg1_t |= ValueKind.String;
                        result.arg1_s = memory_val;
                    }
                    else
                    {
                        memory_requested = Converter.ToUInt32(mem);
                        memory_read = false;
                        MemoryRead?.Invoke(memory_requested);
                        result.fetched = false;
                    }
                }
                else
                {
                    switch (op_code.Literal1)
                    {
                        case int i:
                            result.arg1_t |= ValueKind.Int;
                            result.arg1_i = Converter.ToUInt32(i);
                            break;
                        case float f:
                            result.arg1_t |= ValueKind.Float;
                            result.arg1_f = f;
                            break;
                        case string s:
                            result.arg1_t |= ValueKind.String;
                            result.arg1_s = s;
                            break;
                        default:
                            var reg = (int)op_code.Arg1;
                            var idx = RegistersIndexMapping[reg];
                            switch (RegistersKindMapping[reg])
                            {
                                case OpKind.irx:
                                    result.arg1_t |= ValueKind.Int;
                                    result.arg1_i = Converter.ToUInt32(ir_registers[idx]);
                                    break;
                                case OpKind.frx:
                                    result.arg1_t |= ValueKind.Float;
                                    result.arg1_f = fr_registers[idx];
                                    break;
                                case OpKind.srx:
                                    result.arg1_t |= ValueKind.String;
                                    result.arg1_s = sr_registers[idx];
                                    break;
                                case OpKind.inx:
                                    result.fetched = in_set[idx];
                                    in_set[idx] = false;
                                    var i = in_int[idx];
                                    result.arg1_t |= ValueKind.Int;
                                    result.arg1_i = Converter.ToUInt32(i);
                                    var f = in_float[idx];
                                    result.arg1_t |= ValueKind.Float;
                                    result.arg1_f = f;
                                    var s = in_str[idx];
                                    result.arg1_t |= ValueKind.String;
                                    result.arg1_s = s;
                                    (in_int[idx], in_float[idx], in_str[idx]) = (0, 0, null);
                                    break;
                            }
                            break;
                    }
                }
            }
            else if ((OperandKinds[op][0] & OpKind.w) != OpKind.none)
            {
                var reg = (int)op_code.Arg1;
                if (RegistersKindMapping[reg] == OpKind.oux && out_set[RegistersIndexMapping[reg]])
                    result.fetched = false;
            }
        }
        else
            return true; // no operands
        if (operands >= 2)
        {
            if (op_code is { IsArg2Ref: true })
            {
                var mem = op_code.Literal2 != null ? (int)op_code.Literal2 : ir_registers[RegistersIndexMapping[(int)op_code.Arg2]];
                if (mem == memory_requested && memory_read)
                {
                    if (IntConverter.IsValid(memory_val) && IntConverter.ConvertFromString(memory_val) is int i)
                    {
                        result.arg2_t |= ValueKind.Int;
                        result.arg2_i = Converter.ToUInt32(i);
                    }
                    result.arg2_t |= ValueKind.String;
                    result.arg2_s = memory_val;
                }
                else
                {
                    memory_requested = Converter.ToUInt32(mem);
                    memory_read = false;
                    MemoryRead?.Invoke(memory_requested);
                    result.fetched = false;
                }
            }
            else
            {
                switch (op_code.Literal2)
                {
                    case int i:
                        result.arg2_t |= ValueKind.Int;
                        result.arg2_i = Converter.ToUInt32(i);
                        break;
                    case float f:
                        result.arg2_t |= ValueKind.Float;
                        result.arg2_f = f;
                        break;
                    case string s:
                        result.arg2_t |= ValueKind.String;
                        result.arg2_s = s;
                        break;
                    default:
                        var reg = (int)op_code.Arg2;
                        var idx = RegistersIndexMapping[reg];
                        switch (RegistersKindMapping[reg])
                        {
                            case OpKind.irx:
                                result.arg2_t |= ValueKind.Int;
                                result.arg2_i = Converter.ToUInt32(ir_registers[idx]);
                                break;
                            case OpKind.frx:
                                result.arg2_t |= ValueKind.Float;
                                result.arg2_f = fr_registers[idx];
                                break;
                            case OpKind.srx:
                                result.arg2_t |= ValueKind.String;
                                result.arg2_s = sr_registers[idx];
                                break;
                            case OpKind.inx:
                                result.fetched = in_set[idx];
                                in_set[idx] = false;
                                var i = in_int[idx];
                                result.arg2_t |= ValueKind.Int;
                                result.arg2_i = Converter.ToUInt32(i);
                                var f = in_float[idx];
                                result.arg2_t |= ValueKind.Float;
                                result.arg2_f = f;
                                var s = in_str[idx];
                                result.arg2_t |= ValueKind.String;
                                result.arg2_s = s;
                                (in_int[idx], in_float[idx], in_str[idx]) = (0, 0, null);
                                break;
                        }
                        break;
                }
            }
        }
        else
            return result.fetched;
        if (operands >= 3)
        {
            switch (op_code.Literal3)
            {
                case int i:
                    result.arg3_t |= ValueKind.Int;
                    result.arg3_i = Converter.ToUInt32(i);
                    break;
                case float f:
                    result.arg3_t |= ValueKind.Float;
                    result.arg3_f = f;
                    break;
                case string s:
                    result.arg3_t |= ValueKind.String;
                    result.arg3_s = s;
                    break;
                default:
                    var reg = (int)op_code.Arg3;
                    var idx = RegistersIndexMapping[reg];
                    switch (RegistersKindMapping[reg])
                    {
                        case OpKind.irx:
                            result.arg3_t |= ValueKind.Int;
                            result.arg3_i = Converter.ToUInt32(ir_registers[idx]);
                            break;
                        case OpKind.frx:
                            result.arg3_t |= ValueKind.Float;
                            result.arg3_f = fr_registers[idx];
                            break;
                        case OpKind.srx:
                            result.arg3_t |= ValueKind.String;
                            result.arg3_s = sr_registers[idx];
                            break;
                        case OpKind.inx:
                            result.fetched = in_set[idx];
                            in_set[idx] = false;
                            var i = in_int[idx];
                            result.arg3_t |= ValueKind.Int;
                            result.arg3_i = Converter.ToUInt32(i);
                            var f = in_float[idx];
                            result.arg3_t |= ValueKind.Float;
                            result.arg3_f = f;
                            var s = in_str[idx];
                            result.arg3_t |= ValueKind.String;
                            result.arg3_s = s;
                            (in_int[idx], in_float[idx], in_str[idx]) = (0, 0, null);
                            break;
                    }
                    break;
            }
        }
        return result.fetched;
    }

    private void Execute(ref Operation op, ref OpCode opcode)
    {
        if (Debug == DebugMode.Verbose)
        {
            MemoryWrite?.Invoke(uint.MaxValue,
                $"{ip_register - 1}(@{opcode.SourceLine}): {op.operation} {{{op.arg1_i}|{op.arg1_f}|{op.arg1_s}}} {{{op.arg2_i}|{op.arg2_f}|{op.arg2_s}}} {{{op.arg3_i}|{op.arg3_f}|{op.arg3_s}}} = {{{op.out_i}|{op.out_f}|{op.out_s}}}");
        }
        switch (op.operation)
        {
            case Ops.nop: break;
            case Ops.mov:
                (op.out_i, op.out_f, op.out_s, op.out_t) = ((ulong)op.arg2_i, op.arg2_f, op.arg2_s, op.arg2_t);
                break;
            case Ops.add:
                (op.out_i, op.out_f, op.out_s, op.out_t) = ((ulong)op.arg1_i + op.arg2_i, op.arg1_f + op.arg2_f, op.arg1_s + op.arg2_s, op.arg1_t | op.arg2_t);
                break;
            case Ops.adc:
                (op.out_i, op.out_f, op.out_s, op.out_t) = ((ulong)op.arg1_i + op.arg2_i, op.arg1_f + op.arg2_f, op.arg1_s + op.arg2_s, op.arg1_t | op.arg2_t);
                break;
            case Ops.sub:
                (op.out_i, op.out_f, op.out_s, op.out_t) = ((ulong)op.arg1_i - op.arg2_i, op.arg1_f - op.arg2_f,
                    string.IsNullOrEmpty(op.arg2_s) ? op.arg1_s : op.arg1_s?.Replace(op.arg2_s, null), op.arg1_t | op.arg2_t);
                break;
            case Ops.cmp:
                (op.out_i, op.out_f, op.out_s, op.out_t) = ((ulong)op.arg1_i - op.arg2_i, op.arg1_f - op.arg2_f,
                    string.IsNullOrEmpty(op.arg2_s) ? op.arg1_s : op.arg1_s?.Replace(op.arg2_s, null), op.arg1_t | op.arg2_t);
                break;
            case Ops.inc:
                (op.out_i, op.out_f, op.out_t) = ((ulong)op.arg1_i + 1, op.arg1_f + 1f, op.arg1_t);
                break;
            case Ops.dec:
                (op.out_i, op.out_f, op.out_t) = ((ulong)op.arg1_i - 1, op.arg1_f - 1f, op.arg1_t);
                break;
            case Ops.mul:
                (op.out_i, op.out_f, op.out_t) = ((ulong)op.arg1_i * op.arg2_i, op.arg1_f * op.arg2_f, op.arg1_t | op.arg2_t);
                break;
            case Ops.div:
                (op.out_i, op.out_f, op.out_t) = ((ulong)op.arg1_i / op.arg2_i, op.arg2_f != 0 ? op.arg1_f / op.arg2_f : float.NaN, op.arg1_t | op.arg2_t);
                break;
            case Ops.shl:
                (op.out_i, op.out_s, op.out_t) =
                    (((ulong)op.arg1_i << (int)op.arg2_i),
                        string.IsNullOrEmpty(op.arg1_s)
                            ? ""
                            : op.arg2_i < op.arg1_s.Length
                                ? op.arg1_s?.Substring((int)op.arg2_i)
                                : "",
                        op.arg1_t);
                break;
            case Ops.shr:
                (op.out_i, op.out_s, op.out_t) =
                    (((ulong)op.arg1_i >> (int)op.arg2_i),
                        string.IsNullOrEmpty(op.arg1_s)
                            ? new string(' ', (int)op.arg2_i)
                            : op.arg1_s.PadLeft(op.arg1_s.Length + (int)op.arg2_i),
                        op.arg1_t);
                break;
            case Ops.rol:
                {
                    string result = null!;
                    if (!string.IsNullOrEmpty(op.arg1_s))
                    {
                        var i = (int)(op.arg2_i % op.arg1_s.Length);
                        result = i == 0 ? op.arg1_s : op.arg1_s.Substring(i) + op.arg1_s.Substring(0, i);
                    }
                    var norm = (int)(op.arg2_i % 32);
                    (op.out_i, op.out_s, op.out_t) = (((ulong)op.arg1_i << norm) | ((ulong)op.arg1_i >> (32 - norm)),
                        string.IsNullOrEmpty(op.arg1_s)
                            ? ""
                            : result,
                        op.arg1_t);
                    break;
                }
            case Ops.ror:
                {
                    string result = null!;
                    if (!string.IsNullOrEmpty(op.arg1_s))
                    {
                        var i = op.arg1_s.Length - (int)(op.arg2_i % op.arg1_s.Length);
                        result = i == 0 ? op.arg1_s : op.arg1_s.Substring(i) + op.arg1_s.Substring(0, i);
                    }
                    var norm = (int)(op.arg2_i % 32);
                    (op.out_i, op.out_s, op.out_t) = (((ulong)op.arg1_i >> norm) | ((ulong)op.arg1_i << (32 - norm)),
                        string.IsNullOrEmpty(op.arg1_s)
                            ? ""
                            : result,
                        op.arg1_t);
                    break;
                }
            case Ops.and:
                (op.out_i, op.out_t) = (op.arg1_i & op.arg2_i, ValueKind.Int);
                break;
            case Ops.or:
                (op.out_i, op.out_t) = (op.arg1_i | op.arg2_i, ValueKind.Int);
                break;
            case Ops.xor:
                (op.out_i, op.out_t) = (op.arg1_i ^ op.arg2_i, ValueKind.Int);
                break;
            case Ops.not:
                (op.out_i, op.out_t) = (~op.arg1_i, ValueKind.Int);
                break;
            case Ops.test:
                (op.out_i, op.out_t) = (op.arg1_i & op.arg2_i, ValueKind.Int);
                break;
            case Ops.inr:
                (op.out_i) = ((in_set[0] ? 0x01u : 0x00) | (in_set[1] ? 0x02u : 0x00) | (in_set[2] ? 0x04u : 0x00) | (in_set[3] ? 0x08u : 0x00));
                break;
            case Ops.flr:
                (op.out_i, op.out_t) = ((OF ? (uint)0x8 : 0) | (SF ? (uint)0x4 : 0) | (ZF ? (uint)0x2 : 0) | (CF ? (uint)0x1 : 0), ValueKind.Int);
                break;
            case Ops.fls:
                (OF, SF, ZF, CF) = ((op.arg1_i & 0x8) != 0, (op.arg1_i & 0x4) != 0, (op.arg1_i & 0x2) != 0, (op.arg1_i & 0x1) != 0);
                break;
            case Ops.jmp:
                ip_register = op.arg1_i;
                break;
            case Ops.je:
                ip_register = ZF ? op.arg1_i : ip_register;
                break;
            case Ops.jnz:
            case Ops.jne:
                ip_register = ZF ? ip_register : op.arg1_i;
                break;
            case Ops.jg:
                ip_register = ZF || SF != OF ? ip_register : op.arg1_i;
                break;
            case Ops.jge:
                ip_register = SF == OF ? op.arg1_i : ip_register;
                break;
            case Ops.jl:
                ip_register = SF == OF ? ip_register : op.arg1_i;
                break;
            case Ops.jle:
                ip_register = ZF || SF != OF ? op.arg1_i : ip_register;
                break;
            case Ops.mvi2f:
                (op.out_f, op.out_t) = (op.arg2_i, ValueKind.Float);
                break;
            case Ops.mvi2s:
                (op.out_s, op.out_t) = (op.arg2_i.ToString(), ValueKind.String);
                break;
            case Ops.mvf2i:
                (op.out_i, op.out_t) = ((uint)(int)op.arg2_f, ValueKind.Int);
                break;
            case Ops.mvf2s:
                (op.out_s, op.out_t) = (op.arg2_f.ToString(), ValueKind.String);
                break;
            case Ops.mvs2i:
                (op.out_i, op.out_t) = (Converter.ToUInt32((int)(op.arg2_s != null ? IntConverter.ConvertFrom(op.arg2_s) ?? -1 : -1)), ValueKind.Int);
                break;
            case Ops.mvs2f:
                (op.out_f, op.out_t) = ((float)(op.arg2_s != null ? FloatConverter.ConvertFrom(op.arg2_s) ?? float.NaN : float.NaN), ValueKind.Float);
                break;
            case Ops.ldi2f:
                (op.out_f, op.out_t) = (Converter.ToSingle(op.arg2_i), ValueKind.Float);
                break;
            case Ops.ldf2i:
                (op.out_i, op.out_t) = (Converter.ToUInt32(op.arg2_f), ValueKind.Int);
                break;
            case Ops.find:
                (op.out_i, op.out_t) = (string.IsNullOrEmpty(op.arg2_s) || string.IsNullOrEmpty(op.arg3_s)
                    ? Converter.ToUInt32(-1)
                    : Converter.ToUInt32(op.arg2_s.IndexOf(op.arg3_s)), ValueKind.Int);
                break;
            case Ops.rmv:
                (op.out_s, op.out_t) = (
                    string.IsNullOrEmpty(op.arg2_s) ? "" : string.IsNullOrEmpty(op.arg3_s) ? op.arg2_s : op.arg2_s.Replace(op.arg3_s, null),
                    ValueKind.String);
                break;
            case Ops.sbs:
                if (string.IsNullOrEmpty(op.arg1_s) || op.arg2_i >= op.arg1_s.Length)
                    op.out_s = "";
                else
                {
                    var from = Converter.ToInt32(op.arg2_i);
                    var length = Math.Min(Converter.ToInt32(op.arg3_i), op.arg1_s.Length - Converter.ToInt32(op.arg2_i));
                    op.out_s = op.arg1_s.Substring(from, length);
                }
                op.out_t = ValueKind.String;
                break;
            case Ops.rpl:
                (op.out_s, op.out_t) = (string.IsNullOrEmpty(op.arg1_s)
                    ? ""
                    : string.IsNullOrEmpty(op.arg2_s)
                        ? op.arg1_s
                        : op.arg1_s.Replace(op.arg2_s, op.arg3_s), ValueKind.String);
                break;
            case Ops.chr:
                (op.out_i, op.out_t) = (string.IsNullOrEmpty(op.arg2_s) || op.arg3_i >= op.arg2_s.Length
                    ? Converter.ToUInt32(-1)
                    : Converter.ToUInt32(op.arg2_s[Converter.ToInt32(op.arg3_i)]), ValueKind.Int);
                break;
            default:
                State &= ~ProcessorState.Working;
                break;
        }
    }

    private bool Write(ref OpCode op_code, ref Operation operation)
    {
        var op = (int)op_code.Operation;
        var operands = OperandNumbers[op];
        // When we have some output, set flags.
        if (operation.out_t != ValueKind.Unknown)
        {
            // Rectify output type, so in case of multiple possible output types we use the most meaningful one
            // (the integer value kind, otherwise floating point and the least meaningful for flags - string)
            var out_t =
                (operation.out_t & ValueKind.Int) != ValueKind.Unknown
                    ? ValueKind.Int
                    : (operation.out_t & ValueKind.Float) != ValueKind.Unknown
                        ? ValueKind.Float
                        : ValueKind.String;

            (OF, SF, ZF, CF) =
                out_t switch
                {
                    // Honestly, I have no idea what I'm doing here, I just read in wiki that OF is calculated internally
                    // as XOR of CF and the sign bit.
                    ValueKind.Int =>
                        (((operation.out_i & 0xffffffff00000000) != 0) ^ ((operation.out_i & 0x80000000) == 0), // OF
                            (operation.out_i & 0x80000000) != 0, // SF
                            (operation.out_i & 0xffffffff) == 0, // ZF
                            (operation.out_i & 0xffffffff00000000) != 0), // CF
                    ValueKind.Float =>
                        (OF,
                            operation.out_f < 0,
                            operation.out_f == 0f,
                            CF),
                    ValueKind.String =>
                        (OF, SF, string.IsNullOrEmpty(operation.out_s), CF),
                    _ => (OF, SF, ZF, CF),
                };
        }
        if (operands >= 1)
        {
            if ((OperandKinds[op][0] & OpKind.w) != OpKind.none)
            {
                if (op_code is { IsArg1Ref: true })
                {
                    var mem = op_code.Literal1 != null ? (int)op_code.Literal1 : ir_registers[RegistersIndexMapping[(int)op_code.Arg1]];
                    MemoryWrite?.Invoke(
                        Converter.ToUInt32(mem),
                        (operation.out_t & ValueKind.String) != ValueKind.Unknown
                            ? operation.out_s
                            : (operation.out_t & ValueKind.Float) != ValueKind.Unknown
                                ? operation.out_f.ToString()
                                : operation.out_i.ToString());
                    return true;
                }
                else
                {
                    var reg = (int)op_code.Arg1;
                    var idx = RegistersIndexMapping[reg];
                    switch (RegistersKindMapping[reg])
                    {
                        case OpKind.irx:
                            ir_registers[idx] = Converter.ToInt32((uint)operation.out_i);
                            break;
                        case OpKind.frx:
                            fr_registers[idx] = operation.out_f;
                            break;
                        case OpKind.srx:
                            sr_registers[idx] = operation.out_s;
                            break;
                        case OpKind.oux:
                            if ((operation.out_t & ValueKind.Int) != ValueKind.Unknown)
                            {
                                out_int[idx] = Converter.ToInt32((uint) operation.out_i);
                                out_kind[idx] = ValueKind.Int;
                            }
                            if ((operation.out_t & ValueKind.Float) != ValueKind.Unknown)
                            {
                                out_float[idx] = operation.out_f;
                                out_kind[idx] = ValueKind.Float;
                            }
                            if ((operation.out_t & ValueKind.String) != ValueKind.Unknown)
                            {
                                out_str[idx] = operation.out_s;
                                out_kind[idx] = ValueKind.String;
                            }
                            out_set[idx] = true;
                            break;
                    }
                }
            }
        }
        return false;
    }
}
