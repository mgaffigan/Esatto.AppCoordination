namespace Esatto.AppCoordination.Teleport;

static class TeleportConstants
{
    public const string
        ReceiverKey = "/Teleport/Target/",
        StreamKeyPrefix = "/Teleport/Stream/",
        TeleportDepth = "TELEPORTDEPTH";

    public const int
        DefaultPriority = 10_000;

    public static readonly Guid
        ReceiverClsid = Guid.Parse("{BDC952ED-B54B-4E65-B652-5DBE197B8ABB}");
}
