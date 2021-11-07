using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
public class ChatServer : MonoBehaviour
{
    private Socket serverSocket;
    //What container do I use???
    //Remember to cast to Socket if using ArrayList!!!
    //ArrayList clients = new ArrayList();
    private List<Socket> clients = new List<Socket>();

    private class Client
    {
        string name;
        List<string> bannedClients;
        Socket socket;
    }

    private class Message
    {
        enum MessageType
        {
            ServerHellow,
            //ChangeNameServerRequest
            BroadcastMessageRequest,
            ServerChatUpdate
        }
        MessageType Type;
        string senderName;
        System.DateTime date;
        string message;
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
        while (serverSocket.Poll(1000, SelectMode.SelectRead))
        {
            Socket accepted = serverSocket.Accept();
            clients.Add(accepted);
            IPEndPoint clientEndPoint = (IPEndPoint)accepted.RemoteEndPoint;
            Debug.Log("Accepted new client with address: " + clientEndPoint.Address + "and port: " + clientEndPoint.Port);
        }
        for (int i = 0; i < clients.Count; ++i)
        {
            while (clients[i].Poll(1000, SelectMode.SelectRead))
            {
                byte[] data = new byte[1024];
                int recv = clients[i].Receive(data);
                if (recv == 0)
                {
                    IPEndPoint clientEndPoint = (IPEndPoint)clients[i].RemoteEndPoint;
                    Debug.Log("Client Disconected with address: " + clientEndPoint.Address + "and port: " + clientEndPoint.Port);
                    clients[i].Close();
                    clients.RemoveAt(i);
                    --i;
                    break;
                }
                string received = Encoding.ASCII.GetString(data, 0, recv);
                if (received == "Ping" && clients[i].Poll(1000, SelectMode.SelectWrite))
                {
                    clients[i].Send(Encoding.ASCII.GetBytes("Pong"));
                    Debug.Log("Sending pong to client");
                }
            }
        }
    }
    private void OnDestroy()
    {
        //Peta al fer el shutdown
        //serverSocket.Shutdown(SocketShutdown.Both);
        serverSocket.Close();
        Debug.Log("Bye :)");
    }
}
