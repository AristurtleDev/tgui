// Copyright (c) Christopher Whitley (AristurtleDev). All rights reserved.
// Licensed under the MIT license.
// See LICENSE file in the project root for full license information.

using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Pipelines;
using System.Linq.Expressions;
using System.Numerics;
using System.Text;
using System.Text.Unicode;
using StbTrueTypeSharp;

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
        switch (theme)
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

#region Text

public static class TGuiTextParsing
{
    public static bool TryParseInt32Loose(ReadOnlySpan<char> text, out int value)
    {
        value = 0;

        if (text.IsEmpty)
        {
            return false;
        }

        int sign = 1;
        int index = 0;

        if (text[0] == '+' || text[0] == '-')
        {
            if (text[0] == '-')
            {
                sign = -1;
            }

            index++;
        }

        if ((index >= text.Length) || (text[index] < '0') || (text[index] > '9'))
        {
            return false;
        }

        int result = 0;

        for (; index < text.Length; index++)
        {
            char c = text[index];

            if ((c < '0') || (c > '9'))
            {
                break;
            }

            checked
            {
                result = (result * 10) + (c - '0');
            }
        }

        value = result * sign;
        return true;
    }

    public static bool TryParseSingleLoose(ReadOnlySpan<char> text, out float value)
    {
        value = 0.0f;

        if (text.IsEmpty)
        {
            return false;
        }

        float sign = 1.0f;
        int index = 0;

        char c = text[0];

        if (c == '+' || c == '-')
        {
            if (c == '-')
            {
                sign = -1.0f;
            }

            index++;

            if (index >= text.Length)
            {
                return false;
            }
        }

        float result = 0.0f;
        bool hasDigits = false;

        while (index < text.Length)
        {
            c = text[index];

            if ((c < '0') || (c > '9'))
            {
                break;
            }

            result = (result * 10.0f) + (c - '0');
            hasDigits = true;
            index++;
        }

        if ((index < text.Length) && (text[index] == '.'))
        {
            index++;

            float scale = 0.1f;

            while (index < text.Length)
            {
                c = text[index];

                if ((c < '0') || (c > '9'))
                {
                    break;
                }

                result += (c - '0') * scale;
                scale *= 0.1f;
                hasDigits = true;
                index++;
            }
        }

        if (!hasDigits)
        {
            return false;
        }

        value = result * sign;
        return true;
    }

    public static string[] SplitRows(ReadOnlySpan<char> text, char delimiter, out int[] textRows)
    {
        if (text.IsEmpty)
        {
            textRows = Array.Empty<int>();
            return Array.Empty<string>();
        }

        List<string> items = new List<string>();
        List<int> rows = new List<int>();

        int currentRow = 0;
        int start = 0;

        for (int index = 0; index < text.Length; index++)
        {
            char character = text[index];

            if ((character != delimiter) && (character != '\n'))
            {
                continue;
            }

            items.Add(text.Slice(start, index - start).ToString());
            rows.Add(currentRow);

            start = index + 1;

            if (character == '\n') { currentRow++; }
        }

        items.Add(text.Slice(start).ToString());
        rows.Add(currentRow);

        textRows = rows.ToArray();
        return items.ToArray();
    }
}

public sealed class TGuiTextBuffer
{
    private readonly byte[] _data;

    public int Capacity => _data.Length + 1;
    public int Length { get; private set; }
    public byte this[int index] => _data[index];
    public ReadOnlySpan<byte> PayloadSpan => _data.AsSpan(0, Length);

    public TGuiTextBuffer(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _data = new byte[capacity + 1];
    }

    public TGuiTextBuffer(string initial, int capacity)
    {
        ArgumentNullException.ThrowIfNull(initial);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        int encodedLength = Encoding.UTF8.GetByteCount(initial);
        int actualCapacity = Math.Max(capacity, encodedLength);

        _data = new byte[actualCapacity + 1];
        Length = Encoding.UTF8.GetBytes(initial, 0, initial.Length, _data, 0);
        _data[Length] = 0;
    }

    internal void SetLength(int length)
    {
        Length = length;
        _data[Length] = 0;
    }

    internal void SetByte(int index, byte value)
    {
        _data[index] = value;
    }

