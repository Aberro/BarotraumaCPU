using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using RestSharp.Extensions;

namespace Barotrauma.Items.Components.Signals.Controller;

public class Compiler
{
    private static readonly Int32Converter IntConverter = new();
    private static readonly SingleConverter FloatConverter = new();
    private static readonly Dictionary<string, Ops> Commands;
    private static readonly Dictionary<string, Reg> Registers;
    private static readonly Dictionary<Ops, OperandsAttribute> OperandsInfo;
    private static readonly Dictionary<Reg, OpKind> RegistersInfo;

    private static readonly Regex Command =
        new(@"^\s*(?<command>\w+)(?<arg1>\s+(?<addr1>\[)?(?<arg1val>(?<reg1>(ir|fr|sr|ou|in)\d+)|(?<literal1>(?<int1>\d+|0x[A-Fa-f0-9]+)|(?<float1>\d*\.\d+)|(?<str1>"".*(?<!(?<!\\)\\)"")|(?<label1>[A-Za-z0-9\-_]+)))(?(addr1)\]))?(\s+(?<arg2>(?<addr2>\[)?(?<arg2val>(?<reg2>(ir|fr|sr|ou|in)\d+)|(?<literal2>(?<int2>\d+|0x[A-Fa-f0-9]+)|(?<float2>\d*\.\d+)|(?<str2>"".*(?<!(?<!\\)\\)"")|(?<label2>[A-Za-z0-9\-_]+)))(?(addr2)\])))?(\s+(?<arg3>(?<addr3>\[)?(?<arg3val>(?<reg3>(ir|fr|sr|ou|in)\d+)|(?<literal3>(?<int3>\d+|0x[A-Fa-f0-9]+)|(?<float3>\d*\.\d+)|(?<str3>"".*(?<!(?<!\\)\\)"")|(?<label3>[A-Za-z0-9\-_]+)))(?(addr3)\])))?\s*(?<comment>;.*)?$",
            RegexOptions.Compiled);
    private static readonly Regex Label = new("^\\s*(?<label>[A-Za-z0-9\\-_]+):\\s*(;.*)?$", RegexOptions.Compiled);
    private static readonly Regex Comment = new("^\\s*(;.*)?$", RegexOptions.Compiled);

    static Compiler()
    {
        Commands = typeof(Ops).GetEnumValues().OfType<Ops>().ToDictionary(x => x.ToString().ToLowerInvariant());
        Registers = typeof(Reg).GetEnumValues().OfType<Reg>().ToDictionary(x => x.ToString().ToLowerInvariant());
        OperandsInfo = typeof(Ops).GetEnumNames()
            .Select(x => (typeof(Ops).GetEnumValues().OfType<Ops>().First(y => y.ToString() == x),
                typeof(Ops).GetMember(x).FirstOrDefault()?.GetAttribute<OperandsAttribute>()
                    ?? throw new InvalidProgramException("Every command in Ops enumeration should have an Operands attribute!")))
            .Where(x =>
            {
                if (x.Item2!.Kinds.Any(kind => (kind & OpKind.rw) == OpKind.none))
                    throw new InvalidProgramException("Every OpKind value in Operands attribute of a command should have read-write flags set!");
                return true;
            })
            .ToDictionary(x => x.Item1, x => x.Item2);
        RegistersInfo = typeof(Reg).GetEnumNames()
            .Select(x => (typeof(Reg).GetEnumValues().OfType<Reg>().First(y => y.ToString() == x),
                typeof(Reg).GetMember(x).FirstOrDefault()?.GetAttribute<OperandsAttribute>()?.Kinds.Single()))
            .Where(x =>
            {
                if (x.Item1 is not Reg.none && x.Item2 is null)
                    throw new InvalidProgramException("Every register in Reg enumeration should have an Operands attribute!");
                return x.Item2 is not null;
            })
            .ToDictionary(x => x.Item1, x => x.Item2!.Value);
    }

