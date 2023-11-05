using Esatto.Win32.Com;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Debug;
using System.Configuration;

namespace Esatto.AppCoordination.DemoCoordinatedNotepad;

internal static class Program
{
    private const string AppKey = "/App/Notepad/";
    private static readonly Guid AppClsid = Guid.Parse("{82B5AF43-D7B5-481C-88BC-F73070417EEF}");

    [STAThread]
    static void Main(string[] args)
    {
        if (SingleInstanceApp.IsEmbedding(args))
        {
            args = Array.Empty<string>();
        }

        var logger = new DebugLoggerProvider().CreateLogger("CoordinatedNotepad");

        var sync = new WindowsFormsSynchronizationContext();
        SynchronizationContext.SetSynchronizationContext(sync);

        using var app = new CoordinatedApp(sync, true, logger);
        var singleInstance = app.GetSingleInstanceApp(AppKey, AppClsid);
        if (singleInstance.TryInvokeActive(args))
        {
            return;
        }

        var form = new MainForm();
        using var reg = singleInstance.PublishAndRegisterStatic(new(), form.AcceptArgs);
        form.AcceptArgs(args);
        Application.Run(form);
    }
}