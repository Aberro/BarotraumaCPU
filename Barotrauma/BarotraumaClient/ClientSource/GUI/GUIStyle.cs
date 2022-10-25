using System;
using Barotrauma.Extensions;
using Microsoft.Xna.Framework;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;

namespace Barotrauma
{
    public static class GUIStyle
    {
        public readonly static ImmutableDictionary<Identifier, GUIFont> Fonts;
        public readonly static ImmutableDictionary<Identifier, GUISprite> Sprites;
        public readonly static ImmutableDictionary<Identifier, GUISpriteSheet> SpriteSheets;
        public readonly static ImmutableDictionary<Identifier, GUIColor> Colors;
        static GUIStyle()
        {
            var guiClassProperties = typeof(GUIStyle).GetFields(BindingFlags.Public | BindingFlags.Static);

            ImmutableDictionary<Identifier, T> getPropertiesOfType<T>() where T : class
            {
                return guiClassProperties
                    .Where(p => p.FieldType == typeof(T))
                    .Select(p => (p.Name.ToIdentifier(), p.GetValue(null) as T))
                    .ToImmutableDictionary();
            }

            Fonts = getPropertiesOfType<GUIFont>();
            Sprites = getPropertiesOfType<GUISprite>();
            SpriteSheets = getPropertiesOfType<GUISpriteSheet>();
            Colors = getPropertiesOfType<GUIColor>();
        }

        public readonly static PrefabCollection<GUIComponentStyle> ComponentStyles = new PrefabCollection<GUIComponentStyle>();

        public readonly static GUIFont Font = new GUIFont("Font");
        public readonly static GUIFont UnscaledSmallFont = new GUIFont("UnscaledSmallFont");
        public readonly static GUIFont SmallFont = new GUIFont("SmallFont");
        public readonly static GUIFont LargeFont = new GUIFont("LargeFont");
        public readonly static GUIFont SubHeadingFont = new GUIFont("SubHeadingFont");
        public readonly static GUIFont DigitalFont = new GUIFont("DigitalFont");
        public readonly static GUIFont HotkeyFont = new GUIFont("HotkeyFont");
        public readonly static GUIFont MonospacedFont = new GUIFont("MonospacedFont");

        public readonly static GUICursor CursorSprite = new GUICursor("Cursor");

        public readonly static GUISprite SubmarineLocationIcon = new GUISprite("SubmarineLocationIcon");
        public readonly static GUISprite Arrow = new GUISprite("Arrow");
        public readonly static GUISprite SpeechBubbleIcon = new GUISprite("SpeechBubbleIcon");
        public readonly static GUISprite BrokenIcon = new GUISprite("BrokenIcon");
        public readonly static GUISprite YouAreHereCircle = new GUISprite("YouAreHereCircle");

        public readonly static GUISprite Radiation = new GUISprite("Radiation");
        public readonly static GUISpriteSheet RadiationAnimSpriteSheet = new GUISpriteSheet("RadiationAnimSpriteSheet");

        public readonly static GUISpriteSheet SavingIndicator = new GUISpriteSheet("SavingIndicator");
        public readonly static GUISpriteSheet GenericThrobber = new GUISpriteSheet("GenericThrobber");

        public readonly static GUISprite UIGlow = new GUISprite("UIGlow");
        public readonly static GUISprite TalentGlow = new GUISprite("TalentGlow");
        public readonly static GUISprite PingCircle = new GUISprite("PingCircle");
        public readonly static GUISprite UIGlowCircular = new GUISprite("UIGlowCircular");
        public readonly static GUISprite UIGlowSolidCircular = new GUISprite("UIGlowSolidCircular");
        public readonly static GUISprite UIThermalGlow = new GUISprite("UIGlowSolidCircular");
        public readonly static GUISprite ButtonPulse = new GUISprite("ButtonPulse");
        public readonly static GUISprite WalletPortraitBG = new GUISprite("WalletPortraitBG");
        public readonly static GUISprite CrewWalletIconSmall = new GUISprite("CrewWalletIconSmall");

        public readonly static GUISprite EndRoundButtonPulse = new GUISprite("EndRoundButtonPulse");