    struct LabelLiteral
    {
        public string Name;
    }
    public int ErrorLineNum { get; private set; }
    public string ErrorCode { get; private set; }
    public OpCode[] Operations { get; private set; }
    public bool Compile(string[] lines)
    {
        bool Error(int lineNum, string code)
        {
            ErrorLineNum = lineNum;
            ErrorCode = code;
            return false;
        }

        bool MakeLiteral(Match match, int lineNum, int argNum, out object result, out OpKind kind)
        {
            result = null;
            kind = (OpKind)0;
            var group = match.Groups["int" + argNum];
            if (group.Success)
            {
                var val = IntConverter.ConvertFromString(group.Value);
                if (val is null)
                    return Error(lineNum, "Failed to parse integer literal: " + group.Value);
                result = val;
                kind = OpKind.i;
                return true;
            }
            group = match.Groups["float" + argNum];
            if (group.Success)
            {
                var val = FloatConverter.ConvertFromString(group.Value);
                if (val is null)
                    return Error(lineNum, "Failed to parse floating point literal: " + group.Value);
                result = val;
                kind = OpKind.f;
                return true;
            }
            group = match.Groups["str" + argNum];
            if (group.Success)
            {
                result = group.ValueSpan.Slice(1, group.ValueSpan.Length - 2).ToString();
                kind = OpKind.s;
                return true;
            }
            group = match.Groups["label" + argNum];
            if (group.Success)
            {
                result = new LabelLiteral { Name = group.Value };
                kind = OpKind.i;
                return true;
            }
            return Error(lineNum, "Unknown literal parsing error at arg" + argNum);
        }

        bool MakeOperand(byte operandNumber, Match match, int sourceLine, Ops operation, out Reg reg, out object literal, out bool isRef)
        {
            (reg, literal, isRef) = (Reg.none, null, false);
            var operandsInfo = OperandsInfo[operation];
            if (match.Groups["arg" + operandNumber].Success)
            {
                OpKind opKind = OpKind.none;
                if (operandsInfo.Kinds.Length < operandNumber)
                    return Error(sourceLine, $"Operation {operation} has too many operands!");
                if (match.Groups["reg" + operandNumber].Success)
                {
                    var regStr = match.Groups["reg" + operandNumber].Value;
                    if (!Registers.TryGetValue(regStr, out reg))
                        return Error(sourceLine, $"Unknown register as arg{operandNumber}: {regStr}");
                }
                else if (match.Groups["literal" + operandNumber].Success)
                {
                    if (!MakeLiteral(match, sourceLine, operandNumber, out literal, out opKind))
                        return false;
                }
                else return Error(sourceLine, $"Unknown parsing error at arg{operandNumber}.");
                isRef = match.Groups["addr" + operandNumber].Success;
                if (isRef && (opKind is OpKind.none ? RegistersInfo[reg] is not OpKind.irx : opKind is not OpKind.i))
                    return Error(sourceLine, $"Only integer literal can be used as reference!");
                if (isRef && reg is not Reg.none && RegistersInfo[reg] is OpKind.oux)
                    return Error(sourceLine, $"oux operand cannot be used for reference!");
                opKind = isRef
                        ? OpKind.im | OpKind.sm // operand is memory reference
                        : opKind is OpKind.i or OpKind.f or OpKind.s
                            ? opKind
                            : RegistersInfo[reg];
                var expectedKind = operandsInfo.Kinds[operandNumber - 1];
                if ((opKind & expectedKind) == OpKind.none)
                    return Error(sourceLine, $"Wrong arg{operandNumber} kind, expected {expectedKind}, actual is {opKind}.");
            }
            else
            {
                if (operandsInfo.Kinds.Length >= operandNumber)
                    return Error(sourceLine, $"Operation {operation} has too few operands.");
            }
            return true;
        }

        Operations = Array.Empty<OpCode>();
        var operations = new List<OpCode>();
        var labels = new Dictionary<string, int>();
        int idx = -1;
        foreach (var line in lines)
        {
            idx++;
            if (string.IsNullOrWhiteSpace(line))
                continue;
            // Try to match from simplest to more complex expressions
            if (Comment.IsMatch(line)) 
                continue;
            var label = Label.Match(line);
            if (label.Success)
            {
                var labelVal = label.Groups["label"].Value;
                if (Registers.ContainsKey(labelVal))
                    return Error(idx, "Label cannot have same name as register: " + labelVal);
                if (labels.ContainsKey(labelVal))
                    return Error(idx, "Label with same name already defined: " + labelVal);
                if (Char.IsDigit(labelVal[0]) || labelVal[0] == '-')
                    return Error(idx, "Label may start only with letter or underscore: " + labelVal);
                labels.Add(labelVal, operations.Count);
                continue;
            }
            var command = Command.Match(line);
            if (command.Success)
            {
                var cmd = command.Groups["command"].Value;
                if (!Commands.TryGetValue(cmd, out var op))
                    return Error(idx, "Unknown command name: " + cmd);
                var opcode = new OpCode
                {
                    SourceLine = idx,
                    Operation = op
                };

                // Parse arg1
                if (!MakeOperand(1, command, idx, op, out opcode.Arg1, out opcode.Literal1, out opcode.IsArg1Ref)
                    || !MakeOperand(2, command, idx, op, out opcode.Arg2, out opcode.Literal2, out opcode.IsArg2Ref)
                    || !MakeOperand(3, command, idx, op, out opcode.Arg3, out opcode.Literal3, out var arg3Ref))
                    return false;
                if (arg3Ref)
                    return Error(idx, "arg3 cannot be a reference type!");
                // Processor cannot read two memory values at single command,
                // so if both arguments are of reference kind, ensure that
                // only one is read, and second one is write.
                if (opcode.IsArg1Ref && opcode.IsArg2Ref)
                {
                    var opInfo = OperandsInfo[op];
                    if ((opInfo.Kinds[0] & OpKind.r) == OpKind.none)
                        return Error(idx, "Command cannot have two readable reference arguments");
                }

                operations.Add(opcode);
                continue;
            }
            return Error(idx, "Failed to parse line: " + line);
        }
        Operations = operations.ToArray();
        for (var i = 0; i < Operations.Length; i++)
        {
            var opcode = Operations[i];
            if (opcode.Literal1 is LabelLiteral label1)
            {
                if (!labels.TryGetValue(label1.Name, out idx))
                    return Error(opcode.SourceLine, "Undefined label as arg1: " + label1.Name);
                opcode.Literal1 = idx;
            }
            if (opcode.Literal2 is LabelLiteral label2)
            {
                if (!labels.TryGetValue(label2.Name, out idx))
                    return Error(opcode.SourceLine, "Undefined label as arg2: " + label2.Name);
                opcode.Literal2 = idx;
            }
            if (opcode.Literal3 is LabelLiteral label3)
            {
                if (!labels.TryGetValue(label3.Name, out idx))
                    return Error(opcode.SourceLine, "Undefined label as arg3: " + label3.Name);
                opcode.Literal3 = idx;
            }
            Operations[i] = opcode;
            ErrorCode = null;
            ErrorLineNum = -1;
        }
        return true;
    }
}
