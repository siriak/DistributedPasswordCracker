using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Lab1_Client
{
    class Program
    {
        static TcpClient client;
        const int connectTimeoutMs = 10_000;

        static async Task Main()
        {
            while (true)
            {
                client = new TcpClient()
                {
                    NoDelay = true,
                };
                try
                {
                connect:
                    try
                    {
                        var ip = "159.224.194.148";
                        Console.WriteLine();
                        Console.WriteLine($"Trying to connect to {ip}");
                        await client.ConnectAsync(ip, 8888);
                        Console.WriteLine("Connected");
                    }
                    catch (SocketException ex)
                    {
                        Console.WriteLine(ex.Message);
                        await Task.Delay(connectTimeoutMs);
                        goto connect;
                    }

                    var stream = client.GetStream();
                    sw = new StreamWriter(stream)
                    {
                        AutoFlush = true
                    };
                    sr = new StreamReader(stream);

                    var hash = await Read("hash");
                    var salt = await Read("salt");

                    var tasks = new List<Task<(bool, string)>>();
                    while (true)
                    {
                        var str = await Read("string base");
                        var length = int.Parse(await Read("continue count"));

                        tasks.Add(Verify(str, hash, salt, length));

                        while (tasks.Count < Environment.ProcessorCount)
                        {
                            await Send("Success status", bool.FalseString);
                            str = await Read("string base");
                            length = int.Parse(await Read("continue count"));

                            tasks.Add(Verify(str, hash, salt, length));
                        }

                        var finished = await Task.WhenAny(tasks);

                        tasks.Remove(finished);

                        if (finished.Result.Item1)
                        {
                            await Send("success status", bool.TrueString);
                            await Send("password", finished.Result.Item2);
                            throw new Exception("Password was found");
                        }

                        await Send("Success status", bool.FalseString);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Session terminated");
                    Console.WriteLine(ex.Message);

                    await Task.Delay(connectTimeoutMs);
                }
                finally
                {
                    client?.Close();
                }
            }
        }

        static Task<(bool, string)> Verify(string start, string hash, string salt, int continueCount)
        {
            return Task.Run(() =>
            {
                var password = GetFirstPassword(salt, start, continueCount);
                var buffer = new char[hash.Length];
                do
                {
                    GetHashString(password, buffer);

                    var success = true;
                    for (var i = 0; i < hash.Length; i++)
                    {
                        if (buffer[i] != hash[i])
                        {
                            success = false;
                            break;
                        }
                    }

                    if (success)
                    {
                        return (true, new string(password.Select(b => (char)b).ToArray()).Substring(salt.Length));
                    }
                } while (GetNextPassword(password, salt.Length, start.Length));
                return (false, null);
            });
        }


        static byte[] GetFirstPassword(string salt, string start, int continueCount)
        {
            var arr = new byte[salt.Length + start.Length + continueCount];

            for (var i = 0; i < salt.Length; i++)
            {
                arr[i] = (byte)salt[i];
            }

            for (var i = 0; i < start.Length; i++)
            {
                arr[salt.Length + i] = (byte)start[i];
            }

            for (var i = 0; i < continueCount; i++)
            {
                arr[salt.Length + start.Length + i] = (byte)'A';
            }

            return arr;
        }

        static bool GetNextPassword(byte[] buffer, int saltLength, int startLength)
        {
            var unchangeableLength = saltLength + startLength;
            var len = buffer.Length;
            while (len != unchangeableLength && buffer[len - 1] == 'z')
            {
                len--;
            }

            if (len == unchangeableLength)
            {
                return false;
            }

            var ch = (char)buffer[len - 1];

            while (!char.IsLetter(++ch))
            {
            }

            buffer[len - 1] = (byte)ch;
            len++;

            while (len <= buffer.Length)
            {
                buffer[len - 1] = (byte)'A';
                len++;
            }

            return true;
        }

        static void GetHashString(byte[] password, char[] buffer)
        {
            var hexAlphabet = "0123456789ABCDEF";
            var b = GetHash(password);
            for (var i = 0; i < b.Length; i++)
            {
                buffer[2 * i] = hexAlphabet[b[i] >> 4];
                buffer[2 * i + 1] = hexAlphabet[b[i] & 0xF];
            }
        }

        static readonly ThreadLocal<HashAlgorithm> algorithm = new ThreadLocal<HashAlgorithm>(() => MD5.Create());
        static byte[] GetHash(byte[] input) => algorithm.Value.ComputeHash(input);

        #region SocketWrappers
        static StreamWriter sw;
        static StreamReader sr;

        static async Task Send(string name, string data)
        {
            Console.WriteLine();
            Console.WriteLine($"Sending {name}: {data}");
            await sw.WriteLineAsync(data);
            Console.WriteLine($"Sent");
        }

        static async Task<string> Read(string name)
        {
            Console.WriteLine();
            Console.WriteLine($"Reading {name}");
            var data = await sr.ReadLineAsync();
            Console.WriteLine($"Read: {data}");
            return data;
        }

        #endregion
    }
}
