using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Network2_Server
{
    public class User
    {
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? RemoteEndpoint { get; set; }
        public bool IsOnline { get; set; }
    }

    public class Program
    {
        static TcpListener listener = null;
        static List<TcpClient> Clients = new List<TcpClient>();
        static string jsonPath = "users.json";
        static object locker = new object();

        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("Server işə düşür...");
            var ip = IPAddress.Parse("192.168.100.81");
            var port = 27001;

            var ep = new IPEndPoint(ip, port);
            listener = new TcpListener(ep);
            listener.Start();

            Console.WriteLine($"Dinlənilir: {listener.LocalEndpoint}");

            while (true)
            {
                var client = listener.AcceptTcpClient();
                Clients.Add(client);

                Console.WriteLine($"{client.Client.RemoteEndPoint} qoşuldu . . . ");

                Task.Run(() =>
                {
                    string loggedInUser = null;
                    string remoteEp = client.Client.RemoteEndPoint?.ToString() ?? "";

                    try
                    {
                        var stream = client.GetStream();
                       var br = new BinaryReader(stream, Encoding.UTF8);
                       var bw = new BinaryWriter(stream, Encoding.UTF8);

                        string username = br.ReadString();
                        string password = br.ReadString();

                        lock (locker)
                        {
                            var users = LoadUsers();
                            var user = users.FirstOrDefault(u => u.Username == username && u.Password == password);

                            if (user == null)
                            {
                                bw.Write("REJECTED: Yanlış istifadəçi adı və ya şifrə!");
                                client.Close();
                                Clients.Remove(client);
                                return;
                            }

                            if (user.IsOnline)
                            {
                                bw.Write($"REJECTED: Bu istifadəçi artıq online-dır! (IP: {user.RemoteEndpoint})");
                                client.Close();
                                Clients.Remove(client);
                                return;
                            }

                            user.IsOnline = true;
                            user.RemoteEndpoint = remoteEp;
                            SaveUsers(users);
                            loggedInUser = username;

                            bw.Write("SUCCESS");
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"[QOŞULDU] {username} ({remoteEp}) sistemə daxil oldu.");
                            Console.ResetColor();
                        }

                        while (true)
                        {
                            if (stream.DataAvailable)
                            {
                                var msg = br.ReadString();
                                if (msg == "EXIT")
                                {
                                    break;
                                }
                                Console.WriteLine($"{loggedInUser} ({remoteEp}) : {msg}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                    }
                    finally
                    {
                        if (loggedInUser != null)
                        {
                            lock (locker)
                            {
                                var users = LoadUsers();
                                var user = users.FirstOrDefault(u => u.Username == loggedInUser);
                                if (user != null)
                                {
                                    user.IsOnline = false;
                                    SaveUsers(users);
                                }
                            }
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"[ÇIXIŞ] {loggedInUser} ({remoteEp}) sistemdən ayrıldı.");
                            Console.ResetColor();
                        }

                        Clients.Remove(client);
                        client.Close();
                    }
                });
            }
        }

        static List<User> LoadUsers()
        {
            if (!File.Exists(jsonPath)) return new List<User>();
            string json = File.ReadAllText(jsonPath);
            return JsonSerializer.Deserialize<List<User>>(json) ?? new List<User>();
        }

        static void SaveUsers(List<User> users)
        {
            string json = JsonSerializer.Serialize(users, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(jsonPath, json);
        }
    }
}