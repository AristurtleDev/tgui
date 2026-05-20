// Copyright (c) Christopher Whitley (AristurtleDev). All rights reserved.
// Licensed under the MIT license.
// See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace TGui;

#region Primitives

public readonly struct TGuiColor : IEquatable<TGuiColor>
{
    public static readonly TGuiColor Transparent = new(0, 0, 0, 0);
    public static readonly TGuiColor White = new(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
    public static readonly TGuiColor Black = new(0, 0, 0, byte.MaxValue);

    public readonly byte R;
    public readonly byte G;
    public readonly byte B;
    public readonly byte A;

    public TGuiColor(byte r, byte g, byte b, byte a) => (R, G, B, A) = (r, g, b, a);

    public static TGuiColor FromPackedRgba(int packedRgba)
    {
        uint value = (uint)packedRgba;

        byte r = (byte)((value >> 24) & 0xFF);
        byte g = (byte)((value >> 16) & 0xFF);
        byte b = (byte)((value >> 8) & 0xFF);
        byte a = (byte)(value & 0xFF);

        return new TGuiColor(r, g, b, a);
    }

    public int ToPackedRgba()
    {
        return (int)(((uint)R << 24) |
                     ((uint)G << 16) |
                     ((uint)B << 8) |
                     A);

    }

    public static TGuiColor Fade(TGuiColor color, float alpha)
    {
        alpha = Math.Clamp(alpha, 0.0f, 1.0f);
        return new TGuiColor(color.R, color.G, color.B, (byte)(color.A * alpha));
    }

    public bool Equals(TGuiColor other) => R == other.R &&
                                           G == other.G &&
                                           B == other.B &&
                                           A == other.A;

    public override bool Equals([NotNullWhen(true)] object? obj) => obj is TGuiColor other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(R, G, B, A);
    public override string ToString() => $"(R:{R}, G:{G}, B:{B}, A:{A})";
    public static bool operator ==(TGuiColor left, TGuiColor right) => left.Equals(right);
    public static bool operator !=(TGuiColor left, TGuiColor right) => !left.Equals(right);
}

public readonly struct TGuiRectangle : IEquatable<TGuiRectangle>
{
    public readonly float X;
    public readonly float Y;
    public readonly float Width;
    public readonly float Height;
    public float Left => X;
    public float Top => Y;
    public float Right => X + Width;
    public float Bottom => Y + Height;

    public TGuiRectangle(float x, float y, float width, float height) => (X, Y, Width, Height) = (x, y, width, height);

    public bool Contains(Vector2 point) => point.X >= X &&
                                           point.X <= Right &&
                                           point.Y >= Y &&
                                           point.Y <= Bottom;

    public TGuiRectangle Offset(float x, float y) => new TGuiRectangle(X + x, Y + y, Width, Height);

    public TGuiRectangle Inset(float amount)
    {
        float insetX = X + amount;
        float insetY = Y + amount;
        float insetWidth = Width - (2.0f * amount);
        float insetHeight = Height - (2.0f * amount);

        if (float.IsNegative(insetWidth))
        {
            insetX = X + (Width * 0.5f);
            insetWidth = 0.0f;
        }

        if (float.IsNegative(insetHeight))
        {
            insetY = Y + (Height * 0.5f);
            insetHeight = 0.0f;
        }

        return new TGuiRectangle(insetX, insetY, insetWidth, insetHeight);
    }

    public bool Equals(TGuiRectangle other) => X == other.X &&
                                               Y == other.Y &&
                                               Width == other.Width &&
                                               Height == other.Height;

    public override bool Equals([NotNullWhen(true)] object? obj) => obj is TGuiRectangle other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);
    public override string ToString() => $"({X}, {Y}, {Width}, {Height})";
    public static bool operator ==(TGuiRectangle left, TGuiRectangle right) => left.Equals(right);
    public static bool operator !=(TGuiRectangle left, TGuiRectangle right) => !left.Equals(right);
}

