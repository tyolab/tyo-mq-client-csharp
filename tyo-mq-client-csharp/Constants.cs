namespace TYO_MQ_CLIENT;

public static class Constants
{


    public static readonly string ANONYMOUS = "ANONYMOUS";

    public static readonly string EVENT_DEFAULT = "tyo-mq-event-default";

    public static readonly string EVENT_ALL = "TM-ALL";

    public static readonly string SYSTEM = "TYO-MQ-SYSTEM";

    public static readonly string ALL_PUBLISHERS = "TYO-MQ-ALL";

    public static readonly string METHOD_BROADCAST = "broadcast";

    public static readonly string METHOD_UNICAST = "unicast";

    public static readonly string SCOPE_ALL = "all";
    public static readonly string SCOPE_DEFAULT = "default";

    // SERVER DEFAULTS
    public static readonly int DEFAULT_PORT = 17352;

    // Must match Publisher.CHUNK_SIZE in the JS server/client (256 KB)
    public static readonly int CHUNK_SIZE = 256 * 1024;
}