﻿using TYO_MQ_CLIENT;
using System.Timers;
using SocketIOClient;
namespace TYO_MQ_CLIENT.Examples;

using System.Text.Json;
using System.Text.Json.Serialization;

public class Program
{
    private Publisher publisher;
    private System.Timers.Timer timer;

    private string producerName = "sample-publisher";
    private string? topic = null; // "sample-topic";

    public string ProducerName {
        get { return producerName; }
        set { producerName = value; }
    }

    public string Topic {
        get { return topic; }
        set { topic = value; }
    }

    private void OnTimedEvent(object source, ElapsedEventArgs e)
    {
        string message = $"{{\"time\": \"{DateTime.Now.ToString("H:mm:ss")}\"}}";
        string escapedMessage = message.Replace("\"", "\\\"");
        publisher.produce(escapedMessage, Topic/* Utils.JavaScriptStringEncode(message) */);
        // Console.WriteLine("The Elapsed event was raised at {0}", e.SignalTime);

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

        publisher = new Publisher(producerName, "s" /*the default event name*/);
        await publisher.register(() => {
            Console.WriteLine("Connected");
        });

        // var sio = new SocketIO("http://localhost:17352/", new SocketIOOptions()
        //     {
        //         EIO = 4,
        //         Transport = SocketIOClient.Transport.TransportProtocol.WebSocket,
        //     });
        // sio.OnConnected += async (sender, e) =>
        // {
        //     Console.WriteLine("Connected");
        //     await sio.EmitAsync("subscribe", "sample-publisher");
        // };
        // await sio.ConnectAsync();
        NewTImer();
    }
    public static async Task Main(string[] args)
    {
        Program program = new Program();

        if (args.Length > 0) {
            program.ProducerName = args[0];
            Console.WriteLine("Producer name: " + program.ProducerName);
        }

        if (args.Length > 1) {
            program.Topic = args[1];
            Console.WriteLine("Topic: " + program.Topic);
        }
        
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