    public void SetString(ReadOnlySpan<char> text)
    {
        int byteCount = Encoding.UTF8.GetByteCount(text);

        if (byteCount > Capacity)
        {
            byteCount = Capacity;
        }

        int written = 0;
        Encoder encoder = Encoding.UTF8.GetEncoder();
        encoder.Convert(text, _data.AsSpan(0, byteCount), true, out int _, out written, out bool _);
        SetLength(written);
    }

    public override string ToString() => Encoding.UTF8.GetString(_data, 0, Length);
}

public static class TGuiUtf8
{
    private const int InvalidCodePoint = 0x3F;

    private const int Utf8MaxOneByteCodePoint = 0x7F;
    private const int Utf8MaxTwoByteCodePoint = 0x7FF;
    private const int Utf8MaxThreeByteCodePoint = 0xFFFF;
    private const int Utf8MaxFourByteCodePoint = 0x10FFFF;

    private const byte Utf8AsciiMask = 0b1000_0000;

    private const byte Utf8ContinuationMask = 0b1100_0000;
    private const byte Utf8ContinuationPattern = 0b1000_0000;
    private const byte Utf8ContinuationValueMask = 0b0011_1111;

    private const byte Utf8TwoByteMask = 0b1110_0000;
    private const byte Utf8TwoBytePattern = 0b1100_0000;

    private const byte Utf8ThreeByteMask = 0b1111_0000;
    private const byte Utf8ThreeBytePattern = 0b1110_0000;

    private const byte Utf8FourByteMask = 0b1111_1000;
    private const byte Utf8FourBytePattern = 0b1111_0000;

    private const byte Utf8TwoByteValueMask = 0b0001_1111;
    private const byte Utf8ThreeByteValueMask = 0b0000_1111;

    private const byte Utf8FourByteValueMask = 0b0000_0111;

    public static int GetCodepointNext(ReadOnlySpan<byte> text, int offset, out int codePointSize)
    {
        codePointSize = 1;
        int codePoint = InvalidCodePoint;

        // Ensure the starting offset is within the buffer
        if ((uint)offset >= (uint)text.Length) { return codePoint; }

        byte b0 = text[offset];

        // 4-byte UTF-8 sequence: 11110xxx 10xxxxxx 10xxxxxx 10xxxxxx
        if ((b0 & Utf8FourByteMask) == Utf8FourBytePattern)
        {
            if (offset + 3 >= text.Length) { return codePoint; }

            // All following bytes must be UTF-8 continuation bytes: 10xxxxxx
            byte b1 = text[offset + 1];
            byte b2 = text[offset + 2];
            byte b3 = text[offset + 3];

            if (((b1 & Utf8ContinuationMask) ^ Utf8ContinuationPattern) != 0 ||
                ((b2 & Utf8ContinuationMask) ^ Utf8ContinuationPattern) != 0 ||
                ((b3 & Utf8ContinuationMask) ^ Utf8ContinuationPattern) != 0)
            {
                return codePoint;
            }

            // Reconstruct the Unicode code point from the UTF-8 payload bits
            codePoint = ((b0 & Utf8FourByteValueMask) << 18) |
                        ((b1 & Utf8ContinuationValueMask) << 12) |
                        ((b2 & Utf8ContinuationValueMask) << 6) |
                        (b3 & Utf8ContinuationValueMask);

            codePointSize = 4;
        }
        else if ((b0 & Utf8ThreeByteMask) == Utf8ThreeBytePattern)
        {
            if (offset + 2 >= text.Length)
            {
                return codePoint;
            }

            // All following bytes must be UTF-8 continuation bytes: 10xxxxxx
            byte b1 = text[offset + 1];
            byte b2 = text[offset + 2];

            if (((b1 & Utf8ContinuationMask) ^ Utf8ContinuationPattern) != 0 ||
                ((b2 & Utf8ContinuationMask) ^ Utf8ContinuationPattern) != 0)
            {
                return codePoint;
            }

            codePoint = ((b0 & Utf8ThreeByteValueMask) << 12) |
                        ((b1 & Utf8ContinuationValueMask) << 6) |
                        (b2 & Utf8ContinuationValueMask);

            codePointSize = 3;
        }

        // 2-byte UTF-8 sequence: 110xxxxx 10xxxxxx
        else if ((b0 & Utf8TwoByteMask) == Utf8TwoBytePattern)
        {
            if (offset + 1 >= text.Length)
            {
                return codePoint;
            }

            // All following bytes must be UTF-8 continuation bytes: 10xxxxxx
            byte b1 = text[offset + 1];

            if (((b1 & Utf8ContinuationMask) ^ Utf8ContinuationPattern) != 0)
            {
                return codePoint;
            }

            codePoint = ((b0 & Utf8TwoByteValueMask) << 6) |
                        (b1 & Utf8ContinuationValueMask);

            codePointSize = 2;
        }

        // Single-byte ASCII character: 0xxxxxxx
        else if ((b0 & Utf8AsciiMask) == 0)
        {
            codePoint = b0;
            codePointSize = 1;
        }

        return codePoint;
    }

