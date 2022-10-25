using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using FarseerPhysics.Collision;
using Microsoft.Xna.Framework;
namespace Barotrauma.Items.Components
{
    class RamComponent : ProgrammableComponent
    {
        public const int RAMMaxLines = 1024;
        private string[] memory;
        private string[] inEditMemory;
        private int addr;
        private bool addrRecv;
        private string value;
        private bool valueRecv;
        private Int32Converter intConverter;
        private SingleConverter floatConverter;

        [InGameEditable(CommandNames = new[] { "Modify" })]
        public string[] Memory
        {
            get => memory;
            set => UpdateCode(value);
        }
        public new bool Read { get; private set; }
        public override int MaxLines => RAMMaxLines;

        public RamComponent(Item item, ContentXElement element)
            : base(item, element)
        {
            IsActive = true;
            if(memory == null)
                Memory = new string[RAMMaxLines];
            intConverter = new Int32Converter();
            floatConverter = new SingleConverter();
        }
        protected override void Reprogram(string[] code)
        {
            if (code.Length != RAMMaxLines)
                Array.Resize(ref code, RAMMaxLines);
            memory = code;
            base.Reprogram(code);
        }

        public override void ReceiveSignal(Signal signal, Connection connection)
        {
            if (signal.strength <= 0)
                return;
            switch (connection.Name)
            {
                case "address_in":
                    if(intConverter.IsValid(signal.value))
                    {
                        addr = (int)(intConverter.ConvertFromString(signal.value) ?? -1);
                        addrRecv = true;
                    }
                    else
                    {
                        addr = -1;
                    }
                    break;
                case "memory_in":
                    value = signal.value;
                    valueRecv = true;
                    break;
                case "clock":
                    if (addrRecv && addr >= 0 && addr < Memory.Length)
                    {
                        if (valueRecv)
                        {
                            Memory[addr] = value;
#if CLIENT
                            if (DisplayEditor)
                            {
                                UpdateLine(value, addr);
                            }
#endif
                            valueRecv = false;
                            item.SendSignal("1", "write_out");
                            break;
                        }
                        var val = Memory[addr];

                        if (val != null && val.StartsWith('"') && val.EndsWith('"') && val.Length > 1)
                        {
                            val = val.Substring(1, val.Length - 2);
                        }
                        else
                        {
                            if(intConverter.IsValid(val))
                            {
                                val = intConverter.ConvertFromString(val)?.ToString() ?? "";
                            }
                            else
                            {
                                val = "";
                            }
                        }
                        item.SendSignal(val, "memory_out");
                        item.SendSignal("1", "read_out");
                        addrRecv = false;
                    }
                    else
                    {
                        item.SendSignal("0", "read_out");
                    }
                    break;
            }
        }
#if SERVER
        protected override void UpdateLine(string line, int lineNumber)
        {
            if (lineNumber < 0 || lineNumber >= MaxLines)
                return;
            memory[lineNumber] = line;
        }
#elif CLIENT
        protected override bool ForbidLineRemoval => true;
        public override bool DisplayEditor
        {
            get => base.DisplayEditor;
            set
            {
                base.DisplayEditor = value;
                if (value)
                {
                    inEditMemory = new string[memory.Length];
                    Array.Copy(memory, inEditMemory, memory.Length);
                    for (int i = 0; i < RAMMaxLines; i++)
                    {
                        if (memory[i].IsNullOrEmpty())
                            memory[i] = "0";
                    }
                }
                else
                {
                    inEditMemory = null;
                }
            }
        }

