using System;
using System.Collections;
using Mirror;
using UnityEngine;

namespace Astrvo.Debugging
{
    // Simple ping/pong messages for connectivity and RTT verification
    public struct PingMessage : NetworkMessage
    {
        public long clientUnixTimeMs;
        public int sequence;
    }

    public struct PongMessage : NetworkMessage
    {
        public long clientUnixTimeMs;
        public long serverUnixTimeMs;
        public int sequence;
    }

    /// <summary>
    /// Drops a tiny overlay in-game to show Mirror connection state and RTT.
    /// Works in both Server and Client. No dependency on NetworkManager subclasses.
    /// </summary>
    public class NetworkConnectivityTester : MonoBehaviour
    {
        private static NetworkConnectivityTester instance;

        // UI state
        private string lastStatus = "Idle";
        private string lastError = string.Empty;
        private float lastPongSecondsAgo = -1f;
        private int pongsReceived;
        private double lastRttMs;
        private int pingSeq;

        // Config
        [Tooltip("Send a ping every N seconds while connected (client only)")]
        [SerializeField] private float pingIntervalSeconds = 3f;

        private Coroutine pingCoroutine;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (instance != null) return;
            var go = new GameObject("NetworkConnectivityTester");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<NetworkConnectivityTester>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            RegisterHandlers();
            SubscribeClientEvents();
            UpdateStatus("Initialized");
        }

        private void OnEnable()
        {
            // Re-subscribe in case of domain reload disabled
            SubscribeClientEvents();
        }

        private void OnDisable()
        {
            UnsubscribeClientEvents();
        }

        private void RegisterHandlers()
        {
            // Server replies to pings with a pong
            NetworkServer.RegisterHandler<PingMessage>(OnServerReceivePing, false);

            // Client receives pong and computes RTT
            NetworkClient.RegisterHandler<PongMessage>(OnClientReceivePong, false);
        }

        private void SubscribeClientEvents()
        {
            NetworkClient.OnConnectedEvent += OnClientConnected;
            NetworkClient.OnDisconnectedEvent += OnClientDisconnected;
        }

        private void UnsubscribeClientEvents()
        {
            NetworkClient.OnConnectedEvent -= OnClientConnected;
            NetworkClient.OnDisconnectedEvent -= OnClientDisconnected;
        }

        private void OnClientConnected()
        {
            UpdateStatus("Client connected");
            StartPinging();
        }

        private void OnClientDisconnected()
        {
            UpdateStatus("Client disconnected");
            StopPinging();
        }

        private void StartPinging()
        {
            if (pingCoroutine != null) return;
            pingCoroutine = StartCoroutine(PingLoop());
        }

        private void StopPinging()
        {
            if (pingCoroutine != null)
            {
                StopCoroutine(pingCoroutine);
                pingCoroutine = null;
            }
        }

        private IEnumerator PingLoop()
        {
            var wait = new WaitForSeconds(pingIntervalSeconds);
            while (NetworkClient.isConnected)
            {
                SendPing();
                yield return wait;
                if (lastPongSecondsAgo >= 0f) lastPongSecondsAgo += pingIntervalSeconds;
            }
            pingCoroutine = null;
        }

        private void SendPing()
        {
            try
            {
                if (!NetworkClient.isConnected)
                {
                    UpdateStatus("Not connected; ping skipped");
                    return;
                }

                var msg = new PingMessage
                {
                    clientUnixTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    sequence = ++pingSeq
                };
                NetworkClient.Send(msg);
                UpdateStatus($"Ping {msg.sequence} sent");
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                UpdateStatus("Ping send failed");
            }
        }

        private void OnServerReceivePing(NetworkConnectionToClient conn, PingMessage msg)
        {
            var reply = new PongMessage
            {
                clientUnixTimeMs = msg.clientUnixTimeMs,
                serverUnixTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                sequence = msg.sequence
            };
            conn.Send(reply);
        }

        private void OnClientReceivePong(PongMessage msg)
        {
            var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            // Approximate RTT: time now - when client originally sent
            lastRttMs = Math.Max(0, nowMs - msg.clientUnixTimeMs);
            pongsReceived++;
            lastPongSecondsAgo = 0f;
            UpdateStatus($"Pong {msg.sequence} received; RTT ~{lastRttMs:0} ms");
        }

        private void Update()
        {
            // advance timer smoothly when connected
            if (NetworkClient.isConnected && lastPongSecondsAgo >= 0f)
            {
                lastPongSecondsAgo += Time.unscaledDeltaTime;
            }
        }

        private void UpdateStatus(string status)
        {
            lastStatus = status;
            Debug.unityLogger.Log("NetworkConnectivity", status);
        }