    public static int GetCodepointPrevious(ReadOnlySpan<byte> text, int offset, out int codePointSize)
    {
        codePointSize = 1;
        int codePoint = InvalidCodePoint;

        // Cannot move backward from the start of the buffer
        if (offset <= 0) { return codePoint; }

        int index = offset - 1;
        int bytesBack = 0;

        // Walk backwards across UTF-8 continuation bytes: 10xxxxxx
        while (index >= 0 && bytesBack < 4 &&
             (text[index] & Utf8ContinuationMask) == Utf8ContinuationPattern)
        {
            index--;
            bytesBack++;
        }

        if (index < 0) { return codePoint; }

        // Decode the code point starting from the detected lead byte
        return GetCodepointNext(text, index, out codePointSize);


    }

    public static byte[] CodepointToUtf8(int codePoint, out int byteSize)
    {
        byte[] buffer = new byte[4];

        // ASCII character: 0xxxxxxx
        if (codePoint <= Utf8MaxOneByteCodePoint)
        {
            buffer[0] = (byte)codePoint;
            byteSize = 1;
        }
        // 2-byte UTF-8 sequence: 110xxxxx 10xxxxxx
        else if (codePoint <= Utf8MaxTwoByteCodePoint)
        {
            buffer[0] = (byte)(((codePoint >> 6) & Utf8TwoByteValueMask) | Utf8TwoBytePattern);
            buffer[1] = (byte)((codePoint & Utf8ContinuationMask) | Utf8ContinuationPattern);
            byteSize = 2;
        }
        // 3-byte UTF-8 sequence: 1110xxxx 10xxxxxx 10xxxxxx
        else if (codePoint <= Utf8MaxThreeByteCodePoint)
        {
            buffer[0] = (byte)(((codePoint >> 12) & Utf8ThreeByteValueMask) | Utf8ThreeBytePattern);
            buffer[1] = (byte)(((codePoint >> 6) & Utf8ContinuationValueMask) | Utf8ContinuationPattern);
            buffer[2] = (byte)((codePoint & Utf8ContinuationValueMask) | Utf8ContinuationPattern);
            byteSize = 3;
        }
        // 4-byte UTF-8 sequence: 11110xxx 10xxxxxx 10xxxxxx 10xxxxxx
        else if (codePoint <= Utf8MaxFourByteCodePoint)
        {
            buffer[0] = (byte)(((codePoint >> 18) & Utf8FourByteValueMask) | Utf8FourBytePattern);
            buffer[1] = (byte)(((codePoint >> 12) & Utf8ContinuationValueMask) | Utf8ContinuationPattern);
            buffer[2] = (byte)(((codePoint >> 6) & Utf8ContinuationValueMask) | Utf8ContinuationPattern);
            buffer[3] = (byte)((codePoint & Utf8ContinuationValueMask) | Utf8ContinuationPattern);
            byteSize = 4;
        }
        else
        {
            byteSize = 0;
        }

        return buffer;
    }

}

public readonly struct TGuiCodePointRange
{
    public readonly int Start;
    public readonly int End;

    public TGuiCodePointRange(int start, int end)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfNegative(end);
        Start = start;
        End = end;
    }
}