        public readonly static GUISpriteSheet FocusIndicator = new GUISpriteSheet("FocusIndicator");
        
        public readonly static GUISprite IconOverflowIndicator = new GUISprite("IconOverflowIndicator");

        /// <summary>
        /// General green color used for elements whose colors are set from code
        /// </summary>
        public readonly static GUIColor Green = new GUIColor("Green");

        /// <summary>
        /// General red color used for elements whose colors are set from code
        /// </summary>
        public readonly static GUIColor Orange = new GUIColor("Orange");

        /// <summary>
        /// General red color used for elements whose colors are set from code
        /// </summary>
        public readonly static GUIColor Red = new GUIColor("Red");

        /// <summary>
        /// General blue color used for elements whose colors are set from code
        /// </summary>
        public readonly static GUIColor Blue = new GUIColor("Blue");

        /// <summary>
        /// General yellow color used for elements whose colors are set from code
        /// </summary>
        public readonly static GUIColor Yellow = new GUIColor("Yellow");
        public readonly static GUIColor DimGray = new GUIColor("DimGray"); // #696969
        public readonly static GUIColor Gray = new GUIColor("Gray"); // #808080
        public readonly static GUIColor DarkGray = new GUIColor("DarkGray"); // #A9A9A9
        public readonly static GUIColor Silver = new GUIColor("Silver"); // #C0C0C0
        public readonly static GUIColor LightGray = new GUIColor("LightGray"); // #D3D3D3
        public readonly static GUIColor Gainsboro = new GUIColor("Gainsboro"); // #DCDCDC
        public readonly static GUIColor LightSlateGray = new GUIColor("LightSlateGray"); // #778899
        public readonly static GUIColor SlateGray = new GUIColor("SlateGray"); // #708090
        public readonly static GUIColor MidnightBlue = new GUIColor("MidnightBlue"); // #191970
        public readonly static GUIColor Navy = new GUIColor("Navy"); // #000080
        public readonly static GUIColor DarkBlue = new GUIColor("DarkBlue"); // #00008B
        public readonly static GUIColor MediumBlue = new GUIColor("MediumBlue"); // #0000CD
        public readonly static GUIColor RoyalBlue = new GUIColor("RoyalBlue"); // #4169E1
        public readonly static GUIColor DodgerBlue = new GUIColor("DodgerBlue"); // #1E90FF
        public readonly static GUIColor SteelBlue = new GUIColor("SteelBlue"); // #4682B4
        public readonly static GUIColor CornflowerBlue = new GUIColor("CornflowerBlue"); // #6495ED
        public readonly static GUIColor DeepSkyBlue = new GUIColor("DeepSkyBlue"); // #00BFFF
        public readonly static GUIColor LightSkyBlue = new GUIColor("LightSkyBlue"); // #87CEFA
        public readonly static GUIColor SkyBlue = new GUIColor("SkyBlue"); // #87CEEB
        public readonly static GUIColor PowderBlue = new GUIColor("PowderBlue"); // #B0E0E6
        public readonly static GUIColor LightBlue = new GUIColor("LightBlue"); // #ADD8E6
        public readonly static GUIColor LightSteelBlue = new GUIColor("LightSteelBlue"); // #B0CFDE
        public readonly static GUIColor Lavender = new GUIColor("Lavender"); // #E6E6FA
        public readonly static GUIColor AliceBlue = new GUIColor("AliceBlue"); // #F0F8FF
        public readonly static GUIColor GhostWhite = new GUIColor("GhostWhite"); // #F8F8FF
        public readonly static GUIColor Azure = new GUIColor("Azure"); // #F0FFFF
        public readonly static GUIColor LightCyan = new GUIColor("LightCyan"); // #E0FFFF
        public readonly static GUIColor Aqua = new GUIColor("Aqua"); // #00FFFF
        public readonly static GUIColor PaleTurquoise = new GUIColor("PaleTurquoise"); // #AFEEEE
        public readonly static GUIColor MediumAquaMarine = new GUIColor("MediumAquaMarine"); // #66CDAA
        public readonly static GUIColor Aquamarine = new GUIColor("Aquamarine"); // #7FFFD4
        public readonly static GUIColor Turquoise = new GUIColor("Turquoise"); // #40E0D0
        public readonly static GUIColor MediumTurquoise = new GUIColor("MediumTurquoise"); // #48D1CC
        public readonly static GUIColor DarkTurquoise = new GUIColor("DarkTurquoise"); // #00CED1
        public readonly static GUIColor LightSeaGreen = new GUIColor("LightSeaGreen"); // #20B2AA
        public readonly static GUIColor CadetBlue = new GUIColor("CadetBlue"); // #5F9EA0
        public readonly static GUIColor DarkCyan = new GUIColor("DarkCyan"); // #008B8B
        public readonly static GUIColor Teal = new GUIColor("Teal"); // #008080
        public readonly static GUIColor DarkSlateGray = new GUIColor("DarkSlateGray"); // #25383C
        public readonly static GUIColor SeaGreen = new GUIColor("SeaGreen"); // #2E8B57
        public readonly static GUIColor MediumSeaGreen = new GUIColor("MediumSeaGreen"); // #3CB371
        public readonly static GUIColor OliveDrab = new GUIColor("OliveDrab"); // #6B8E23
        public readonly static GUIColor Olive = new GUIColor("Olive"); // #808000
        public readonly static GUIColor DarkOliveGreen = new GUIColor("DarkOliveGreen"); // #556B2F
        public readonly static GUIColor ForestGreen = new GUIColor("ForestGreen"); // #228B22
        public readonly static GUIColor DarkGreen = new GUIColor("DarkGreen"); // #006400
        public readonly static GUIColor LimeGreen = new GUIColor("LimeGreen"); // #32CD32
        public readonly static GUIColor DarkSeaGreen = new GUIColor("DarkSeaGreen"); // #8FBC8F
        public readonly static GUIColor YellowGreen = new GUIColor("YellowGreen"); // #9ACD32
        public readonly static GUIColor SpringGreen = new GUIColor("SpringGreen"); // #00FF7F
        public readonly static GUIColor MediumSpringGreen = new GUIColor("MediumSpringGreen"); // #00FA9A
        public readonly static GUIColor Lime = new GUIColor("Lime"); // #00FF00
        public readonly static GUIColor LawnGreen = new GUIColor("LawnGreen"); // #7CFC00
        public readonly static GUIColor Chartreuse = new GUIColor("Chartreuse"); // #7FFF00
        public readonly static GUIColor GreenYellow = new GUIColor("GreenYellow"); // #ADFF2F
        public readonly static GUIColor LightGreen = new GUIColor("LightGreen"); // #90EE90
        public readonly static GUIColor PaleGreen = new GUIColor("PaleGreen"); // #98FB98
        public readonly static GUIColor HoneyDew = new GUIColor("HoneyDew"); // #F0FFF0
        public readonly static GUIColor MintCream = new GUIColor("MintCream"); // #F5FFFA
        public readonly static GUIColor LemonChiffon = new GUIColor("LemonChiffon"); // #FFFACD
        public readonly static GUIColor LightGoldenRodYellow = new GUIColor("LightGoldenRodYellow"); // #FAFAD2
        public readonly static GUIColor LightYellow = new GUIColor("LightYellow"); // #FFFFE0
        public readonly static GUIColor Beige = new GUIColor("Beige"); // #F5F5DC
        public readonly static GUIColor Cornsilk = new GUIColor("Cornsilk"); // #FFF8DC
        public readonly static GUIColor AntiqueWhite = new GUIColor("AntiqueWhite"); // #FAEBD7
        public readonly static GUIColor PapayaWhip = new GUIColor("PapayaWhip"); // #FFEFD5
        public readonly static GUIColor BlanchedAlmond = new GUIColor("BlanchedAlmond"); // #FFEBCD
        public readonly static GUIColor Bisque = new GUIColor("Bisque"); // #FFE4C4
        public readonly static GUIColor Wheat = new GUIColor("Wheat"); // #F5DEB3
        public readonly static GUIColor Moccasin = new GUIColor("Moccasin"); // #FFE4B5
        public readonly static GUIColor PeachPuff = new GUIColor("PeachPuff"); // #FFDAB9
        public readonly static GUIColor NavajoWhite = new GUIColor("NavajoWhite"); // #FFDEAD
        public readonly static GUIColor PaleGoldenRod = new GUIColor("PaleGoldenRod"); // #EEE8AA
        public readonly static GUIColor Khaki = new GUIColor("Khaki"); // #F0E68C
        public readonly static GUIColor Gold = new GUIColor("Gold"); // #FFD700
        public readonly static GUIColor SandyBrown = new GUIColor("SandyBrown"); // #F4A460
        public readonly static GUIColor BurlyWood = new GUIColor("BurlyWood"); // #DEB887
        public readonly static GUIColor Tan = new GUIColor("Tan"); // #D2B48C
        public readonly static GUIColor DarkKhaki = new GUIColor("DarkKhaki"); // #BDB76B
        public readonly static GUIColor GoldenRod = new GUIColor("GoldenRod"); // #DAA520
        public readonly static GUIColor DarkGoldenRod = new GUIColor("DarkGoldenRod"); // #B8860B
        public readonly static GUIColor Peru = new GUIColor("Peru"); // #CD853F
        public readonly static GUIColor Sienna = new GUIColor("Sienna"); // #A0522D
        public readonly static GUIColor SaddleBrown = new GUIColor("SaddleBrown"); // #8B4513
        public readonly static GUIColor Chocolate = new GUIColor("Chocolate"); // #D2691E
        public readonly static GUIColor DarkOrange = new GUIColor("DarkOrange"); // #FF8C00
        public readonly static GUIColor Coral = new GUIColor("Coral"); // #FF7F50
        public readonly static GUIColor LightSalmon = new GUIColor("LightSalmon"); // #FFA07A
        public readonly static GUIColor DarkSalmon = new GUIColor("DarkSalmon"); // #E9967A
        public readonly static GUIColor Salmon = new GUIColor("Salmon"); // #FA8072
        public readonly static GUIColor LightCoral = new GUIColor("LightCoral"); // #F08080
        public readonly static GUIColor IndianRed = new GUIColor("IndianRed"); // #CD5C5C
        public readonly static GUIColor Tomato = new GUIColor("Tomato"); // #FF6347
        public readonly static GUIColor OrangeRed = new GUIColor("OrangeRed"); // #FF4500
        public readonly static GUIColor FireBrick = new GUIColor("FireBrick"); // #B22222
        public readonly static GUIColor Brown = new GUIColor("Brown"); // #A52A2A
        public readonly static GUIColor DarkRed = new GUIColor("DarkRed"); // #8B0000
        public readonly static GUIColor Maroon = new GUIColor("Maroon"); // #800000
        public readonly static GUIColor RosyBrown = new GUIColor("RosyBrown"); // #BC8F8F
        public readonly static GUIColor MistyRose = new GUIColor("MistyRose"); // #FFE4E1
        public readonly static GUIColor Pink = new GUIColor("Pink"); // #FFC0CB
        public readonly static GUIColor LightPink = new GUIColor("LightPink"); // #FFB6C1
        public readonly static GUIColor PaleVioletRed = new GUIColor("PaleVioletRed"); // #DB7093
        public readonly static GUIColor HotPink = new GUIColor("HotPink"); // #FF69B4
        public readonly static GUIColor DeepPink = new GUIColor("DeepPink"); // #FF1493
        public readonly static GUIColor Crimson = new GUIColor("Crimson"); // #DC143C
        public readonly static GUIColor MediumVioletRed = new GUIColor("MediumVioletRed"); // #C71585
        public readonly static GUIColor Orchid = new GUIColor("Orchid"); // #DA70D6
        public readonly static GUIColor Violet = new GUIColor("Violet"); // #EE82EE
        public readonly static GUIColor Fuchsia = new GUIColor("Fuchsia"); // #FF00FF
        public readonly static GUIColor MediumOrchid = new GUIColor("MediumOrchid"); // #BA55D3
        public readonly static GUIColor SlateBlue = new GUIColor("SlateBlue"); // #6A5ACD
        public readonly static GUIColor MediumSlateBlue = new GUIColor("MediumSlateBlue"); // #7B68EE
        public readonly static GUIColor DarkSlateBlue = new GUIColor("DarkSlateBlue"); // #483D8B
        public readonly static GUIColor Indigo = new GUIColor("Indigo"); // #4B0082
        public readonly static GUIColor RebeccaPurple = new GUIColor("RebeccaPurple"); // #663399
        public readonly static GUIColor DarkMagenta = new GUIColor("DarkMagenta"); // #8B008B
        public readonly static GUIColor Purple = new GUIColor("Purple"); // #800080
        public readonly static GUIColor DarkOrchid = new GUIColor("DarkOrchid"); // #9932CC
        public readonly static GUIColor DarkViolet = new GUIColor("DarkViolet"); // #9400D3
        public readonly static GUIColor BlueViolet = new GUIColor("BlueViolet"); // #8A2BE2
        public readonly static GUIColor MediumPurple = new GUIColor("MediumPurple"); // #9370DB
        public readonly static GUIColor Plum = new GUIColor("Plum"); // #DDA0DD
        public readonly static GUIColor Thistle = new GUIColor("Thistle"); // #D8BFD8
        public readonly static GUIColor LavenderBlush = new GUIColor("LavenderBlush"); // #FFF0F5
        public readonly static GUIColor OldLace = new GUIColor("OldLace"); // #FDF5E6
        public readonly static GUIColor Linen = new GUIColor("Linen"); // #FAF0E6
        public readonly static GUIColor SeaShell = new GUIColor("SeaShell"); // #FFF5EE
        public readonly static GUIColor FloralWhite = new GUIColor("FloralWhite"); // #FFFAF0
        public readonly static GUIColor Ivory = new GUIColor("Ivory"); // #FFFFF0
        public readonly static GUIColor WhiteSmoke = new GUIColor("WhiteSmoke"); // #F5F5F5
        public readonly static GUIColor Snow = new GUIColor("Snow"); // #FFFAFA
        public readonly static GUIColor White = new GUIColor("White"); // #FFFFFF