public readonly struct TGuiCornerRadius : IEquatable<TGuiCornerRadius>
{
    public static readonly TGuiCornerRadius Zero = new TGuiCornerRadius(0.0f);

    public readonly float TopLeft;
    public readonly float TopRight;
    public readonly float BottomRight;
    public readonly float BottomLeft;

    public TGuiCornerRadius(float uniformRadius) =>
        (TopLeft, TopRight, BottomRight, BottomLeft) = (uniformRadius, uniformRadius, uniformRadius, uniformRadius);

    public TGuiCornerRadius(float topLeft, float topRight, float bottomRight, float bottomLeft) =>
        (TopLeft, TopRight, BottomLeft, BottomLeft) = (topLeft, topRight, bottomRight, bottomLeft);

    public TGuiCornerRadius Inset(float amount)
    {
        float topLeft = MathF.Max(0.0f, TopLeft - amount);
        float topRight = MathF.Max(0.0f, TopRight - amount);
        float bottomRight = MathF.Max(0.0f, BottomRight - amount);
        float bottomLeft = MathF.Max(0.0f, BottomLeft - amount);

        return new TGuiCornerRadius(topLeft, topRight, bottomRight, bottomLeft);
    }

    public TGuiCornerRadius ClampToBounds(float width, float height)
    {
        if (width <= 0.0f || height <= 0.0f) { return Zero; }

        float topLeft = MathF.Max(0.0f, TopLeft);
        float topRight = MathF.Max(0.0f, TopRight);
        float bottomRight = MathF.Max(0.0f, BottomRight);
        float bottomLeft = MathF.Max(0.0f, BottomLeft);

        float scale = 1.0f;
        scale = MathF.Min(scale, GetEdgeScale(width, topLeft, topRight));
        scale = MathF.Min(scale, GetEdgeScale(width, bottomLeft, bottomRight));
        scale = MathF.Min(scale, GetEdgeScale(height, topLeft, bottomLeft));
        scale = MathF.Min(scale, GetEdgeScale(height, topLeft, bottomRight));

        return new TGuiCornerRadius(topLeft * scale, topRight * scale, bottomRight * scale, bottomLeft * scale);
    }

    public TGuiCornerRadius ClampToBounds(TGuiRectangle bounds) => ClampToBounds(bounds.Width, bounds.Height);

    public Vector2 GetTopLeftCenter(TGuiRectangle bounds)
    {
        TGuiCornerRadius radius = ClampToBounds(bounds);

        float x = bounds.Left + radius.TopLeft;
        float y = bounds.Top + radius.TopLeft;
        return new Vector2(x, y);
    }

    public Vector2 GetTopRightCenter(TGuiRectangle bounds)
    {
        TGuiCornerRadius radius = ClampToBounds(bounds);

        float x = bounds.Right - radius.TopRight;
        float y = bounds.Top - radius.TopRight;
        return new Vector2(x, y);
    }

    public Vector2 GetBottomRightCenter(TGuiRectangle bounds)
    {
        TGuiCornerRadius radius = ClampToBounds(bounds);

        float x = bounds.Right - radius.BottomRight;
        float y = bounds.Bottom - radius.BottomRight;
        return new Vector2(x, y);
    }

    public Vector2 GetBottomLeftCenter(TGuiRectangle bounds)
    {
        TGuiCornerRadius radius = ClampToBounds(bounds);

        float x = bounds.Left + radius.BottomLeft;
        float y = bounds.Bottom - radius.BottomLeft;
        return new Vector2(x, y);
    }

    private static float GetEdgeScale(float availableLength, float firstRadius, float secondRadius)
    {
        float radiusSum = firstRadius + secondRadius;
        if (radiusSum <= 0.0f || radiusSum <= availableLength) { return 1.0f; }
        return availableLength / radiusSum;
    }

    public bool Equals(TGuiCornerRadius other) => TopLeft == other.TopLeft &&
                                                  TopRight == other.TopRight &&
                                                  BottomRight == other.BottomRight &&
                                                  BottomLeft == other.BottomLeft;

    public override bool Equals([NotNullWhen(true)] object? obj) => obj is TGuiCornerRadius other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(TopLeft, TopRight, BottomRight, BottomLeft);
    public override string ToString() => $"(TL:{TopLeft}, TR:{TopRight}, BR:{BottomRight}, BL:{BottomLeft})";
    public static bool operator ==(TGuiCornerRadius left, TGuiCornerRadius right) => left.Equals(right);
    public static bool operator !=(TGuiCornerRadius left, TGuiCornerRadius right) => !left.Equals(right);
}

#endregion Primitives

#region Input

public enum TGuiKey
{
    Backspace,
    Enter,
    KeyPadEnter,
    RightArrow,
    LeftArrow,
    Down,
    Up,
    Delete,
    Home,
    End,
    LeftControl,
    RightControl,
    LeftShift,
    Minus,
    V,
    C,
    X,
}

public enum TGuiMouseButton
{
    Left,
    Middle,
    Right,
    XButton1,
    XButton2
}

#endregion Input

#region Enums

public enum TGuiControl
{
    Default,
    Label,
    Button,
    Toggle,
    Slider,
    ProgressBar,
    Checkbox,
    DropdownBox,
    TextBox,
    ValueBox,
    ListView,
    ScrollBar,
    StatusBar
}

public enum TGuiControlState
{
    Normal,
    Focused,
    Pressed,
    Disabled
}

public enum TGuiScrollBarSide
{
    Left,
    Right
}

public enum TGuiTextAlignment
{
    Left,
    Center,
    Right
}

public enum TGuiTextAlignmentVertical
{
    Top,
    Middle,
    Bottom
}

public enum TGuiTextWrapMode
{
    None,
    Char,
    Word
}

#endregion Enums

#region Style

public enum TGuiStyleColor
{
    Border,
    BorderFocused,
    BorderPressed,
    BorderDisabled,
    Surface,
    SurfaceFocused,
    SurfacePressed,
    SurfaceDisabled,
    Text,
    TextFocused,
    TextPressed,
    TextDisabled,
    Line,
    Background
}

