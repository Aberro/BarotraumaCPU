using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Barotrauma.Extensions;
using Barotrauma.Networking;
using Microsoft.Xna.Framework;
#if CLIENT
using Microsoft.Xna.Framework.Input;
#endif

namespace Barotrauma.Items.Components
{
    abstract partial class ProgrammableComponent : ItemComponent, IServerSerializable, IClientSerializable
    {
        private readonly struct ProgrammableEventData : IEventData
        {
            public readonly string Code;

            public ProgrammableEventData(string[] code)
            {
                Code = code.Aggregate(new StringBuilder(), (sb, line) => sb.AppendLine(line)).ToString();
            }
            public ProgrammableEventData(string code)
            {
                Code = code;
            }
        }
        private enum EventType : byte
        {
            Insert = 1,
            Change = 2,
        }
        public virtual int MaxLines { get; } = 64;
        public int Lines { get; private set; }

        public ProgrammableComponent(Item item, ContentXElement element)
            : base(item, element)
        {
            IsActive = true;
        }

        public void UpdateCode(string[] code)
        {
            if (GameMain.NetworkMember == null)
            {
                Reprogram(code);
            }
            else
            {
#if SERVER
                item.CreateServerEvent(this, new ProgrammableEventData(code));
#elif CLIENT
                item.CreateClientEvent(this, new ProgrammableEventData(code));
#endif
            }
        }
        public override XElement Save(XElement parentElement)
        {
            var componentElement = base.Save(parentElement);
            var lines = 0;
            for (var i = 0; i < Lines; i++)
                if (!string.IsNullOrEmpty(GetHumanReadableLine(i)))
                    lines = i+1;
            componentElement.Add(new XAttribute("Lines", lines));
            for (int i = 0; i < lines; i++)
            {
                var line = GetHumanReadableLine(i);
                componentElement.Add(new XElement("Line", new XAttribute("Value", XmlConvert.EncodeName(line))));
            }
            return componentElement;
        }

        public override void Load(ContentXElement componentElement, bool usePrefabValues, IdRemap idRemap)
        {
            base.Load(componentElement, usePrefabValues, idRemap);
            var lines = componentElement.GetAttributeInt("Lines", 0);
            var idx = 0;
            string[] code = new string[lines];
            foreach (var xmlLine in componentElement.GetChildElements("Line"))
            {
                code[idx] = XmlConvert.DecodeName(xmlLine.GetAttributeString("Value", ""));
                UpdateLine(code[idx], idx++);
            }
            UpdateCode(code);
        }

        protected virtual void Reprogram(string[] code)
        {
        }
#if SERVER
        protected virtual void UpdateLine(string value, int lineNumber) { }

        protected virtual string GetLine(int lineNumber)
        {
            return "";
        }

        protected virtual string GetHumanReadableLine(int lineNumber)
        {
            return GetLine(lineNumber);
        }
        public override IEventData ServerGetEventData()
        {
            return base.ServerGetEventData();
        }

        public void ServerEventWrite(IWriteMessage msg, Client c, NetEntityEvent.IData extraData = null)
        {
            if (TryExtractEventData(extraData, out ProgrammableEventData eventData))
            {
                msg.WriteString(eventData.Code);
            }
        }
        public void ServerEventRead(IReadMessage msg, Client c)
        {
            string code = msg.ReadString();

            if (item.CanClientAccess(c))
            {
                GameServer.Log($"{GameServer.CharacterLogName(c.Character)} updated controller code for {item.Name}", ServerLog.MessageType.ItemInteraction);
                Reprogram(code.Split("\r\n"));
                item.CreateServerEvent(this, new ProgrammableEventData(code));
            }
        }
#elif CLIENT
        private GUIListBox listingBox;
        private GUITextBox inputBox;
        private GUIButton storeButton;
        private SerializableEntityEditor editor;
        private GUIComponent parent;
        private GUILayoutGroup debugScreen;
        private bool shouldSelectInputBox;
        private bool suppressTextChanged;
        // Sadly, input box do not notify of keyboard presses and handles input independently from our handling.
        // This result in scenario where backspace was pressed when caret was at position 1, input box remove first character,
        // then our handler kick in and sees that backspace was pressed and caret is at zero position, so it removes current line,
        // when it actually shouldn't've to do it. Same for deletion.
        // Current workaround is to remember whether caret was at either first or last position during last update.
        // This way if it wasn't, that means that we don't need to remove line.
        private bool caretWasAtZero;
        private bool caretWasAtLast;
        private bool displayEditor;
        private Color textColor = Color.LimeGreen;
        protected virtual bool Debuggable => false;
        protected virtual bool ForbidLineRemoval => false;

