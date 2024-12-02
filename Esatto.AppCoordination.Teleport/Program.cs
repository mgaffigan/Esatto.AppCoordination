using Microsoft.Extensions.Logging.Debug;

namespace Esatto.AppCoordination.Teleport;

internal static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        try
        {
            if (args.Length < 1)
            {
                throw new ArgumentException("No command was specified");
            }

            var logger = new DebugLoggerProvider().CreateLogger("Teleport");
            var sync = new WindowsFormsSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(sync);
            using var app = new CoordinatedApp(sync, silentlyFail: false, logger);

            if (SingleInstanceApp.IsEmbedding(args))
            {
                TeleportReceiver.ReceiveInvocation(app, logger);
            }
            else
            {
                TeleportInitiator.SendInvocation(app, logger, args);
            }

            return 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString(), "Teleport", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return Usage();
        }
    }

    private static int Usage()
    {
        MessageBox.Show(@"Teleport.exe <command> <options>
Copyright In Touch Technologies 2023

Commands:
    file <path>    Open a file on the remote computer
    url <url>      Open a url on the remote computer

Examples:
    teleport url https://google.com
    teleport url tel:+18004444444
    teleport file c:\drop\path.docx
    teleport file relative\path.docx
    teleport file \\foo\bar\baz.txt
", "Teleport", MessageBoxButtons.OK, MessageBoxIcon.Error);
        return -1;
    }
}
