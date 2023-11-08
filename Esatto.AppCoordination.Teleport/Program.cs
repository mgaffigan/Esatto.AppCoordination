using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Debug;
using Esatto.Utilities;
using System.Text.Json;
using Esatto.Win32.Windows;
using System.Diagnostics;
using System.Text;
using Esatto.Win32.Net;
using System.Runtime.InteropServices;

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

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool AllocConsole();

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

    private static void SendInvocation(CoordinatedApp app, ILogger logger, string[] args)
    {
        if (args.Length < 2)
        {
            throw new ArgumentException("No target was specified");
        }

        var (req, prov) = CreateRequest(app, logger, args[0], args[1]);
        using (prov)
        {
            var destination = GetTargetForRequest(app, req.Registration);
            using var msgLoop = new FileStreamProviderServer(prov);

            destination.Invoke(JsonSerializer.Serialize(req));
            msgLoop.Run();
        }
    }

    private class FileStreamProviderServer : IDisposable
    {
        private readonly FileStreamProvider? Prov;
        private readonly ContextAwareCoalescingAction? CA;
        private readonly ThreadAssert threadAssert = new();
        private bool isStarted;
        private bool isShutdown;

        public FileStreamProviderServer(FileStreamProvider? prov)
        {
            this.Prov = prov;
            if (prov is null)
            {
                isShutdown = true;
            }
            else
            {
                CA = new ContextAwareCoalescingAction(Shutdown,
                    TeleportSettings.Instance.MaxReadInterval,
                    TeleportSettings.Instance.MaxReadTime,
                    SynchronizationContext.Current);

                prov.Disposed += (_, _) => Shutdown();
                prov.Touched += (_, _) => CA?.Set();
            }
        }

        public void Dispose() => CA?.Dispose();

        private void Shutdown()
        {
            threadAssert.Assert();

            if (isShutdown) return;
            isShutdown = true;

            if (isStarted)
            {
                // prov is disposed by parent
                Application.Exit();
            }
        }

        public void Run()
        {
            threadAssert.Assert();

            if (isShutdown) return;

            isStarted = true;
            Application.Run();
        }
    }

    private static ForeignEntry GetTargetForRequest(CoordinatedApp app, string registration)
    {
        // Select the farthest away entry with the lowest priority
        var ents = app.ForeignEntities
            .Where(e => e.Key == TeleportConstants.ReceiverKey)
            .OrderByDescending(e => e.SourcePath.Length)
            .ToList();
        ForeignEntry? pref = null;
        int minPriority = int.MaxValue;
        foreach (var ent in ents)
        {
            var targetPriority = ent.Value.GetValueOrDefault<int>("Priority", TeleportConstants.DefaultPriority);
            var entPriority = ent.Value.GetValueOrDefault<int>(registration, targetPriority);
            if (pref is null || entPriority < minPriority)
            {
                pref = ent;
                minPriority = entPriority;
            }
        }
        return pref ?? throw new InvokeDeniedException("No Teleport target is available");
    }

    private static (InvokeRequestDto, FileStreamProvider?) CreateRequest(CoordinatedApp app, ILogger logger, string command, string target)
    {
        if (string.Equals(command, "url", StringComparison.OrdinalIgnoreCase))
        {
            return CreateUrlRequest(target);
        }
        else if (string.Equals(command, "file", StringComparison.OrdinalIgnoreCase))
        {
            return CreateFileRequest(app, logger, target);
        }
        else
        {
            throw new ArgumentException($"Unknown command {command}");
        }
    }

    private static (InvokeRequestDto, FileStreamProvider?) CreateUrlRequest(string target)
    {
        var uri = ParseAndValidateUrl(target);

        return (new InvokeRequestDto(uri.Scheme, target), null);
    }

    private static Uri ParseAndValidateUrl(string target)
    {
        if (!Uri.TryCreate(target, UriKind.Absolute, out var uri))
        {
            throw new InvokeDeniedException("Could not parse target as a URL");
        }
        if (!IsPermitted(uri.Scheme, TeleportSettings.Instance.PermittedUrlSchemes, TeleportSettings.Instance.BlockedUrlSchemes))
        {
            throw new InvokeDeniedException($"Scheme '{uri.Scheme}' is not permitted to be sent via Teleport");
        }

        return uri;
    }

    private static (InvokeRequestDto, FileStreamProvider?) CreateFileRequest(CoordinatedApp app, ILogger logger, string target)
    {
        using var fileStream = OpenFileOrThrow(target).MakeUnique();

        long length = fileStream.Value.Length;
        if (length > TeleportSettings.Instance.MaxFileSize)
        {
            throw new InvokeDeniedException("File is too large to be sent via Teleport");
        }

        var extension = Path.GetExtension(target).TrimStart('.') ?? throw new InvalidOperationException("No extension found");
        if (!IsPermitted(extension, TeleportSettings.Instance.PermittedFileTypes, TeleportSettings.Instance.BlockedFileTypes))
        {
            throw new InvokeDeniedException($"File type '{extension}' is not permitted to be sent via Teleport");
        }

        var filename = Path.GetFileName(target);
        if (fileStream.Value.Length > TeleportSettings.Instance.MaxMemoryFileSize)
        {
            var fileProv = new FileStreamProvider(app, fileStream, length);
            var fileRef = new InvokeFileReferenceDto(filename, fileProv.StreamKey);
            return (new InvokeRequestDto(extension, fileRef), fileProv);
        }
        else
        {
            var fileRef = new InvokeFileReferenceDto(filename, fileStream.Value.GetByteArray());
            return (new InvokeRequestDto(extension, fileRef), null);
        }
    }

    private static bool IsPermitted(string value, string? whitelist, string? blacklist)
    {
        var blocked = blacklist?.Split(new[] { ';', ' ', ',' }, StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
        if (blocked.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        var allowed = whitelist?.Split(new[] { ';', ' ', ',' }, StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
        if (allowed.Length == 0)
        {
            return true;
        }
        return allowed.Contains(value, StringComparer.OrdinalIgnoreCase);
    }

    private static FileStream OpenFileOrThrow(string target)
    {
        try
        {
            return File.Open(target, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        }
        catch (IOException ex)
        {
            // Pass through "File not found" and "Access denied" as InvokeDeniedException
            throw new InvokeDeniedException(ex.Message);
        }
    }

    private static void ReceiveInvocation(CoordinatedApp app, ILogger logger)
    {
        var singleInstance = app.GetSingleInstanceApp(TeleportConstants.ReceiverKey, TeleportConstants.ReceiverClsid);
        using var reg = singleInstance.RegisterStatic((uuid, key, payload) => HandleInvocation(app, payload, logger));
        Application.Run();
    }

    private static string HandleInvocation(CoordinatedApp app, string payload, ILogger logger)
    {
        var req = JsonSerializer.Deserialize<InvokeRequestDto>(payload)
            ?? throw new ArgumentException("Invalid request");
        req.Validate();

        try
        {
            using var depth = new TeleportDepthScope();
            bool useOpenWith = depth.Depth > TeleportSettings.Instance.RecursionLimit;
            if (req.Url is not null)
            {
                return HandleUrlInvocation(req.Url, useOpenWith);
            }
            else if (req.File is not null)
            {
                return HandleFileInvocation(app, req.File, useOpenWith);
            }
            else
            {
                throw new InvalidOperationException("No target specified");
            }
        }
        catch (OperationCanceledException)
        {
            // nop
            return string.Empty;
        }
        catch (InvokeDeniedException ex)
        {
            MessageBox.Show(ex.Message, "Teleport", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return string.Empty;
        }
    }

    private static string HandleFileInvocation(CoordinatedApp app, InvokeFileReferenceDto file, bool useOpenWith)
    {
        var localPath = DownloadFile(app, file);

        // launch the file
        if (useOpenWith)
        {
            OpenWithDialog.Show(IntPtr.Zero, localPath);
        }
        else
        {
            Process.Start(new ProcessStartInfo(localPath) { UseShellExecute = true });
        }

        return string.Empty;
    }

    private static string DownloadFile(CoordinatedApp app, InvokeFileReferenceDto file)
    {
        string result;
        var tempFile = Path.GetTempFileName();
        try
        {
            if (file.Contents is not null)
            {
                File.WriteAllBytes(tempFile, file.Contents);
            }
            else if (file.StreamKey is not null)
            {
                ReadFileStream(app, file.StreamKey, tempFile);
            }
            else throw new NotSupportedException("Unknown file reference type");

            var localPath = GetSaveFilePath(file.FileName);
#if NET
            File.Move(tempFile, localPath, overwrite: true);
#else
            // No way to specify overwrite on File.Move in .NET Framework
            // TOCTOU bugs abound here:
            if (File.Exists(localPath))
            {
                File.Delete(localPath);
            }
            File.Move(tempFile, localPath);
#endif
            AttachmentServices.SetMarkOfTheWeb(localPath, TeleportConstants.ReceiverClsid,
                new Uri("http://teleport.example.com"));
            result = localPath;
        }
        catch
        {
            try
            {
                File.Delete(tempFile);
            }
            catch (FileNotFoundException)
            {
                // nop, might have been moved already
            }
            throw;
        }

        return result;
    }

    private static string GetSaveFilePath(string filename)
    {
        var defaultDir = KnownFolder.GetPath(TeleportSettings.Instance.DefaultSaveDirectoryFolderID);
        var relDir = TeleportSettings.Instance.DefaultSaveDirectory;
        if (!string.IsNullOrWhiteSpace(relDir))
        {
            defaultDir = Path.Combine(defaultDir, relDir);
            Directory.CreateDirectory(defaultDir);
        }

        filename = GetSafeFileName(filename);
        if (TeleportSettings.Instance.PromptForSaveFile)
        {
            var sfd = new SaveFileDialog()
            {
                FileName = filename,
                InitialDirectory = defaultDir,
            };
            if (sfd.ShowDialog() != DialogResult.OK)
            {
                throw new OperationCanceledException();
            }
            return sfd.FileName;
        }
        else
        {
            var proposedFile = Path.Combine(defaultDir, filename);
            while (File.Exists(proposedFile))
            {
                proposedFile = IncrementFile(proposedFile);
            }
            return proposedFile;
        }
    }

    // File.txt -> File (1).txt
    // File (1).txt -> File (2).txt
    private static string IncrementFile(string proposedFile)
    {
        // Split to [ 'C:\drop', 'File (1)', '.txt' ]
        var dir = Path.GetDirectoryName(proposedFile);
        var ext = Path.GetExtension(proposedFile);
        var name = Path.GetFileNameWithoutExtension(proposedFile).TrimEnd();
        int count;
        if (name.Length >= 3 && name[name.Length - 1] == ')')
        {
            var lastOpen = name.LastIndexOf('(');
            if (lastOpen >= 0 && int.TryParse(name.Substring(lastOpen + 1, name.Length - 2 /* () */ - lastOpen), out count))
            {
                name = name.Substring(0, lastOpen).TrimEnd();
            }
            else
            {
                count = 0;
            }
        }
        else
        {
            count = 0;
        }

        return Path.Combine(dir, $"{name} ({count + 1}){ext}".Trim());
    }

    private static string GetSafeFileName(string filename)
    {
        const string safeChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_ .()";
        var sb = new StringBuilder(filename.Length);
        foreach (var c in filename)
        {
            if (safeChars.Contains(c))
            {
                sb.Append(c);
            }
            else
            {
                sb.Append('_');
            }
        }
        filename = sb.ToString();

        const int maxSafeLength = 50;
        if (filename.Length > maxSafeLength)
        {
            var ext = Path.GetExtension(filename);
            if (ext.Length > 20)
            {
                throw new InvokeDeniedException("Filename or extension is too long");
            }
            filename = Path.GetFileNameWithoutExtension(filename).Ellipsis(maxSafeLength - ext.Length) + ext;
        }
        return filename;
    }

    private static void ReadFileStream(CoordinatedApp app, string streamKey, string tempFile)
    {
        var streamEnt = app.ForeignEntities.SingleOrDefault(fe => fe.Key == streamKey)
            ?? throw new InvokeDeniedException("The file stream is no longer available");
        using var stream = new FileStreamEntryReader(streamEnt);
        using var file = File.Open(tempFile, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        stream.CopyTo(file, stream.MaxReadSize);
    }

    private static string HandleUrlInvocation(string target, bool useOpenWith)
    {
        var url = ParseAndValidateUrl(target);

        if (useOpenWith)
        {
            OpenWithDialog.Show(IntPtr.Zero, url);
        }
        else
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }

        return string.Empty;
    }
}