public enum TGuiStyleVar
{
    BorderWidth,
    TextPadding,
    TextAlignment,
    TextSize,
    TextSpacing,
    TextLineSpacing,
    TextAlignmentVertical,
    TextWrapMode,
    PanelCornerRadius,
    ToggleGroupPadding,
    CheckboxCheckPadding,
    DropdownButtonSpacing,
    DropdownArrowPadding,
    DropdownItemsSpacing,
    DropdownArrowVisible,
    DropdownRollUp,
    TextBoxReadOnly,
    SliderThumbWidth,
    SliderPadding,
    ProgressPadding,
    ProgressSide,
    ListItemHeight,
    ListItemSpacing,
    ListItemBorderVisible,
    ListItemBorderWidth,
    ScrollBarWidth,
    ListViewScrollBarSide,
    ScrollBarArrowSize,
    ScrollBarArrowsVisible,
    ScrollBarSliderPadding,
    ScrollBarSliderSize,
    ScrollBarPadding,
    ScrollBarScrollSpeed,
    SpinnerButtonWidth,
    SpinnerButtonSpacing
}

// Themes based on Catppucin palettes
// https://github.com/catppuccin/catppuccin
// -----------------------------------------------------------------------------
// MIT License
//
// Copyright (c) 2021 Catppuccin
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
// -----------------------------------------------------------------------------
public enum TGuiTheme
{
    Latte,
    Frappe,
    Macchiato,
    Mocha,
}

internal static class TGuiLatteTheme
{
    public static void Apply(TGuiStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);

        ApplySharedDefaults(style);
        ApplyControlDefaults(style);
    }

    private static void ApplySharedDefaults(TGuiStyle style)
    {
        style.SetColor(TGuiStyleColor.Border, new TGuiColor(156, 160, 176, 255));
        style.SetColor(TGuiStyleColor.Surface, new TGuiColor(230, 233, 239, 255));
        style.SetColor(TGuiStyleColor.Text, new TGuiColor(76, 79, 105, 255));
        style.SetColor(TGuiStyleColor.BorderFocused, new TGuiColor(114, 135, 253, 255));
        style.SetColor(TGuiStyleColor.SurfaceFocused, new TGuiColor(204, 208, 218, 255));
        style.SetColor(TGuiStyleColor.TextFocused, new TGuiColor(114, 135, 253, 255));
        style.SetColor(TGuiStyleColor.BorderPressed, new TGuiColor(30, 102, 245, 255));
        style.SetColor(TGuiStyleColor.SurfacePressed, new TGuiColor(188, 192, 204, 255));
        style.SetColor(TGuiStyleColor.TextPressed, new TGuiColor(30, 102, 245, 255));
        style.SetColor(TGuiStyleColor.BorderDisabled, new TGuiColor(172, 176, 190, 255));
        style.SetColor(TGuiStyleColor.SurfaceDisabled, new TGuiColor(239, 241, 245, 255));
        style.SetColor(TGuiStyleColor.TextDisabled, new TGuiColor(108, 111, 133, 255));
        style.SetColor(TGuiStyleColor.Line, new TGuiColor(140, 143, 161, 255));
        style.SetColor(TGuiStyleColor.Background, new TGuiColor(239, 241, 245, 255));

        style.SetVar(TGuiStyleVar.BorderWidth, 1);
        style.SetVar(TGuiStyleVar.TextPadding, 0);
        style.SetVar(TGuiStyleVar.TextAlignment, (int)TGuiTextAlignment.Center);
        style.SetVar(TGuiStyleVar.TextSize, TGuiStyle.DefaultTextSize);
        style.SetVar(TGuiStyleVar.TextSpacing, 1);
        style.SetVar(TGuiStyleVar.TextLineSpacing, 4);
        style.SetVar(TGuiStyleVar.TextAlignmentVertical, (int)TGuiTextAlignmentVertical.Middle);
        style.SetVar(TGuiStyleVar.PanelCornerRadius, 0);
    }

    private static void ApplyControlDefaults(TGuiStyle style)
    {
        style.SetVar(TGuiControl.Label, TGuiStyleVar.TextAlignment, (int)TGuiTextAlignment.Left);
        style.SetVar(TGuiControl.Button, TGuiStyleVar.BorderWidth, 1);
        style.SetVar(TGuiControl.Slider, TGuiStyleVar.TextPadding, 4);
        style.SetVar(TGuiControl.ProgressBar, TGuiStyleVar.TextPadding, 4);
        style.SetVar(TGuiControl.Checkbox, TGuiStyleVar.TextPadding, 6);
        style.SetVar(TGuiControl.Checkbox, TGuiStyleVar.TextAlignment, (int)TGuiTextAlignment.Right);
        style.SetVar(TGuiControl.DropdownBox, TGuiStyleVar.TextPadding, 0);
        style.SetVar(TGuiControl.DropdownBox, TGuiStyleVar.TextAlignment, (int)TGuiTextAlignment.Center);
        style.SetVar(TGuiStyleVar.DropdownArrowVisible, 1);
        style.SetVar(TGuiControl.TextBox, TGuiStyleVar.TextPadding, 6);
        style.SetVar(TGuiControl.TextBox, TGuiStyleVar.TextAlignment, (int)TGuiTextAlignment.Left);
        style.SetVar(TGuiControl.ValueBox, TGuiStyleVar.TextPadding, 6);
        style.SetVar(TGuiControl.ValueBox, TGuiStyleVar.TextAlignment, (int)TGuiTextAlignment.Left);
        style.SetVar(TGuiControl.StatusBar, TGuiStyleVar.TextPadding, 8);
        style.SetVar(TGuiControl.StatusBar, TGuiStyleVar.TextAlignment, (int)TGuiTextAlignment.Left);

        style.SetVar(TGuiStyleVar.ToggleGroupPadding, 3);
        style.SetVar(TGuiStyleVar.SliderThumbWidth, 18);
        style.SetVar(TGuiStyleVar.SliderPadding, 2);
        style.SetVar(TGuiStyleVar.ProgressPadding, 2);
        style.SetVar(TGuiStyleVar.CheckboxCheckPadding, 2);
        style.SetVar(TGuiStyleVar.DropdownButtonSpacing, 2);
        style.SetVar(TGuiStyleVar.DropdownArrowPadding, 18);
        style.SetVar(TGuiStyleVar.DropdownItemsSpacing, 2);
        style.SetVar(TGuiStyleVar.SpinnerButtonWidth, 28);
        style.SetVar(TGuiStyleVar.SpinnerButtonSpacing, 4);
        style.SetVar(TGuiControl.ScrollBar, TGuiStyleVar.BorderWidth, 0);
        style.SetVar(TGuiStyleVar.ScrollBarArrowsVisible, 0);
        style.SetVar(TGuiStyleVar.ScrollBarArrowSize, 7);
        style.SetVar(TGuiStyleVar.ScrollBarSliderPadding, 0);
        style.SetVar(TGuiStyleVar.ScrollBarSliderSize, 18);
        style.SetVar(TGuiStyleVar.ScrollBarPadding, 0);
        style.SetVar(TGuiStyleVar.ScrollBarScrollSpeed, 12);
        style.SetVar(TGuiStyleVar.ListItemHeight, 28);
        style.SetVar(TGuiStyleVar.ListItemSpacing, 2);
        style.SetVar(TGuiStyleVar.ListItemBorderWidth, 1);
        style.SetVar(TGuiStyleVar.ScrollBarWidth, 12);
        style.SetVar(TGuiStyleVar.ListViewScrollBarSide, (int)TGuiScrollBarSide.Right);
    }
}

