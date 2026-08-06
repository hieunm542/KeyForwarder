using System.Reflection;

namespace KeyForwarder;

internal static class AppIcon
{
    private static Icon? _cached;

    public static Icon Get()
    {
        if (_cached is not null)
        {
            return _cached;
        }

        var asm = Assembly.GetExecutingAssembly();
        const string resourceName = "KeyForwarder.Assets.app.ico";
        using var stream = asm.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return SystemIcons.Application;
        }

        // Clone so the stream can be closed; Icon(stream) needs the stream to stay open otherwise.
        using var temp = new Icon(stream);
        _cached = (Icon)temp.Clone();
        return _cached;
    }
}
