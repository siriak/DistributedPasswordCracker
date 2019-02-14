using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Lab1_Server
{
    class Program
    {
        static List<TcpClient> clients;
        const int connectTimeoutMs = 30_000;

        public static async Task Main()
        {
            _handler += Handler;
            SetConsoleCtrlHandler(_handler, true);

            clients = await AcceptClientsAsync();
            Console.Title = $"Clients: {clients.Count}";
            var data = await GetDataAsync();
            var password = await SendDataToClientsForProcessingAsync(data);

            Console.WriteLine($"Password is: {password}");
            Console.ReadLine();
        }

        static async Task<(string hash, string salt)> GetDataAsync()
        {
            var lines = await File.ReadAllLinesAsync("password.txt");
            return (lines[0], lines[1]);
        }

        static async Task<string> SendDataToClientsForProcessingAsync((string hash, string salt) data)
        {
            Console.WriteLine("Work started");
            var cts = new CancellationTokenSource();
            var pass = await Task.WhenAny(clients.AsParallel().Select(c => ProcessClient(c, data, cts.Token))).Unwrap();

            cts.Cancel();

            return pass;
        }

        static async Task<string> ProcessClient(TcpClient client, (string hash, string salt) data, CancellationToken ct)
        {
            using var stream = client.GetStream();
            using var sw = new StreamWriter(stream)
            {
                AutoFlush = true
            };
            using var sr = new StreamReader(stream);

            await Send(sw, "hash", data.hash);
            await Send(sw, "salt", data.salt);

            while (true)
            {
                var (str, count) = GetStartInfo();
                await Send(sw, "string base", str);
                await Send(sw, "continue count", count.ToString());

                var msg = await Read(sr, "success status");
                var isSuccess = bool.Parse(msg);

                if (isSuccess)
                {
                    var pass = await Read(sr, "password");
                    client.Close();
                    return pass;
                }

                if (ct.IsCancellationRequested)
                {
                    client.Close();
                    return null;
                }
            }
        }

        static int length = 0;
        static int clientLength = -1;
        static IEnumerator<string> enumerator;
        static readonly object _lock = new object();
        private static (string, int) GetStartInfo()
        {
            lock (_lock)
            {
                if (clientLength < 4)
                {
                    clientLength++;
                    return (string.Empty, clientLength);
                }
                else if (enumerator is null || !enumerator.MoveNext())
                {
                    length++;
                    enumerator = GetPasswords(length).GetEnumerator();
                    enumerator.MoveNext();
                }

                return (enumerator.Current, clientLength);
            }
        }

        static async Task<List<TcpClient>> AcceptClientsAsync()
        {
            var clients = new List<TcpClient>();
            var listener = TcpListener.Create(8888);
            listener.Start();
            Console.WriteLine("Waiting for clients to connect");
            var timeoutTask = Task.Delay(connectTimeoutMs);
            while (true)
            {
                var clientTask = listener.AcceptTcpClientAsync();

                if (await Task.WhenAny(clientTask, timeoutTask) == timeoutTask)
                {
                    Console.WriteLine("Time to connect ended");

                    if (clients.Count == 0)
                    {
                        Console.WriteLine("No connected clients, waiting longer");
                        timeoutTask = Task.Delay(connectTimeoutMs);
                        continue;
                    }

                    break;
                }

                var client = clientTask.Result;
                client.NoDelay = true;
                clients.Add(client);
                Console.WriteLine("Client connected");
            }
            listener.Stop();

            return clients;
        }

        static IEnumerable<string> GetPasswords(int length)
        {
            var sb = new StringBuilder(new string('A', length));
            while (true)
            {
                yield return sb.ToString();

                while (sb.Length != 0 && sb[sb.Length - 1] == 'z')
                {
                    sb.Remove(sb.Length - 1, 1);
                }

                if (sb.Length == 0)
                {
                    yield break;
                }

                var ch = sb[sb.Length - 1];

                while (!char.IsLetter(++ch))
                {
                }

                sb[sb.Length - 1] = ch;

                sb.Append(new string('A', length - sb.Length));
            }
        }

        #region SocketWrappers

        static async Task Send(StreamWriter sw, string name, string data)
        {
            Console.WriteLine();
            Console.WriteLine($"Sending {name}: {data}");
            await sw.WriteLineAsync(data);
            Console.WriteLine($"Sent");
        }

        static async Task<string> Read(StreamReader sr, string name)
        {
            Console.WriteLine();
            Console.WriteLine($"Reading {name}");
            var data = await sr.ReadLineAsync();
            Console.WriteLine($"Read: {data}");
            return data;
        }

        #endregion

        #region FaultHandling

        [DllImport("Kernel32")]
        private static extern bool SetConsoleCtrlHandler(EventHandler handler, bool add);

        private delegate bool EventHandler(CtrlType sig);
        static EventHandler _handler;

        enum CtrlType
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT = 1,
            CTRL_CLOSE_EVENT = 2,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT = 6
        }

        private static bool Handler(CtrlType sig)
        {
            clients?.ForEach(c => c.Close());
            return true;
        }

        #endregion
    }
}
