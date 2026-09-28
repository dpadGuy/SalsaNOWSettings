using System;

namespace SalsaNOWSettings.Helpers;

public static class SettingsNavigation
{
    public static event Action<string>? Requested;

    public static void Go(string tag) => Requested?.Invoke(tag);
}
