using System.Text.Json;

// Only engine/service boundaries are substituted. Production source files are linked by the project.
namespace UnityEngine
{
    public class Object
    {
        protected static void Destroy(Object value) { }
        protected static void DontDestroyOnLoad(Object value) { }
    }
    public class GameObject : Object
    {
        public GameObject(string name = "test") { }
        public T AddComponent<T>() where T : new() => new T();
    }
    public class MonoBehaviour : Object { public GameObject gameObject = new(); }
    public class AudioClip : Object { }
    public struct Vector3 { public float x, y, z; }
    public class SerializeField : Attribute { }
    public class HeaderAttribute : Attribute { public HeaderAttribute(string text) { } }
    public class MinAttribute : Attribute { public MinAttribute(float value) { } }
    public class TooltipAttribute : Attribute { public TooltipAttribute(string text) { } }
    public class ContextMenu : Attribute { public ContextMenu(string text) { } }
    public class DefaultExecutionOrder : Attribute { public DefaultExecutionOrder(int order) { } }
    public enum RuntimeInitializeLoadType { BeforeSceneLoad }
    public class RuntimeInitializeOnLoadMethodAttribute : Attribute
    {
        public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType value) { }
    }
    public static class Application { public static bool isEditor => false; }
    public static class Time
    {
        public static float time;
        public static float realtimeSinceStartup;
        public static float unscaledTime;
    }
    public static class Debug
    {
        public static bool isDebugBuild => false;
        public static readonly List<string> Errors = new();
        public static void Log(object message, Object context = null) { }
        public static void LogWarning(object message) { }
        public static void LogError(object message) => Errors.Add(message.ToString());
        public static void LogException(Exception error) => Errors.Add(error.Message);
    }
    public static class Mathf
    {
        public static float Floor(float value) => MathF.Floor(value);
        public static int FloorToInt(float value) => (int)MathF.Floor(value);
        public static float Max(float a, float b) => Math.Max(a, b);
        public static int Max(int a, int b) => Math.Max(a, b);
    }
    public static class JsonUtility
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            IncludeFields = true,
            IgnoreReadOnlyProperties = true
        };
        public static string ToJson(object value, bool prettyPrint = false)
            => JsonSerializer.Serialize(value, Options);
        public static T FromJson<T>(string json) => JsonSerializer.Deserialize<T>(json, Options);
    }
    public static class PlayerPrefs
    {
        private static readonly Dictionary<string, string> Values = new();
        public static bool HasKey(string key) => Values.ContainsKey(key);
        public static string GetString(string key) => Values[key];
        public static void SetString(string key, string value) => Values[key] = value;
        public static void DeleteKey(string key) => Values.Remove(key);
        public static void Save() { }
    }
}

// No real simulation scheduler, scene, audio or cloud service is started.
public class Simulation
{
    public static Simulation Instance;
    public float Now;
    public float GetCurrentTime() => Now;
}
public class AudioManager
{
    public static AudioManager Instance;
    public void PlaySFX(UnityEngine.AudioClip clip) { }
}
public class Node { }
public class Edge { }
public class Server { }
// These constants only satisfy GameData dependencies; star-rating/difficulty rules are not tested.
public enum Difficulty { Easy, Normal, Hard }
public static class StarRating { public const int MaxStars = 3; }
public class UnityCloudSaveStorage : IAsyncSaveStorage
{
    public Task<bool> ExistsAsync(string key) => throw new NotSupportedException();
    public Task<string> LoadAsync(string key) => throw new NotSupportedException();
    public Task SaveAsync(string key, string json) => throw new NotSupportedException();
    public Task DeleteAsync(string key) => throw new NotSupportedException();
    public Task<string> GetPlayerIdAsync() => throw new NotSupportedException();
}

public sealed class MemoryStorage : ISaveStorage
{
    public string Json;
    public bool Exists(string key) => Json != null;
    public string Load(string key) => Json;
    public void Save(string key, string json) => Json = json;
    public void Delete(string key) => Json = null;
    public GameData Read() => UnityEngine.JsonUtility.FromJson<GameData>(Json);
}

public sealed class ControlledCloud : IAsyncSaveStorage
{
    public sealed record Write(string Json, TaskCompletionSource Completion);
    public readonly List<Write> Writes = new();
    public bool FailReads;
    public Task<bool> ExistsAsync(string key) => FailReads
        ? Task.FromException<bool>(new IOException("injected read failure")) : Task.FromResult(false);
    public Task<string> LoadAsync(string key) => throw new NotSupportedException();
    public Task DeleteAsync(string key) => Task.CompletedTask;
    public Task SaveAsync(string key, string json)
    {
        var completion = new TaskCompletionSource();
        Writes.Add(new Write(json, completion));
        return completion.Task;
    }
    public void Succeed(int index) => Writes[index].Completion.SetResult();
    public void Fail(int index) => Writes[index].Completion.SetException(new IOException("injected upload failure"));
}
