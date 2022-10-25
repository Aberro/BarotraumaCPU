using System;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Xna.Framework;
using static Barotrauma.DebugConsole;
using Barotrauma.Items.Components.Signals.Controller;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Barotrauma.Extensions;
using System.Text;

namespace Barotrauma.Items.Components
{
    class ControllerComponent : ProgrammableComponent
    {
        public const int ControllerMaxCommands = 1024;

        private Compiler compiler;
        private Processor processor;
        private string[] controllerCode;
        private int multiplier;
        private bool compilationError;
        private bool clock;
        protected Compiler Compiler
        {
            get => compiler ??= new Compiler();
        }
        protected Processor Processor
        {
            get
            {
                if (processor == null)
                {
                    processor = new Processor();
                    processor.ChannelWrite += ProcessorOnChannelWrite;
                    processor.MemoryRead += ProcessorMemoryRead;
                    processor.MemoryWrite += ProcessorMemoryWrite;
                }
                return processor;
            }
        }

        [InGameEditable(CommandNames = new[] { "Program" })]
        public string[] ControllerCode
        {
            get => controllerCode;
            set
            {
                controllerCode = value;
                Reprogram(controllerCode);
            }
        }
        [InGameEditable(MinValueInt = 1, MaxValueInt = 16), Serialize(0, IsPropertySaveable.Yes, "", AlwaysUseInstanceValues = true)]
        public int Multiplier
        {
            get => this.multiplier;
            set
            {
                value = Math.Clamp(value, 1, 16);
                if (value == multiplier) return;
                multiplier = value;
                Processor.Multiplier = (byte)value;
            }
        }
        public override int MaxLines => ControllerMaxCommands;
        public ControllerComponent(Item item, ContentXElement element)
            : base(item, element)
        {
            if(controllerCode is null)
                controllerCode = new string[ControllerMaxCommands];
            Reprogram(controllerCode);
            IsActive = true;
        }

        protected override void Reprogram(string[] code)
        {
            if (code == null) code = Array.Empty<string>();
            if (code.Length > MaxLines) Array.Resize(ref code, MaxLines);
            if (Compiler.Compile(code))
            {
                Processor.Load(Compiler.Operations);
            }
#if CLIENT
            else
            {
                var msg = $"Error at {Compiler.ErrorLineNum}: {Compiler.ErrorCode}";
                if (Screen.Selected.IsEditor)
                    new GUIMessageBox("Compilation error", msg, new Vector2(0.25f, 0.0f), new Point(400, 200));
                else
                    Character.Controlled.AddMessage(msg, Color.Red, true);
            }
#endif

        }
        public override void ReceiveSignal(Signal signal, Connection connection)
        {
            if (signal.strength <= 0)
                return;
            int port = -1;
            switch (connection.Name)
            {
                case "signal_in0":
                    port = 0;
                    break;
                case "signal_in1":
                    port = 1;
                    break;
                case "signal_in2":
                    port = 2;
                    break;
                case "signal_in3":
                    port = 3;
                    break;
                case "clock":
                    clock = true;
                    break;
                case "memory_in":
                    Processor.Memory(signal.value);
                    break;
                case "set_state":
                    if (signal.value is not (null or "0"))
                        Processor.Start();
                    else
                        Processor.Stop();
                    break;
            }
            if (port >= 0 && port < 4)
            {
                Processor.Channel((uint)port, signal.value);
            }
        }
        public override void Update(float deltaTime, Camera cam)
        {
            base.Update(deltaTime, cam);
            if (clock)
            {
                clock = false;
                Processor.Cycle();
                item.SendSignal((Processor.State & ProcessorState.Underloaded) != 0 ? "0" : "1", "load_out");
                item.SendSignal((Processor.State & ProcessorState.Working) != ProcessorState.Stopped ? "1" : "0", "state_out");
            }
        }
        protected override string GetLine(int lineNumber)
        {
            if (lineNumber < 0 || lineNumber >= MaxLines || lineNumber >= ControllerCode.Length)
                return null;
            return ControllerCode[lineNumber];
        }

        private void ProcessorOnChannelWrite(uint channel, string value)
        {
            if (channel < 4)
                item.SendSignal(value, $"signal_out{channel}");
        }

        private void ProcessorMemoryRead(uint address)
        {
            item.SendSignal(address.ToString(), "address_out");
        }

        private void ProcessorMemoryWrite(uint address, string value)
        {
            item.SendSignal(address.ToString(), "address_out");
            item.SendSignal(value, "memory_out");
        }
#if SERVER
        protected override void UpdateLine(string line, int lineNumber)
        {
            if (lineNumber < 0 || lineNumber >= MaxLines)
                return;
            controllerCode[lineNumber] = line;
            base.UpdateLine(line, lineNumber);
        }
#elif CLIENT
        protected override bool Debuggable => true;
        public void Program()
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

