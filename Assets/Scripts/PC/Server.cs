using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using UnityEngine;
using TMPro;

public class Server : MonoBehaviour
{
    // Singleton instance
    public static Server instance = null;

    // Server conection
    [SerializeField] private int _port = 8888;
    private UdpClient _server;
    private readonly object _lock = new();

    // Server data
    [SerializeField, Disable] private bool _connected = false;
    [SerializeField] private MobileSensorFlag _sensorEnable;
    private readonly Dictionary<uint, User> _users = new();
    private readonly ConcurrentQueue<MessageHandler> _tasks = new();
    private readonly Dictionary<NetworkMessageType, Action<NetworkMessage>> _actionHandles = new();

    // to generate user uid
    private uint _genUID = 0;

    public TMP_Text debugText;

    #region Unity events
    public void Awake()
    {
        if (instance != null)
            Destroy(gameObject);
        else
            instance = this;
    }

    void Start()
    {
        _actionHandles.Add(NetworkMessageType.JoinServer, HandleJoinServer);
        _actionHandles.Add(NetworkMessageType.LeaveServer, HandleLeaveServer);
        _actionHandles.Add(NetworkMessageType.MobileSensorEnable, HandleLeaveServer);
        _actionHandles.Add(NetworkMessageType.MobileSensorData, HandleLeaveServer);

        StartServerAsync();

        debugText.text = GetLocalIPAddress();
    }

    void Update()
    {
        if (_tasks.Count > 0)
        {
            if (_tasks.TryDequeue(out MessageHandler task))
                task.Execute();
        }

        if (Input.GetKeyDown(KeyCode.D))
            RequestEnableSensor();
    }

    private void OnDestroy()
    {
        CloseServerUDP();
    }

    #endregion

    #region Network
    private void StartServerAsync()
    {
        if(!_connected)
        {
            _server = new(_port);
            _connected = true;

            Debug.Log("Server Start!");

            ListenForClientAsync();
        }
    }
   
    async private void ListenForClientAsync()
    {
        if (_connected)
        {
            try
            {
                // Receive result async
                UdpReceiveResult result = await _server.ReceiveAsync();

                // Start to listen immediatly
                ListenForClientAsync();

                // get message
                NetworkMessage message = NetworkPackage.GetDataFromBytes(result.Buffer, result.Buffer.Length);

                // Get sender ip
                message.endPoint = result.RemoteEndPoint;

                _tasks.Enqueue(new(message, _actionHandles[message.type]));
            }
            catch (Exception ex)
            {
                if (!_connected)
                {
                    Debug.LogWarning("Server already closed!");
                }

                Debug.LogWarning($"Error: {ex.Message}");

                CloseServerUDP();
            }
        }
    }

    public async void SendMessageToClient(NetworkMessage message)
    {
        Debug.Log($"Server: send messages [{message.type}] to client ({message.endPoint})");

        NetworkPackage package = new(message.type, message.GetBytes());

        byte[] data = package.GetBytes();

        await _server.SendAsync(data, data.Length, message.endPoint);

        Debug.Log($"Message send to {message.endPoint} sucessful");
    }

    private async void SendMessageToClient(byte[] data, IPEndPoint endPoint)
    {
        await _server.SendAsync(data, data.Length, endPoint);

        Debug.Log($"Message send to {endPoint} sucessful");
    }

    public void SendMessageToClients(NetworkPackage package)
    {
        Debug.Log($"Server: send messages [{package.type}] to all client");

        byte[] data = package.GetBytes();

        foreach (var user in _users)
        {
            SendMessageToClient(data, user.Value.userEndPoint);
        } 
    }

    private void CloseServerUDP()
    {
        _connected = false;

        if (_server != null)
        {
            _connected = false;
            _server.Close();
            _server = null;
            Debug.Log("Server close!");
        }
    }
    #endregion

    #region Requests

    private void RequestEnableSensor()
    {
        var msg = NetworkMessageFactory.MobileSensorEnableMessage(_sensorEnable);

        SendMessageToClients(msg);
    }

    #endregion