        public virtual int MaxLineLength { get; } = ChatMessage.MaxLength;
        public int CurrentLine { get; protected set; }
        public int CaretIndex => inputBox?.CaretIndex ?? -1;
        public virtual bool DisplayEditor
        {
            get => displayEditor;
            set
            {
                if (value == displayEditor)
                    return;
                displayEditor = value;
                if (value)
                {
                    shouldSelectInputBox = true;
                    if (parent != null)
                        parent.Visible = true;
                }
                else
                {
                    if (parent != null)
                        parent.Visible = false;
                }
            }
        }
        public Color TextColor
        {
            get => textColor;
            set
            {
                textColor = value;
                if (inputBox is { } input)
                {
                    input.TextColor = value;
                }
            }
        }

        public void ClientEventRead(IReadMessage msg, float sendingTime)
        {
            string code = msg.ReadString();
            Reprogram(code.Split("\r\n"));
        }
        public void ClientEventWrite(IWriteMessage msg, NetEntityEvent.IData extraData = null)
        {
            if (TryExtractEventData(extraData, out ProgrammableEventData eventData))
            {
                msg.WriteString(eventData.Code);
            }
        }
        public override void Update(float deltaTime, Camera cam)
        {
            base.Update(deltaTime, cam);
            if (Character.Controlled.SelectedItem != Item && DisplayEditor)
                Store();
        }

        public override void CreateEditingHUD(SerializableEntityEditor editor)
        {
            this.editor = editor;
            base.CreateEditingHUD(editor);
        }
        public override bool ShouldDrawHUD(Character character)
        {
            if (Screen.Selected is GameScreen && Character.Controlled != null && Character.Controlled.GetEquippedItem("programmer") == null)
                return false;
            return DisplayEditor;
        }
        public override void UpdateEditing(float deltaTime)
        {
            base.UpdateEditing(deltaTime);
            if (DisplayEditor && !(MapEntity.SelectedList.Contains(Item) || (Screen.Selected is SubEditorScreen subScreen && subScreen.WiringMode && Item.EditingHUD != null && Item.EditingHUD.UserData as Item == Item)))
                Store();
            UpdateHUD(null, deltaTime, Screen.Selected.Cam);
        }
        public override void UpdateHUD(Character character, float deltaTime, Camera cam)
        {
            if (!Screen.Selected.IsEditor || Screen.Selected is not SubEditorScreen subScreen && GuiFrame != null && inputBox == null)
                GuiFrame.Visible = false;
            if (parent != null)
            {
                if (shouldSelectInputBox)
                {
                    var caretPos = inputBox.CaretIndex;
                    inputBox.Select();
                    inputBox.CaretIndex = caretPos;
                    shouldSelectInputBox = false;
                }
                parent.Visible = DisplayEditor;
            }
            if (inputBox != null && GUI.KeyboardDispatcher.Subscriber == inputBox)
            {
                var lineNumber = CurrentLine;
                var line = inputBox.Text;
                var save = false;
                if (PlayerInput.KeyHit(Keys.Up))
                {
                    save = true;
                    if (CurrentLine > 0)
                        this.SelectLine(CurrentLine - 1);
                }
                else if (PlayerInput.KeyHit(Keys.Down))
                {
                    save = true;
                    if (CurrentLine + 1 < listingBox.Content.Children.Count())
                        SelectLine(CurrentLine + 1);
                }
                else if (!ForbidLineRemoval)
                {
                    if (PlayerInput.KeyHit(Keys.Back) && inputBox.CaretIndex == 0 && CurrentLine > 0 && caretWasAtZero && !ForbidLineRemoval)
                    {
                        var str = inputBox.Text;
                        inputBox.Text = "";
                        RemoveLine(CurrentLine);
                        inputBox.Text = GetLine(CurrentLine);
                        //inputBox.Text = GetLine(CurrentLine);
                        SelectLine(CurrentLine - 1);
                        var caretPos = GetLine(CurrentLine).Length;
                        UpdateLine(GetLine(CurrentLine) + str, CurrentLine);
                        inputBox.CaretIndex = caretPos;
                        for (var i = CurrentLine; i < Lines; i++)
                            UpdateLine(GetLine(i), i);
                    }
                    else if (PlayerInput.KeyHit(Keys.Delete) && inputBox.CaretIndex == inputBox.Text.Length && CurrentLine < Lines - 1 && caretWasAtLast)
                    {
                        var nextLine = GetLine(CurrentLine + 1);
                        UpdateLine(GetLine(CurrentLine) + nextLine, CurrentLine);
                        RemoveLine(CurrentLine + 1);
                        for (var i = CurrentLine+1; i < Lines; i++)
                            UpdateLine(GetLine(i), i);
                    }
                }
                if (save)
                {
                    UpdateLine(line, lineNumber);
                }
                caretWasAtZero = inputBox.CaretIndex == 0;
                caretWasAtLast = inputBox.CaretIndex == inputBox.Text.Length;
            }
            base.UpdateHUD(character, deltaTime, cam);
        }

