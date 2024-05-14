using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using Raylib_cs;
using System.IO;
//using System;

namespace comsec;

struct ConnectedClient
{
    public Socket socket;
    public Thread myThread;
    public bool ClientDisconnecting;
    public string Username;

    public ConnectedClient(Socket socket, Thread myThread, string username, bool disconnecting = false)
    {
        this.socket = socket;
        this.myThread = myThread;
        Username = username;
        ClientDisconnecting = disconnecting;
    }
}

class Program
{
    public const string VERSION = "0.1";
    private static string? USERNAME;
    private static string? SANITISED_USERNAME;

    private const byte CLIENT_CONNECTED_CODE = 0;
    private const byte MESSAGE_SENT_CODE = 1;
    private const byte CLIENT_LEFT_CODE = 2;
    private const byte RECIEVE_MESSAGE_CODE = 3;
    private const byte RECIEVE_CLIENT_JOIN_CODE = 4;
    private const byte RECIEVE_CLIENT_LEFT_CODE = 5;
    private const byte LIST_CLIENTS_CODE = 6;
    private const byte RECIEVE_CLIENT_LIST_CODE = 7;

    private const int HEIGHT = 480, WIDTH = 800;
    private static CustomTerminal TERMINAL;

    private static Socket? SERVER_SOCKET, CLIENT_SOCKET; //client socket is cuz we run server+client at once, for pure clients server_socket is unused
    private static List<ConnectedClient>? CLIENTS;
    private static string[] ARGS;

    static bool IsClientConnected(Socket client)
    {
        if (CLIENTS == null) { return false; }
        foreach (var item in CLIENTS)
        {
            if (item.socket == client) { return true; }
        }
        return false;
    }

    //yes I know this is goofy as fuck, but hear me out.
    //RAYLIB window screws up if its not on the main thread for some reason
    //so I literally have to start a new thread for everything and run raylib
    //on the main thread. Then theres issues because the raylib thread is halted
    //forever since its an update loop, so i have to wait until raylib inits
    //(in username) and THEN get a reference to it. ffs
    static void Main(string[] args)
    {
        Console.InputEncoding = Encoding.Unicode;
        Console.OutputEncoding = Encoding.Unicode;
        ARGS = args;
        CLIENTS = new();
        if (args.Length == 1)
        {
            if (args[0] != "-server") { return; }
            else { StartServer(); return; }
        }
        Thread thr = new(Username);
        thr.Start();
        _ = new CustomTerminal(WIDTH, HEIGHT, $"COMSEC VERSION {VERSION}");
    }

    private static void Username()
    {
        while (CustomTerminal.TERMINAL==null) { Thread.Sleep(10); }
        //if(CustomTerminal.TERMINAL == null) { return; }
        TERMINAL = CustomTerminal.TERMINAL;
        if (!File.Exists(AppContext.BaseDirectory + "/settings.txt")) { File.WriteAllLines(AppContext.BaseDirectory + "/settings.txt", new string[] { "255,255,255,255", "0,0,0,255", "200,200,200,255" }); }
        var settings = File.ReadAllLines(AppContext.BaseDirectory + "/settings.txt");
        {
            var values = settings[0].Split(',');
            bool valid = true;
            if (!int.TryParse(values[0], out int val1) || val1 > 255 || val1 < 0) { valid = false; }
            if (!int.TryParse(values[1], out int val2) || val2 > 255 || val2 < 0) { valid = false; }
            if (!int.TryParse(values[2], out int val3) || val3 > 255 || val3 < 0) { valid = false; }
            if (!int.TryParse(values[3], out int val4) || val4 > 255 || val4 < 0) { valid = false; }
            if (valid) { TERMINAL.SetBackgroundColour(new Color(val1, val2, val3, val4)); }
        }
        {
            var values = settings[1].Split(',');
            bool valid = true;
            if (!int.TryParse(values[0], out int val1) || val1 > 255 || val1 < 0) { valid = false; }
            if (!int.TryParse(values[1], out int val2) || val2 > 255 || val2 < 0) { valid = false; }
            if (!int.TryParse(values[2], out int val3) || val3 > 255 || val3 < 0) { valid = false; }
            if (!int.TryParse(values[3], out int val4) || val4 > 255 || val4 < 0) { valid = false; }
            if (valid) { TERMINAL.SetTextColour(new Color(val1, val2, val3, val4)); }
        }
        {
            var values = settings[2].Split(',');
            bool valid = true;
            if (!int.TryParse(values[0], out int val1) || val1 > 255 || val1 < 0) { valid = false; }
            if (!int.TryParse(values[1], out int val2) || val2 > 255 || val2 < 0) { valid = false; }
            if (!int.TryParse(values[2], out int val3) || val3 > 255 || val3 < 0) { valid = false; }
            if (!int.TryParse(values[3], out int val4) || val4 > 255 || val4 < 0) { valid = false; }
            if (valid) { TERMINAL.SetInputColour(new Color(val1, val2, val3, val4)); }
        }

        TERMINAL.Output("Enter username: ");
        var s = TERMINAL.Input();
        if (s == null || string.IsNullOrWhiteSpace(s) || s.Length > 20 || s.Contains('\\')) { TERMINAL.Clear(); TERMINAL.Output("Enter a valid username please, under 20 characters, without \\. Press any key to restart..."); _ = TERMINAL.InputKey(); Username(); return; }
        USERNAME = s;
        SANITISED_USERNAME = s.Replace(".", "").Replace("/", "").Replace("@", "").Replace("\\", "").Replace(",", "").ToLower();
        Selection();
    }