internal static class TGuiFrappeTheme
{
    public static void Apply(TGuiStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);

        ApplySharedDefaults(style);
        ApplyControlDefaults(style);
    }

    private static void ApplySharedDefaults(TGuiStyle style)
    {
        style.SetColor(TGuiStyleColor.Border, new TGuiColor(115, 121, 148, 255));
        style.SetColor(TGuiStyleColor.Surface, new TGuiColor(41, 44, 60, 255));
        style.SetColor(TGuiStyleColor.Text, new TGuiColor(198, 208, 245, 255));
        style.SetColor(TGuiStyleColor.BorderFocused, new TGuiColor(186, 187, 241, 255));
        style.SetColor(TGuiStyleColor.SurfaceFocused, new TGuiColor(65, 69, 89, 255));
        style.SetColor(TGuiStyleColor.TextFocused, new TGuiColor(186, 187, 241, 255));
        style.SetColor(TGuiStyleColor.BorderPressed, new TGuiColor(140, 170, 238, 255));
        style.SetColor(TGuiStyleColor.SurfacePressed, new TGuiColor(81, 87, 109, 255));
        style.SetColor(TGuiStyleColor.TextPressed, new TGuiColor(140, 170, 238, 255));
        style.SetColor(TGuiStyleColor.BorderDisabled, new TGuiColor(98, 104, 128, 255));
        style.SetColor(TGuiStyleColor.SurfaceDisabled, new TGuiColor(48, 52, 70, 255));
        style.SetColor(TGuiStyleColor.TextDisabled, new TGuiColor(165, 173, 206, 255));
        style.SetColor(TGuiStyleColor.Line, new TGuiColor(131, 139, 167, 255));
        style.SetColor(TGuiStyleColor.Background, new TGuiColor(48, 52, 70, 255));

        style.SetVar(TGuiStyleVar.BorderWidth, 1);
        style.SetVar(TGuiStyleVar.TextPadding, 0);
        style.SetVar(TGuiStyleVar.TextAlignment, (int)TGuiTextAlignment.Center);
        style.SetVar(TGuiStyleVar.TextSize, TGuiStyle.DefaultTextSize);
        style.SetVar(TGuiStyleVar.TextSpacing, 1);
        style.SetVar(TGuiStyleVar.TextLineSpacing, 4);
        style.SetVar(TGuiStyleVar.TextAlignmentVertical, (int)TGuiTextAlignmentVertical.Middle);
        style.SetVar(TGuiStyleVar.PanelCornerRadius, 0);
    }

    private static void ApplyControlDefaults(TGuiStyle style)
    {
        style.SetVar(TGuiControl.Label, TGuiStyleVar.TextAlignment, (int)TGuiTextAlignment.Left);
        style.SetVar(TGuiControl.Button, TGuiStyleVar.BorderWidth, 1);
        style.SetVar(TGuiControl.Slider, TGuiStyleVar.TextPadding, 4);
        style.SetVar(TGuiControl.ProgressBar, TGuiStyleVar.TextPadding, 4);
        style.SetVar(TGuiControl.Checkbox, TGuiStyleVar.TextPadding, 6);
        style.SetVar(TGuiControl.Checkbox, TGuiStyleVar.TextAlignment, (int)TGuiTextAlignment.Right);
        style.SetVar(TGuiControl.DropdownBox, TGuiStyleVar.TextPadding, 0);
        style.SetVar(TGuiControl.DropdownBox, TGuiStyleVar.TextAlignment, (int)TGuiTextAlignment.Center);
        style.SetVar(TGuiStyleVar.DropdownArrowVisible, 1);
        style.SetVar(TGuiControl.TextBox, TGuiStyleVar.TextPadding, 6);
        style.SetVar(TGuiControl.TextBox, TGuiStyleVar.TextAlignment, (int)TGuiTextAlignment.Left);
        style.SetVar(TGuiControl.ValueBox, TGuiStyleVar.TextPadding, 6);
        style.SetVar(TGuiControl.ValueBox, TGuiStyleVar.TextAlignment, (int)TGuiTextAlignment.Left);
        style.SetVar(TGuiControl.StatusBar, TGuiStyleVar.TextPadding, 8);
        style.SetVar(TGuiControl.StatusBar, TGuiStyleVar.TextAlignment, (int)TGuiTextAlignment.Left);

        style.SetVar(TGuiStyleVar.ToggleGroupPadding, 3);
        style.SetVar(TGuiStyleVar.SliderThumbWidth, 18);
        style.SetVar(TGuiStyleVar.SliderPadding, 2);
        style.SetVar(TGuiStyleVar.ProgressPadding, 2);
        style.SetVar(TGuiStyleVar.CheckboxCheckPadding, 2);
        style.SetVar(TGuiStyleVar.DropdownButtonSpacing, 2);
        style.SetVar(TGuiStyleVar.DropdownArrowPadding, 18);
        style.SetVar(TGuiStyleVar.DropdownItemsSpacing, 2);
        style.SetVar(TGuiStyleVar.SpinnerButtonWidth, 28);
        style.SetVar(TGuiStyleVar.SpinnerButtonSpacing, 4);
        style.SetVar(TGuiControl.ScrollBar, TGuiStyleVar.BorderWidth, 0);
        style.SetVar(TGuiStyleVar.ScrollBarArrowsVisible, 0);
        style.SetVar(TGuiStyleVar.ScrollBarArrowSize, 7);
        style.SetVar(TGuiStyleVar.ScrollBarSliderPadding, 0);
        style.SetVar(TGuiStyleVar.ScrollBarSliderSize, 18);
        style.SetVar(TGuiStyleVar.ScrollBarPadding, 0);
        style.SetVar(TGuiStyleVar.ScrollBarScrollSpeed, 12);
        style.SetVar(TGuiStyleVar.ListItemHeight, 28);
        style.SetVar(TGuiStyleVar.ListItemSpacing, 2);
        style.SetVar(TGuiStyleVar.ListItemBorderWidth, 1);
        style.SetVar(TGuiStyleVar.ScrollBarWidth, 12);
        style.SetVar(TGuiStyleVar.ListViewScrollBarSide, (int)TGuiScrollBarSide.Right);
    }
}

