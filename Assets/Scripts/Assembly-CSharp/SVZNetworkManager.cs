using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

/// <summary>
/// Lightweight, reliable TCP Socket Network Manager for SVZ 1v1 PvP
/// Supports Localhost (127.0.0.1), LAN (192.168.x.x), Virtual VPN (Radmin/Hamachi 26.x.x.x), and Port Forwarding.
/// </summary>
public class SVZNetworkManager : MonoBehaviour
{
    private static SVZNetworkManager sInstance;

    public const int kDefaultPort = 7777;

    public enum NetworkRole
    {
        None,
        Host,
        Client,
        SamePC
    }

    private NetworkRole mRole = NetworkRole.None;
    private TcpListener mListener;
    private TcpClient mSocket;
    private NetworkStream mStream;
    private Thread mNetworkThread;
    private volatile bool mIsRunning = false;

    private readonly object mLock = new object();
    private Queue<string> mIncomingPackets = new Queue<string>();
    private Queue<string> mOutgoingPackets = new Queue<string>();

    public event Action<string, string> OnPacketReceived;
    public event Action OnConnected;
    public event Action OnDisconnected;

    public static SVZNetworkManager Instance
    {
        get
        {
            if (sInstance == null)
            {
                Init();
            }
            return sInstance;
        }
    }

    public NetworkRole Role
    {
        get { return mRole; }
    }

    public bool IsConnected
    {
        get
        {
            if (mRole == NetworkRole.SamePC) return true;
            try
            {
                return mSocket != null && mSocket.Client != null && mSocket.Connected;
            }
            catch
            {
                return false;
            }
        }
    }

    public bool IsHost
    {
        get { return mRole == NetworkRole.Host || mRole == NetworkRole.SamePC; }
    }

    public int Port = kDefaultPort;
    public string RemoteIP = "127.0.0.1";
    public int CurrentPingMs = 0;
    private float mPingTimer = 0f;

    public string GetLocalIPAddress()
    {
        List<string> ips = GetLocalIPAddresses();
        if (ips == null || ips.Count == 0) return "127.0.0.1";

        // 1. Radmin VPN (26.x.x.x) or Hamachi (25.x.x.x)
        for (int i = 0; i < ips.Count; i++)
        {
            if (ips[i].StartsWith("26.") || ips[i].StartsWith("25."))
            {
                return ips[i];
            }
        }

        // 2. Local Wi-Fi / Ethernet LAN (192.168.x.x or 10.x.x.x)
        for (int i = 0; i < ips.Count; i++)
        {
            if (ips[i].StartsWith("192.168.") || ips[i].StartsWith("10."))
            {
                return ips[i];
            }
        }

        return ips[0];
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        if (sInstance == null)
        {
            GameObject go = new GameObject("[SVZNetworkManager]");
            DontDestroyOnLoad(go);
            sInstance = go.AddComponent<SVZNetworkManager>();
        }
    }

