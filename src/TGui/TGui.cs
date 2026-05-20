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