internal static class TGuiMacchiatoTheme
{
    public static void Apply(TGuiStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);

        ApplySharedDefaults(style);
        ApplyControlDefaults(style);
    }

    private static void ApplySharedDefaults(TGuiStyle style)
    {
        style.SetColor(TGuiStyleColor.Border, new TGuiColor(110, 115, 141, 255));
        style.SetColor(TGuiStyleColor.Surface, new TGuiColor(30, 32, 48, 255));
        style.SetColor(TGuiStyleColor.Text, new TGuiColor(202, 211, 245, 255));
        style.SetColor(TGuiStyleColor.BorderFocused, new TGuiColor(183, 189, 248, 255));
        style.SetColor(TGuiStyleColor.SurfaceFocused, new TGuiColor(54, 58, 79, 255));
        style.SetColor(TGuiStyleColor.TextFocused, new TGuiColor(183, 189, 248, 255));
        style.SetColor(TGuiStyleColor.BorderPressed, new TGuiColor(138, 173, 244, 255));
        style.SetColor(TGuiStyleColor.SurfacePressed, new TGuiColor(73, 77, 100, 255));
        style.SetColor(TGuiStyleColor.TextPressed, new TGuiColor(138, 173, 244, 255));
        style.SetColor(TGuiStyleColor.BorderDisabled, new TGuiColor(91, 96, 120, 255));
        style.SetColor(TGuiStyleColor.SurfaceDisabled, new TGuiColor(36, 39, 58, 255));
        style.SetColor(TGuiStyleColor.TextDisabled, new TGuiColor(165, 173, 203, 255));
        style.SetColor(TGuiStyleColor.Line, new TGuiColor(128, 135, 162, 255));
        style.SetColor(TGuiStyleColor.Background, new TGuiColor(36, 39, 58, 255));

        style.SetVar(TGuiStyleVar.BorderWidth, 1);
        style.SetVar(TGuiStyleVar.TextPadding, 0);
        style.SetVar(TGuiStyleVar.TextAlignment, (int)TGuiTextAlignment.Center);
        style.SetVar(TGuiStyleVar.TextSize, TGuiStyle.DefaultTextSize);
        style.SetVar(TGuiStyleVar.TextSpacing, 1);
        style.SetVar(TGuiStyleVar.TextLineSpacing, 4);
        style.SetVar(TGuiStyleVar.TextAlignmentVertical, (int)TGuiTextAlignmentVertical.Middle);
        style.SetVar(TGuiStyleVar.PanelCornerRadius, 0);
    }

    private static void ApplyControlDefaults(TGuiStyle style)
    {
        style.SetVar(TGuiControl.Label, TGuiStyleVar.TextAlignment, (int)TGuiTextAlignment.Left);
        style.SetVar(TGuiControl.Button, TGuiStyleVar.BorderWidth, 1);
        style.SetVar(TGuiControl.Slider, TGuiStyleVar.TextPadding, 4);
        style.SetVar(TGuiControl.ProgressBar, TGuiStyleVar.TextPadding, 4);
        style.SetVar(TGuiControl.Checkbox, TGuiStyleVar.TextPadding, 6);
        style.SetVar(TGuiControl.Checkbox, TGuiStyleVar.TextAlignment, (int)TGuiTextAlignment.Right);
        style.SetVar(TGuiControl.DropdownBox, TGuiStyleVar.TextPadding, 0);
        style.SetVar(TGuiControl.DropdownBox, TGuiStyleVar.TextAlignment, (int)TGuiTextAlignment.Center);
        style.SetVar(TGuiStyleVar.DropdownArrowVisible, 1);
        style.SetVar(TGuiControl.TextBox, TGuiStyleVar.TextPadding, 6);
        style.SetVar(TGuiControl.TextBox, TGuiStyleVar.TextAlignment, (int)TGuiTextAlignment.Left);
        style.SetVar(TGuiControl.ValueBox, TGuiStyleVar.TextPadding, 6);
        style.SetVar(TGuiControl.ValueBox, TGuiStyleVar.TextAlignment, (int)TGuiTextAlignment.Left);
        style.SetVar(TGuiControl.StatusBar, TGuiStyleVar.TextPadding, 8);
        style.SetVar(TGuiControl.StatusBar, TGuiStyleVar.TextAlignment, (int)TGuiTextAlignment.Left);

        style.SetVar(TGuiStyleVar.ToggleGroupPadding, 3);
        style.SetVar(TGuiStyleVar.SliderThumbWidth, 18);
        style.SetVar(TGuiStyleVar.SliderPadding, 2);
        style.SetVar(TGuiStyleVar.ProgressPadding, 2);
        style.SetVar(TGuiStyleVar.CheckboxCheckPadding, 2);
        style.SetVar(TGuiStyleVar.DropdownButtonSpacing, 2);
        style.SetVar(TGuiStyleVar.DropdownArrowPadding, 18);
        style.SetVar(TGuiStyleVar.DropdownItemsSpacing, 2);
        style.SetVar(TGuiStyleVar.SpinnerButtonWidth, 28);
        style.SetVar(TGuiStyleVar.SpinnerButtonSpacing, 4);
        style.SetVar(TGuiControl.ScrollBar, TGuiStyleVar.BorderWidth, 0);
        style.SetVar(TGuiStyleVar.ScrollBarArrowsVisible, 0);
        style.SetVar(TGuiStyleVar.ScrollBarArrowSize, 7);
        style.SetVar(TGuiStyleVar.ScrollBarSliderPadding, 0);
        style.SetVar(TGuiStyleVar.ScrollBarSliderSize, 18);
        style.SetVar(TGuiStyleVar.ScrollBarPadding, 0);
        style.SetVar(TGuiStyleVar.ScrollBarScrollSpeed, 12);
        style.SetVar(TGuiStyleVar.ListItemHeight, 28);
        style.SetVar(TGuiStyleVar.ListItemSpacing, 2);
        style.SetVar(TGuiStyleVar.ListItemBorderWidth, 1);
        style.SetVar(TGuiStyleVar.ScrollBarWidth, 12);
        style.SetVar(TGuiStyleVar.ListViewScrollBarSide, (int)TGuiScrollBarSide.Right);
    }
}

