using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity2017Mcp
{
    [InitializeOnLoad]
    public static class Unity2017McpBridge
    {
        public const int DefaultPort = 20174;
        private const int MaxLogEntries = 300;
        private const int RequestTimeoutMs = 30000;
        private const int BuildTimeoutMs = 1800000; // 30 min for IL2CPP/Android builds

        private static readonly object QueueLock = new object();
        private static readonly Queue<PendingRequest> PendingQueue = new Queue<PendingRequest>();

        private static readonly object LogLock = new object();
        private static readonly List<ConsoleRecord> LiveLogs = new List<ConsoleRecord>();

        private static TcpListener _listener;
        private static Thread _acceptThread;
        private static volatile bool _running;
        private static int _port = DefaultPort;
        private static int _handledRequests;
        private static string _lastError = "";

        private sealed class ConsoleRecord
        {
            public string type;
            public string message;
            public string stackTrace;
            public string timestamp;
        }

        private sealed class PendingRequest
        {
            public string method;
            public string path;
            public string body;
            public string responseJson;
            public int statusCode = 200;
            public ManualResetEvent done = new ManualResetEvent(false);
        }

        static Unity2017McpBridge()
        {
            string envPort = Environment.GetEnvironmentVariable("UNITY2017_MCP_PORT");
            int parsedPort;
            if (!IsNullOrBlank(envPort) && int.TryParse(envPort, out parsedPort) && parsedPort > 0 && parsedPort < 65536)
            {
                _port = parsedPort;
            }

            Application.logMessageReceivedThreaded -= OnLogReceived;
            Application.logMessageReceivedThreaded += OnLogReceived;
            EditorApplication.update -= ProcessMainThreadQueue;
            EditorApplication.update += ProcessMainThreadQueue;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            AppDomain.CurrentDomain.DomainUnload -= OnDomainUnload;
            AppDomain.CurrentDomain.DomainUnload += OnDomainUnload;

            StartServer();
        }

        public static bool IsRunning { get { return _running; } }
        public static int Port { get { return _port; } }
        public static int HandledRequests { get { return _handledRequests; } }
        public static string LastError { get { return _lastError; } }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetHandleInformation(IntPtr hObject, uint dwMask, uint dwFlags);

        public static void StartServer()
        {
            if (_running) return;
            try
            {
                _listener = new TcpListener(IPAddress.Loopback, _port);
                _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                try { SetHandleInformation(_listener.Server.Handle, 1u, 0u); } catch { }
                _listener.Start();
                _running = true;
                _lastError = "";
                _acceptThread = new Thread(AcceptLoop);
                _acceptThread.IsBackground = true;
                _acceptThread.Name = "Unity2017McpBridgeAccept";
                _acceptThread.Start();
            }
            catch (Exception ex)
            {
                _running = false;
                _lastError = ex.Message;
            }
        }

        public static void StopServer()
        {
            _running = false;
            try
            {
                if (_listener != null)
                {
                    _listener.Stop();
                    _listener = null;
                }
            }
            catch
            {
            }

            lock (QueueLock)
            {
                while (PendingQueue.Count > 0)
                {
                    PendingRequest req = PendingQueue.Dequeue();
                    req.statusCode = 503;
                    req.responseJson = BuildErrorJson("Unity MCP bridge shutting down.");
                    req.done.Set();
                }
            }
        }

        private static void OnBeforeAssemblyReload()
        {
            StopServer();
        }

        private static void OnDomainUnload(object sender, EventArgs e)
        {
            StopServer();
        }

        private static void OnLogReceived(string condition, string stackTrace, LogType type)
        {
            lock (LogLock)
            {
                if (LiveLogs.Count >= MaxLogEntries)
                {
                    LiveLogs.RemoveAt(0);
                }
                LiveLogs.Add(new ConsoleRecord
                {
                    type = type.ToString(),
                    message = condition ?? "",
                    stackTrace = stackTrace ?? "",
                    timestamp = DateTime.UtcNow.ToString("o")
                });
            }
        }

        private static void AcceptLoop()
        {
            while (_running && _listener != null)
            {
                try
                {
                    TcpClient client = _listener.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(HandleClient, client);
                }
                catch (SocketException)
                {
                    if (!_running) break;
                }
                catch (Exception ex)
                {
                    _lastError = ex.Message;
                    if (!_running) break;
                }
            }
        }

        private static void HandleClient(object state)
        {
            TcpClient client = state as TcpClient;
            if (client == null) return;

            try
            {
                client.ReceiveTimeout = 10000;
                client.SendTimeout = 10000;
                using (NetworkStream stream = client.GetStream())
                {
                    string method;
                    string path;
                    string body;
                    if (!ReadHttpRequest(stream, out method, out path, out body))
                    {
                        WriteHttpResponse(stream, 400, BuildErrorJson("Malformed HTTP request."));
                        return;
                    }

                    PendingRequest req = new PendingRequest
                    {
                        method = method,
                        path = path,
                        body = body
                    };

                    lock (QueueLock)
                    {
                        PendingQueue.Enqueue(req);
                    }

                    int waitMs = (path == "/build" || path == "/action") ? BuildTimeoutMs : RequestTimeoutMs;
                    if (!req.done.WaitOne(waitMs))
                    {
                        WriteHttpResponse(stream, 504, BuildErrorJson("Timed out waiting for Unity Editor main thread."));
                        return;
                    }

                    WriteHttpResponse(stream, req.statusCode, req.responseJson ?? "{}");
                }
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
            }
            finally
            {
                try { client.Close(); } catch { }
            }
        }

        private static bool ReadHttpRequest(NetworkStream stream, out string method, out string path, out string body)
        {
            method = "";
            path = "/";
            body = "";

            MemoryStream headerBuf = new MemoryStream();
            byte[] single = new byte[1];
            int matchState = 0;
            while (headerBuf.Length < 65536)
            {
                int r = stream.Read(single, 0, 1);
                if (r <= 0) break;
                byte b = single[0];
                headerBuf.WriteByte(b);
                if (matchState == 0 && b == '\r') matchState = 1;
                else if (matchState == 1 && b == '\n') matchState = 2;
                else if (matchState == 2 && b == '\r') matchState = 3;
                else if (matchState == 3 && b == '\n') break;
                else matchState = (b == '\r') ? 1 : 0;
            }

            string headerText = Encoding.UTF8.GetString(headerBuf.ToArray());
            if (IsNullOrBlank(headerText)) return false;

            string[] lines = headerText.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0) return false;

            string[] parts = lines[0].Split(' ');
            if (parts.Length < 2) return false;
            method = parts[0].ToUpperInvariant();
            string rawPath = parts[1];
            int qIdx = rawPath.IndexOf('?');
            path = (qIdx >= 0) ? rawPath.Substring(0, qIdx) : rawPath;

            int contentLength = 0;
            for (int i = 1; i < lines.Length; i++)
            {
                int colon = lines[i].IndexOf(':');
                if (colon > 0)
                {
                    string hName = lines[i].Substring(0, colon).Trim();
                    string hVal = lines[i].Substring(colon + 1).Trim();
                    if (string.Equals(hName, "Content-Length", StringComparison.OrdinalIgnoreCase))
                    {
                        int.TryParse(hVal, out contentLength);
                    }
                }
            }

            if (contentLength > 0 && contentLength < 4 * 1024 * 1024)
            {
                byte[] bodyBytes = new byte[contentLength];
                int offset = 0;
                while (offset < contentLength)
                {
                    int read = stream.Read(bodyBytes, offset, contentLength - offset);
                    if (read <= 0) break;
                    offset += read;
                }
                body = Encoding.UTF8.GetString(bodyBytes, 0, offset);
            }

            return true;
        }

        private static void WriteHttpResponse(NetworkStream stream, int statusCode, string jsonBody)
        {
            byte[] payload = Encoding.UTF8.GetBytes(jsonBody ?? "{}");
            string statusText = (statusCode == 200) ? "OK" : ((statusCode == 400) ? "Bad Request" : ((statusCode == 404) ? "Not Found" : "Internal Server Error"));
            string headers = string.Format(
                CultureInfo.InvariantCulture,
                "HTTP/1.1 {0} {1}\r\nContent-Type: application/json; charset=utf-8\r\nContent-Length: {2}\r\nConnection: close\r\n\r\n",
                statusCode, statusText, payload.Length);
            byte[] headBytes = Encoding.UTF8.GetBytes(headers);
            stream.Write(headBytes, 0, headBytes.Length);
            stream.Write(payload, 0, payload.Length);
            stream.Flush();
        }

        private static void ProcessMainThreadQueue()
        {
            if (!_running)
            {
                StartServer();
            }
            while (true)
            {
                PendingRequest req = null;
                lock (QueueLock)
                {
                    if (PendingQueue.Count > 0)
                    {
                        req = PendingQueue.Dequeue();
                    }
                }
                if (req == null) break;

                try
                {
                    _handledRequests++;
                    DispatchOnMainThread(req);
                }
                catch (Exception ex)
                {
                    req.statusCode = 500;
                    req.responseJson = BuildErrorJson(ex.ToString());
                }
                finally
                {
                    req.done.Set();
                }
            }
        }

        private static void DispatchOnMainThread(PendingRequest req)
        {
            Dictionary<string, object> args = ParseJsonObject(req.body);
            if (req.path == "/health")
            {
                req.responseJson = HandleHealth();
                return;
            }
            if (req.path == "/action")
            {
                string action = GetString(args, "action", "");
                req.responseJson = ExecuteAction(action, args, out req.statusCode);
                return;
            }
            req.statusCode = 404;
            req.responseJson = BuildErrorJson("Unknown endpoint: " + req.path);
        }

        public static string ExecuteAction(string action, Dictionary<string, object> args, out int statusCode)
        {
            statusCode = 200;
            switch (action)
            {
                case "health":
                    return HandleHealth();
                case "get_scenes":
                    return HandleGetScenes();
                case "open_scene":
                    return HandleOpenScene(args, out statusCode);
                case "save_scenes":
                    EditorSceneManager.SaveOpenScenes();
                    return JsonSerialize(new Dictionary<string, object> { { "ok", true }, { "saved", true } });
                case "get_hierarchy":
                    return HandleGetHierarchy(args);
                case "get_gameobject":
                    return HandleGetGameObject(args, out statusCode);
                case "create_gameobject":
                    return HandleCreateGameObject(args, out statusCode);
                case "set_transform":
                    return HandleSetTransform(args, out statusCode);
                case "find_assets":
                    return HandleFindAssets(args);
                case "read_console":
                    return HandleReadConsole(args);
                case "clear_console":
                    return HandleClearConsole();
                case "refresh_assets":
                    AssetDatabase.Refresh();
                    return JsonSerialize(new Dictionary<string, object> { { "ok", true }, { "isCompiling", EditorApplication.isCompiling } });
                case "control_editor":
                    return HandleControlEditor(args, out statusCode);
                case "execute_menu_item":
                    return HandleExecuteMenuItem(args, out statusCode);
                case "build_player":
                    return HandleBuildPlayer(args, out statusCode);
                default:
                    statusCode = 400;
                    return BuildErrorJson("Unknown action: " + action);
            }
        }

        private static string HandleHealth()
        {
            Scene active = EditorSceneManager.GetActiveScene();
            var data = new Dictionary<string, object>
            {
                { "ok", true },
                { "unityVersion", Application.unityVersion },
                { "projectPath", Directory.GetCurrentDirectory() },
                { "activeSceneName", active.name ?? "" },
                { "activeScenePath", active.path ?? "" },
                { "isDirty", active.isDirty },
                { "isPlaying", EditorApplication.isPlaying },
                { "isPaused", EditorApplication.isPaused },
                { "isCompiling", EditorApplication.isCompiling },
                { "activeBuildTarget", EditorUserBuildSettings.activeBuildTarget.ToString() },
                { "androidBuildSystem", EditorUserBuildSettings.androidBuildSystem.ToString() },
                { "androidScriptingBackend", PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android).ToString() },
                { "androidSdkRoot", EditorPrefs.GetString("AndroidSdkRoot", "") },
                { "androidNdkRoot", EditorPrefs.GetString("AndroidNdkRoot", "") },
                { "jdkPath", EditorPrefs.GetString("JdkPath", "") },
                { "port", _port },
                { "handledRequests", _handledRequests }
            };
            return JsonSerialize(data);
        }

        private static string HandleGetScenes()
        {
            var buildScenes = new List<object>();
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            for (int i = 0; i < scenes.Length; i++)
            {
                buildScenes.Add(new Dictionary<string, object>
                {
                    { "index", i },
                    { "path", scenes[i].path },
                    { "enabled", scenes[i].enabled }
                });
            }

            var loadedScenes = new List<object>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene s = SceneManager.GetSceneAt(i);
                loadedScenes.Add(new Dictionary<string, object>
                {
                    { "name", s.name },
                    { "path", s.path },
                    { "isLoaded", s.isLoaded },
                    { "isDirty", s.isDirty }
                });
            }

            return JsonSerialize(new Dictionary<string, object>
            {
                { "ok", true },
                { "buildScenes", buildScenes },
                { "loadedScenes", loadedScenes }
            });
        }

        private static string HandleOpenScene(Dictionary<string, object> args, out int statusCode)
        {
            statusCode = 200;
            string scenePath = GetString(args, "scenePath", "");
            if (IsNullOrBlank(scenePath))
            {
                statusCode = 400;
                return BuildErrorJson("scenePath is required.");
            }
            if (!File.Exists(scenePath))
            {
                statusCode = 404;
                return BuildErrorJson("Scene file not found: " + scenePath);
            }
            bool additive = GetBool(args, "additive", false);
            Scene s = EditorSceneManager.OpenScene(scenePath, additive ? OpenSceneMode.Additive : OpenSceneMode.Single);
            return JsonSerialize(new Dictionary<string, object>
            {
                { "ok", true },
                { "sceneName", s.name },
                { "scenePath", s.path },
                { "rootCount", s.rootCount }
            });
        }

        private static string HandleGetHierarchy(Dictionary<string, object> args)
        {
            int maxDepth = GetInt(args, "maxDepth", 3);
            int maxNodes = GetInt(args, "maxNodes", 200);
            string rootPath = GetString(args, "rootPath", "");

            Scene active = EditorSceneManager.GetActiveScene();
            var nodes = new List<object>();
            int remaining = maxNodes;

            if (!IsNullOrBlank(rootPath))
            {
                GameObject target = FindGameObjectByPath(active, rootPath);
                if (target != null)
                {
                    CollectHierarchy(target.transform, 0, maxDepth, ref remaining, nodes, GetParentPath(rootPath));
                }
            }
            else
            {
                GameObject[] roots = active.GetRootGameObjects();
                for (int i = 0; i < roots.Length && remaining > 0; i++)
                {
                    CollectHierarchy(roots[i].transform, 0, maxDepth, ref remaining, nodes, "");
                }
            }

            return JsonSerialize(new Dictionary<string, object>
            {
                { "ok", true },
                { "sceneName", active.name ?? "" },
                { "scenePath", active.path ?? "" },
                { "count", nodes.Count },
                { "truncated", remaining <= 0 },
                { "nodes", nodes }
            });
        }

        private static void CollectHierarchy(Transform t, int depth, int maxDepth, ref int remaining, List<object> outNodes, string parentPath)
        {
            if (remaining <= 0) return;
            remaining--;

            string fullPath = IsNullOrBlank(parentPath) ? t.name : (parentPath + "/" + t.name);
            Component[] comps = t.GetComponents<Component>();
            var compNames = new List<object>();
            for (int i = 0; i < comps.Length; i++)
            {
                compNames.Add(comps[i] != null ? comps[i].GetType().Name : "<MissingScript>");
            }

            var childrenList = new List<object>();
            if (depth < maxDepth)
            {
                for (int i = 0; i < t.childCount && remaining > 0; i++)
                {
                    CollectHierarchy(t.GetChild(i), depth + 1, maxDepth, ref remaining, childrenList, fullPath);
                }
            }

            outNodes.Add(new Dictionary<string, object>
            {
                { "name", t.name },
                { "path", fullPath },
                { "activeSelf", t.gameObject.activeSelf },
                { "activeInHierarchy", t.gameObject.activeInHierarchy },
                { "tag", t.gameObject.tag },
                { "layer", LayerMask.LayerToName(t.gameObject.layer) },
                { "childCount", t.childCount },
                { "components", compNames },
                { "children", childrenList }
            });
        }

        private static string HandleGetGameObject(Dictionary<string, object> args, out int statusCode)
        {
            statusCode = 200;
            string objectPath = GetString(args, "path", "");
            if (IsNullOrBlank(objectPath))
            {
                statusCode = 400;
                return BuildErrorJson("path is required.");
            }

            Scene active = EditorSceneManager.GetActiveScene();
            GameObject go = FindGameObjectByPath(active, objectPath);
            if (go == null)
            {
                statusCode = 404;
                return BuildErrorJson("GameObject not found: " + objectPath);
            }

            Component[] comps = go.GetComponents<Component>();
            var compDetails = new List<object>();
            for (int i = 0; i < comps.Length; i++)
            {
                Component c = comps[i];
                if (c == null)
                {
                    compDetails.Add(new Dictionary<string, object> { { "type", "<MissingScript>" } });
                    continue;
                }

                var props = new Dictionary<string, object>();
                SerializedObject so = new SerializedObject(c);
                SerializedProperty it = so.GetIterator();
                int propCount = 0;
                if (it.NextVisible(true))
                {
                    do
                    {
                        if (propCount >= 32) break;
                        props[it.name] = ReadSerializedPropertySummary(it);
                        propCount++;
                    }
                    while (it.NextVisible(false));
                }

                compDetails.Add(new Dictionary<string, object>
                {
                    { "type", c.GetType().FullName },
                    { "properties", props }
                });
            }

            return JsonSerialize(new Dictionary<string, object>
            {
                { "ok", true },
                { "name", go.name },
                { "path", objectPath },
                { "activeSelf", go.activeSelf },
                { "activeInHierarchy", go.activeInHierarchy },
                { "tag", go.tag },
                { "layer", LayerMask.LayerToName(go.layer) },
                { "position", Vec3ToDict(go.transform.position) },
                { "localPosition", Vec3ToDict(go.transform.localPosition) },
                { "eulerAngles", Vec3ToDict(go.transform.eulerAngles) },
                { "localScale", Vec3ToDict(go.transform.localScale) },
                { "components", compDetails }
            });
        }

        private static object ReadSerializedPropertySummary(SerializedProperty p)
        {
            switch (p.propertyType)
            {
                case SerializedPropertyType.Integer: return p.intValue;
                case SerializedPropertyType.Boolean: return p.boolValue;
                case SerializedPropertyType.Float: return Math.Round(p.floatValue, 4);
                case SerializedPropertyType.String: return p.stringValue ?? "";
                case SerializedPropertyType.Enum:
                    return (p.enumValueIndex >= 0 && p.enumValueIndex < p.enumDisplayNames.Length)
                        ? p.enumDisplayNames[p.enumValueIndex]
                        : p.enumValueIndex.ToString(CultureInfo.InvariantCulture);
                case SerializedPropertyType.Vector2: return string.Format(CultureInfo.InvariantCulture, "({0:F2}, {1:F2})", p.vector2Value.x, p.vector2Value.y);
                case SerializedPropertyType.Vector3: return string.Format(CultureInfo.InvariantCulture, "({0:F2}, {1:F2}, {2:F2})", p.vector3Value.x, p.vector3Value.y, p.vector3Value.z);
                case SerializedPropertyType.ObjectReference: return p.objectReferenceValue != null ? p.objectReferenceValue.name : "null";
                default: return p.propertyType.ToString();
            }
        }

        private static string HandleCreateGameObject(Dictionary<string, object> args, out int statusCode)
        {
            statusCode = 200;
            string name = GetString(args, "name", "New GameObject");
            string parentPath = GetString(args, "parentPath", "");
            Scene active = EditorSceneManager.GetActiveScene();

            GameObject parentGo = null;
            if (!IsNullOrBlank(parentPath))
            {
                parentGo = FindGameObjectByPath(active, parentPath);
                if (parentGo == null)
                {
                    statusCode = 404;
                    return BuildErrorJson("Parent GameObject not found: " + parentPath);
                }
            }

            GameObject created = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(created, "MCP Create GameObject");
            if (parentGo != null)
            {
                created.transform.SetParent(parentGo.transform, false);
            }
            created.transform.localPosition = new Vector3(
                GetFloat(args, "x", 0f),
                GetFloat(args, "y", 0f),
                GetFloat(args, "z", 0f));
            EditorSceneManager.MarkSceneDirty(active);

            string fullPath = IsNullOrBlank(parentPath) ? created.name : (parentPath + "/" + created.name);
            return JsonSerialize(new Dictionary<string, object>
            {
                { "ok", true },
                { "name", created.name },
                { "path", fullPath },
                { "position", Vec3ToDict(created.transform.position) }
            });
        }

        private static string HandleSetTransform(Dictionary<string, object> args, out int statusCode)
        {
            statusCode = 200;
            string path = GetString(args, "path", "");
            if (IsNullOrBlank(path))
            {
                statusCode = 400;
                return BuildErrorJson("path is required.");
            }
            Scene active = EditorSceneManager.GetActiveScene();
            GameObject go = FindGameObjectByPath(active, path);
            if (go == null)
            {
                statusCode = 404;
                return BuildErrorJson("GameObject not found: " + path);
            }

            Undo.RecordObject(go.transform, "MCP Set Transform");
            if (args.ContainsKey("x") || args.ContainsKey("y") || args.ContainsKey("z"))
            {
                Vector3 pos = go.transform.localPosition;
                pos.x = GetFloat(args, "x", pos.x);
                pos.y = GetFloat(args, "y", pos.y);
                pos.z = GetFloat(args, "z", pos.z);
                go.transform.localPosition = pos;
            }
            if (args.ContainsKey("rx") || args.ContainsKey("ry") || args.ContainsKey("rz"))
            {
                Vector3 rot = go.transform.localEulerAngles;
                rot.x = GetFloat(args, "rx", rot.x);
                rot.y = GetFloat(args, "ry", rot.y);
                rot.z = GetFloat(args, "rz", rot.z);
                go.transform.localEulerAngles = rot;
            }
            if (args.ContainsKey("sx") || args.ContainsKey("sy") || args.ContainsKey("sz"))
            {
                Vector3 sc = go.transform.localScale;
                sc.x = GetFloat(args, "sx", sc.x);
                sc.y = GetFloat(args, "sy", sc.y);
                sc.z = GetFloat(args, "sz", sc.z);
                go.transform.localScale = sc;
            }
            EditorSceneManager.MarkSceneDirty(active);

            return JsonSerialize(new Dictionary<string, object>
            {
                { "ok", true },
                { "path", path },
                { "localPosition", Vec3ToDict(go.transform.localPosition) },
                { "localEulerAngles", Vec3ToDict(go.transform.localEulerAngles) },
                { "localScale", Vec3ToDict(go.transform.localScale) }
            });
        }

        private static string HandleFindAssets(Dictionary<string, object> args)
        {
            string filter = GetString(args, "filter", "");
            string folder = GetString(args, "folder", "");
            int limit = GetInt(args, "limit", 50);
            if (limit <= 0) limit = 50;

            string[] guids = IsNullOrBlank(folder)
                ? AssetDatabase.FindAssets(filter)
                : AssetDatabase.FindAssets(filter, new[] { folder });

            var results = new List<object>();
            int max = Math.Min(limit, guids.Length);
            for (int i = 0; i < max; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                results.Add(new Dictionary<string, object>
                {
                    { "guid", guids[i] },
                    { "path", assetPath }
                });
            }

            return JsonSerialize(new Dictionary<string, object>
            {
                { "ok", true },
                { "totalMatches", guids.Length },
                { "returned", results.Count },
                { "assets", results }
            });
        }

        private static string HandleReadConsole(Dictionary<string, object> args)
        {
            string filterType = GetString(args, "type", "all").ToLowerInvariant();
            int limit = GetInt(args, "limit", 50);
            if (limit <= 0) limit = 50;

            int errCount = 0;
            int warnCount = 0;
            int logCount = 0;
            var entries = new List<object>();

            Type logEntriesType = typeof(EditorApplication).Assembly.GetType("UnityEditor.LogEntries");
            if (logEntriesType != null)
            {
                MethodInfo getCounts = logEntriesType.GetMethod("GetCountsByType", BindingFlags.Public | BindingFlags.Static);
                if (getCounts != null)
                {
                    object[] countArgs = new object[] { 0, 0, 0 };
                    getCounts.Invoke(null, countArgs);
                    errCount = (int)countArgs[0];
                    warnCount = (int)countArgs[1];
                    logCount = (int)countArgs[2];
                }

                PropertyInfo consoleFlagsProp = logEntriesType.GetProperty("consoleFlags", BindingFlags.Public | BindingFlags.Static);
                MethodInfo startGetting = logEntriesType.GetMethod("StartGettingEntries", BindingFlags.Public | BindingFlags.Static);
                MethodInfo endGetting = logEntriesType.GetMethod("EndGettingEntries", BindingFlags.Public | BindingFlags.Static);
                MethodInfo getCount = logEntriesType.GetMethod("GetCount", BindingFlags.Public | BindingFlags.Static);
                MethodInfo getLines = logEntriesType.GetMethod("GetLinesAndModeFromEntryInternal", BindingFlags.Public | BindingFlags.Static);

                if (startGetting != null && endGetting != null && getCount != null && getLines != null)
                {
                    int savedFlags = 0;
                    bool hasSavedFlags = false;
                    if (consoleFlagsProp != null)
                    {
                        savedFlags = (int)consoleFlagsProp.GetValue(null, null);
                        hasSavedFlags = true;
                        // Ensure Log (128), Warning (256), and Error (512) levels are visible while reading
                        consoleFlagsProp.SetValue(null, savedFlags | 128 | 256 | 512, null);
                    }
                    startGetting.Invoke(null, null);
                    try
                    {
                        int total = (int)getCount.Invoke(null, null);
                        int start = Math.Max(0, total - Math.Max(limit * 3, 100));
                        for (int i = total - 1; i >= start && entries.Count < limit; i--)
                        {
                            object[] lineArgs = new object[] { i, 4, 0, "" };
                            getLines.Invoke(null, lineArgs);
                            int mode = (int)lineArgs[2];
                            string text = (lineArgs[3] as string) ?? "";
                            string kind = ClassifyConsoleMode(mode);
                            if (filterType != "all" && kind.ToLowerInvariant() != filterType)
                            {
                                continue;
                            }
                            entries.Add(new Dictionary<string, object>
                            {
                                { "index", i },
                                { "type", kind },
                                { "mode", mode },
                                { "message", text }
                            });
                        }
                    }
                    finally
                    {
                        endGetting.Invoke(null, null);
                        if (hasSavedFlags && consoleFlagsProp != null)
                        {
                            consoleFlagsProp.SetValue(null, savedFlags, null);
                        }
                    }
                }
            }

            var liveSnapshot = new List<object>();
            lock (LogLock)
            {
                for (int i = Math.Max(0, LiveLogs.Count - limit); i < LiveLogs.Count; i++)
                {
                    ConsoleRecord rec = LiveLogs[i];
                    if (filterType != "all" && rec.type.ToLowerInvariant() != filterType) continue;
                    liveSnapshot.Add(new Dictionary<string, object>
                    {
                        { "type", rec.type },
                        { "message", rec.message },
                        { "timestamp", rec.timestamp }
                    });
                }
            }

            return JsonSerialize(new Dictionary<string, object>
            {
                { "ok", true },
                { "counts", new Dictionary<string, object> { { "error", errCount }, { "warning", warnCount }, { "log", logCount } } },
                { "consoleEntries", entries },
                { "liveEntries", liveSnapshot }
            });
        }

        private static string ClassifyConsoleMode(int mode)
        {
            // Unity 2017 ConsoleWindow.Mode bitmasks: Error(1)|Assert(2)|Fatal(16)|AssetImportError(64)|ScriptingError(256)|ScriptCompileError(2048)|ScriptingException(131072)|GraphCompileError(1048576)|ScriptingAssertion(2097152)
            if ((mode & (1 | 2 | 16 | 64 | 256 | 2048 | 131072 | 1048576 | 2097152)) != 0)
            {
                return "Error";
            }
            // AssetImportWarning(128)|ScriptingWarning(512)|ScriptCompileWarning(4096)
            if ((mode & (128 | 512 | 4096)) != 0)
            {
                return "Warning";
            }
            return "Log";
        }

        private static string HandleClearConsole()
        {
            Type logEntriesType = typeof(EditorApplication).Assembly.GetType("UnityEditor.LogEntries");
            if (logEntriesType != null)
            {
                MethodInfo clear = logEntriesType.GetMethod("Clear", BindingFlags.Public | BindingFlags.Static);
                if (clear != null) clear.Invoke(null, null);
            }
            lock (LogLock)
            {
                LiveLogs.Clear();
            }
            return JsonSerialize(new Dictionary<string, object> { { "ok", true }, { "cleared", true } });
        }

        private static string HandleControlEditor(Dictionary<string, object> args, out int statusCode)
        {
            statusCode = 200;
            string command = GetString(args, "command", "").ToLowerInvariant();
            switch (command)
            {
                case "play":
                    EditorApplication.isPlaying = true;
                    break;
                case "stop":
                    EditorApplication.isPlaying = false;
                    break;
                case "pause":
                    EditorApplication.isPaused = true;
                    break;
                case "unpause":
                    EditorApplication.isPaused = false;
                    break;
                case "step":
                    EditorApplication.Step();
                    break;
                default:
                    statusCode = 400;
                    return BuildErrorJson("command must be one of: play, stop, pause, unpause, step.");
            }
            return JsonSerialize(new Dictionary<string, object>
            {
                { "ok", true },
                { "isPlaying", EditorApplication.isPlaying },
                { "isPaused", EditorApplication.isPaused }
            });
        }

        private static string HandleExecuteMenuItem(Dictionary<string, object> args, out int statusCode)
        {
            statusCode = 200;
            string menuPath = GetString(args, "menuPath", "");
            if (IsNullOrBlank(menuPath))
            {
                statusCode = 400;
                return BuildErrorJson("menuPath is required.");
            }
            bool executed = EditorApplication.ExecuteMenuItem(menuPath);
            return JsonSerialize(new Dictionary<string, object>
            {
                { "ok", executed },
                { "menuPath", menuPath },
                { "executed", executed }
            });
        }

        private static string HandleBuildPlayer(Dictionary<string, object> args, out int statusCode)
        {
            statusCode = 200;
            string targetStr = GetString(args, "target", "Android");
            string outputPath = GetString(args, "outputPath", "");
            BuildTarget target;
            try
            {
                target = (BuildTarget)Enum.Parse(typeof(BuildTarget), targetStr, true);
            }
            catch
            {
                statusCode = 400;
                return BuildErrorJson("Invalid BuildTarget: " + targetStr);
            }

            if (IsNullOrBlank(outputPath))
            {
                statusCode = 400;
                return BuildErrorJson("outputPath is required.");
            }

            string dir = Path.GetDirectoryName(outputPath);
            if (!IsNullOrBlank(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var scenesList = new List<string>();
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            for (int i = 0; i < scenes.Length; i++)
            {
                if (scenes[i].enabled) scenesList.Add(scenes[i].path);
            }

            if (target == BuildTarget.Android)
            {
                BuildWindow.ApplyUniversalAndroid64BitSettings();
            }

            BuildPlayerOptions opts = new BuildPlayerOptions();
            opts.scenes = scenesList.ToArray();
            opts.locationPathName = outputPath;
            opts.target = target;
            opts.options = BuildOptions.None;

            string err = BuildPipeline.BuildPlayer(opts);
            bool exists = File.Exists(outputPath) || Directory.Exists(outputPath);
            long size = File.Exists(outputPath) ? new FileInfo(outputPath).Length : 0L;

            if (!IsNullOrBlank(err))
            {
                statusCode = 500;
                return JsonSerialize(new Dictionary<string, object>
                {
                    { "ok", false },
                    { "error", err },
                    { "target", target.ToString() },
                    { "outputPath", outputPath }
                });
            }

            return JsonSerialize(new Dictionary<string, object>
            {
                { "ok", exists },
                { "target", target.ToString() },
                { "outputPath", outputPath },
                { "sizeBytes", size },
                { "sceneCount", scenesList.Count }
            });
        }

        private static GameObject FindGameObjectByPath(Scene scene, string path)
        {
            if (IsNullOrBlank(path)) return null;
            string[] segments = path.Trim('/').Split('/');
            if (segments.Length == 0) return null;

            GameObject[] roots = scene.GetRootGameObjects();
            Transform current = null;
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == segments[0])
                {
                    current = roots[i].transform;
                    break;
                }
            }
            if (current == null) return null;

            for (int s = 1; s < segments.Length; s++)
            {
                Transform next = current.Find(segments[s]);
                if (next == null) return null;
                current = next;
            }
            return current.gameObject;
        }

        private static string GetParentPath(string path)
        {
            string trimmed = (path ?? "").Trim('/');
            int idx = trimmed.LastIndexOf('/');
            return (idx > 0) ? trimmed.Substring(0, idx) : "";
        }

        private static Dictionary<string, object> Vec3ToDict(Vector3 v)
        {
            return new Dictionary<string, object>
            {
                { "x", Math.Round(v.x, 4) },
                { "y", Math.Round(v.y, 4) },
                { "z", Math.Round(v.z, 4) }
            };
        }

        private static bool IsNullOrBlank(string s)
        {
            return string.IsNullOrEmpty(s) || s.Trim().Length == 0;
        }

        private static string GetString(Dictionary<string, object> d, string key, string defVal)
        {
            object v;
            if (d != null && d.TryGetValue(key, out v) && v != null) return v.ToString();
            return defVal;
        }

        private static bool GetBool(Dictionary<string, object> d, string key, bool defVal)
        {
            object v;
            if (d != null && d.TryGetValue(key, out v) && v != null)
            {
                if (v is bool) return (bool)v;
                bool b;
                if (bool.TryParse(v.ToString(), out b)) return b;
            }
            return defVal;
        }

        private static int GetInt(Dictionary<string, object> d, string key, int defVal)
        {
            object v;
            if (d != null && d.TryGetValue(key, out v) && v != null)
            {
                if (v is int) return (int)v;
                if (v is long) return (int)(long)v;
                if (v is double) return (int)(double)v;
                int n;
                if (int.TryParse(v.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) return n;
            }
            return defVal;
        }

        private static float GetFloat(Dictionary<string, object> d, string key, float defVal)
        {
            object v;
            if (d != null && d.TryGetValue(key, out v) && v != null)
            {
                if (v is double) return (float)(double)v;
                if (v is long) return (float)(long)v;
                if (v is int) return (float)(int)v;
                float f;
                if (float.TryParse(v.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out f)) return f;
            }
            return defVal;
        }

        private static string BuildErrorJson(string msg)
        {
            return JsonSerialize(new Dictionary<string, object>
            {
                { "ok", false },
                { "error", msg ?? "Unknown error" }
            });
        }

        // --- Minimal self-contained JSON serializer/parser (C# 4 / .NET 3.5 compatible) ---

        public static string JsonSerialize(object obj)
        {
            StringBuilder sb = new StringBuilder();
            SerializeValue(obj, sb);
            return sb.ToString();
        }

        private static void SerializeValue(object value, StringBuilder sb)
        {
            if (value == null)
            {
                sb.Append("null");
                return;
            }
            if (value is string)
            {
                SerializeString((string)value, sb);
                return;
            }
            if (value is bool)
            {
                sb.Append(((bool)value) ? "true" : "false");
                return;
            }
            if (value is IDictionary<string, object>)
            {
                sb.Append('{');
                bool first = true;
                foreach (KeyValuePair<string, object> kv in (IDictionary<string, object>)value)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    SerializeString(kv.Key, sb);
                    sb.Append(':');
                    SerializeValue(kv.Value, sb);
                }
                sb.Append('}');
                return;
            }
            if (value is IList<object>)
            {
                sb.Append('[');
                IList<object> list = (IList<object>)value;
                for (int i = 0; i < list.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    SerializeValue(list[i], sb);
                }
                sb.Append(']');
                return;
            }
            if (value is int || value is long || value is float || value is double || value is decimal)
            {
                sb.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
            }
            SerializeString(value.ToString(), sb);
        }

        private static void SerializeString(string str, StringBuilder sb)
        {
            sb.Append('"');
            for (int i = 0; i < str.Length; i++)
            {
                char c = str[i];
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ')
                        {
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            sb.Append('"');
        }

        public static Dictionary<string, object> ParseJsonObject(string json)
        {
            if (IsNullOrBlank(json)) return new Dictionary<string, object>();
            int idx = 0;
            object parsed = ParseValue(json, ref idx);
            var dict = parsed as Dictionary<string, object>;
            return dict ?? new Dictionary<string, object>();
        }

        private static object ParseValue(string s, ref int i)
        {
            SkipWs(s, ref i);
            if (i >= s.Length) return null;
            char c = s[i];
            if (c == '{') return ParseObject(s, ref i);
            if (c == '[') return ParseArray(s, ref i);
            if (c == '"') return ParseString(s, ref i);
            if (c == 't' && s.IndexOf("true", i, StringComparison.Ordinal) == i) { i += 4; return true; }
            if (c == 'f' && s.IndexOf("false", i, StringComparison.Ordinal) == i) { i += 5; return false; }
            if (c == 'n' && s.IndexOf("null", i, StringComparison.Ordinal) == i) { i += 4; return null; }
            return ParseNumber(s, ref i);
        }

        private static Dictionary<string, object> ParseObject(string s, ref int i)
        {
            var dict = new Dictionary<string, object>();
            i++; // '{'
            while (i < s.Length)
            {
                SkipWs(s, ref i);
                if (i >= s.Length) break;
                if (s[i] == '}') { i++; return dict; }
                string key = ParseString(s, ref i);
                SkipWs(s, ref i);
                if (i < s.Length && s[i] == ':') i++;
                object val = ParseValue(s, ref i);
                dict[key] = val;
                SkipWs(s, ref i);
                if (i < s.Length && s[i] == ',') i++;
            }
            return dict;
        }

        private static List<object> ParseArray(string s, ref int i)
        {
            var list = new List<object>();
            i++; // '['
            while (i < s.Length)
            {
                SkipWs(s, ref i);
                if (i >= s.Length) break;
                if (s[i] == ']') { i++; return list; }
                list.Add(ParseValue(s, ref i));
                SkipWs(s, ref i);
                if (i < s.Length && s[i] == ',') i++;
            }
            return list;
        }

        private static string ParseString(string s, ref int i)
        {
            StringBuilder sb = new StringBuilder();
            if (i < s.Length && s[i] == '"') i++;
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') break;
                if (c == '\\' && i < s.Length)
                {
                    char esc = s[i++];
                    switch (esc)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (i + 4 <= s.Length)
                            {
                                int code;
                                if (int.TryParse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code))
                                {
                                    sb.Append((char)code);
                                }
                                i += 4;
                            }
                            break;
                        default: sb.Append(esc); break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        private static object ParseNumber(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && ("-0123456789.eE+".IndexOf(s[i]) >= 0)) i++;
            string tok = s.Substring(start, i - start);
            long l;
            if (tok.IndexOf('.') < 0 && long.TryParse(tok, NumberStyles.Integer, CultureInfo.InvariantCulture, out l)) return l;
            double d;
            if (double.TryParse(tok, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) return d;
            return 0;
        }

        private static void SkipWs(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }
    }

    public class Unity2017McpWindow : EditorWindow
    {
        [MenuItem("Tools/Unity 2017 MCP/Bridge Status")]
        public static void ShowWindow()
        {
            GetWindow<Unity2017McpWindow>("Unity 2017 MCP");
        }

        private void OnGUI()
        {
            GUILayout.Label("Unity 2017.4 MCP Bridge", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Status", Unity2017McpBridge.IsRunning ? "Listening (127.0.0.1:" + Unity2017McpBridge.Port + ")" : "Stopped");
            EditorGUILayout.LabelField("Handled Requests", Unity2017McpBridge.HandledRequests.ToString(CultureInfo.InvariantCulture));
            if (!string.IsNullOrEmpty(Unity2017McpBridge.LastError))
            {
                EditorGUILayout.HelpBox("Last Error: " + Unity2017McpBridge.LastError, MessageType.Warning);
            }
            EditorGUILayout.Space();
            if (GUILayout.Button("Restart Bridge"))
            {
                Unity2017McpBridge.StopServer();
                Unity2017McpBridge.StartServer();
            }
        }
    }
}
