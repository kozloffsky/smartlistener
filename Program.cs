using System;
using System.IO;
using System.Linq;
using System.Net;
using Akka.IO;
using Akka.Configuration;
using Akka.Actor;

namespace SmartListener 
{
    class Program
    {
        static void Main(string[] args)
        {
            var config = ConfigurationFactory.ParseString(@"
                akka {
                    actor{ 
                        inbox {
                            inbox-size = 100000
                        }
                    }
                    tcp {
                        batch-accept-limit = 50
                    }
                }
            ");
            var system = ActorSystem.Create("SmartListener", config);
            var manager = system.Tcp();
            var server = system.ActorOf(Server.Props());
            var writer = system.ActorOf(FileWriter.Props(), "file-writer");
            Console.WriteLine("Hello World!");
            Console.ReadKey();
        }
    }

    class WriteData {
        public WriteData(ByteString data)
        {
            Data = data;
        }

        public ByteString Data { get; }
    }

    class FileWriter: UntypedActor
    {
        public FileWriter() {
        }

        protected override void OnReceive(object message)
        {
            if (message is WriteData) {
                var data = message as WriteData;
                var file = File.Open("test.txt", FileMode.Open, FileAccess.Read);
                var reader = new StreamReader(file);
                var lines = reader.ReadToEnd().Split('\n')
                .SkipWhile(line => !line.Contains(data.Data.ToString())); 
                var count = lines.Count();
                reader.Close();
                file.Close();
                if (count == 0) {
                    var writter = File.AppendText("test.txt");
                    writter.WriteLine(data.Data.ToString());
                    writter.Flush();
                    writter.Close();
                }
            }
        }

        public static Props Props() {
            return Akka.Actor.Props.Create(() => new FileWriter());
        }
    }

    class Server: UntypedActor
    {
        public Server(int port) 
        {
            Context.System.Tcp().Tell(new Tcp.Bind(Self, new IPEndPoint(IPAddress.Any, port)));
            
        }
        protected override void OnReceive(object message)
        {
            if (message is Tcp.Bound) {
                var bound = message as Tcp.Bound;
                Console.WriteLine("Listtening on {0}", bound.LocalAddress);
            } else if (message is Tcp.Connected)
            {
                var connection = Context.ActorOf(Akka.Actor.Props.Create(() => new Connection(Sender)));
                Sender.Tell(new Tcp.Register(connection));
            } else {
                Unhandled(message);
            }
        }

        public static Props Props() {
            return Akka.Actor.Props.Create(() => new Server(7777));
        }
    }

    public class Connection : UntypedActor
    {
        private readonly IActorRef _connection;

        public Connection(IActorRef connection)
        {
            _connection = connection;
        }

        protected override void OnReceive(object message)
        {
            if (message is Tcp.Received)
            {
                var received = message as Tcp.Received;
                Console.WriteLine(received.Data);
                ActorSelection writer = Context.ActorSelection("/user/file-writer");
                writer.Tell(new WriteData(received.Data));
                Context.Stop(Self);
            } 
            else Unhandled(message);
        }
    }
}