internal static class TGuiMochaTheme
{
    public static void Apply(TGuiStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);

        ApplySharedDefaults(style);
        ApplyControlDefaults(style);
    }

    private static void ApplySharedDefaults(TGuiStyle style)
    {
        style.SetColor(TGuiStyleColor.Border, new TGuiColor(108, 112, 134, 255));
        style.SetColor(TGuiStyleColor.Surface, new TGuiColor(24, 24, 37, 255));
        style.SetColor(TGuiStyleColor.Text, new TGuiColor(205, 214, 244, 255));
        style.SetColor(TGuiStyleColor.BorderFocused, new TGuiColor(180, 190, 254, 255));
        style.SetColor(TGuiStyleColor.SurfaceFocused, new TGuiColor(49, 50, 68, 255));
        style.SetColor(TGuiStyleColor.TextFocused, new TGuiColor(180, 190, 254, 255));
        style.SetColor(TGuiStyleColor.BorderPressed, new TGuiColor(137, 180, 250, 255));
        style.SetColor(TGuiStyleColor.SurfacePressed, new TGuiColor(69, 71, 90, 255));
        style.SetColor(TGuiStyleColor.TextPressed, new TGuiColor(137, 180, 250, 255));
        style.SetColor(TGuiStyleColor.BorderDisabled, new TGuiColor(88, 91, 112, 255));
        style.SetColor(TGuiStyleColor.SurfaceDisabled, new TGuiColor(30, 30, 46, 255));
        style.SetColor(TGuiStyleColor.TextDisabled, new TGuiColor(166, 173, 200, 255));
        style.SetColor(TGuiStyleColor.Line, new TGuiColor(127, 132, 156, 255));
        style.SetColor(TGuiStyleColor.Background, new TGuiColor(30, 30, 46, 255));

        style.SetVar(TGuiStyleVar.BorderWidth, 1);
        style.SetVar(TGuiStyleVar.TextPadding, 0);
        style.SetVar(TGuiStyleVar.TextAlignment, (int)TGuiTextAlignment.Center);
        style.SetVar(TGuiStyleVar.TextSize, TGuiStyle.DefaultTextSize);
        style.SetVar(TGuiStyleVar.TextSpacing, 1);
        style.SetVar(TGuiStyleVar.TextLineSpacing, 4);
        style.SetVar(TGuiStyleVar.TextAlignmentVertical, (int)TGuiTextAlignmentVertical.Middle);
        style.SetVar(TGuiStyleVar.PanelCornerRadius, 0);
    }

    private static void ApplyControlDefaults(TGuiStyle style)
    {
        style.SetVar(TGuiControl.Label, TGuiStyleVar.TextAlignment, (int)TGuiTextAlignment.Left);
        style.SetVar(TGuiControl.Button, TGuiStyleVar.BorderWidth, 1);
        style.SetVar(TGuiControl.Slider, TGuiStyleVar.TextPadding, 4);
        style.SetVar(TGuiControl.ProgressBar, TGuiStyleVar.TextPadding, 4);
        style.SetVar(TGuiControl.Checkbox, TGuiStyleVar.TextPadding, 6);
        style.SetVar(TGuiControl.Checkbox, TGuiStyleVar.TextAlignment, (int)TGuiTextAlignment.Right);
        style.SetVar(TGuiControl.DropdownBox, TGuiStyleVar.TextPadding, 0);
        style.SetVar(TGuiControl.DropdownBox, TGuiStyleVar.TextAlignment, (int)TGuiTextAlignment.Center);
        style.SetVar(TGuiStyleVar.DropdownArrowVisible, 1);
        style.SetVar(TGuiControl.TextBox, TGuiStyleVar.TextPadding, 6);
        style.SetVar(TGuiControl.TextBox, TGuiStyleVar.TextAlignment, (int)TGuiTextAlignment.Left);
        style.SetVar(TGuiControl.ValueBox, TGuiStyleVar.TextPadding, 6);
        style.SetVar(TGuiControl.ValueBox, TGuiStyleVar.TextAlignment, (int)TGuiTextAlignment.Left);
        style.SetVar(TGuiControl.StatusBar, TGuiStyleVar.TextPadding, 8);
        style.SetVar(TGuiControl.StatusBar, TGuiStyleVar.TextAlignment, (int)TGuiTextAlignment.Left);

        style.SetVar(TGuiStyleVar.ToggleGroupPadding, 3);
        style.SetVar(TGuiStyleVar.SliderThumbWidth, 18);
        style.SetVar(TGuiStyleVar.SliderPadding, 2);
        style.SetVar(TGuiStyleVar.ProgressPadding, 2);
        style.SetVar(TGuiStyleVar.CheckboxCheckPadding, 2);
        style.SetVar(TGuiStyleVar.DropdownButtonSpacing, 2);
        style.SetVar(TGuiStyleVar.DropdownArrowPadding, 18);
        style.SetVar(TGuiStyleVar.DropdownItemsSpacing, 2);
        style.SetVar(TGuiStyleVar.SpinnerButtonWidth, 28);
        style.SetVar(TGuiStyleVar.SpinnerButtonSpacing, 4);
        style.SetVar(TGuiControl.ScrollBar, TGuiStyleVar.BorderWidth, 0);
        style.SetVar(TGuiStyleVar.ScrollBarArrowsVisible, 0);
        style.SetVar(TGuiStyleVar.ScrollBarArrowSize, 7);
        style.SetVar(TGuiStyleVar.ScrollBarSliderPadding, 0);
        style.SetVar(TGuiStyleVar.ScrollBarSliderSize, 18);
        style.SetVar(TGuiStyleVar.ScrollBarPadding, 0);
        style.SetVar(TGuiStyleVar.ScrollBarScrollSpeed, 12);
        style.SetVar(TGuiStyleVar.ListItemHeight, 28);
        style.SetVar(TGuiStyleVar.ListItemSpacing, 2);
        style.SetVar(TGuiStyleVar.ListItemBorderWidth, 1);
        style.SetVar(TGuiStyleVar.ScrollBarWidth, 12);
        style.SetVar(TGuiStyleVar.ListViewScrollBarSide, (int)TGuiScrollBarSide.Right);
    }
}