        protected virtual void PrepareGUI(XElement element)
        {
            if (listingBox != null)
                return;
            if (!Screen.Selected.IsEditor || Screen.Selected is not SubEditorScreen subScreen)
            {
                parent = GuiFrame;
            }
            else
            {
                subScreen.EntityMenu.OnAddedToGUIUpdateList += (_) =>
                {
                    var frame = this.listingBox.Parent.Parent;
                    if (DisplayEditor && !(MapEntity.SelectedList.Contains(Item) || (Screen.Selected is SubEditorScreen subScreen && subScreen.WiringMode && Item.EditingHUD != null && Item.EditingHUD.UserData as Item == Item)))
                        Store();
                    if (DisplayEditor)
                    {
                        frame.AddToGUIUpdateList();
                    }
                };
                ReloadGuiFrame();
                parent = GuiFrame;
            }

            var layoutGroup = new GUILayoutGroup(new RectTransform((parent?.Rect.Size ?? Screen.Selected.Cam.Resolution) - GUIStyle.ItemFrameMargin, parent?.RectTransform, Anchor.Center)
            { AbsoluteOffset = GUIStyle.ItemFrameOffset })
            {
                ChildAnchor = Anchor.TopCenter,
                RelativeSpacing = 0.02f,
                Stretch = true,
            };
            new GUIFrame(new RectTransform(new Vector2(1f, 0.01f), layoutGroup.RectTransform), style: "HorizontalLine");


            var tabsGroup = new GUILayoutGroup(new RectTransform(new Vector2(1, 0.1f), layoutGroup.RectTransform, Anchor.TopLeft))
            {
                ChildAnchor = Anchor.CenterLeft,
                IsHorizontal = true,
                Stretch = true,
            };

            var listingScreen = new GUILayoutGroup(new RectTransform(new Vector2(1, 0.9f), layoutGroup.RectTransform, Anchor.Center))
            {
                ChildAnchor = Anchor.TopCenter,
                RelativeSpacing = 0.02f,
                Stretch = true,
            };

            var listingTab = new GUIButton(new RectTransform(new Vector2(0.5f, 1), tabsGroup.RectTransform, Anchor.CenterLeft))
            {
                Text = "LISTING",
                OnClicked = (_, _) =>
                {
                    if (Debuggable)
                        this.debugScreen.Visible = false;
                    listingScreen.Visible = true;
                    return true;
                },
            };

            if (Debuggable)
            {
                var debugTab = new GUIButton(new RectTransform(new Vector2(0.5f, 1), tabsGroup.RectTransform, Anchor.CenterRight))
                {
                    Text = "DEBUG",
                    OnClicked = (_, _) =>
                    {
                        if (Debuggable)
                            debugScreen.Visible = true;
                        listingScreen.Visible = false;
                        return true;
                    }
                };

                debugScreen = new GUILayoutGroup(new RectTransform(new Vector2(1, 0.9f), listingScreen.RectTransform, Anchor.Center))
                {
                    ChildAnchor = Anchor.Center,
                    Stretch = true,
                    Visible = false,
                };

                PrepareDebugGUI(debugScreen);
            }

            listingBox = new GUIListBox(new RectTransform(new Vector2(1, .8f), layoutGroup.RectTransform), style: null)
            {
                AutoHideScrollBar = false
            };

            inputBox = new GUITextBox(new RectTransform(new Vector2(1, .05f), layoutGroup.RectTransform), "", TextColor, GUIStyle.MonospacedFont)
            {
                MaxTextLength = 65535,
                OverflowClip = true,
                OnEnterPressed = (_, _) =>
                {
                    if (!ForbidLineRemoval)
                    {
                        var text = inputBox.Text;
                        var caret = inputBox.CaretIndex;
                        UpdateLine(text.Substring(0, caret), CurrentLine);
                        InsertLine(text.Substring(caret), CurrentLine + 1);
                        SelectLine(CurrentLine + 1);
                        inputBox.Text = GetLine(CurrentLine);
                        inputBox.CaretIndex = 0;
                    }
                    else
                    {
                        UpdateLine(inputBox.Text, CurrentLine);
                    }
                    return true;
                },
            };
            inputBox.OnTextChanged += (_, _) =>
            {
                if (suppressTextChanged)
                    return false;
                suppressTextChanged = true;
                if (inputBox.Text.Contains("\n"))
                {
                    // this is probably inserted text, so split it by lines and insert new lines.
                    // Ensure any possible line endings are removed:
                    bool atEnd = inputBox.CaretIndex >= inputBox.Text.Length;
                    var lineEnd = inputBox.Text.Substring(inputBox.CaretIndex);
                    var lines = inputBox.Text.Split("\r\n").SelectMany(l => l.Split("\n\r")).SelectMany(l => l.Split("\r")).SelectMany(l => l.Split("\n")).ToList();
                    var lineIdx = CurrentLine;
                    CurrentLine = -1; // temporarily disable cursor;
                    UpdateLine(lines[0], lineIdx);
                    lines.RemoveAt(0);
                    lines.ForEach(line => InsertLine(line, ++lineIdx));
                    var text = GetLine(lineIdx);
                    CurrentLine = lineIdx;
                    UpdateLine(text, lineIdx);
                    inputBox.Text = text;
                    if (!atEnd)
                        inputBox.CaretIndex = inputBox.Text.LastIndexOf(lineEnd);
                }
                UpdateLine(inputBox.Text, CurrentLine);
                suppressTextChanged = false;
                return true;
            };

            storeButton = new GUIButton(new RectTransform(new Vector2(1, .05f), layoutGroup.RectTransform))
            {
                Text = "Write",
                OnClicked = (_, _) =>
                {
                    UpdateLine(inputBox.Text, CurrentLine);
                    Store();
                    return true;
                }
            };
            for (var i = 0; i < Lines; i++)
                UpdateLine(GetLine(i), i);
            layoutGroup.Recalculate();
            inputBox.CaretIndex = 0;
        }

