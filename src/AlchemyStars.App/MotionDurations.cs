using System.Windows;

namespace AlchemyStars.App;

public static class MotionDurations
{
    public static Duration Control { get; } = new(
        SystemParameters.ClientAreaAnimation
            ? TimeSpan.FromMilliseconds(200)
            : TimeSpan.Zero);
}
