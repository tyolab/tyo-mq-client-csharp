namespace tyo_mq_client_csharp;
public class Logger {

    public enum LoggerLevel {
        VERBOSE,
        DEBUG,
        INFO,
        WARN,
        ERROR
    }


    public static LoggerLevel LOG_LEVEL = LoggerLevel.VERBOSE;

    public static void log(string what){
        Console.WriteLine(what);

    }
    public static void error(string what){
        Console.Error.WriteLine(what);

    }
    public static void warn(string what){
        Console.WriteLine(what);

    }
    public static void verbose(string what){
        Console.WriteLine(what);

    }
    public static void debug(string what){
        Console.WriteLine(what);

    }
    public static void info(string what){
        Console.WriteLine(what);
    }
}