        protected virtual void PrepareDebugGUI(GUILayoutGroup debugScreen)
        {

        }

        protected virtual string GetLine(int lineNumber)
        {
            if (lineNumber < 0 || lineNumber >= Lines)
                return null;
            return ((GUITextBlock)listingBox.Content.GetChild(lineNumber)).Text.SanitizedValue;
        }

        protected virtual string GetHumanReadableLine(int lineNumber)
        {
            return GetLine(lineNumber);
        }

        protected virtual void SelectLine(int selectedLine)
        {
            if (selectedLine < 0 || selectedLine >= MaxLines || selectedLine == CurrentLine)
                return;
            var currentLine = CurrentLine;
            var caretPos = inputBox.CaretIndex;
            CurrentLine = selectedLine;
            if (currentLine >= 0 && currentLine < Lines)
                UpdateLine(inputBox.Text, currentLine);
            if (selectedLine < Lines)
                UpdateLine(GetLine(CurrentLine), CurrentLine);
            inputBox.Text = GetHumanReadableLine(CurrentLine);
            inputBox.CaretIndex = Math.Clamp(caretPos, 0, inputBox.Text.Length);
            // Currently, scroll scrolls to position element on top, so we want to find element which
            // would be on top if we want to place current line in center of the screen.
            var halfHeight = listingBox.RectTransform.Rect.Height / 2.2;
            var elem = listingBox.Content.GetChild(CurrentLine);
            if (elem == null)
            {
                return;
            }
            var idx = CurrentLine;
            var elemY = elem.RectTransform.AbsoluteOffset.Y;
            GUIComponent scrollTo = elem;
            while (--idx >= 0 && (elemY - (scrollTo = listingBox.Content.GetChild(idx)).RectTransform.AbsoluteOffset.Y) < halfHeight) ;
            listingBox.ScrollToElement(scrollTo);
        }