        private Rect overlayRect = new Rect(10, 10, 360, 140);

        private void OnGUI()
        {
            // Minimal footprint overlay; safe for WebGL
            overlayRect = GUI.Window(987654, overlayRect, DrawWindow, "Network Connectivity");
        }

        private void DrawWindow(int id)
        {
            var transport = Transport.active;
            var transportName = transport != null ? transport.GetType().Name : "(no transport)";
            var isClient = NetworkClient.active;
            var isServer = NetworkServer.active;
            var isConnected = NetworkClient.isConnected;

            GUILayout.BeginVertical();
            GUILayout.Label($"Transport: {transportName}");
            GUILayout.Label($"Mode: {(isServer && isClient ? "Host" : isServer ? "Server" : isClient ? "Client" : "Offline")}");
            GUILayout.Label($"Connected: {isConnected}");
            GUILayout.Label($"Status: {lastStatus}");
            if (!string.IsNullOrEmpty(lastError))
                GUILayout.Label($"Error: {lastError}");
            if (pongsReceived > 0)
                GUILayout.Label($"RTT: {lastRttMs:0} ms (pongs: {pongsReceived}, last {lastPongSecondsAgo:0.0}s ago)");
            GUILayout.Space(6);
            DrawConnectControls();
            GUILayout.Space(6);
            if (isClient)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Send Ping Now"))
                {
                    SendPing();
                }
                if (GUILayout.Button("Reset Stats"))
                {
                    pongsReceived = 0;
                    lastRttMs = 0;
                    lastPongSecondsAgo = -1f;
                    lastError = string.Empty;
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();

            GUI.DragWindow();
        }

        // --- Connect controls ---
        private string connectAddress = string.Empty;
        private void EnsureDefaultAddress()
        {
            if (!string.IsNullOrWhiteSpace(connectAddress)) return;
            // Try to use NetworkManager's configured address
            if (NetworkManager.singleton != null && !string.IsNullOrWhiteSpace(NetworkManager.singleton.networkAddress))
            {
                connectAddress = NetworkManager.singleton.networkAddress;
            }
            else
            {
                connectAddress = "localhost";
            }
        }

        private void DrawConnectControls()
        {
            EnsureDefaultAddress();
            GUILayout.BeginVertical("box");
            GUILayout.Label("Manual Connect (Client)");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Address/URI:", GUILayout.Width(90));
            connectAddress = GUILayout.TextField(connectAddress);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Connect"))
            {
                TryManualConnect(connectAddress);
            }
            if (GUILayout.Button("Disconnect"))
            {
                TryManualDisconnect();
            }
            GUILayout.EndHorizontal();

            // Hints
            if (Transport.active == null)
                GUILayout.Label("Hint: 未找到激活的 Transport，检查场景中的 NetworkManager.transport。");
            GUILayout.EndVertical();
        }

        private void TryManualConnect(string addressOrUri)
        {
            try
            {
                if (NetworkClient.active)
                {
                    UpdateStatus("Client already active");
                    return;
                }

                // Ensure Transport.active is set (usually by NetworkManager)
                if (Transport.active == null && NetworkManager.singleton != null && NetworkManager.singleton.transport != null)
                {
                    Transport.active = NetworkManager.singleton.transport;
                }

                if (NetworkManager.singleton != null)
                {
                    // Prefer NetworkManager to keep internal state consistent
                    NetworkManager.singleton.networkAddress = addressOrUri;
                    NetworkManager.singleton.StartClient();
                    UpdateStatus($"StartClient to {addressOrUri}");
                }
                else
                {
                    // Raw connect fallback
                    if (addressOrUri.StartsWith("ws://", StringComparison.OrdinalIgnoreCase) ||
                        addressOrUri.StartsWith("wss://", StringComparison.OrdinalIgnoreCase) ||
                        addressOrUri.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                        addressOrUri.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        NetworkClient.Connect(new Uri(addressOrUri));
                    }
                    else
                    {
                        NetworkClient.Connect(addressOrUri);
                    }
                    UpdateStatus($"Connect to {addressOrUri}");
                }
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                UpdateStatus("Manual connect failed");
            }
        }

        private void TryManualDisconnect()
        {
            try
            {
                if (!NetworkClient.active)
                {
                    UpdateStatus("Client not active");
                    return;
                }
                if (NetworkManager.singleton != null)
                {
                    NetworkManager.singleton.StopClient();
                }
                else
                {
                    NetworkClient.Disconnect();
                }
                UpdateStatus("Disconnect requested");
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                UpdateStatus("Manual disconnect failed");
            }
        }
    }
}