        protected override void Store()
        {
            if (inEditMemory != null && (Screen.Selected is not GameScreen || 
                (Character.Controlled != null && Character.Controlled.GetEquippedItem("programmer") != null)))
            {
                UpdateCode(inEditMemory);
            }
            base.Store();
        }
        public void Modify()
        {
            if (Screen.Selected is GameScreen && Character.Controlled != null && Character.Controlled.GetEquippedItem("programmer") == null)
            {
                Character.Controlled.AddMessage("You need programmer to do that.", Color.Yellow, true);
                DisplayEditor = false;
                return;
            }
            DisplayEditor = !DisplayEditor;
            PrepareGUI(null);
        }
        protected override void PrepareGUI(XElement element)
        {
            if (!DisplayEditor)
                return;
            if (memory.Length != RAMMaxLines)
                Array.Resize(ref memory, RAMMaxLines);
            base.PrepareGUI(element);
            if (Lines == 0)
            {
                for (int i = 0; i < RAMMaxLines; i++)
                    base.InsertLine(FormatLine(GetLine(i), i, i == 0), i);
                InitInputBox(GetLine(0));
            }
        }
        protected override string GetLine(int lineNumber)
        {
            if (lineNumber < 0 || lineNumber >= MaxLines)
                return null;
            var result = (inEditMemory ?? memory)[lineNumber] ?? "0";
            if (!(result.StartsWith('"') && result.EndsWith('"') && result.Length > 1))
            {
                if(intConverter.IsValid(result))
                    return $"0x{(int)(intConverter.ConvertFromString(result) ?? 0):X8}";
                if (floatConverter.IsValid(result))
                    return ((float) (floatConverter.ConvertFromString(result) ?? 0)).ToString("E");
            }
            return result;
        }
        protected override string GetHumanReadableLine(int lineNumber)
        {
            if (lineNumber < 0 || lineNumber >= MaxLines)
                return null;
            var result = (inEditMemory ?? memory)[lineNumber] ?? "0";
            if (!(result.StartsWith('"') && result.EndsWith('"') && result.Length > 1))
            {
                if (intConverter.IsValid(result))
                    return ((int)(intConverter.ConvertFromString(result) ?? 0)).ToString();
                if (floatConverter.IsValid(result))
                    return ((float)(floatConverter.ConvertFromString(result) ?? 0)).ToString();
            }
            return result;
        }
        protected override void UpdateLine(RichString richLine, int lineNumber)
        {
            if (!DisplayEditor || lineNumber < 0 || lineNumber >= MaxLines)
                return;
            var line = richLine.ToString();
            if (line.StartsWith('"') && line.EndsWith('"') && line.Length>1)
                inEditMemory[lineNumber] = line;
            else
            {
                if(intConverter.IsValid(line))
                    inEditMemory[lineNumber] = intConverter.ConvertFromString(line)?.ToString() ?? "0";
                else if (floatConverter.IsValid(line))
                    inEditMemory[lineNumber] = ((float)(floatConverter.ConvertFromString(line) ?? 0f)).ToString("0.0");
            }
            var cursor = CurrentLine == lineNumber;
            richLine = FormatLine(inEditMemory[lineNumber] ?? "", lineNumber, cursor);
            base.UpdateLine(richLine, lineNumber);
        }
        protected override void RemoveLine(int lineNumber)
        {
        }
        protected override void InsertLine(RichString richLine, int lineNumber)
        {
            if (!DisplayEditor || lineNumber < 0 || lineNumber >= MaxLines)
                return;
            if (lineNumber < Lines)
            {
                Array.Copy(inEditMemory, lineNumber, inEditMemory, lineNumber + 1, inEditMemory.Length - lineNumber - 1);
            }
            var line = richLine.ToString();
            if (line.StartsWith('"') && line.EndsWith('"') && line.Length > 1)
                inEditMemory[lineNumber] = line;
            else
            {
                if(intConverter.IsValid(line))
                    inEditMemory[lineNumber] = intConverter.ConvertFromString(line)?.ToString() ?? "0";
                else if (floatConverter.IsValid(line))
                    inEditMemory[lineNumber] = floatConverter.ConvertFromString(line)?.ToString() ?? "0.0";
            }
            base.InsertLine(FormatLine(line, lineNumber, lineNumber == CurrentLine), lineNumber);
        }

        private RichString FormatLine(string line, int lineNumber, bool lineSelector)
        {
            if (!(line.StartsWith('"') && line.EndsWith('"') && line.Length > 1))
            {
                try
                {
                    if(intConverter.IsValid(line))
                        line = $"0x{((int)(intConverter.ConvertFromString(line) ?? 0)):X8}";
                    else if(floatConverter.IsValid(line))
                        line = ((float)(floatConverter.ConvertFromString(line) ?? 0f)).ToString("E");
                }
                catch
                {
                }
            }
            var cursor = lineSelector ? '>' : ' ';
            var lineNumWidth = MaxLineLength.ToString().Length;
            var lineNumberStr = lineNumber.ToString().PadRight(lineNumWidth);
            return $"{cursor}{lineNumberStr}:{line}";
        }
#endif
    }
}