    private static void Selection()
    {
        TERMINAL.Output($"Press 1 to host room, Press 2 to join room, Press 3 to change settings");
        var clser = TERMINAL.InputKey();
        if (clser == '1') { TERMINAL.Clear(); StartHostRoom(); }
        else if (clser == '2') { TERMINAL.Clear(); StartClientJoin(); }
        else if (clser == '3') { TERMINAL.Clear(); Settings(); }
        else { TERMINAL.Clear(); TERMINAL.Output("Enter a valid option. Press any key to restart."); _ = TERMINAL.InputKey(); TERMINAL.Clear(); Selection(); return; }
    }

    private static void Settings()
    {
        TERMINAL.Output($"Press 1 to change background colour, Press 2 to change foreground colour,");
        TERMINAL.Output("Press 3 to change inputfield colour, Press 4 to go back");
        var clser = TERMINAL.InputKey();
        if (clser == '1') {
            TERMINAL.Output("Enter RGBA (0-255,0-255,0-255,0-255) value separated by commas for background or 'back' to go back.");
            var value = TERMINAL.Input();
            if (value == "back") { TERMINAL.Clear(); Settings(); return; }
            var values = value.Split(',');
            if (values.Length != 4) { TERMINAL.Clear(); TERMINAL.Output("Invalid RGB, press any key to restart."); _ = TERMINAL.InputKey(); TERMINAL.Clear(); Settings(); return; }
            if (!int.TryParse(values[0], out int val1) || val1>255||val1<0) { TERMINAL.Clear(); TERMINAL.Output("Invalid RGB, press any key to restart."); _ = TERMINAL.InputKey(); TERMINAL.Clear(); Settings(); return; }
            if (!int.TryParse(values[1], out int val2) || val2>255 || val2<0) { TERMINAL.Clear(); TERMINAL.Output("Invalid RGB, press any key to restart."); _ = TERMINAL.InputKey(); TERMINAL.Clear(); Settings(); return; }
            if (!int.TryParse(values[2], out int val3) || val3>255 || val3<0) { TERMINAL.Clear(); TERMINAL.Output("Invalid RGB, press any key to restart."); _ = TERMINAL.InputKey(); TERMINAL.Clear(); Settings(); return; }
            if (!int.TryParse(values[3], out int val4)|| val4>255 || val4<0) { TERMINAL.Clear(); TERMINAL.Output("Invalid RGB, press any key to restart."); _ = TERMINAL.InputKey(); TERMINAL.Clear(); Settings(); return; }
            TERMINAL.SetBackgroundColour(new Color(val1, val2, val3, val4));

            //save the thing
            if(!File.Exists(AppContext.BaseDirectory + "/settings.txt")) { File.WriteAllLines(AppContext.BaseDirectory + "/settings.txt", new string[] { "255,255,255,255", "0,0,0,255", "200,200,200,255" }); }
            var settings = File.ReadAllLines(AppContext.BaseDirectory + "/settings.txt");
            if (settings.Length <= 0) { File.WriteAllLines(AppContext.BaseDirectory + "/settings.txt", new string[] { "255,255,255,255", "0,0,0,255", "200,200,200,255" }); }
            settings[0] = val1 + "," + val2 + "," + val3 + "," + val4;
            File.WriteAllLines(AppContext.BaseDirectory + "/settings.txt", settings);

            TERMINAL.Clear();
            TERMINAL.Output("Done, press any key to continue.");
            TERMINAL.InputKey();
            TERMINAL.Clear();
            Settings();
            return;
        }
        else if (clser == '2') {
            TERMINAL.Output("Enter RGBA (0-255,0-255,0-255,0-255) value separated by commas for text or 'back' to go back.");
            var value = TERMINAL.Input();
            if (value == "back") { TERMINAL.Clear(); Settings(); return; }
            var values = value.Split(',');
            if (values.Length != 4) { TERMINAL.Clear(); TERMINAL.Output("Invalid RGB, press any key to restart."); _ = TERMINAL.InputKey(); TERMINAL.Clear(); Settings(); return; }
            if (!int.TryParse(values[0], out int val1) || val1 > 255 || val1 < 0) { TERMINAL.Clear(); TERMINAL.Output("Invalid RGB, press any key to restart."); _ = TERMINAL.InputKey(); TERMINAL.Clear(); Settings(); return; }
            if (!int.TryParse(values[1], out int val2) || val2 > 255 || val2 < 0) { TERMINAL.Clear(); TERMINAL.Output("Invalid RGB, press any key to restart."); _ = TERMINAL.InputKey(); TERMINAL.Clear(); Settings(); return; }
            if (!int.TryParse(values[2], out int val3) || val3 > 255 || val3 < 0) { TERMINAL.Clear(); TERMINAL.Output("Invalid RGB, press any key to restart."); _ = TERMINAL.InputKey(); TERMINAL.Clear(); Settings(); return; }
            if (!int.TryParse(values[3], out int val4) || val4 > 255 || val4 < 0) { TERMINAL.Clear(); TERMINAL.Output("Invalid RGB, press any key to restart."); _ = TERMINAL.InputKey(); TERMINAL.Clear(); Settings(); return; }

            //save the thing
            if (!File.Exists(AppContext.BaseDirectory + "/settings.txt")) { File.WriteAllLines(AppContext.BaseDirectory + "/settings.txt", new string[] { "255,255,255,255", "0,0,0,255", "200,200,200,255" }); }
            var settings = File.ReadAllLines(AppContext.BaseDirectory + "/settings.txt");
            if (settings.Length <= 1) { File.WriteAllLines(AppContext.BaseDirectory + "/settings.txt", new string[] { "255,255,255,255", "0,0,0,255", "200,200,200,255" }); }
            settings[1] = val1 + "," + val2 + "," + val3 + "," + val4;
            File.WriteAllLines(AppContext.BaseDirectory + "/settings.txt", settings);

            TERMINAL.SetTextColour(new Color(val1, val2, val3, val4));
            TERMINAL.Clear();
            TERMINAL.Output("Done, press any key to continue.");
            TERMINAL.InputKey();
            TERMINAL.Clear();
            Settings();
            return;
        }
        else if (clser == '3') {
            TERMINAL.Output("Enter RGBA (0-255,0-255,0-255,0-255) value separated by commas for inputfield or 'back' to go back.");
            var value = TERMINAL.Input();
            if (value == "back") { TERMINAL.Clear(); Settings(); return; }
            var values = value.Split(',');
            if (values.Length != 4) { TERMINAL.Clear(); TERMINAL.Output("Invalid RGB, press any key to restart."); _ = TERMINAL.InputKey(); TERMINAL.Clear(); Settings(); return; }
            if (!int.TryParse(values[0], out int val1) || val1 > 255 || val1 < 0) { TERMINAL.Clear(); TERMINAL.Output("Invalid RGB, press any key to restart."); _ = TERMINAL.InputKey(); TERMINAL.Clear(); Settings(); return; }
            if (!int.TryParse(values[1], out int val2) || val2 > 255 || val2 < 0) { TERMINAL.Clear(); TERMINAL.Output("Invalid RGB, press any key to restart."); _ = TERMINAL.InputKey(); TERMINAL.Clear(); Settings(); return; }
            if (!int.TryParse(values[2], out int val3) || val3 > 255 || val3 < 0) { TERMINAL.Clear(); TERMINAL.Output("Invalid RGB, press any key to restart."); _ = TERMINAL.InputKey(); TERMINAL.Clear(); Settings(); return; }
            if (!int.TryParse(values[3], out int val4) || val4 > 255 || val4 < 0) { TERMINAL.Clear(); TERMINAL.Output("Invalid RGB, press any key to restart."); _ = TERMINAL.InputKey(); TERMINAL.Clear(); Settings(); return; }

            //save the thing
            if (!File.Exists(AppContext.BaseDirectory + "/settings.txt")) { File.WriteAllLines(AppContext.BaseDirectory + "/settings.txt", new string[] { "255,255,255,255", "0,0,0,255", "200,200,200,255" }); }
            var settings = File.ReadAllLines(AppContext.BaseDirectory + "/settings.txt");
            if (settings.Length <= 2) { File.WriteAllLines(AppContext.BaseDirectory + "/settings.txt", new string[] { "255,255,255,255", "0,0,0,255", "200,200,200,255" }); }
            settings[2] = val1 + "," + val2 + "," + val3 + "," + val4;
            File.WriteAllLines(AppContext.BaseDirectory + "/settings.txt", settings);

            TERMINAL.SetInputColour(new Color(val1, val2, val3, val4));
            TERMINAL.Clear();
            TERMINAL.Output("Done, press any key to continue.");
            TERMINAL.InputKey();
            TERMINAL.Clear();
            Settings();
            return;
        }
        else if (clser == '4') {
            TERMINAL.Clear();
            Selection();
            return;
        }
        else { TERMINAL.Clear(); TERMINAL.Output("Enter 1-3 please. Press any key to restart."); _ = TERMINAL.InputKey(); TERMINAL.Clear(); Settings(); return; }
    }

