namespace SalsaNOWSettings.Helpers;

public static class AppVersion
{
    public static string Display
    {
        get
        {
            var version = typeof(App).Assembly.GetName().Version;
            if (version is null)
            {
                return "1.0.1";
            }

            return version.Revision is 0 or -1
                ? $"{version.Major}.{version.Minor}.{version.Build}"
                : version.ToString();
        }
    }
}
