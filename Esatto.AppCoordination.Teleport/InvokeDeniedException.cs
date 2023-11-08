namespace Esatto.AppCoordination.Teleport;

internal sealed class InvokeDeniedException : Exception
{
    public InvokeDeniedException(string message)
        : base(message)
    {
    }
}