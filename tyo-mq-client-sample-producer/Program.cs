using TYO_MQ_CLIENT;
using System.Timers;

namespace TYO_MQ_CLIENT.Examples;

public class Program
{
    private Publisher publisher;
    private System.Timers.Timer timer;

    private void OnTimedEvent(object source, ElapsedEventArgs e)
    {
        string message = $"{{\"time\": \"{DateTime.Now.ToString("H:mm:ss")}\"}}";
        publisher.produce(message);
        Console.WriteLine("The Elapsed event was raised at {0}", e.SignalTime);

        NewTImer();
    }

    private void NewTImer() {
        timer = new System.Timers.Timer();
        timer.Interval = 1000;

        // Set elapsed event for the timer. This occurs when the interval elapses −
        timer.Elapsed += OnTimedEvent;
        timer.AutoReset = false;
        // Now start the timer.
        timer.Enabled = true;

        timer.Start();
    }

    public async Task run() {

        publisher = new Publisher("sample-publisher");
        await publisher.register(() => {
            Console.WriteLine("Connected");
        });

        NewTImer();
    }

    
    public static async Task Main(string[] args)
    {
        Program program = new Program();
        program.run();

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
