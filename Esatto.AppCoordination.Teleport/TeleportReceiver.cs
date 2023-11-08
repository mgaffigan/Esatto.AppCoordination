using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Debug;
using Esatto.Utilities;
using System.Text.Json;
using Esatto.Win32.Windows;
using System.Diagnostics;
using System.Text;
using Esatto.Win32.Net;
using System.Security.Policy;

namespace Esatto.AppCoordination.Teleport;

internal static class TeleportReceiver
{
    public static void ReceiveInvocation(CoordinatedApp app, ILogger logger)
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

        // AppCoord has 1 minute timeout on invoke.  Downloading a file may
        // take longer.
        SynchronizationContext.Current.Post(_ => HandleInvocationInternal(app, req, logger), null);
        return string.Empty;
    }

    private static void HandleInvocationInternal(CoordinatedApp app, InvokeRequestDto req, ILogger logger)
    { 
        try
        {
            using var depth = new TeleportDepthScope();
            bool useOpenWith = depth.Depth > TeleportSettings.Instance.RecursionLimit;
            if (req.Url is not null)
            {
                HandleUrlInvocation(req.Url, useOpenWith);
            }
            else if (req.File is not null)
            {
                HandleFileInvocation(app, req.File, useOpenWith);
            }
            else throw new NotSupportedException("No target specified");
        }
        catch (OperationCanceledException)
        {
            // nop
        }
        catch (InvokeDeniedException ex)
        {
            MessageBox.Show(ex.Message, "Teleport", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");
            MessageBox.Show(ex.Message, "Teleport", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void HandleUrlInvocation(string target, bool useOpenWith)
    {
        var url = TeleportInitiator.ParseAndValidateUrl(target);

        if (useOpenWith)
        {
            OpenWithDialog.Show(IntPtr.Zero, url);
        }
        else
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
    }

    private static void HandleFileInvocation(CoordinatedApp app, InvokeFileReferenceDto file, bool useOpenWith)
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
    }

    private static string DownloadFile(CoordinatedApp app, InvokeFileReferenceDto file)
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            TeleportInitiator.GetExtensionAndValidate(file.FileName);
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
            return localPath;
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
}