        /// <summary>
        /// Color to display the name of modded servers in the server list.
        /// </summary>
        public readonly static GUIColor ModdedServerColor = new GUIColor("ModdedServerColor");

        public readonly static GUIColor ColorInventoryEmpty = new GUIColor("ColorInventoryEmpty");
        public readonly static GUIColor ColorInventoryHalf = new GUIColor("ColorInventoryHalf");
        public readonly static GUIColor ColorInventoryFull = new GUIColor("ColorInventoryFull");
        public readonly static GUIColor ColorInventoryBackground = new GUIColor("ColorInventoryBackground");
        public readonly static GUIColor ColorInventoryEmptyOverlay = new GUIColor("ColorInventoryEmptyOverlay");

        public readonly static GUIColor TextColorNormal = new GUIColor("TextColorNormal");
        public readonly static GUIColor TextColorBright = new GUIColor("TextColorBright");
        public readonly static GUIColor TextColorDark = new GUIColor("TextColorDark");
        public readonly static GUIColor TextColorDim = new GUIColor("TextColorDim");

        public readonly static GUIColor ItemQualityColorPoor = new GUIColor("ItemQualityColorPoor");
        public readonly static GUIColor ItemQualityColorNormal = new GUIColor("ItemQualityColorNormal");
        public readonly static GUIColor ItemQualityColorGood = new GUIColor("ItemQualityColorGood");
        public readonly static GUIColor ItemQualityColorExcellent = new GUIColor("ItemQualityColorExcellent");
        public readonly static GUIColor ItemQualityColorMasterwork = new GUIColor("ItemQualityColorMasterwork");
            