        protected virtual void UpdateLine(RichString line, int lineNumber)
        {
            if (lineNumber < 0 || lineNumber >= MaxLines || listingBox == null)
                return;
            GUITextBlock newBlock;
            if (lineNumber < Lines && listingBox.Content.Children.Count() > lineNumber)
            {
                newBlock = (GUITextBlock)listingBox.Content.Children.ElementAt(lineNumber);
            }
            else
            {
                newBlock = new GUITextBlock(
                    new RectTransform(new Vector2(1, 0), listingBox.Content.RectTransform, anchor: Anchor.TopCenter),
                    line, textColor: TextColor, wrap: false, font: GUIStyle.MonospacedFont)
                {
                    CanBeFocused = false
                };
            }
            newBlock.SetRichText(line);
            if (lineNumber == CurrentLine && !suppressTextChanged)
            {
                var caretPos = inputBox.CaretIndex;
                inputBox.Text = GetLine(lineNumber);
                inputBox.CaretIndex = caretPos;
            }

            listingBox.RecalculateChildren();
            listingBox.UpdateScrollBarSize();
        }

        protected virtual void InsertLine(RichString line, int lineNumber)
        {
            if (lineNumber < 0 || lineNumber >= MaxLines)
                return;
            lineNumber = Math.Min(lineNumber, Lines);
            Lines++;
            GUITextBlock newBlock = new GUITextBlock(
                new RectTransform(new Vector2(1, 0), listingBox.Content.RectTransform, anchor: Anchor.TopCenter),
                line, textColor: TextColor, wrap: false, font: GUIStyle.MonospacedFont)
            {
                CanBeFocused = false
            };
            if (listingBox.Content.GetChildIndex(newBlock) != lineNumber)
            {
                newBlock.RectTransform.RepositionChildInHierarchy(Math.Min(lineNumber, listingBox.Content.Children.Count() - 1));
            }
            for (var i = lineNumber; i < Lines; i++)
                UpdateLine(GetLine(i), i);
            listingBox.RecalculateChildren();
            listingBox.UpdateScrollBarSize();
        }

        protected virtual void RemoveLine(int lineNumber)
        {
            if (lineNumber < 0 || lineNumber >= Lines)
                return;
            Lines--;
            listingBox.RemoveChild(listingBox.Content.Children.ElementAt(lineNumber));
        }

        protected void InitInputBox(string value)
        {
            inputBox.Text = value;
        }

        protected virtual void Store()
        {
            DisplayEditor = false;
            if (inputBox != null)
            {
                UpdateLine(inputBox.Text, CurrentLine);
            }
        }
#endif
    }
}
