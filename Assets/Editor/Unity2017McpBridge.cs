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
                case "capture_screenshot":
                    return HandleCaptureScreenshot(args, out statusCode);
                case "instantiate_prefab":
                    return HandleInstantiatePrefab(args, out statusCode);
                case "inspect_bounds":
                    return HandleInspectBounds(args, out statusCode);
                case "sample_animation":
                    return HandleSampleAnimation(args, out statusCode);
                case "create_prefab":
                    return HandleCreatePrefab(args, out statusCode);
                case "destroy_object":
                    return HandleDestroyObject(args, out statusCode);
                case "set_material":
                    return HandleSetMaterial(args, out statusCode);
                case "play_spawn_character":
                    return HandlePlaySpawnCharacter(args, out statusCode);
                case "play_get_characters":
                    return HandlePlayGetCharacters(args, out statusCode);
                case "dump_skeleton":
                    return HandleDumpSkeleton(args, out statusCode);
                case "build_legacy_anim":
                    return HandleBuildLegacyAnim(args, out statusCode);
                case "register_character_clips":
                    return HandleRegisterCharacterClips(args, out statusCode);
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

        private static string HandleCaptureScreenshot(Dictionary<string, object> args, out int statusCode)
        {
            statusCode = 200;
            string mode = GetString(args, "mode", "camera").ToLowerInvariant();
            string outputPath = GetString(args, "outputPath", "");
            int width = Mathf.Clamp(GetInt(args, "width", 1280), 64, 4096);
            int height = Mathf.Clamp(GetInt(args, "height", 720), 64, 4096);
            string cameraPath = GetString(args, "cameraPath", "");

            if (IsNullOrBlank(outputPath))
            {
                outputPath = Path.Combine(Directory.GetCurrentDirectory(), "Temp/unity2017_capture.png");
            }
            if (!Path.IsPathRooted(outputPath))
            {
                outputPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), outputPath));
            }
            string dir = Path.GetDirectoryName(outputPath);
            if (!IsNullOrBlank(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            if (mode == "screen")
            {
                ScreenCapture.CaptureScreenshot(outputPath);
                return JsonSerialize(new Dictionary<string, object>
                {
                    { "ok", true },
                    { "mode", "screen" },
                    { "outputPath", outputPath },
                    { "isPlaying", EditorApplication.isPlaying },
                    { "note", "ScreenCapture queued; written at end of next rendered frame." }
                });
            }

            Camera cam = null;
            if (mode == "scene")
            {
                SceneView sv = SceneView.lastActiveSceneView;
                if (sv != null) cam = sv.camera;
                if (cam == null && SceneView.sceneViews != null && SceneView.sceneViews.Count > 0)
                {
                    SceneView first = SceneView.sceneViews[0] as SceneView;
                    if (first != null) cam = first.camera;
                }
            }
            else
            {
                if (!IsNullOrBlank(cameraPath))
                {
                    GameObject go = FindGameObjectByPath(EditorSceneManager.GetActiveScene(), cameraPath);
                    if (go != null) cam = go.GetComponent<Camera>();
                }
                if (cam == null) cam = Camera.main;
                if (cam == null)
                {
                    Camera[] cams = UnityEngine.Object.FindObjectsOfType<Camera>();
                    for (int i = 0; i < cams.Length; i++)
                    {
                        if (cams[i] != null && cams[i].enabled) { cam = cams[i]; break; }
                    }
                    if (cam == null && cams.Length > 0) cam = cams[0];
                }
            }

            if (cam == null)
            {
                statusCode = 404;
                return BuildErrorJson("No suitable Camera found for screenshot mode '" + mode + "'.");
            }

            RenderTexture rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture prevCamTarget = cam.targetTexture;
            RenderTexture prevActive = RenderTexture.active;
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                byte[] pngBytes = tex.EncodeToPNG();
                File.WriteAllBytes(outputPath, pngBytes);
                return JsonSerialize(new Dictionary<string, object>
                {
                    { "ok", true },
                    { "mode", mode },
                    { "camera", cam.name },
                    { "width", width },
                    { "height", height },
                    { "outputPath", outputPath },
                    { "sizeBytes", pngBytes.Length }
                });
            }
            finally
            {
                cam.targetTexture = prevCamTarget;
                RenderTexture.active = prevActive;
                UnityEngine.Object.DestroyImmediate(tex);
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        private static string HandleInstantiatePrefab(Dictionary<string, object> args, out int statusCode)
        {
            statusCode = 200;
            string assetPath = GetString(args, "assetPath", "");
            if (IsNullOrBlank(assetPath))
            {
                statusCode = 400;
                return BuildErrorJson("assetPath is required.");
            }
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
            {
                statusCode = 404;
                return BuildErrorJson("Prefab/Model asset not found at: " + assetPath);
            }

            GameObject inst = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (inst == null)
            {
                inst = UnityEngine.Object.Instantiate(prefab) as GameObject;
            }
            if (inst == null)
            {
                statusCode = 500;
                return BuildErrorJson("Failed to instantiate asset: " + assetPath);
            }

            string customName = GetString(args, "name", "");
            if (!IsNullOrBlank(customName)) inst.name = customName;

            string parentPath = GetString(args, "parentPath", "");
            if (!IsNullOrBlank(parentPath))
            {
                GameObject parentGo = FindGameObjectByPath(EditorSceneManager.GetActiveScene(), parentPath);
                if (parentGo == null)
                {
                    UnityEngine.Object.DestroyImmediate(inst);
                    statusCode = 404;
                    return BuildErrorJson("parentPath not found: " + parentPath);
                }
                inst.transform.SetParent(parentGo.transform, false);
            }

            Vector3 pos = inst.transform.localPosition;
            Vector3 rot = inst.transform.localEulerAngles;
            Vector3 scl = inst.transform.localScale;
            if (args.ContainsKey("x")) pos.x = GetFloat(args, "x", pos.x);
            if (args.ContainsKey("y")) pos.y = GetFloat(args, "y", pos.y);
            if (args.ContainsKey("z")) pos.z = GetFloat(args, "z", pos.z);
            if (args.ContainsKey("rx")) rot.x = GetFloat(args, "rx", rot.x);
            if (args.ContainsKey("ry")) rot.y = GetFloat(args, "ry", rot.y);
            if (args.ContainsKey("rz")) rot.z = GetFloat(args, "rz", rot.z);
            if (args.ContainsKey("sx")) scl.x = GetFloat(args, "sx", scl.x);
            if (args.ContainsKey("sy")) scl.y = GetFloat(args, "sy", scl.y);
            if (args.ContainsKey("sz")) scl.z = GetFloat(args, "sz", scl.z);
            inst.transform.localPosition = pos;
            inst.transform.localEulerAngles = rot;
            inst.transform.localScale = scl;

            Undo.RegisterCreatedObjectUndo(inst, "MCP Instantiate Prefab");
            return JsonSerialize(new Dictionary<string, object>
            {
                { "ok", true },
                { "name", inst.name },
                { "assetPath", assetPath },
                { "localPosition", Vec3ToDict(inst.transform.localPosition) },
                { "worldPosition", Vec3ToDict(inst.transform.position) }
            });
        }

        private static string HandleInspectBounds(Dictionary<string, object> args, out int statusCode)
        {
            statusCode = 200;
            string goPath = GetString(args, "path", "");
            string assetPath = GetString(args, "assetPath", "");
            GameObject targetGo = null;
            bool tempSpawned = false;

            if (!IsNullOrBlank(goPath))
            {
                targetGo = FindGameObjectByPath(EditorSceneManager.GetActiveScene(), goPath);
            }
            else if (!IsNullOrBlank(assetPath))
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (prefab != null)
                {
                    targetGo = UnityEngine.Object.Instantiate(prefab) as GameObject;
                    tempSpawned = true;
                }
            }

            if (targetGo == null)
            {
                statusCode = 404;
                return BuildErrorJson("Target GameObject or asset not found.");
            }

            try
            {
                Renderer[] renderers = targetGo.GetComponentsInChildren<Renderer>(true);
                MeshFilter[] filters = targetGo.GetComponentsInChildren<MeshFilter>(true);
                Bounds worldBounds = new Bounds(targetGo.transform.position, Vector3.zero);
                bool hasWorld = false;
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] == null) continue;
                    if (!hasWorld) { worldBounds = renderers[i].bounds; hasWorld = true; }
                    else worldBounds.Encapsulate(renderers[i].bounds);
                }

                var meshInfos = new List<object>();
                for (int i = 0; i < filters.Length; i++)
                {
                    if (filters[i] == null || filters[i].sharedMesh == null) continue;
                    Bounds lb = filters[i].sharedMesh.bounds;
                    meshInfos.Add(new Dictionary<string, object>
                    {
                        { "object", filters[i].gameObject.name },
                        { "localPos", Vec3ToDict(filters[i].transform.localPosition) },
                        { "meshMin", Vec3ToDict(lb.min) },
                        { "meshMax", Vec3ToDict(lb.max) },
                        { "meshSize", Vec3ToDict(lb.size) }
                    });
                }

                return JsonSerialize(new Dictionary<string, object>
                {
                    { "ok", true },
                    { "name", targetGo.name },
                    { "rendererCount", renderers.Length },
                    { "worldCenter", Vec3ToDict(worldBounds.center) },
                    { "worldSize", Vec3ToDict(worldBounds.size) },
                    { "worldMin", Vec3ToDict(worldBounds.min) },
                    { "worldMax", Vec3ToDict(worldBounds.max) },
                    { "meshes", meshInfos }
                });
            }
            finally
            {
                if (tempSpawned && targetGo != null)
                {
                    UnityEngine.Object.DestroyImmediate(targetGo);
                }
            }
        }

        private static string HandleSampleAnimation(Dictionary<string, object> args, out int statusCode)
        {
            statusCode = 200;
            string goPath = GetString(args, "path", "");
            string clipName = GetString(args, "clipName", "");
            float normalizedTime = Mathf.Clamp01(GetFloat(args, "normalizedTime", 0.5f));
            GameObject go = FindGameObjectByPath(EditorSceneManager.GetActiveScene(), goPath);
            if (go == null)
            {
                statusCode = 404;
                return BuildErrorJson("GameObject not found: " + goPath);
            }

            Animation[] anims = go.GetComponentsInChildren<Animation>(true);
            var available = new List<string>();
            AnimationState matchedState = null;
            Animation matchedAnim = null;

            for (int i = 0; i < anims.Length; i++)
            {
                Animation anim = anims[i];
                if (anim == null) continue;
                foreach (AnimationState st in anim)
                {
                    if (st == null || st.clip == null) continue;
                    available.Add(anim.gameObject.name + ":" + st.name + " (" + st.length.ToString("F2", CultureInfo.InvariantCulture) + "s)");
                    if (!IsNullOrBlank(clipName) && (string.Equals(st.name, clipName, StringComparison.OrdinalIgnoreCase) || st.name.ToLowerInvariant().Contains(clipName.ToLowerInvariant())))
                    {
                        matchedState = st;
                        matchedAnim = anim;
                    }
                }
            }

            if (matchedState != null && matchedAnim != null)
            {
                matchedState.clip.SampleAnimation(matchedAnim.gameObject, normalizedTime * matchedState.length);
                SkinnedMeshRenderer[] smrs = matchedAnim.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                for (int i = 0; i < smrs.Length; i++)
                {
                    if (smrs[i] != null)
                    {
                        smrs[i].updateWhenOffscreen = true;
                        smrs[i].enabled = false;
                        smrs[i].enabled = true;
                    }
                }
                return JsonSerialize(new Dictionary<string, object>
                {
                    { "ok", true },
                    { "sampledClip", matchedState.name },
                    { "onObject", matchedAnim.gameObject.name },
                    { "timeSec", Math.Round(normalizedTime * matchedState.length, 3) },
                    { "availableClips", available }
                });
            }

            return JsonSerialize(new Dictionary<string, object>
            {
                { "ok", IsNullOrBlank(clipName) },
                { "note", IsNullOrBlank(clipName) ? "Listed available animation clips." : "Clip not found: " + clipName },
                { "availableClips", available }
            });
        }

        private static string HandleCreatePrefab(Dictionary<string, object> args, out int statusCode)
        {
            statusCode = 200;
            string goPath = GetString(args, "path", "");
            string assetPath = GetString(args, "assetPath", "");
            if (IsNullOrBlank(goPath) || IsNullOrBlank(assetPath))
            {
                statusCode = 400;
                return BuildErrorJson("path and assetPath are required.");
            }
            GameObject go = FindGameObjectByPath(EditorSceneManager.GetActiveScene(), goPath);
            if (go == null)
            {
                statusCode = 404;
                return BuildErrorJson("GameObject not found: " + goPath);
            }
            string dir = Path.GetDirectoryName(assetPath);
            if (!IsNullOrBlank(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            UnityEngine.Object existing = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            GameObject saved;
            if (existing != null)
            {
                saved = PrefabUtility.ReplacePrefab(go, existing, ReplacePrefabOptions.ReplaceNameBased);
            }
            else
            {
                saved = PrefabUtility.CreatePrefab(assetPath, go, ReplacePrefabOptions.ConnectToPrefab);
            }
            AssetDatabase.SaveAssets();
            return JsonSerialize(new Dictionary<string, object>
            {
                { "ok", saved != null },
                { "assetPath", assetPath },
                { "prefabName", saved != null ? saved.name : "" }
            });
        }

        private static string HandleDestroyObject(Dictionary<string, object> args, out int statusCode)
        {
            statusCode = 200;
            string goPath = GetString(args, "path", "");
            GameObject go = FindGameObjectByPath(EditorSceneManager.GetActiveScene(), goPath);
            if (go == null)
            {
                statusCode = 404;
                return BuildErrorJson("GameObject not found: " + goPath);
            }
            Undo.DestroyObjectImmediate(go);
            return JsonSerialize(new Dictionary<string, object> { { "ok", true }, { "destroyed", goPath } });
        }

        private static string HandleSetMaterial(Dictionary<string, object> args, out int statusCode)
        {
            statusCode = 200;
            string goPath = GetString(args, "path", "");
            string shaderName = GetString(args, "shader", "Standard");
            string matAssetPath = GetString(args, "materialAssetPath", "");
            string texPath = GetString(args, "texturePath", "");
            string hexColor = GetString(args, "color", "");
            string hexEmission = GetString(args, "emission", "");
            float metallic = GetFloat(args, "metallic", -1f);
            float gloss = GetFloat(args, "glossiness", -1f);
            bool includeChildren = GetBool(args, "includeChildren", false);

            GameObject go = FindGameObjectByPath(EditorSceneManager.GetActiveScene(), goPath);
            if (go == null)
            {
                statusCode = 404;
                return BuildErrorJson("GameObject not found: " + goPath);
            }

            Material mat = null;
            if (!IsNullOrBlank(matAssetPath))
            {
                mat = AssetDatabase.LoadAssetAtPath<Material>(matAssetPath);
            }
            if (mat == null)
            {
                Shader sh = Shader.Find(shaderName);
                if (sh == null) sh = Shader.Find("Standard");
                mat = new Material(sh);
                if (!IsNullOrBlank(matAssetPath))
                {
                    string dir = Path.GetDirectoryName(matAssetPath);
                    if (!IsNullOrBlank(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    AssetDatabase.CreateAsset(mat, matAssetPath);
                }
            }

            Color col;
            if (!IsNullOrBlank(hexColor) && ColorUtility.TryParseHtmlString(hexColor, out col))
            {
                mat.color = col;
            }
            if (!IsNullOrBlank(texPath))
            {
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                if (tex != null) mat.mainTexture = tex;
            }
            if (metallic >= 0f && mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (gloss >= 0f && mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", gloss);
            if (!IsNullOrBlank(hexEmission) && ColorUtility.TryParseHtmlString(hexEmission, out col))
            {
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", col);
                }
            }
            EditorUtility.SetDirty(mat);

            Renderer[] renderers = includeChildren ? go.GetComponentsInChildren<Renderer>(true) : new Renderer[] { go.GetComponent<Renderer>() };
            int assigned = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].sharedMaterial = mat;
                    assigned++;
                }
            }
            return JsonSerialize(new Dictionary<string, object>
            {
                { "ok", assigned > 0 },
                { "assignedRenderers", assigned },
                { "materialAssetPath", matAssetPath }
            });
        }

        private static string HandlePlaySpawnCharacter(Dictionary<string, object> args, out int statusCode)
        {
            statusCode = 200;
            if (!EditorApplication.isPlaying)
            {
                statusCode = 400;
                return BuildErrorJson("Editor must be in Play Mode.");
            }
            WaveManager wm = WeakGlobalInstance<WaveManager>.instance;
            InGameImpl ig = WeakGlobalSceneBehavior<InGameImpl>.instance;
            if (wm == null || ig == null)
            {
                statusCode = 400;
                return BuildErrorJson("WaveManager or InGameImpl is not active in the current scene.");
            }
            if (GetBool(args, "unpause", true))
            {
                Application.runInBackground = true;
                TerasuPlaytestMenu.ClearBlockingPauseDialog();
                Time.timeScale = 1f;
            }

            string enemyId = GetString(args, "enemyId", "Shogun");
            bool isAlly = GetBool(args, "isAlly", false);
            float zOffsetFromHero = GetFloat(args, "zOffsetFromHero", 350f);
            bool hasExplicitZ = args != null && args.ContainsKey("z");

            Vector3 spawnPos = (ig.enemiesSpawnArea != null) ? ig.enemiesSpawnArea.transform.position : Vector3.zero;
            if (ig.hero != null)
            {
                spawnPos = ig.hero.position;
                spawnPos.z += zOffsetFromHero;
            }
            if (hasExplicitZ)
            {
                spawnPos.z = GetFloat(args, "z", spawnPos.z);
            }

            Character spawned = wm.ConstructEnemyAt(enemyId, spawnPos, isAlly);
            if (spawned == null && WeakGlobalInstance<Smithy>.instance != null)
            {
                spawned = WeakGlobalInstance<Smithy>.instance.ForceSpawnAt(enemyId, spawnPos, !isAlly);
            }
            if (spawned == null)
            {
                statusCode = 404;
                return BuildErrorJson("Failed to construct character: " + enemyId);
            }

            string weaponChild = "";
            if (spawned.controlledObject != null)
            {
                Transform[] trs = spawned.controlledObject.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < trs.Length; i++)
                {
                    if (trs[i].name.IndexOf("Spear", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        trs[i].name.IndexOf("Sword", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        trs[i].name.IndexOf("Yari", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        weaponChild = trs[i].name;
                        break;
                    }
                }
            }

            return JsonSerialize(new Dictionary<string, object>
            {
                { "ok", true },
                { "enemyId", enemyId },
                { "isAlly", isAlly },
                { "health", spawned.health },
                { "maxHealth", spawned.maxHealth },
                { "meleeDamage", spawned.meleeDamage },
                { "meleeRange", spawned.meleeAttackRange },
                { "position", Vec3ToDict(spawned.position) },
                { "meleeWeaponPrefab", spawned.meleeWeaponPrefab != null ? spawned.meleeWeaponPrefab.name : "" },
                { "equippedWeaponChild", weaponChild },
                { "objectName", spawned.controlledObject != null ? spawned.controlledObject.name : "" }
            });
        }

        private static string HandlePlayGetCharacters(Dictionary<string, object> args, out int statusCode)
        {
            statusCode = 200;
            if (!EditorApplication.isPlaying)
            {
                statusCode = 400;
                return BuildErrorJson("Editor must be in Play Mode.");
            }
            CharactersManager cm = WeakGlobalInstance<CharactersManager>.instance;
            if (cm == null)
            {
                statusCode = 400;
                return BuildErrorJson("CharactersManager is not active.");
            }
            if (GetBool(args, "unpause", false))
            {
                Application.runInBackground = true;
                TerasuPlaytestMenu.ClearBlockingPauseDialog();
                Time.timeScale = 1f;
            }

            var list = new List<object>();
            List<Character> alive = cm.allAlive;
            for (int i = 0; i < alive.Count; i++)
            {
                Character c = alive[i];
                if (c == null) continue;
                string animName = "";
                if (c.controller != null)
                {
                    animName = c.controller.currentAnimation ?? "";
                }
                string weaponChild = "";
                var playingClips = new List<object>();
                if (c.controlledObject != null)
                {
                    Transform[] trs = c.controlledObject.GetComponentsInChildren<Transform>(true);
                    for (int k = 0; k < trs.Length; k++)
                    {
                        if (trs[k].name.IndexOf("PFZombieShogunSpear", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            trs[k].name.IndexOf("PFUndeadShogunSword", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            weaponChild = trs[k].name;
                            break;
                        }
                    }
                    TaggedAnimPlayer tap = (c.controller != null && c.controller.animPlayer != null)
                        ? c.controller.animPlayer
                        : c.controlledObject.GetComponentInChildren<TaggedAnimPlayer>();
                    if (tap != null)
                    {
                        AnimationState js = tap.currJointAnimState;
                        if (js != null && js.clip != null)
                        {
                            playingClips.Add(js.clip.name + "@" + js.normalizedTime.ToString("F2"));
                        }
                        string tc = tap.currAnim;
                        if (!string.IsNullOrEmpty(tc))
                        {
                            float nt = (tap.currAnimState != null) ? tap.currAnimState.normalizedTime : 0f;
                            playingClips.Add("tag:" + tc + "@" + nt.ToString("F2"));
                        }
                    }
                    Animation[] anims = c.controlledObject.GetComponentsInChildren<Animation>(true);
                    for (int k = 0; k < anims.Length; k++)
                    {
                        if (anims[k] == null) continue;
                        foreach (AnimationState st in anims[k])
                        {
                            if (st != null && st.enabled)
                            {
                                playingClips.Add(st.name + "@" + st.normalizedTime.ToString("F2"));
                            }
                        }
                    }
                }
                list.Add(new Dictionary<string, object>
                {
                    { "uniqueID", c.uniqueID ?? "" },
                    { "type", c.GetType().Name },
                    { "isEnemy", c.isEnemy },
                    { "health", Math.Round(c.health, 2) },
                    { "maxHealth", Math.Round(c.maxHealth, 2) },
                    { "position", Vec3ToDict(c.position) },
                    { "currentAnimation", animName },
                    { "playingClips", playingClips },
                    { "meleeWeaponPrefab", c.meleeWeaponPrefab != null ? c.meleeWeaponPrefab.name : "" },
                    { "equippedWeaponChild", weaponChild },
                    { "objectName", c.controlledObject != null ? c.controlledObject.name : "" }
                });
            }

            return JsonSerialize(new Dictionary<string, object>
            {
                { "ok", true },
                { "count", list.Count },
                { "characters", list }
            });
        }

        private static string GetRelPath(Transform root, Transform node)
        {
            if (node == null || node == root) return "";
            var parts = new List<string>();
            Transform cur = node;
            while (cur != null && cur != root)
            {
                parts.Add(cur.name);
                cur = cur.parent;
            }
            parts.Reverse();
            return string.Join("/", parts.ToArray());
        }

        private static string HandleDumpSkeleton(Dictionary<string, object> args, out int statusCode)
        {
            statusCode = 200;
            string assetPath = GetString(args, "assetPath", "");
            string clipName = GetString(args, "clipName", "idle");
            float normalizedTime = Mathf.Clamp01(GetFloat(args, "normalizedTime", 0f));
            string objExportPath = GetString(args, "objExportPath", "");
            string jsonOutPath = GetString(args, "jsonOutPath", "");

            if (IsNullOrBlank(assetPath))
            {
                statusCode = 400;
                return BuildErrorJson("assetPath is required.");
            }
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
            {
                statusCode = 404;
                return BuildErrorJson("Prefab not found: " + assetPath);
            }

            GameObject tempGo = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                tempGo.transform.position = Vector3.zero;
                tempGo.transform.rotation = Quaternion.identity;
                tempGo.transform.localScale = Vector3.one;

                Animation jointAnim = null;
                TaggedAnimPlayer tap = tempGo.GetComponent<TaggedAnimPlayer>();
                if (tap != null && tap.jointAnimation != null)
                {
                    foreach (AnimationState st in tap.jointAnimation)
                    {
                        if (st != null && st.clip != null && string.Equals(st.name, clipName, StringComparison.OrdinalIgnoreCase))
                        {
                            jointAnim = tap.jointAnimation;
                            st.clip.SampleAnimation(jointAnim.gameObject, normalizedTime * st.length);
                            break;
                        }
                    }
                }
                if (jointAnim == null)
                {
                    Animation[] anims = tempGo.GetComponentsInChildren<Animation>(true);
                    for (int i = anims.Length - 1; i >= 0; i--)
                    {
                        foreach (AnimationState st in anims[i])
                        {
                            if (st != null && st.clip != null && string.Equals(st.name, clipName, StringComparison.OrdinalIgnoreCase))
                            {
                                jointAnim = anims[i];
                                st.clip.SampleAnimation(anims[i].gameObject, normalizedTime * st.length);
                                break;
                            }
                        }
                        if (jointAnim != null) break;
                    }
                }

                Transform animRoot = (jointAnim != null) ? jointAnim.transform : tempGo.transform;
                Transform[] all = tempGo.GetComponentsInChildren<Transform>(true);
                var bones = new List<object>();
                for (int i = 0; i < all.Length; i++)
                {
                    Transform t = all[i];
                    string animPath = GetRelPath(animRoot, t);
                    string parentAnimPath = (t.parent != null) ? GetRelPath(animRoot, t.parent) : "";
                    bones.Add(new Dictionary<string, object>
                    {
                        { "name", t.name },
                        { "animPath", animPath },
                        { "parentAnimPath", parentAnimPath },
                        { "localPosition", Vec3ToDict(t.localPosition) },
                        { "localRotation", new Dictionary<string, object>
                            {
                                { "x", Math.Round(t.localRotation.x, 6) },
                                { "y", Math.Round(t.localRotation.y, 6) },
                                { "z", Math.Round(t.localRotation.z, 6) },
                                { "w", Math.Round(t.localRotation.w, 6) }
                            }
                        },
                        { "worldPosition", Vec3ToDict(t.position) },
                        { "worldRight", Vec3ToDict(t.right) },
                        { "worldUp", Vec3ToDict(t.up) },
                        { "worldForward", Vec3ToDict(t.forward) }
                    });
                }

                bool wroteObj = false;
                if (!IsNullOrBlank(objExportPath))
                {
                    StringBuilder sbObj = new StringBuilder();
                    int vOffset = 1;
                    SkinnedMeshRenderer[] smrs = tempGo.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                    for (int s = 0; s < smrs.Length; s++)
                    {
                        Mesh baked = new Mesh();
                        smrs[s].BakeMesh(baked);
                        // In Unity, SkinnedMeshRenderer.BakeMesh already outputs vertices scaled by transform.lossyScale.
                        Matrix4x4 mat = Matrix4x4.TRS(smrs[s].transform.position, smrs[s].transform.rotation, Vector3.one);
                        Vector3[] verts = baked.vertices;
                        sbObj.AppendLine("o " + smrs[s].gameObject.name);
                        for (int vi = 0; vi < verts.Length; vi++)
                        {
                            Vector3 wp = mat.MultiplyPoint3x4(verts[vi]);
                            sbObj.AppendFormat(CultureInfo.InvariantCulture, "v {0:F5} {1:F5} {2:F5}\n", wp.x, wp.y, wp.z);
                        }
                        int[] tris = baked.triangles;
                        for (int ti = 0; ti + 2 < tris.Length; ti += 3)
                        {
                            sbObj.AppendFormat(CultureInfo.InvariantCulture, "f {0} {1} {2}\n", tris[ti] + vOffset, tris[ti + 1] + vOffset, tris[ti + 2] + vOffset);
                        }
                        vOffset += verts.Length;
                        UnityEngine.Object.DestroyImmediate(baked);
                    }
                    string dir = Path.GetDirectoryName(objExportPath);
                    if (!IsNullOrBlank(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    File.WriteAllText(objExportPath, sbObj.ToString(), Encoding.UTF8);
                    wroteObj = true;
                }

                var resultDict = new Dictionary<string, object>
                {
                    { "ok", true },
                    { "assetPath", assetPath },
                    { "animRootName", animRoot.name },
                    { "sampledClip", clipName },
                    { "boneCount", bones.Count },
                    { "objExported", wroteObj },
                    { "bones", bones }
                };
                string jsonStr = JsonSerialize(resultDict);
                if (!IsNullOrBlank(jsonOutPath))
                {
                    string jdir = Path.GetDirectoryName(jsonOutPath);
                    if (!IsNullOrBlank(jdir) && !Directory.Exists(jdir)) Directory.CreateDirectory(jdir);
                    File.WriteAllText(jsonOutPath, jsonStr, Encoding.UTF8);
                    return JsonSerialize(new Dictionary<string, object>
                    {
                        { "ok", true },
                        { "boneCount", bones.Count },
                        { "objExported", wroteObj },
                        { "jsonOutPath", jsonOutPath }
                    });
                }
                return jsonStr;
            }
            finally
            {
                if (tempGo != null) UnityEngine.Object.DestroyImmediate(tempGo);
            }
        }

        private static string HandleBuildLegacyAnim(Dictionary<string, object> args, out int statusCode)
        {
            statusCode = 200;
            string jsonPath = GetString(args, "jsonPath", "");
            if (IsNullOrBlank(jsonPath) || !File.Exists(jsonPath))
            {
                statusCode = 400;
                return BuildErrorJson("Existing jsonPath is required.");
            }
            Dictionary<string, object> spec = ParseJsonObject(File.ReadAllText(jsonPath, Encoding.UTF8));
            string clipName = GetString(spec, "clipName", "");
            string outAnimPath = GetString(spec, "outAnimPath", "");
            float fps = GetFloat(spec, "fps", 30f);
            float duration = GetFloat(spec, "duration", 1.5f);

            if (IsNullOrBlank(clipName) || IsNullOrBlank(outAnimPath))
            {
                statusCode = 400;
                return BuildErrorJson("spec.clipName and spec.outAnimPath are required.");
            }

            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(outAnimPath);
            bool isNew = (clip == null);
            if (isNew)
            {
                clip = new AnimationClip();
            }
            else
            {
                clip.ClearCurves();
            }
            clip.name = clipName;
            clip.legacy = true;
            clip.frameRate = fps;
            clip.wrapMode = WrapMode.Once;

            object tracksObj;
            int curveCount = 0;
            if (spec.TryGetValue("tracks", out tracksObj) && tracksObj is IList<object>)
            {
                IList<object> tracks = (IList<object>)tracksObj;
                for (int i = 0; i < tracks.Count; i++)
                {
                    Dictionary<string, object> tr = tracks[i] as Dictionary<string, object>;
                    if (tr == null) continue;
                    string path = GetString(tr, "path", "");
                    string prop = GetString(tr, "property", "");
                    IList<object> times = tr["times"] as IList<object>;
                    IList<object> vals = tr["values"] as IList<object>;
                    if (IsNullOrBlank(prop) || times == null || vals == null || times.Count != vals.Count) continue;
                    Keyframe[] kfs = new Keyframe[times.Count];
                    for (int k = 0; k < times.Count; k++)
                    {
                        kfs[k] = new Keyframe(Convert.ToSingle(times[k], CultureInfo.InvariantCulture), Convert.ToSingle(vals[k], CultureInfo.InvariantCulture));
                    }
                    AnimationCurve curve = new AnimationCurve(kfs);
                    for (int k = 0; k < kfs.Length; k++)
                    {
                        AnimationUtility.SetKeyLeftTangentMode(curve, k, AnimationUtility.TangentMode.Auto);
                        AnimationUtility.SetKeyRightTangentMode(curve, k, AnimationUtility.TangentMode.Auto);
                    }
                    clip.SetCurve(path, typeof(Transform), prop, curve);
                    curveCount++;
                }
            }
            clip.EnsureQuaternionContinuity();

            string dir = Path.GetDirectoryName(outAnimPath);
            if (!IsNullOrBlank(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            if (isNew)
            {
                AssetDatabase.CreateAsset(clip, outAnimPath);
            }
            else
            {
                EditorUtility.SetDirty(clip);
            }

            string outTagPath = "";
            object tagObj;
            if (spec.TryGetValue("tagClip", out tagObj) && tagObj is Dictionary<string, object>)
            {
                Dictionary<string, object> tagSpec = (Dictionary<string, object>)tagObj;
                outTagPath = GetString(tagSpec, "outTagPath", "");
                if (!IsNullOrBlank(outTagPath))
                {
                    AnimationClip tClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(outTagPath);
                    bool tNew = (tClip == null);
                    if (tNew) tClip = new AnimationClip();
                    else tClip.ClearCurves();

                    tClip.name = clipName;
                    tClip.legacy = true;
                    tClip.frameRate = fps;
                    tClip.wrapMode = WrapMode.Once;

                    AnimationCurve frameCurve = new AnimationCurve(
                        new Keyframe(0f, 0f, 0f, fps),
                        new Keyframe(duration, duration * fps, fps, 0f)
                    );
                    AnimationUtility.SetEditorCurve(tClip, EditorCurveBinding.FloatCurve("", typeof(TaggedAnimPlayer), "animFrame"), frameCurve);

                    float atkTime = GetFloat(tagSpec, "attackActiveTime", duration * 0.55f);
                    Keyframe k0 = new Keyframe(0f, 0f);
                    Keyframe k1 = new Keyframe(atkTime, 1f);
                    AnimationCurve atkCurve = new AnimationCurve(k0, k1);
                    AnimationUtility.SetKeyRightTangentMode(atkCurve, 0, AnimationUtility.TangentMode.Constant);
                    AnimationUtility.SetKeyLeftTangentMode(atkCurve, 1, AnimationUtility.TangentMode.Constant);
                    AnimationUtility.SetEditorCurve(tClip, EditorCurveBinding.FloatCurve("", typeof(CharacterModelController), "attackActive"), atkCurve);

                    var animEvents = new List<AnimationEvent>();
                    object evsObj;
                    if (tagSpec.TryGetValue("events", out evsObj) && evsObj is IList<object>)
                    {
                        IList<object> evList = (IList<object>)evsObj;
                        for (int ei = 0; ei < evList.Count; ei++)
                        {
                            Dictionary<string, object> ed = evList[ei] as Dictionary<string, object>;
                            if (ed == null) continue;
                            AnimationEvent ae = new AnimationEvent();
                            ae.time = GetFloat(ed, "time", 0f);
                            ae.functionName = GetString(ed, "fn", "");
                            ae.stringParameter = GetString(ed, "data", "");
                            animEvents.Add(ae);
                        }
                    }
                    AnimationUtility.SetAnimationEvents(tClip, animEvents.ToArray());

                    string tdir = Path.GetDirectoryName(outTagPath);
                    if (!IsNullOrBlank(tdir) && !Directory.Exists(tdir)) Directory.CreateDirectory(tdir);
                    if (tNew) AssetDatabase.CreateAsset(tClip, outTagPath);
                    else EditorUtility.SetDirty(tClip);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return JsonSerialize(new Dictionary<string, object>
            {
                { "ok", true },
                { "clipName", clipName },
                { "outAnimPath", outAnimPath },
                { "outTagPath", outTagPath },
                { "curveCount", curveCount }
            });
        }

        private static string HandleRegisterCharacterClips(Dictionary<string, object> args, out int statusCode)
        {
            statusCode = 200;
            string prefabPath = GetString(args, "prefabPath", "");
            if (IsNullOrBlank(prefabPath))
            {
                statusCode = 400;
                return BuildErrorJson("prefabPath is required.");
            }
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                statusCode = 404;
                return BuildErrorJson("Prefab not found: " + prefabPath);
            }

            TaggedAnimPlayer tap = prefab.GetComponent<TaggedAnimPlayer>();
            Animation rootAnim = prefab.GetComponent<Animation>();
            Animation jointAnim = (tap != null) ? tap.jointAnimation : null;
            CharacterModelController cmc = prefab.GetComponent<CharacterModelController>();

            int addedJoint = 0;
            int addedTag = 0;
            int addedSet = 0;

            object jointListObj;
            if (jointAnim != null && args.TryGetValue("jointAnimPaths", out jointListObj) && jointListObj is IList<object>)
            {
                SerializedObject soJ = new SerializedObject(jointAnim);
                SerializedProperty spArr = soJ.FindProperty("m_Animations");
                IList<object> jPaths = (IList<object>)jointListObj;
                for (int i = 0; i < jPaths.Count; i++)
                {
                    AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(jPaths[i].ToString());
                    if (clip == null) continue;
                    bool exists = false;
                    for (int k = 0; k < spArr.arraySize; k++)
                    {
                        if (spArr.GetArrayElementAtIndex(k).objectReferenceValue == clip) { exists = true; break; }
                    }
                    if (!exists)
                    {
                        int idx = spArr.arraySize;
                        spArr.InsertArrayElementAtIndex(idx);
                        spArr.GetArrayElementAtIndex(idx).objectReferenceValue = clip;
                        addedJoint++;
                    }
                }
                soJ.ApplyModifiedProperties();
            }

            object tagListObj;
            if (rootAnim != null && args.TryGetValue("tagAnimPaths", out tagListObj) && tagListObj is IList<object>)
            {
                SerializedObject soR = new SerializedObject(rootAnim);
                SerializedProperty spArr = soR.FindProperty("m_Animations");
                IList<object> tPaths = (IList<object>)tagListObj;
                for (int i = 0; i < tPaths.Count; i++)
                {
                    AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(tPaths[i].ToString());
                    if (clip == null) continue;
                    bool exists = false;
                    for (int k = 0; k < spArr.arraySize; k++)
                    {
                        if (spArr.GetArrayElementAtIndex(k).objectReferenceValue == clip) { exists = true; break; }
                    }
                    if (!exists)
                    {
                        int idx = spArr.arraySize;
                        spArr.InsertArrayElementAtIndex(idx);
                        spArr.GetArrayElementAtIndex(idx).objectReferenceValue = clip;
                        addedTag++;
                    }
                    if (cmc != null)
                    {
                        SerializedObject soCmc = new SerializedObject(cmc);
                        SerializedProperty sets = soCmc.FindProperty("randomAnimSets");
                        for (int s = 0; s < sets.arraySize; s++)
                        {
                            SerializedProperty setElem = sets.GetArrayElementAtIndex(s);
                            if (setElem.FindPropertyRelative("name").stringValue == "attack")
                            {
                                SerializedProperty clipsProp = setElem.FindPropertyRelative("clips");
                                bool inSet = false;
                                for (int ci = 0; ci < clipsProp.arraySize; ci++)
                                {
                                    if (clipsProp.GetArrayElementAtIndex(ci).objectReferenceValue == clip) { inSet = true; break; }
                                }
                                if (!inSet)
                                {
                                    int idx = clipsProp.arraySize;
                                    clipsProp.InsertArrayElementAtIndex(idx);
                                    clipsProp.GetArrayElementAtIndex(idx).objectReferenceValue = clip;
                                    addedSet++;
                                }
                            }
                        }
                        soCmc.ApplyModifiedProperties();
                    }
                }
                soR.ApplyModifiedProperties();
            }

            EditorUtility.SetDirty(prefab);
            AssetDatabase.SaveAssets();
            return JsonSerialize(new Dictionary<string, object>
            {
                { "ok", true },
                { "addedJointClips", addedJoint },
                { "addedTagClips", addedTag },
                { "addedToAttackSet", addedSet }
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