        public readonly static GUIColor ColorReputationVeryLow = new GUIColor("ColorReputationVeryLow");
        public readonly static GUIColor ColorReputationLow = new GUIColor("ColorReputationLow");
        public readonly static GUIColor ColorReputationNeutral = new GUIColor("ColorReputationNeutral");
        public readonly static GUIColor ColorReputationHigh = new GUIColor("ColorReputationHigh");
        public readonly static GUIColor ColorReputationVeryHigh = new GUIColor("ColorReputationVeryHigh");

        // Inventory
        public readonly static GUIColor EquipmentSlotIconColor = new GUIColor("EquipmentSlotIconColor");

        // Health HUD
        public readonly static GUIColor BuffColorLow = new GUIColor("BuffColorLow");
        public readonly static GUIColor BuffColorMedium = new GUIColor("BuffColorMedium");
        public readonly static GUIColor BuffColorHigh = new GUIColor("BuffColorHigh");

        public readonly static GUIColor DebuffColorLow = new GUIColor("DebuffColorLow");
        public readonly static GUIColor DebuffColorMedium = new GUIColor("DebuffColorMedium");
        public readonly static GUIColor DebuffColorHigh = new GUIColor("DebuffColorHigh");

        public readonly static GUIColor HealthBarColorLow = new GUIColor("HealthBarColorLow");
        public readonly static GUIColor HealthBarColorMedium = new GUIColor("HealthBarColorMedium");
        public readonly static GUIColor HealthBarColorHigh = new GUIColor("HealthBarColorHigh");

