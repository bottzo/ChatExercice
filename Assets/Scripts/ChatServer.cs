using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.IO;

public struct Message
{
    public enum MessageType
    {
        ServerNameRequest,
        ClientNameRejected,
        ClientNameAccepted,
        ClientNewClientOnChat,
        ClientClientLeavedChat,
        ClientChangedName,
        ServerBroadcastMessageRequest,
        ClientBroadcastedMessage
    }
    public MessageType Type;
    public string senderName;
    //public System.DateTime date;
    //Color color 
    public string message;
}

public class ChatServer : MonoBehaviour
{
    private Socket serverSocket;
    //What container do I use???
    //Remember to cast to Socket if using ArrayList!!!
    //ArrayList clients = new ArrayList();
    private List<Client> clients = new List<Client>();
    private List<Message> msgsToBroadcast = new List<Message>();

    private class Client
    {
        public bool connected = false;
        public string name = "";
        public List<string> bannedClients;
        public Socket socket;
        //Color color
    }

    void Start()
    {
        serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        IPEndPoint ipep = new IPEndPoint(IPAddress.Any, 9052);
        serverSocket.Bind(ipep);
        serverSocket.Listen(15);
        Debug.Log("Server socket ready");
    }

    void Update()
    {
        while (serverSocket.Poll(0, SelectMode.SelectRead))
        {
            Client accepted = new Client();
            accepted.socket = serverSocket.Accept();
            clients.Add(accepted);
            IPEndPoint clientEndPoint = (IPEndPoint)accepted.socket.RemoteEndPoint;
            Debug.Log("Accepted new client with address: " + clientEndPoint.Address + "and port: " + clientEndPoint.Port);
        }
        for (int i = 0; i < clients.Count; ++i)
        {
            while (clients[i].socket.Poll(0, SelectMode.SelectRead))
            {
                byte[] data = new byte[2048];
                int recv = clients[i].socket.Receive(data);
                if (recv == 0)
                {
                    IPEndPoint clientEndPoint = (IPEndPoint)clients[i].socket.RemoteEndPoint;
                    Debug.Log("Client Disconected with address: " + clientEndPoint.Address + "and port: " + clientEndPoint.Port);
                    Message msg = new Message();
                    msg.Type = Message.MessageType.ClientClientLeavedChat;
                    msg.message = clients[i].name;
                    msgsToBroadcast.Add(msg);
                    clients[i].socket.Close();
                    clients.RemoveAt(i);
                    --i;
                    break;
                }
                MemoryStream stream = new MemoryStream(data);
                BinaryReader reader = new BinaryReader(stream);
                string json;
                //string json = Encoding.ASCII.GetString(data, 0, recv);
                while (true)
                {
                    json = reader.ReadString();
                    if (json == "")
                        break;

                    Message msg = new Message();
                    msg = JsonUtility.FromJson<Message>(json);
                    switch (msg.Type)
                    {
                        case Message.MessageType.ServerBroadcastMessageRequest:
                            msg.Type = Message.MessageType.ClientBroadcastedMessage;
                            msgsToBroadcast.Add(msg);
                            break;
                        case Message.MessageType.ServerNameRequest:
                            if (msg.message.StartsWith("/") || UserNameInUse(msg.message))
                            {
                                msg.Type = Message.MessageType.ClientNameRejected;
                                JSONSerializeAndSendMessage(msg, clients[i].socket);
                            }
                            else
                            {
                                msg.Type = Message.MessageType.ClientNameAccepted;
                                JSONSerializeAndSendMessage(msg, clients[i].socket);
                                if (clients[i].name == "")
                                {
                                    msg.Type = Message.MessageType.ClientNewClientOnChat;
                                    clients[i].connected = true;
                                }
                                else
                                {
                                    msg.Type = Message.MessageType.ClientChangedName;
                                }
                                msgsToBroadcast.Add(msg);
                                clients[i].name = msg.message;
                            }
                            break;
                        default:
                            Debug.LogWarning("Received and unhandlable message type");
                            break;
                    }
                }
            }
        }
        if (msgsToBroadcast.Count > 0)
        {
            foreach (Client client in clients)
            {
                if (client.connected)
                {
                    foreach (Message msg in msgsToBroadcast)
                    {
                        //What if we fail here unable to send the message because of the poll???
                        //look here for the banned clients
                        if (client.socket.Poll(0, SelectMode.SelectWrite))
                            JSONSerializeAndSendMessage(msg, client.socket);
                    }
                }
            }
            msgsToBroadcast.Clear();
        }
    }
    private void OnDestroy()
    {
        serverSocket.Close();
        Debug.Log("Bye :)");
    }
    private void JSONSerializeAndSendMessage(Message message, Socket socket)
    {
        //The endiannes???
        //chack with the poll if we can send the message: what happens if we can't????
        string json = JsonUtility.ToJson(message);
        MemoryStream stream = new MemoryStream();
        BinaryWriter writer = new BinaryWriter(stream);
        writer.Write(json);
        //Encoding.UTF8.GetBytes(json)
        socket.Send(stream.GetBuffer());
        //socket.Send(Encoding.ASCII.GetBytes(json));
        IPEndPoint clientEndPoint = (IPEndPoint)socket.RemoteEndPoint;
        Debug.Log("Sending message to client with address: " + clientEndPoint.Address + " and port: " + clientEndPoint.Port);
    }

    private bool UserNameInUse(string name)
    {
        foreach (Client client in clients)
        {
            if (client.name == name)
                return true;
        }
        return false;
    }
}