public sealed class TGuiStyle
{
    internal const int DefaultTextSize = 12;

    private static readonly int s_controlCount = Enum.GetValues<TGuiControl>().Length;
    private static readonly int s_colorCount = Enum.GetValues<TGuiStyleColor>().Length;
    private static readonly int s_varCount = Enum.GetValues<TGuiStyleVar>().Length;

    private readonly int[] _sharedColors = new int[s_colorCount];
    private readonly int[] _sharedVars = new int[s_varCount];
    private readonly int[] _controlColorOverrides = new int[s_controlCount * s_colorCount];
    private readonly int[] _controlVarOverrides = new int[s_controlCount * s_varCount];
    private readonly bool[] _hasControlColorOverride = new bool[s_controlCount * s_colorCount];
    private readonly bool[] _hasControlVarOverride = new bool[s_controlCount * s_varCount];
    private bool _isLoaded;

    public void SetColor(TGuiStyleColor color, TGuiColor value)
    {
        EnsureLoaded();
        _sharedColors[(int)color] = value.ToPackedRgba();
    }

    public void SetColor(TGuiControl control, TGuiStyleColor color, TGuiColor value)
    {
        EnsureLoaded();

        if (control == TGuiControl.Default)
        {
            SetColor(color, value);
            return;
        }

        int index = GetControlColorIndex(control, color);
        _controlColorOverrides[index] = value.ToPackedRgba();
        _hasControlColorOverride[index] = true;
    }

