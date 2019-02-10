using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;

namespace SecurePasswordVault
{
    class Program
    {
        static void Main()
        {
            Console.Write("Password: ");

            var pass = Console.ReadLine();

            if (!IsAdmin(pass))
            {
                Console.WriteLine("WRONG PASSWORD");
                Console.WriteLine("SYSTEM TERMINATES IN:");

                for (var i = 5; i > 0; i--)
                {
                    Console.WriteLine(i);
                    Thread.Sleep(1000);
                }

                return;
            }

            Console.WriteLine("WELCOME");
            Console.WriteLine("You are legal administrator");

            while (true)
            {
                var command = string.Empty;
                
                read_command:
                {
                    Console.WriteLine("Type command");
                    Console.WriteLine("Change password: C");
                    Console.WriteLine("Quit: Q");
                    command = Console.ReadLine();

                    switch (command)
                    {
                        case "Q":
                            return;
                        case "C":
                            goto change_password;
                        default:
                            Console.WriteLine($"Command '{command}' not recognized");
                            goto read_command;
                    }
                }

                change_password:
                Console.Write("New password (letters only): ");
                var newPass = Console.ReadLine();

                if (newPass.Any(c => !char.IsLetter(c)))
                {
                    Console.WriteLine("Only letters!");
                    goto change_password;
                }

                var salt = GetHashString(Guid.NewGuid().ToString().Select(c => (byte)c).ToArray());
                var hash = GetHashString((salt + newPass).Select(c => (byte)c).ToArray());

                SetSecurityCredentials(hash, salt);

                Console.WriteLine("Your new password is set up and ready to use!");

                goto read_command;
            }
        }

        static bool IsAdmin (string password)
        {
            if (!File.Exists("password.txt"))
            {
                return true;
            }

            var (hash, salt) = GetSecurityCredentials();

            return GetHashString((salt + password).Select(c => (byte)c).ToArray()) == hash;
        }

        static (string hash, string salt) GetSecurityCredentials()
        {
            var lines = File.ReadAllLines("password.txt");

            return (lines[0], lines[1]);
        }

        static void SetSecurityCredentials(string hash, string salt)
        {
            File.WriteAllLines("password.txt", new[]
            {
                hash,
                salt,
            });
        }

        static string GetHashString(byte[] password)
        {
            var hexAlphabet = "0123456789ABCDEF";
            var b = GetHash(password);
            var buffer = new char[b.Length * 2];
            for (var i = 0; i < b.Length; i++)
            {
                buffer[2 * i] = hexAlphabet[b[i] >> 4];
                buffer[2 * i + 1] = hexAlphabet[b[i] & 0xF];
            }
            return new string(buffer);
        }

        static readonly HashAlgorithm algorithm = MD5.Create();
        static byte[] GetHash(byte[] input) => algorithm.ComputeHash(input);
    }
}
