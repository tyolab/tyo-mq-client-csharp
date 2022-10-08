namespace TYO_MQ_CLIENT;
public class Logger {

    public enum LoggerLevel {
        VERBOSE,
        DEBUG,
        INFO,
        WARN,
        ERROR
    }


    public static LoggerLevel LOG_LEVEL = LoggerLevel.INFO;

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
        if (LOG_LEVEL <= LoggerLevel.VERBOSE)
            Console.WriteLine(what);

    }
    public static void debug(string what){
        if (LOG_LEVEL <= LoggerLevel.DEBUG)
            Console.WriteLine(what);

    }
    public static void info(string what){
        if (LOG_LEVEL <= LoggerLevel.INFO)
            Console.WriteLine(what);
    }

    public static void log(string what, object details){
        if (details != null) {
            Console.WriteLine("[" + what + "]: " + details.ToString());
        }
        else {
            Console.WriteLine(what);
        }
    }

    public static void error(string what, object details) {
        Console.Error.WriteLine(what);
        if (details != null) {
            Console.WriteLine("[" + what + "]: " + details.ToString());
        }
        else {
            Console.WriteLine(what);
        }
    }
    public static void warn(string what, object details) {
        if (LOG_LEVEL <= LoggerLevel.WARN) {
            if (details != null) {
                Console.WriteLine("[" + what + "]: " + details.ToString());
            }
            else {
                Console.WriteLine(what);
            }
        }
    }
    public static void verbose(string what, object details) {
        if (LOG_LEVEL <= LoggerLevel.VERBOSE) {
            if (details != null) {
                Console.WriteLine("[" + what + "]: " + details.ToString());
            }
            else {
                Console.WriteLine(what);
            }
        }
    }
    public static void debug(string what, object details) {
        if (LOG_LEVEL <= LoggerLevel.DEBUG) {
            if (details != null) {
                Console.WriteLine("[" + what + "]: " + details.ToString());
            }
            else {
                Console.WriteLine(what);
            }
        }
    }
    public static void info(string what, object details) {
        if (LOG_LEVEL <= LoggerLevel.INFO) {
            if (details != null) {
                Console.WriteLine("[" + what + "]: " + details.ToString());
            }
            else {
                Console.WriteLine(what);
            }
        }
    }

    public static void log(string what, params string[] values){
        Console.WriteLine("[" + what + "]: " + string.Join(" ", values));

    }
    public static void error(string what, params string[] values){
        Console.WriteLine("[" + what + "]: " + string.Join(" ", values));
    }
    public static void warn(string what, params string[] values){
        if (LOG_LEVEL <= LoggerLevel.ERROR)
            Console.WriteLine("[" + what + "]: " + string.Join(" ", values));
    }
    public static void verbose(string what, params string[] values){
        if (LOG_LEVEL <= LoggerLevel.VERBOSE)
            Console.WriteLine("[" + what + "]: " + string.Join(" ", values));
    }
    public static void debug(string what, params string[] values){
        if (LOG_LEVEL <= LoggerLevel.DEBUG)
            Console.WriteLine("[" + what + "]: " + string.Join(" ", values));
    }
    public static void info(string what, params string[] values){
        if (LOG_LEVEL <= LoggerLevel.INFO)
            Console.WriteLine("[" + what + "]: " + string.Join(" ", values));
    }
}
