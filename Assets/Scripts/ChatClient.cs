using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine.UI;
using System.IO;

public class ChatClient : MonoBehaviour
{
    private enum ClientState
    {
        ServerNotAviable,
        Disconected,
        Connecting,
        UnamedConnected,
        Connected
    }
    ClientState state = ClientState.Disconected;
    Socket clientSocket;
    IPEndPoint serverIpep;
    string username = "";
    //public InputField input;
    public Text outputLog;
    public InputField inputField;
    private Text placeHolderText;
    public Button inputButton;
    private bool serverNotActive = false;
    private object notActiveLock = new object();
    private bool connectedToServer = false;
    private object connectedToServerLock = new object();
    public float ReconectTime = 2.0f;
    private float currentReconnectTimer;

    private void Start()
    {
        clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        serverIpep = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9052);
        outputLog.text = "";
        placeHolderText = inputField.placeholder.gameObject.GetComponent<Text>();
    }
    private void Update()
    {
        switch (state)
        {
            case ClientState.ServerNotAviable:
                currentReconnectTimer -= Time.deltaTime;
                if (currentReconnectTimer < 0.0f)
                    state = ClientState.Disconected;
                break;

            case ClientState.Disconected:
                inputField.interactable = false;
                placeHolderText.text = "Connecting";
                inputButton.interactable = false;
                OutputChat("Connecting to server");
                Thread connectingThread = new Thread(ConnectToserver);
                connectingThread.Start();
                state = ClientState.Connecting;
                break;

                case ClientState.Connecting:
                //If not succesfully conected with server try again later
                lock (notActiveLock)
                {
                    if(serverNotActive == true)
                    {
                        state = ClientState.ServerNotAviable;
                        serverNotActive = false;
                        currentReconnectTimer = ReconectTime;
                        OutputChat("Server not aviable");
                        OutputChat("Trying reconection in " + ReconectTime + " seconds");
                        break;
                    }
                }
                //If succesfully connected with server continue to request a name to the server
                lock(connectedToServerLock)
                {
                    if (!connectedToServer)
                        break;
                }
                if (clientSocket.Poll(0, SelectMode.SelectWrite))
                {
                    inputField.interactable = true;
                    placeHolderText.text = "Enter Username";
                    inputButton.interactable = true;
                    state = ClientState.UnamedConnected;
                    OutputChat("Succesfully conected to server");
                    OutputChat("EnterUsername");
                }
                break;

            case ClientState.UnamedConnected:
                if (clientSocket.Poll(0, SelectMode.SelectRead))
                {
                    byte[] data = new byte[2048];
                    int recv = clientSocket.Receive(data);
                    if (recv == 0)
                    {
                        OutputChat("Lost Connection to server");
                        state = ClientState.Disconected;
                        connectedToServer = false;
                        break;
                    }
                    MemoryStream stream = new MemoryStream(data);
                    BinaryReader reader = new BinaryReader(stream);
                    string json;
                    while (true)
                    {
                        json = reader.ReadString();
                        if (json == "")
                            break;

                        Message msg = new Message();
                        msg = JsonUtility.FromJson<Message>(json);
                        if (msg.Type == Message.MessageType.ClientNameAccepted)
                        {
                            username = msg.message;
                            OutputChat("Name " + username + " accepted!!!!");
                            OutputChat("Welcome to the chat");
                            state = ClientState.Connected;
                            placeHolderText.text = "Enter message/command";
                            //Posar aki totes les commands k el server accepta
                        }
                        else if (msg.Type == Message.MessageType.ClientNameRejected)
                        {
                            OutputChat("Username " + msg.message + " rejected");
                            OutputChat("Username already exists or it starts with /");
                            OutputChat("Try another name");
                            placeHolderText.text = "Enter another Username";
                        }
                    }
                }
                break;

            case ClientState.Connected:
                if (clientSocket.Poll(0, SelectMode.SelectRead))
                {
                    byte[] data = new byte[2048];
                    int recv = clientSocket.Receive(data);
                    if (recv == 0)
                    {
                        OutputChat("Lost Connection to server");
                        state = ClientState.Disconected;
                        connectedToServer = false;
                        break;
                    }
                    MemoryStream stream = new MemoryStream(data);
                    BinaryReader reader = new BinaryReader(stream);
                    string json;
                    while (true)
                    {
                        json = reader.ReadString();
                        if (json == "")
                            break;

                        Message msg = new Message();
                        msg = JsonUtility.FromJson<Message>(json);
                        switch(msg.Type)
                        {
                            case Message.MessageType.ClientBroadcastedMessage:
                                OutputChat(msg.message, msg.senderName);
                                break;
                            case Message.MessageType.ClientNewClientOnChat:
                                OutputChat(msg.message + " connected to the chat");
                                break;
                            case Message.MessageType.ClientClientLeavedChat:
                                OutputChat(msg.message + " disconected from the chat");
                                break;
                            default:
                                break;
                        }
                    }
                }
                break;
        }

    }

    private void OnDestroy()
    {
        //jsut shutdown if we have a connection with the server
        if(state != ClientState.ServerNotAviable && state != ClientState.Disconected && state !=ClientState.Connecting)
            clientSocket.Shutdown(SocketShutdown.Both);
        clientSocket.Close();
        Debug.Log("bye :)");
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
        Debug.Log("Sending message to client with address: " + clientEndPoint.Address + "and port: " + clientEndPoint.Port);
    }
    public void SendCommand()
    {
        if (!connectedToServer || inputField.text == "")
            return;
        if(clientSocket.Poll(0,SelectMode.SelectWrite))
        {
            Message msg = new Message();
            if (state == ClientState.UnamedConnected)
                msg.Type = Message.MessageType.ServerNameRequest;
            else
            {
                msg.Type = Message.MessageType.ServerBroadcastMessageRequest;
                msg.senderName = username;
            }
            msg.message = inputField.text;
            inputField.text = "";
            JSONSerializeAndSendMessage(msg, clientSocket);
        }
        else
        {
            OutputChat("Not connected to server");
            state = ClientState.Disconected;
        }
    }
    private void ConnectToserver()
    {
        //Peta si no hi ha server!!!!!
        try
        {
            clientSocket.Connect(serverIpep);
            lock (connectedToServerLock)
            {
                connectedToServer = true;
            }
        }
        catch(SocketException e)
        {
            //Server not active
            if (e.SocketErrorCode == SocketError.ConnectionRefused)
            {
                lock(notActiveLock)
                {
                    serverNotActive = true;
                }
            }
        }
    }
    private void OutputChat(string output, string sender = "")
    {
        if (sender != "")
            outputLog.text += sender + ": ";
        outputLog.text += output + System.Environment.NewLine;
    }
}