        public static Point ItemFrameMargin 
        {
            get 
            { 
                Point size = new Point(50, 56).Multiply(GUI.SlicedSpriteScale);

                var style = GetComponentStyle("ItemUI"); 
                var sprite = style?.Sprites[GUIComponent.ComponentState.None].First();
                if (sprite != null)
                {
                    size.X = Math.Min(sprite.Slices[0].Width + sprite.Slices[2].Width, size.X);
                    size.Y = Math.Min(sprite.Slices[0].Height + sprite.Slices[6].Height, size.Y);
                }
                return size;
            } 
        }

        public static Point ItemFrameOffset => new Point(0, 3).Multiply(GUI.SlicedSpriteScale);

        public static GUIComponentStyle GetComponentStyle(string styleName)
        {
            return GetComponentStyle(styleName.ToIdentifier());
        }

        public static GUIComponentStyle GetComponentStyle(Identifier identifier)
            => ComponentStyles.TryGet(identifier, out var style) ? style : null;

        public static void Apply(GUIComponent targetComponent, string styleName = "", GUIComponent parent = null)
        {
            Apply(targetComponent, styleName.ToIdentifier(), parent);
        }

        public static void Apply(GUIComponent targetComponent, Identifier styleName, GUIComponent parent = null)
        {
            GUIComponentStyle componentStyle;
            if (parent != null)
            {
                GUIComponentStyle parentStyle = parent.Style;

                if (parentStyle == null)
                {
                    Identifier parentStyleName = parent.GetType().Name.ToIdentifier();

                    if (!ComponentStyles.ContainsKey(parentStyleName))
                    {
                        DebugConsole.ThrowError($"Couldn't find a GUI style \"{parentStyleName}\"");
                        return;
                    }
                    parentStyle = ComponentStyles[parentStyleName];
                }
                Identifier childStyleName = styleName.IsEmpty ? targetComponent.GetType().Name.ToIdentifier() : styleName;
                parentStyle.ChildStyles.TryGetValue(childStyleName, out componentStyle);
            }
            else
            {
                Identifier styleIdentifier = styleName.ToIdentifier();
                if (styleIdentifier == Identifier.Empty)
                {
                    styleIdentifier = targetComponent.GetType().Name.ToIdentifier();
                }
                if (!ComponentStyles.ContainsKey(styleIdentifier))
                {
                    DebugConsole.ThrowError($"Couldn't find a GUI style \"{styleIdentifier}\"");
                    return;
                }
                componentStyle = ComponentStyles[styleIdentifier];
            }

            targetComponent.ApplyStyle(componentStyle);
        }

        public static GUIColor GetQualityColor(int quality)
        {
            switch (quality)
            {
                case 1:
                    return ItemQualityColorGood;
                case 2:
                    return ItemQualityColorExcellent;
                case 3:
                    return ItemQualityColorMasterwork;
                case -1:
                    return ItemQualityColorPoor;
                default:
                    return ItemQualityColorNormal;
            }
        }

        public static void RecalculateFonts()
        {
            foreach (var font in Fonts.Values)
            {
                font.Prefabs.ForEach(p => p.LoadFont());
            }
        }

        public static void RecalculateSizeRestrictions()
        {
            foreach (var componentStyle in ComponentStyles)
            {
                componentStyle.RefreshSize();
            }
        }
    }
}