        public override void UpdateHUD(Character character, float deltaTime, Camera cam)
        {
            if (controllerCode.Length != ControllerMaxCommands)
                Array.Resize(ref controllerCode, ControllerMaxCommands);
            base.UpdateHUD(character, deltaTime, cam);
        }
        protected override void PrepareDebugGUI(GUILayoutGroup debugScreen)
        {
            debugScreen.IsHorizontal = false;
            var topRow = new GUILayoutGroup(new RectTransform(new Vector2(1, 0.5f), debugScreen.RectTransform), true);
            var bottomRow = new GUILayoutGroup(new RectTransform(new Vector2(1, 0.5f), debugScreen.RectTransform), true);
            var commands = new GUIListBox(new RectTransform(new Vector2(0.24f, 1), topRow.RectTransform));
            var intRegisters = new GUIListBox(new RectTransform(new Vector2(0.24f, 1), topRow.RectTransform));
            var floatRegisters = new GUIListBox(new RectTransform(new Vector2(0.24f, 1), topRow.RectTransform));
            var stringRegisters = new GUIListBox(new RectTransform(new Vector2(0.24f, 1), topRow.RectTransform));
            var inputChannels = new GUIListBox(new RectTransform(new Vector2(0.24f, 1), bottomRow.RectTransform));
            var outputChannels = new GUIListBox(new RectTransform(new Vector2(0.24f, 1), bottomRow.RectTransform));
            var buttons = new GUILayoutGroup(new RectTransform(new Vector2(0.5f, 1), bottomRow.RectTransform));

            var step = new GUIButton(new RectTransform(new Vector2(1, 0.25f), buttons.RectTransform))
            {
                Text = "Step",
                OnClicked = (_, _) =>
                {
                    processor.Cycle();
                    return true;
                }
            };
            var switchState = new GUIButton(new RectTransform(new Vector2(1, 0.25f), buttons.RectTransform))
            {
                Text = "Continue",
                OnClicked = (_, _) =>
                {
                    if ((processor.State ^ ProcessorState.Working) == ProcessorState.Stopped)
                        processor.Start();
                    else
                        processor.Stop();
                    return true;
                }
            };
            base.PrepareDebugGUI(debugScreen);
        }
        protected override void Store()
        {
            base.Store();
            if (Screen.Selected is not GameScreen || (Character.Controlled != null && Character.Controlled.GetEquippedItem("programmer") != null))
            {
                UpdateCode(controllerCode);
            }
            if (Compiler.ErrorLineNum >= 0 && MapEntity.SelectedList.Contains(Item))
            {
                DisplayEditor = true;
            }
        }
        protected override void PrepareGUI(XElement element)
        {
            if (!DisplayEditor)
                return;
            if (controllerCode.Length != ControllerMaxCommands)
                Array.Resize(ref controllerCode, ControllerMaxCommands);
            base.PrepareGUI(element);
            if (Lines == 0)
            {
                var lastLine = controllerCode.LastOrDefault(x => !string.IsNullOrEmpty(x));
                var lastLineIdx = lastLine == null ? -1 : controllerCode.IndexOf(lastLine);
                for (int i = 0; i <= lastLineIdx; i++)
                    base.InsertLine(FormatLine(controllerCode[i], i, false), i);
                base.InsertLine(FormatLine("", lastLineIdx+1, true), lastLineIdx+1);
            }
        }
        protected override void UpdateLine(RichString line, int lineNumber)
        {
            if (lineNumber < 0 || lineNumber >= MaxLines || lineNumber >= controllerCode.Length)
                return;
            controllerCode[lineNumber] = line.ToString();
            var cursor = CurrentLine == lineNumber;
            line = FormatLine(line.ToString(), lineNumber, cursor);
            base.UpdateLine(line, lineNumber);
        }
        protected override void RemoveLine(int lineNumber)
        {
            Array.Copy(controllerCode, lineNumber + 1, controllerCode, lineNumber, controllerCode.Length - lineNumber - 1);
            base.RemoveLine(lineNumber);
        }
        protected override void InsertLine(RichString line, int lineNumber)
        {
            if (lineNumber < 0 || lineNumber >= MaxLines)
                return;
            Array.Copy(controllerCode, lineNumber, controllerCode, lineNumber + 1, controllerCode.Length - lineNumber - 1);
            controllerCode[lineNumber] = line.ToString();
            base.InsertLine(FormatLine(line.ToString(), lineNumber, lineNumber == CurrentLine), lineNumber);
        }

        private static Regex Comment = new(";.*$");
        private static Regex Label = new("[\\d\\w-_]+\\s*:");
        private static Regex Register = new("(?<=\\s|\\[)(ir\\d|fr\\d|sr\\d)");
        private static Regex Channel = new("(?<=\\s)(in\\d|ou\\d)(?=\\s)");
        private RichString FormatLine(string code, int lineNumber, bool lineSelector)
        {
            if (code == null)
                return "";
            var cursor = lineSelector ? '>' : ' ';
            var lineNumWidth = MaxLineLength.ToString().Length;
            var lineNumberStr = lineNumber.ToString().PadRight(lineNumWidth);
            var insertions = Comment.Matches(code).Where(m => m.Success).SelectMany(m => new[] {(m.Index, "‖end‖"), (m.Index + m.Length, "‖color:Gray;end‖")}).Union(
                Label.Matches(code).Where(m => m.Success).SelectMany(m => new[] {(m.Index, "‖end‖"), (m.Index + m.Length, "‖color:SpringGreen;end‖")})).Union(
                Register.Matches(code).Where(m => m.Success).SelectMany(m => new[] {(m.Index, "‖end‖"), (m.Index + m.Length, "‖color:RoyalBlue;end‖")})).Union(
                Channel.Matches(code).Where(m => m.Success).SelectMany(m => new[] {(m.Index, "‖end‖"), (m.Index + m.Length, "‖color:Aqua;end‖")}))
                //.Append(lineNumber == CurrentLine && CaretIndex >= 0 ? (CaretIndex, "‖u‖") : (0, ""))
                .OrderBy(x => x.Item1).ToArray();
            var codeSB = new StringBuilder(code);
            for (var i = 0; i < insertions.Length; i++)
            {
                var insertion = insertions[i];
                codeSB.Insert(insertion.Item1, insertion.Item2);
                for (var j = i + 1; j < insertions.Length; j++)
                    insertions[j].Item1 += insertion.Item2.Length;
            }
            var result = (RichString)$"{lineNumberStr}‖color:darkgray;end‖{cursor}‖end‖:{codeSB}";
            return result;
        }
#endif
    }
}