    private void Awake()
    {
        if (sInstance == null)
        {
            sInstance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (sInstance != this)
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        // Dispatch incoming packets on Unity's main thread
        while (true)
        {
            string packet = null;
            lock (mLock)
            {
                if (mIncomingPackets.Count > 0)
                {
                    packet = mIncomingPackets.Dequeue();
                }
            }

            if (packet == null) break;

            ProcessPacket(packet);
        }

        // Active Heartbeat & Ping (1.0s interval)
        if (IsConnected && mRole != NetworkRole.SamePC)
        {
            mPingTimer += Time.unscaledDeltaTime;
            if (mPingTimer >= 1.0f)
            {
                mPingTimer = 0f;
                if (IsHost)
                {
                    SendPacket("SYS_PING", Time.realtimeSinceStartup.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
                }
            }
        }
        else
        {
            CurrentPingMs = 0;
            mPingTimer = 0f;
        }
    }

    private void OnDestroy()
    {
        StopNetwork();
    }

    private void OnApplicationQuit()
    {
        StopNetwork();
    }

    #region Host & Client Controls

    public bool StartHost(int port = kDefaultPort)
    {
        StopNetwork();
        try
        {
            mRole = NetworkRole.Host;
            mListener = new TcpListener(IPAddress.Any, port);
            mListener.Start();
            mIsRunning = true;

            mNetworkThread = new Thread(HostListenThread);
            mNetworkThread.IsBackground = true;
            mNetworkThread.Start();

            Debug.Log("[SVZNetwork] Host basariyla baslatildi. Port: " + port);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError("[SVZNetwork] Host baslatilamadi: " + ex.Message);
            StopNetwork();
            return false;
        }
    }

    public void ConnectToHost(string hostIp, int port = kDefaultPort)
    {
        StopNetwork();
        mRole = NetworkRole.Client;
        mIsRunning = true;

        mNetworkThread = new Thread(() => ClientConnectThread(hostIp, port));
        mNetworkThread.IsBackground = true;
        mNetworkThread.Start();
    }

    public void StartSamePCMode()
    {
        StopNetwork();
        mRole = NetworkRole.SamePC;
        if (OnConnected != null)
        {
            OnConnected();
        }
        Debug.Log("[SVZNetwork] Ayni PC 2-Kisilik (Hotseat) Modu Aktif Edildi.");
    }

    public void StopNetwork()
    {
        mIsRunning = false;
        mRole = NetworkRole.None;

        try
        {
            if (mStream != null) { try { mStream.Close(); } catch { } mStream = null; }
            if (mSocket != null) { try { mSocket.Close(); } catch { } mSocket = null; }
            if (mListener != null) { try { mListener.Stop(); } catch { } mListener = null; }
            if (mNetworkThread != null && mNetworkThread.IsAlive)
            {
                if (Thread.CurrentThread != mNetworkThread)
                {
                    try { mNetworkThread.Join(120); } catch { }
                }
                mNetworkThread = null;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[SVZNetwork] StopNetwork: " + ex.Message);
        }

        lock (mLock)
        {
            mIncomingPackets.Clear();
            mOutgoingPackets.Clear();
        }
    }

    #endregion

    #region Socket Threads

    private void HostListenThread()
    {
        try
        {
            while (mIsRunning)
            {
                TcpClient client = mListener.AcceptTcpClient();
                client.NoDelay = true;
                try { client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true); } catch { }
                mSocket = client;
                mStream = client.GetStream();

                lock (mLock)
                {
                    mIncomingPackets.Enqueue("SYS_CONNECTED|Client");
                }

                ReadLoop();
            }
        }
        catch (Exception ex)
        {
            if (mIsRunning)
            {
                Debug.LogWarning("[SVZNetwork] HostListenThread error: " + ex.Message);
            }
        }
    }

    private void ClientConnectThread(string hostIp, int port)
    {
        int attempts = 0;
        const int maxAttempts = 3;
        bool connected = false;

        while (mIsRunning && attempts < maxAttempts && !connected)
        {
            attempts++;
            try
            {
                if (mSocket != null) { try { mSocket.Close(); } catch { } }
                mSocket = new TcpClient();
                mSocket.NoDelay = true;

                IAsyncResult ar = mSocket.BeginConnect(hostIp, port, null, null);
                bool success = ar.AsyncWaitHandle.WaitOne(3500, true);
                if (success && mSocket.Connected)
                {
                    mSocket.EndConnect(ar);
                    try { mSocket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true); } catch { }
                    mStream = mSocket.GetStream();
                    connected = true;
                }
                else
                {
                    try { mSocket.Close(); } catch { }
                    if (attempts < maxAttempts && mIsRunning)
                    {
                        Thread.Sleep(400);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning(string.Format("[SVZNetwork] Baglanti denemesi {0}/{1} ({2}:{3}) basarisiz: {4}", attempts, maxAttempts, hostIp, port, ex.Message));
                if (attempts < maxAttempts && mIsRunning)
                {
                    Thread.Sleep(400);
                }
            }
        }

        if (connected && mIsRunning)
        {
            lock (mLock)
            {
                mIncomingPackets.Enqueue("SYS_CONNECTED|Host");
            }

            ReadLoop();
        }
        else
        {
            try
            {
                if (mStream != null) { mStream.Close(); mStream = null; }
                if (mSocket != null) { mSocket.Close(); mSocket = null; }
            }
            catch { }
            mRole = NetworkRole.None;
            mIsRunning = false;
            lock (mLock)
            {
                mIncomingPackets.Enqueue("SYS_DISCONNECTED|Failed");
            }
        }
    }

    private void ReadLoop()
    {
        byte[] buffer = new byte[8192];
        StringBuilder sb = new StringBuilder();

        while (mIsRunning && IsConnected)
        {
            try
            {
                int bytesRead = mStream.Read(buffer, 0, buffer.Length);
                if (bytesRead <= 0) break;

                string chunk = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                sb.Append(chunk);

                string content = sb.ToString();
                int newlineIdx;
                while ((newlineIdx = content.IndexOf('\n')) >= 0)
                {
                    string packet = content.Substring(0, newlineIdx).Trim();
                    content = content.Substring(newlineIdx + 1);

                    if (!string.IsNullOrEmpty(packet))
                    {
                        lock (mLock)
                        {
                            mIncomingPackets.Enqueue(packet);
                        }
                    }
                }
                sb = new StringBuilder(content);
            }
            catch
            {
                break;
            }
        }

        try
        {
            if (mStream != null) { mStream.Close(); mStream = null; }
            if (mSocket != null) { mSocket.Close(); mSocket = null; }
        }
        catch { }

        lock (mLock)
        {
            mIncomingPackets.Enqueue("SYS_DISCONNECTED|Lost");
        }
    }

    #endregion

    #region Packet Transmission

    public void SendPacket(string cmd, string payload = "")
    {
        if (mRole == NetworkRole.SamePC)
        {
            // Echo locally for same-PC mode
            string fakePacket = cmd + "|" + payload;
            lock (mLock)
            {
                mIncomingPackets.Enqueue(fakePacket);
            }
            return;
        }

        if (mStream == null || !IsConnected) return;

        try
        {
            string line = cmd + "|" + payload + "\n";
            byte[] bytes = Encoding.UTF8.GetBytes(line);
            mStream.Write(bytes, 0, bytes.Length);
            mStream.Flush();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[SVZNetwork] SendPacket failed: " + ex.Message);
        }
    }

    private void ProcessPacket(string raw)
    {
        if (raw.StartsWith("SYS_CONNECTED"))
        {
            if (OnConnected != null) OnConnected();
            return;
        }
        if (raw.StartsWith("SYS_DISCONNECTED"))
        {
            if (OnDisconnected != null) OnDisconnected();
            return;
        }
        if (raw.StartsWith("SYS_PING|"))
        {
            string ts = raw.Substring(9);
            SendPacket("SYS_PONG", ts);
            return;
        }
        if (raw.StartsWith("SYS_PONG|"))
        {
            string ts = raw.Substring(9);
            float sendTime;
            if (float.TryParse(ts, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out sendTime))
            {
                float rtt = (Time.realtimeSinceStartup - sendTime) * 1000f;
                CurrentPingMs = Mathf.Clamp(Mathf.RoundToInt(rtt), 1, 999);
                SendPacket("SYS_PING_SYNC", CurrentPingMs.ToString());
            }
            return;
        }
        if (raw.StartsWith("SYS_PING_SYNC|"))
        {
            int p;
            if (int.TryParse(raw.Substring(14), out p))
            {
                CurrentPingMs = p;
            }
            return;
        }

        int sep = raw.IndexOf('|');
        string cmd = (sep >= 0) ? raw.Substring(0, sep) : raw;
        string payload = (sep >= 0) ? raw.Substring(sep + 1) : string.Empty;

        if (OnPacketReceived != null)
        {
            OnPacketReceived(cmd, payload);
        }
    }

    #endregion

    #region Network Utilities

    private static void TryAddValidIPv4(List<string> ips, string ipStr)
    {
        if (string.IsNullOrEmpty(ipStr)) return;
        ipStr = ipStr.Trim();
        if (ipStr == "127.0.0.1" || ipStr == "0.0.0.0" || ipStr.StartsWith("169.254.")) return;
        if (!ips.Contains(ipStr))
        {
            ips.Add(ipStr);
        }
    }

    public static List<string> GetLocalIPAddresses()
    {
        List<string> ips = new List<string>();

        // 1. Android 5-16 native bridge (Java NetworkInterface + ConnectivityManager LinkProperties + WifiManager)
        try
        {
            if (Application.platform == RuntimePlatform.Android)
            {
                TryAddValidIPv4(ips, SVZDeviceCompatibility.GetAndroidLanIPv4());
            }
        }
        catch { }

        // 2. Local routing table UDP probe (zero packets sent; works across Wi-Fi / Hotspot / Ethernet / VPN)
        try
        {
            using (Socket probe = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                probe.Connect("8.8.8.8", 65530);
                IPEndPoint localEp = probe.LocalEndPoint as IPEndPoint;
                if (localEp != null)
                {
                    TryAddValidIPv4(ips, localEp.Address.ToString());
                }
            }
        }
        catch { }

        // 3. Managed NetworkInterface scan (wrapped for Android 11+ SELinux netlink tolerance)
        try
        {
            System.Net.NetworkInformation.NetworkInterface[] nics =
                System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
            foreach (System.Net.NetworkInformation.NetworkInterface nic in nics)
            {
                if (nic == null ||
                    nic.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up ||
                    nic.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
                {
                    continue;
                }
                System.Net.NetworkInformation.IPInterfaceProperties props = nic.GetIPProperties();
                if (props == null) continue;
                foreach (System.Net.NetworkInformation.UnicastIPAddressInformation uni in props.UnicastAddresses)
                {
                    if (uni != null && uni.Address != null &&
                        uni.Address.AddressFamily == AddressFamily.InterNetwork &&
                        !IPAddress.IsLoopback(uni.Address))
                    {
                        TryAddValidIPv4(ips, uni.Address.ToString());
                    }
                }
            }
        }
        catch { }

        // 4. DNS fallback (PC / legacy Mono)
        try
        {
            string hostName = Dns.GetHostName();
            IPAddress[] addresses = Dns.GetHostAddresses(hostName);
            foreach (IPAddress addr in addresses)
            {
                if (addr.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(addr))
                {
                    TryAddValidIPv4(ips, addr.ToString());
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[SVZNetwork] GetLocalIPAddresses DNS fallback: " + ex.Message);
        }

        if (ips.Count == 0)
        {
            ips.Add("127.0.0.1");
        }
        return ips;
    }

    #endregion
}
