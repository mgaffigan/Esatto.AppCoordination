using Microsoft.Extensions.Logging;
using Esatto.Utilities;
using System.Text.Json;
using Esatto.Win32.Windows;
using System.Diagnostics;
using System.Text;
using Esatto.Win32.Net;

namespace Esatto.AppCoordination.Teleport;

internal static class TeleportInitiator
{
    public static void SendInvocation(CoordinatedApp app, ILogger logger, string[] args)
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

    public static Uri ParseAndValidateUrl(string target)
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

        string extension = GetExtensionAndValidate(target);
        
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

    public static string GetExtensionAndValidate(string target)
    {
        var extension = Path.GetExtension(target).TrimStart('.') ?? throw new InvalidOperationException("No extension found");
        if (!IsPermitted(extension, TeleportSettings.Instance.PermittedFileTypes, TeleportSettings.Instance.BlockedFileTypes))
        {
            throw new InvokeDeniedException($"File type '{extension}' is not permitted to be sent via Teleport");
        }

        return extension;
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
}