    private static void StartServer(bool multithread = false)
    {
        if (multithread) {
            Thread thread = new(new ThreadStart(Host_ListenForConnection));
            thread.Start();
        }
        else { Host_ListenForConnection(); }
    }

    private static void StartHostRoom()
    {
        //start the thread for listening for messages on the server. 
        StartServer(true);

        while (SERVER_SOCKET==null || !SERVER_SOCKET.IsBound)
        {
            Thread.Sleep(10); //hold on a moment
        }
        //now start our client and send our starting message. then, we do the chatroom thing
        StartClientJoin(true);
    }

    private static void Host_ListenForConnection()
    {
        try
        {
            //start the host here
            IPHostEntry ipHost = Dns.GetHostEntry(Dns.GetHostName());
            IPAddress ipAddr = ipHost.AddressList[0];
            IPEndPoint localEndPoint = new(ipAddr, 11000);
            SERVER_SOCKET = new(ipAddr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            SERVER_SOCKET.Bind(localEndPoint);
            SERVER_SOCKET.Listen(100);

            //begin listening for messages
            while (true)
            {
                if (SERVER_SOCKET == null) { return; }
                var client = SERVER_SOCKET.Accept();
                if (client != null && !IsClientConnected(client))
                {
                    Thread thread = new(new ParameterizedThreadStart(ListenForMessage));
                    CLIENTS.Add(new ConnectedClient(client, thread, ""));
                    Thread.Sleep(10);
                    thread.Start(CLIENTS.Count - 1);
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
            if(e is SocketException) { Console.WriteLine("ERRORCODE="+(e as SocketException).ErrorCode); }
        }
    }

    private static void ListenForMessage(object? data)
    {
        if (data == null || CLIENTS==null || SERVER_SOCKET == null) { return; }
        int id = (int)data;
        if (CLIENTS[id].ClientDisconnecting || CLIENTS[id].socket==null) { return; } //end thread if we lose connection or something
        byte[] buffer = new byte[1024];
        int totallen = 0;
        while (true)
        {
            try
            {
                if (CLIENTS[id].ClientDisconnecting) { return; }
                if (SERVER_SOCKET == null) { return; }
                if (!CLIENTS[id].socket.Connected) { return; }
                var bytes = new byte[1024];
                int bytesRec = CLIENTS[id].socket.Receive(bytes);
                if (bytesRec <= 0) { break; }
                Array.Copy(bytes, 0, buffer, totallen, bytesRec);
                totallen += bytesRec;
                if (buffer.Contains((byte)'\r')) { break; }//look for EOF
            }
            catch(Exception e)
            {
                Console.WriteLine(e.ToString());
                Console.WriteLine("Client "+CLIENTS[id].Username+" Disconnected with error. Press any key to continue.");
                CLIENTS[id].socket.Shutdown(SocketShutdown.Both);
                CLIENTS[id].socket.Disconnect(false);
                return;
            }
        }
        if (totallen <= 0) { ListenForMessage(data); return; }
        switch (buffer[0]) //first byte of any message is the identifier
        {
            case CLIENT_CONNECTED_CODE:
                {
                    Console.WriteLine(Encoding.Unicode.GetString(buffer, 1, totallen - 2) + " Connected");
                    buffer[0] = RECIEVE_CLIENT_JOIN_CODE;
                    byte[] sent = new byte[totallen];
                    Array.Copy(buffer, sent, totallen);
                    CLIENTS[id] = new(CLIENTS[id].socket, Thread.CurrentThread, Encoding.ASCII.GetString(buffer, 1, totallen - 2), false); //mark as disconnecting
                    for (int i = 0; i < CLIENTS.Count; i++)
                    {
                        if (CLIENTS[i].ClientDisconnecting) { continue; }
                        CLIENTS[i].socket.Send(sent);
                    }
                    break;
                }
            case LIST_CLIENTS_CODE:
                {
                    Console.WriteLine("List clients request recieved");
                    string fulllist = "";
                    for (int i = 0; i < CLIENTS.Count; i++)
                    {
                        if (CLIENTS[i].ClientDisconnecting) { continue; }//yeah well we can't just remove them from the list so im going to have to compromise :skull:
                        fulllist += CLIENTS[i].Username + "\\";
                    }
                    byte[] sent = new byte[fulllist.Length+2];
                    sent[0] = RECIEVE_CLIENT_LIST_CODE;
                    sent[^1] = (byte)'\r';
                    Array.Copy(Encoding.Unicode.GetBytes(fulllist), 0, sent, 1, fulllist.Length);
                    CLIENTS[id].socket.Send(sent);
                    break;
                }
            case MESSAGE_SENT_CODE:
                //return all messages to the clients
                Console.WriteLine(Encoding.ASCII.GetString(buffer, 1, totallen - 2)); 
                {
                    buffer[0] = RECIEVE_MESSAGE_CODE;
                    byte[] sent = new byte[totallen];
                    Array.Copy(buffer, sent, totallen);
                    for (int i = 0; i < CLIENTS.Count; i++)
                    {
                        if (CLIENTS[i].ClientDisconnecting) { continue; }
                        CLIENTS[i].socket.Send(sent);
                    }
                    break;
                }
            case CLIENT_LEFT_CODE:
                Console.WriteLine(Encoding.Unicode.GetString(buffer, 1, totallen - 2) + " Disconnected");
                {
                    buffer[0] = RECIEVE_CLIENT_LEFT_CODE;
                    byte[] sent = new byte[totallen];
                    Array.Copy(buffer, sent, totallen);
                    CLIENTS[id] = new(CLIENTS[id].socket, Thread.CurrentThread, CLIENTS[id].Username, true); //mark as disconnecting
                    for (int i = 0; i < CLIENTS.Count; i++)
                    {
                        if (i == id) { continue; }
                        if (CLIENTS[i].ClientDisconnecting) { continue; }
                        CLIENTS[i].socket.Send(sent);
                    }
                    break;
                }
            case RECIEVE_CLIENT_JOIN_CODE:
                //server doesnt need to act on this
                break;
            case RECIEVE_MESSAGE_CODE:
                //server doesn't need to act on this
                break;
            case RECIEVE_CLIENT_LIST_CODE:
                //sevrer doesnt need to act pn this
                break;
            default:
                Console.WriteLine("Recieved erroneous message with code " + buffer[0] + ", discarding");
                //TERMINAL.Output("Recieved erroneous message with code " + buffer[0] + ", discarding");
                break;
        }

        ListenForMessage(data); //continue the thing, its ok becuase we are in a different thread!
    }

    private static void StartClientJoin(bool myself = false)
    {
        IPAddress? address = Dns.GetHostEntry(Dns.GetHostName()).AddressList[0];
        if (!myself)
        {
            TERMINAL.Output("Enter IP ('exit' to go back, 'localhost' for yourself): ");
            var ip = TERMINAL.Input();
            if(ip=="exit")
            {
                TERMINAL.Clear();
                Selection();
                return;
            }
            bool valid = IPAddress.TryParse(ip, out IPAddress? tempaddress);
            if (ip != "localhost" && (tempaddress == null || !valid))
            {
                TERMINAL.Clear();
                TERMINAL.Output("Ip address not valid! Press any key to restart...");
                _ = TERMINAL.InputKey();
                StartClientJoin(myself);
                return;
            }
            //ping the target to ensure a server exists
            Ping ping = new();
            var pr = ping.Send(address);
            if (pr.Status != IPStatus.Success) {
                TERMINAL.Clear();
                TERMINAL.Output("Ip ping failed! Press any key to restart...");
                _ = TERMINAL.InputKey();
                StartClientJoin(myself);
                return;
            }
            if (tempaddress != null) { address = tempaddress; }
        }
        try
        {
            //connect to the target (in this case ourselves basically)
            try
            {
                IPEndPoint localEndPoint = new(address, 11000);
                CLIENT_SOCKET = new(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                CLIENT_SOCKET.Connect(localEndPoint);
            }
            catch(Exception e)
            {
                Console.WriteLine(e.ToString());
                TERMINAL.Clear();
                TERMINAL.Output("Failed to connect! Press any key to go back.");
                _ = TERMINAL.InputKey();
                TERMINAL.Clear();
                Selection();
            }

            // send connected message
            {
                var username = Encoding.Unicode.GetBytes(USERNAME); //make sure to add the required EOF character (ascii 26)
                var message = new byte[username.Length + 2];
                message[0] = CLIENT_CONNECTED_CODE;
                message[^1] = (byte)'\r';
                Array.Copy(username, 0, message, 1, username.Length);
                int byteSent = CLIENT_SOCKET.Send(message);
            }
            Thread.Sleep(10); //hold on a moment

            Thread messageListenThread = new(Client_ListenForMessage);
            messageListenThread.Start();

            while(true)
            {
                var CurrText = TERMINAL.Input();
                //Console.WriteLine("\n");
                if (string.IsNullOrWhiteSpace(CurrText)) { continue; }

                if (CurrText == "/leave")
                {
                    var message = Encoding.Unicode.GetBytes(USERNAME);
                    var final = new byte[message.Length + 2];
                    final[0] = CLIENT_LEFT_CODE;
                    final[^1] = (byte)'\r';
                    Array.Copy(message, 0, final, 1, message.Length);
                    int byteSent = CLIENT_SOCKET.Send(final);
                    CLIENT_SOCKET.Shutdown(SocketShutdown.Both);
                    CLIENT_SOCKET.Close();
                    CLIENT_SOCKET = null;
                    Thread.Sleep(10);

                    if (SERVER_SOCKET != null)
                    {
                        for (int i = 0; i < CLIENTS.Count; i++)
                        {
                            CLIENTS[i].socket.Shutdown(SocketShutdown.Both);
                            CLIENTS[i].socket.Close();
                        }
                        CLIENTS = new List<ConnectedClient>();
                        SERVER_SOCKET.Dispose();
                        SERVER_SOCKET = null;
                        TERMINAL.Output("---------------------------------------------");
                        TERMINAL.Output("Closed server, press any key to continue.");
                    }
                    else
                    {
                        TERMINAL.Output("---------------------------------------------");
                        TERMINAL.Output("Disconnected, press any key to continue.");
                    }
                    _ = TERMINAL.InputKey();
                    TERMINAL.Clear();
                    Username();
                }
                else if (CurrText == "/list")
                {
                    var message = Encoding.ASCII.GetBytes("yes" + "\r");
                    var final = new byte[message.Length + 1];
                    final[0] = LIST_CLIENTS_CODE;
                    Array.Copy(message, 0, final, 1, message.Length);
                    int byteSent = CLIENT_SOCKET.Send(final);
                }
                else if(CurrText.Split(' ')[0] == "/emoji")
                {
                    if (!CustomTerminal.EMOJIS.ContainsKey(CurrText.Split(' ')[1])) { TERMINAL.Output("No emoji called " + CurrText.Split(' ')[1]); continue; }
                    else
                    {
                        var message = Encoding.Unicode.GetBytes("[" + USERNAME + "]: " + CurrText);
                        var final = new byte[message.Length + 2];
                        final[0] = MESSAGE_SENT_CODE;
                        final[^1] = (byte)'\r';
                        Array.Copy(message, 0, final, 1, message.Length);
                        int byteSent = CLIENT_SOCKET.Send(final);
                    }
                }
                //send a message
                else
                {
                    var message = Encoding.Unicode.GetBytes("[" + USERNAME + "]: " + CurrText);
                    var final = new byte[message.Length + 2];
                    final[0] = MESSAGE_SENT_CODE;
                    final[^1] = (byte)'\r';
                    Array.Copy(message, 0, final, 1, message.Length);
                    int byteSent = CLIENT_SOCKET.Send(final);
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }
    }

    private static void Client_ListenForMessage()
    {
        if(CLIENT_SOCKET==null || !CLIENT_SOCKET.Connected) { return; }

        byte[] buffer = new byte[1024];
        int totallen = 0;
        while (true)
        {
            if (CLIENT_SOCKET == null || !CLIENT_SOCKET.Connected) { return; }
            try
            {
                var bytes = new byte[1024];
                int bytesRec = CLIENT_SOCKET.Receive(bytes);
                if (bytesRec <= 0) { break; }
                Array.Copy(bytes, 0, buffer, totallen, bytesRec);
                totallen += bytesRec;
                if (buffer.Contains((byte)'\r')) { break; }//look for EOF
            }
            catch(Exception e)
            {
                Console.WriteLine(e.ToString());
                continue;
            }
        }
        switch (buffer[0]) //first byte of any message is the identifier
        {
            case CLIENT_CONNECTED_CODE:
                //nothing here
                break;
            case MESSAGE_SENT_CODE:
                //nothing here
                break;
            case CLIENT_LEFT_CODE:
                //nothing here
                break;
            case LIST_CLIENTS_CODE:
                //nothing here
                break;
            case RECIEVE_CLIENT_JOIN_CODE:                
                TERMINAL.Output(Encoding.Unicode.GetString(buffer, 1, totallen - 2) + " Connected");
                break;
            case RECIEVE_CLIENT_LEFT_CODE:
                TERMINAL.Output(Encoding.Unicode.GetString(buffer, 1, totallen - 2)+ " Disconnected");
                break;
            case RECIEVE_MESSAGE_CODE:
                TERMINAL.Output(Encoding.Unicode.GetString(buffer, 1, totallen - 2));
                break;
            case RECIEVE_CLIENT_LIST_CODE:
                string list = Encoding.Unicode.GetString(buffer, 1, totallen - 2);
                TERMINAL.Output("Connected clients: "+list.Replace("\\", ", "));
                break;
            default:
                TERMINAL.Output("Recieved erroneous message with code " + buffer[0] + ", discarding");
                break;
        }

        Client_ListenForMessage();
    }
}