    #region Utils
    private uint GetNextUID()
    {
        return ++_genUID;
    }
    #endregion

    #region Message handlers
    private void HandleJoinServer(NetworkMessage data)
    {
        var msg = data as JoinServer;

        if (!_users.ContainsKey(msg.ownerUID))
        {
            User user = new(msg.name, msg.endPoint, GetNextUID());

            msg.ownerUID = user.uid;

            _users.Add(user.uid, user);

            msg.successful = true;
            
            Debug.Log($"User with endPoint {msg.endPoint} join successfull");
        }
        else
        {
            msg.successful = false;

            msg.errorCode = NetworkErrorCode.ClientAlreadyInTheServer;

            Debug.LogWarning($"Server error : {msg.errorCode}");
        }

        SendMessageToClient(msg);
    }
    
    private void HandleLeaveServer(NetworkMessage data)
    {
        var msg = data as LeaveServer;

        if (_users.ContainsKey(msg.ownerUID))
        {
            _users.Remove(msg.ownerUID);

            msg.successful = true;

            Debug.Log($"User with endPoint {msg.endPoint} leave successfull");
        }
        else
        {
            msg.successful = false;

            msg.errorCode = NetworkErrorCode.ClientAlreadyLeaveTheServer;

            Debug.LogWarning($"Server error : {msg.errorCode}");
        }

        SendMessageToClient(msg);
    }

    private void HandleSensorEnable(NetworkMessage data)
    {
        
    }

    private void HandleSensorData(NetworkMessage data)
    {
        var msg = data as MobileSensorData;

        if (!_users.ContainsKey(msg.ownerUID))
        {
            Debug.Log($"User with endPoint {msg.endPoint} is not exist in the server");

            msg.successful = false;
        }
        else
        {
            var user = _users[msg.ownerUID];

            // TOFIX check how many flag is by set for update

            if((_sensorEnable & MobileSensorFlag.Velocity) != 0)
            {

            }
            if ((_sensorEnable & MobileSensorFlag.Acceleration) != 0)
            {

            }
            if ((_sensorEnable & MobileSensorFlag.Rotation) != 0)
            {

            }
            if ((_sensorEnable & MobileSensorFlag.Gravity) != 0)
            {

            }

            Debug.Log($"User with endPoint {msg.endPoint} has updated her sensor data");

            msg.successful = true;
        }

        SendMessageToClient(msg);
    }
    #endregion

    public string GetLocalIPAddress()
    {
        string localIP = string.Empty;

        // 获取所有网络接口
        NetworkInterface[] networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();

        foreach (NetworkInterface networkInterface in networkInterfaces)
        {
            // 过滤掉虚拟和回环接口
            if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                networkInterface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
            {
                // 获取网络接口的 IP 属性
                IPInterfaceProperties ipProperties = networkInterface.GetIPProperties();

                foreach (UnicastIPAddressInformation ipAddressInfo in ipProperties.UnicastAddresses)
                {
                    // 过滤 IPv6 地址和回环地址
                    if (ipAddressInfo.Address.AddressFamily == AddressFamily.InterNetwork &&
                        !IPAddress.IsLoopback(ipAddressInfo.Address))
                    {
                        localIP = ipAddressInfo.Address.ToString();
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(localIP))
                    break;
            }
        }

        return localIP;
    }

    // obsolete
    /*
    void StartServer()
    {
        _server = new(8888);
        _connected = true;

        Debug.Log("Server created!");

        while (_connected)
        {
            try
            {
                _receiveData = _server.Receive(ref _remotePoint); // stuck the program
                Debug.Log("Message received!");
                _receiveString = Encoding.Default.GetString(_receiveData);
                Debug.Log(_receiveString);
            }
            catch (SocketException ex)
            {
                if (ex.ErrorCode == 10004) // Socket error: An operation was aborted due to a signal from the user.
                {
                    Console.WriteLine("Receive operation was canceled.");
                }
                else
                {
                    Console.WriteLine("Error receiving data: {0}.", ex.Message);
                }

                _server?.Close();

                _connected = false;

                break;
            }
        }
    }
    */
}
