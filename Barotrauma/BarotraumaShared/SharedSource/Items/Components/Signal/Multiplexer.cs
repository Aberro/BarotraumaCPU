using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Barotrauma.Networking;
using Microsoft.Xna.Framework;
#if CLIENT
using Microsoft.Xna.Framework.Input;
using SharpFont;
#endif

namespace Barotrauma.Items.Components;
class MultiplexerComponent : ItemComponent
{
    private static ByteConverter ByteConverter = new ByteConverter();
    [InGameEditable(MinValueInt = 0, MaxValueInt = 255), Serialize(0, IsPropertySaveable.Yes, "Input/output channel bit mask", alwaysUseInstanceValues: true)]
    public int Channel { get; set; }
    [Editable(ReadOnly = true), Serialize(false, IsPropertySaveable.Yes, description: "", alwaysUseInstanceValues: true)]
    public bool Inverted { get; set; }

    public MultiplexerComponent(Item item, ContentXElement element)
        : base(item, element)
    {

    }

    public override void ReceiveSignal(Signal signal, Connection connection)
    {
        switch (connection.Name)
        {
            case "switch_in":
                Channel = ByteConverter.IsValid(signal.value) ? ((byte)(ByteConverter.ConvertFromString(signal.value) ?? 0)) : Channel;
                break;
            case "signal_in":
                if (!Inverted && Channel > 0)
                {
                    if((Channel & 0x01) != 0)
                        Item.SendSignal(signal, $"signal_out0");
                    if ((Channel & 0x02) != 0)
                        Item.SendSignal(signal, $"signal_out1");
                    if ((Channel & 0x04) != 0)
                        Item.SendSignal(signal, $"signal_out2");
                    if ((Channel & 0x08) != 0)
                        Item.SendSignal(signal, $"signal_out3");
                }
                break;
            case "signal_in0":
                if (Inverted && (Channel & 0x01) != 0)
                    Item.SendSignal(signal, $"signal_out");
                break;
            case "signal_in1":
                if (Inverted && (Channel & 0x02) != 0)
                    Item.SendSignal(signal, $"signal_out");
                break;
            case "signal_in2":
                if (Inverted && (Channel & 0x04) != 0)
                    Item.SendSignal(signal, $"signal_out");
                break;
            case "signal_in3":
                if (Inverted && (Channel & 0x08) != 0)
                    Item.SendSignal(signal, $"signal_out");
                break;
        }
    }
}
