using System.Text.Json;

#if NET
using System.Runtime.ExceptionServices;
#else
using System.Reflection;
#endif

namespace Esatto.AppCoordination;

#if !NET
[Serializable]
#endif
public class InvokeFaultException : Exception
{
    public string? RawPayload { get; set; }
    public string? OriginalExceptionType { get; set; }

    public InvokeFaultException() { }
    public InvokeFaultException(string message) : base(message) { }
    public InvokeFaultException(string message, Exception? inner) : base(message, inner) { }
#if !NET
    protected InvokeFaultException(
      System.Runtime.Serialization.SerializationInfo info,
      System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
#endif

    public static string ToJson(Exception ex)
    {
        if (ex is InvokeFaultException iex && iex.RawPayload != null)
        {
            return iex.RawPayload;
        }

        return JsonSerializer.Serialize(ToInvokeExceptionJson(ex), CoordinationConstants.JsonSerializerOptions);
    }

    private static InvokeExceptionJson ToInvokeExceptionJson(Exception ex)
    {
        return new InvokeExceptionJson()
        {
            Message = ex.Message,
            Type = ex.GetType().FullName ?? "Exception",
            StackTrace = ex.StackTrace,
            InnerException = ex.InnerException == null ? null : ToInvokeExceptionJson(ex.InnerException),
        };
    }

    public static InvokeFaultException FromJson(string json)
    {
        try
        {
            var data = JsonSerializer.Deserialize<InvokeExceptionJson>(json, CoordinationConstants.JsonSerializerOptions)
                ?? throw new ArgumentNullException(nameof(json));

            var ex = FromJson(data);
            ex.RawPayload = json;
            return ex;
        }
        catch (Exception ex)
        {
            return new InvokeFaultException("Invoke failed with unparsable exception", ex)
            {
                RawPayload = json,
            };
        }
    }

    private static InvokeFaultException FromJson(InvokeExceptionJson data)
    {
        if (string.IsNullOrWhiteSpace(data.Message))
        {
            throw new ArgumentNullException(nameof(data.Message));
        }

        var inner = data.InnerException == null ? null : FromJson(data.InnerException);
        var ex = new InvokeFaultException(data.Message, inner);
        ex.OriginalExceptionType = data.Type;
        if (!string.IsNullOrWhiteSpace(data.StackTrace))
        {
#if NET
            ExceptionDispatchInfo.SetRemoteStackTrace(ex, data.StackTrace);
#else
            typeof(Exception).GetField("_remoteStackTraceString", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(ex, data.StackTrace);
#endif
        }
        return ex;
    }

#nullable disable
    private class InvokeExceptionJson
    {
        public string Type { get; set; }
        public string Message { get; set; }
        public string StackTrace { get; set; }
        public InvokeExceptionJson InnerException { get; set; }
    }
#nullable restore
}
