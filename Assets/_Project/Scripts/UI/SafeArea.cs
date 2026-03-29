using UnityEngine;

/// <summary>
/// Provides safe-area boundary values (0–1 anchor space) for notch/home-bar devices.
/// On devices with no cutouts all values are 0/1 and panels render as before.
/// </summary>
public static class SafeArea
{
    /// <summary>Fraction of screen height from the bottom that is unsafe (home indicator).</summary>
    public static float Bottom => Screen.safeArea.yMin / Screen.height;

    /// <summary>Fraction of screen height from the top that is unsafe (notch/status bar).</summary>
    public static float Top => 1f - Screen.safeArea.yMax / Screen.height;

    /// <summary>Fraction of screen width from the left edge that is unsafe.</summary>
    public static float Left => Screen.safeArea.xMin / Screen.width;

    /// <summary>Fraction of screen width from the right edge that is unsafe.</summary>
    public static float Right => 1f - Screen.safeArea.xMax / Screen.width;
}