    public TGuiColor GetColor(TGuiStyleColor color)
    {
        EnsureLoaded();
        return TGuiColor.FromPackedRgba(_sharedColors[(int)color]);
    }

    public TGuiColor GetColor(TGuiControl control, TGuiStyleColor color)
    {
        EnsureLoaded();

        if (control != TGuiControl.Default)
        {
            int index = GetControlColorIndex(control, color);
            if (_hasControlColorOverride[index])
            {
                return TGuiColor.FromPackedRgba(_controlColorOverrides[index]);
            }
        }

        return TGuiColor.FromPackedRgba(_sharedColors[(int)color]);
    }

    public void SetVar(TGuiStyleVar styleVar, int value)
    {
        EnsureLoaded();
        _sharedVars[(int)styleVar] = value;
    }

    public void SetVar(TGuiControl control, TGuiStyleVar styleVar, int value)
    {
        EnsureLoaded();

        if (control == TGuiControl.Default)
        {
            SetVar(styleVar, value);
            return;
        }

        int index = GetControlVarIndex(control, styleVar);
        _controlVarOverrides[index] = value;
        _hasControlVarOverride[index] = true;
    }

    public int GetVar(TGuiStyleVar styleVar)
    {
        EnsureLoaded();
        return _sharedVars[(int)styleVar];
    }

    public int GetVar(TGuiControl control, TGuiStyleVar styleVar)
    {
        EnsureLoaded();

        if (control != TGuiControl.Default)
        {
            int index = GetControlVarIndex(control, styleVar);
            if (_hasControlVarOverride[index])
            {
                return _controlVarOverrides[index];
            }
        }

        return _sharedVars[(int)styleVar];
    }

    public void LoadDefault()
    {
        LoadTheme(TGuiTheme.Mocha);
    }

    public void LoadTheme(TGuiTheme theme)
    {
        _isLoaded = true;
        Reset();
        switch(theme)
        {
            case TGuiTheme.Latte:
                TGuiLatteTheme.Apply(this);
                return;

            case TGuiTheme.Frappe:
                TGuiFrappeTheme.Apply(this);
                return;

            case TGuiTheme.Macchiato:
                TGuiMacchiatoTheme.Apply(this);
                return;

            case TGuiTheme.Mocha:
                TGuiMochaTheme.Apply(this);
                return;

            default:
                throw new ArgumentOutOfRangeException(nameof(theme), theme, "The theme value is not supported.");
        }
    }

    private void EnsureLoaded()
    {
        if (!_isLoaded)
        {
            LoadDefault();
        }
    }

    private static int GetControlColorIndex(TGuiControl control, TGuiStyleColor color)
    {
        return (((int)control) * s_colorCount) + (int)color;
    }

    private static int GetControlVarIndex(TGuiControl control, TGuiStyleVar styleVar)
    {
        return (((int)control) * s_varCount) + (int)styleVar;
    }

    private void Reset()
    {
        Array.Clear(_sharedColors, 0, _sharedColors.Length);
        Array.Clear(_sharedVars, 0, _sharedVars.Length);
        Array.Clear(_controlColorOverrides, 0, _controlColorOverrides.Length);
        Array.Clear(_controlVarOverrides, 0, _controlVarOverrides.Length);
        Array.Clear(_hasControlColorOverride, 0, _hasControlColorOverride.Length);
        Array.Clear(_hasControlVarOverride, 0, _hasControlVarOverride.Length);
    }


}

#endregion Style