public readonly struct TGuiGlyphInfo : IEquatable<TGuiGlyphInfo>
{
    public readonly int Value;
    public readonly int OffsetX;
    public readonly int OffsetY;
    public readonly int AdvanceX;

    public TGuiGlyphInfo(int value, int offsetX, int offsetY, int advanceX) =>
        (Value, OffsetX, OffsetY, AdvanceX) = (value, offsetX, offsetY, advanceX);

    public bool Equals(TGuiGlyphInfo other) => Value == other.Value &&
                                               OffsetX == other.OffsetX &&
                                               OffsetY == other.OffsetY &&
                                               AdvanceX == other.AdvanceX;

    public override bool Equals([NotNullWhen(true)] object? obj) => obj is TGuiGlyphInfo other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Value, OffsetX, OffsetY, AdvanceX);
    public override string ToString() =>
        $"(Value:{Value}, OffsetX:{OffsetX}, OffsetY:{OffsetY}, AdvanceX:{AdvanceX})";

    public static bool operator ==(TGuiGlyphInfo left, TGuiGlyphInfo right) => left.Equals(right);
    public static bool operator !=(TGuiGlyphInfo left, TGuiGlyphInfo right) => !left.Equals(right);
}

public sealed class TGuiFont
{
    public int BaseSize { get; set; }
    public int GlyphCount { get; set; }
    public int GlyphPadding { get; set; }
    public TGuiTexture Texture { get; set; }
    public TGuiRectangle[] Recs { get; set; } = [];
    public TGuiGlyphInfo[] Glyphs { get; set; } = [];
}

public sealed class TGuiFontAtlasOptions
{
    public const float DEFAULT_PIXEL_HEIGHT = 12.0f;
    public const int DEFAULT_GLYPH_PADDING = 1;

    public float PixelHeight { get; }
    public int GlyphPadding { get; }
    public IReadOnlyList<TGuiCodePointRange>? CodePointRanges { get; }

    public TGuiFontAtlasOptions() : this(DEFAULT_PIXEL_HEIGHT, DEFAULT_GLYPH_PADDING, null) { }
    public TGuiFontAtlasOptions(float pixelHeight, int glyphPadding, IReadOnlyList<TGuiCodePointRange>? codePointRanges)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pixelHeight);
        ArgumentOutOfRangeException.ThrowIfNegative(glyphPadding);
        
        PixelHeight = pixelHeight;
        GlyphPadding = glyphPadding;
        CodePointRanges = codePointRanges;
    }
}

public sealed class TGuiFontAtlas
{
    private const int BASIC_LATIN_START = 32;
    private const int BASIC_LATIN_END = 126;
    private const int LATIN1_SUPPLEMENT_START = 160;
    private const int LATIN1_SUPPLEMENT_END = 255;

    public int BaseSize { get; }
    public int GlyphPadding { get; }
    public int AtlasWidth { get; }
    public int AtlasHeight { get; }
    public byte[] AlphaPixels { get; }
    public TGuiRectangle[] Recs { get; }
    public TGuiGlyphInfo[] Glyphs { get; }
    public int GlyphCount => Glyphs.Length;
    public IReadOnlyDictionary<int, int> GlyphIndices { get; }

