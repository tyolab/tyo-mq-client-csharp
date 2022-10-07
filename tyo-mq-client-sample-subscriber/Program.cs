using TYO_MQ_CLIENT;
using System.Timers;
using SocketIOClient;
namespace TYO_MQ_CLIENT.Examples;

public class Program
{
    private Subscriber subscriber;
    delegate void OnNewMessage(Dictionary<string,string> msg);

    public async Task run() {

        subscriber = new Subscriber("sample-subscriber");
        await subscriber.register(() => {
            Console.WriteLine("[Subscriber] Connected to the server.");
        });

        OnNewMessage onNewMessage = (msg) => {
            Console.WriteLine($"[Subscriber] Received message: {msg}");
        };

        subscriber.subscribe("sample-publisher", onNewMessage);
    }

    public static async Task Main(string[] args)
    {
        Program program = new Program();
        await program.run();

        var mre = new ManualResetEvent(false);
        ThreadPool.QueueUserWorkItem( (state) =>
        {
            Console.WriteLine("Press (x) to exit");
            while(true)
            {
                var key = Console.Read();
                break;
                // if (key.Key == ConsoleKey.X)
                // {
                //     mre.Set(); // This will let the main thread exit
                //     break;
                // }
            }
        });
    
        // The main thread can just wait on the wait handle, which basically puts it into a "sleep" state, and blocks it forever
        mre.WaitOne();
    }
}
