namespace Wintty.Core.Configuration;

/// <summary>Works out font sizes for the zoom actions.</summary>
public static class FontSizing
{
    /// <summary>The smallest size the zoom actions reach, matching the config option's lower bound.</summary>
    public const float Minimum = 4f;

    /// <summary>The largest size the zoom actions reach, matching the config option's upper bound.</summary>
    public const float Maximum = 200f;

    /// <summary>The change made by one zoom step, in device-independent pixels.</summary>
    public const float Step = 1f;

    /// <summary>Moves a font size by a number of steps.</summary>
    /// <param name="current">The size now in use.</param>
    /// <param name="steps">Steps to move; negative makes the text smaller.</param>
    /// <returns>The new size, kept between <see cref="Minimum"/> and <see cref="Maximum"/>.</returns>
    public static float Zoom(float current, int steps) => Math.Clamp(current + (steps * Step), Minimum, Maximum);
}