    public TGuiFontAtlas(int baseSize, int glyphPadding, int atlasWidth, int atlasHeight, byte[] alphaPixels, TGuiRectangle[] recs, TGuiGlyphInfo[] glyphs, IReadOnlyDictionary<int, int> glyphIndices)
    {
        ArgumentNullException.ThrowIfNull(alphaPixels);
        ArgumentNullException.ThrowIfNull(recs);
        ArgumentNullException.ThrowIfNull(glyphs);
        ArgumentNullException.ThrowIfNull(glyphIndices);

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(baseSize);
        ArgumentOutOfRangeException.ThrowIfNegative(glyphPadding);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(atlasWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(atlasHeight);

        int expectedPixelCount = checked(atlasWidth * atlasHeight);
        if (alphaPixels.Length != expectedPixelCount)
        {
            throw new ArgumentException("The alpha pixel data lengt hmust match the atlas area", nameof(alphaPixels));
        }

        BaseSize = baseSize;
        GlyphPadding = glyphPadding;
        AtlasWidth = atlasWidth;
        AtlasHeight = atlasHeight;
        AlphaPixels = alphaPixels;
        Recs = recs;
        Glyphs = glyphs;
        GlyphIndices = glyphIndices;
    }

    public static TGuiFontAtlas Create(Stream fontStream, TGuiFontAtlasOptions options, int atlasWidth, int atlasHeight)
    {
        ArgumentNullException.ThrowIfNull(fontStream);
        ArgumentNullException.ThrowIfNull(options);

        if(!fontStream.CanRead)
        {
            throw new ArgumentException("The font stream must support reading", nameof(fontStream));
        }

        using MemoryStream memoryStream = new MemoryStream();
        fontStream.CopyTo(memoryStream);
        return Create(memoryStream.ToArray(), options, atlasWidth, atlasHeight);
    }

    public unsafe static TGuiFontAtlas Create(byte[] fontData, TGuiFontAtlasOptions options, int atlasWidth, int atlasHeight)
    {
        ArgumentNullException.ThrowIfNull(fontData);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(atlasWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(atlasHeight);

        StbTrueType.stbtt_fontinfo? fontInfo = StbTrueType.CreateFont(fontData, 0);
        if(fontInfo is null)
        {
            throw new InvalidOperationException("The font data could not be parsed");
        }

        try
        {
            byte[] alphaPixels = new byte[checked(atlasWidth * atlasHeight)];
            List<TGuiRectangle> glyphRects = new  List<TGuiRectangle>();
            List<TGuiGlyphInfo> glyphInfos = new List<TGuiGlyphInfo>();
            Dictionary<int, int> glyphIndices = new Dictionary<int, int>();

            StbTrueType.stbtt_pack_context packContext = new StbTrueType.stbtt_pack_context();

            fixed(byte* pixelsPtr = alphaPixels)
            {
                int packBeginResult = StbTrueType.stbtt_PackBegin(packContext, pixelsPtr, atlasWidth, atlasHeight, atlasWidth, options.GlyphPadding, null);
                if(packBeginResult == 0)
                {
                    throw new InvalidOperationException("Failed to initialize the font atlas.");
                }

                try
                {
                    PackRanges(fontInfo, packContext, GetCodePointRanges(options), options.PixelHeight, glyphRects, glyphInfos, glyphIndices);
                }
                finally
                {
                    StbTrueType.stbtt_PackEnd(packContext);
                }
            }

            return new TGuiFontAtlas(
                (int)MathF.Round(options.PixelHeight),
                options.GlyphPadding,
                atlasWidth,
                atlasHeight,
                alphaPixels,
                glyphRects.ToArray(),
                glyphInfos.ToArray(),
                glyphIndices
            );
        }
        finally
        {
            fontInfo.Dispose();
        }
    }

    internal static IReadOnlyList<TGuiCodePointRange> GetCodePointRanges(TGuiFontAtlasOptions options)
    {
        if(options.CodePointRanges is not null)
        {
            return options.CodePointRanges;
        }

        return
        [
            new TGuiCodePointRange(BASIC_LATIN_START, BASIC_LATIN_END),
            new TGuiCodePointRange(LATIN1_SUPPLEMENT_START, LATIN1_SUPPLEMENT_END)
        ];
    }

    internal static unsafe void PackRanges(StbTrueType.stbtt_fontinfo fontInfo, StbTrueType.stbtt_pack_context packContext, IReadOnlyList<TGuiCodePointRange> codePointRanges, float fontPixelHeight, List<TGuiRectangle> glyphRects, List<TGuiGlyphInfo> glyphInfos, Dictionary<int, int> glyphIndices)
    {
        float baseLine = GetBaseline(fontInfo, fontPixelHeight);

        for(int rangeIndex = 0; rangeIndex < codePointRanges.Count; rangeIndex++)
        {
            TGuiCodePointRange codePointRange = codePointRanges[rangeIndex];
            PackRange(fontInfo, packContext, codePointRange.Start, codePointRange.End, fontPixelHeight, baseLine, glyphRects, glyphInfos, glyphIndices);
        }
    }

    private static unsafe float GetBaseline(StbTrueType.stbtt_fontinfo fontInfo, float fontPixelHeight)
    {
        float scale = StbTrueType.stbtt_ScaleForPixelHeight(fontInfo, fontPixelHeight);
        int ascent;
        int descent;
        int lineGap;
        StbTrueType.stbtt_GetFontVMetrics(fontInfo, &ascent, &descent, &lineGap);
        return ascent * scale;
    }

    
}

#endregion Text