using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DevMenu
{
    /// <summary>
    /// Finds what makes a frame slow. While on, every Update / LateUpdate / FixedUpdate of the game's scripts (and, if set, every
    /// coroutine step) is wrapped in a timer. A frame over the threshold is written to BepInEx\DevMenu\spikes.txt with the methods
    /// that took the most time in it; stopping adds a summary over all slow frames. Patching runs a few milliseconds per frame,
    /// so starting takes a while but doesn't freeze the game. Native work (rendering, physics, animation, asset loading) isn't
    /// timed: the report shows how much of the frame the scripts account for.
    /// </summary>
    [DefaultExecutionOrder(-32000)] // runs first in a frame, so the timed span is one whole frame
    internal sealed class SpikeProfiler : MonoBehaviour
    {
        private const string HarmonyId = Plugin.Guid + ".spikeprofiler";
        private const float PatchBudgetMs = 6f;
        private const int TopPerFrame = 15;
        private const int MaxSpikes = 500;

        internal static ConfigEntry<float> SpikeMs;
        internal static ConfigEntry<bool> Coroutines;

        public static SpikeProfiler Instance { get; private set; }
        public static string FilePath => Path.Combine(Paths.BepInExRootPath, "DevMenu", "spikes.txt");

        public enum State { Off, Patching, On, Unpatching }
        public State Current { get; private set; }
        public int Patched { get; private set; }
        public int ToPatch { get; private set; }
        public int Failed { get; private set; }
        public int Spikes { get; private set; }
        public string LastSpike { get; private set; } = "";

        private sealed class Entry
        {
            public string Name;
            public long Ticks;
            public int Calls;
            public bool Touched;
            public double SpikeMs; // summed over the slow frames
            public int SpikeFrames;
        }

        private static readonly Dictionary<MethodBase, Entry> entries = new Dictionary<MethodBase, Entry>();
        private static readonly List<Entry> touched = new List<Entry>();
        private static int mainThread;
        private static bool recording;

        private Harmony harmony;
        private StreamWriter writer;
        private ProfilerRecorder drawCalls, batches, triangles, gcAlloc;
        private int skipFrames;

        public static void Bind(ConfigFile config)
        {
            SpikeMs = config.Bind("Spike profiler", "SpikeMs", 33f,
                "Tools tab, Spike profiler: a frame longer than this (milliseconds) is written to BepInEx\\DevMenu\\spikes.txt.");
            Coroutines = config.Bind("Spike profiler", "Coroutines", true,
                "Spike profiler: time coroutine steps too (more methods to patch, so a slower start). " +
                "Takes effect the next time the profiler starts.");
        }

        private void Awake()
        {
            Instance = this;
            mainThread = Thread.CurrentThread.ManagedThreadId;
        }

        public void Toggle()
        {
            if (Current == State.Off)
                StartCoroutine(Run());
            else if (Current == State.On || Current == State.Patching)
                StartCoroutine(Stop());
        }

        private IEnumerator Run()
        {
            Current = State.Patching;
            Spikes = 0;
            Failed = 0;
            LastSpike = "";
            List<MethodInfo> methods = FindMethods(Coroutines.Value);
            ToPatch = methods.Count;
            Patched = 0;
            harmony = new Harmony(HarmonyId);
            var prefix = new HarmonyMethod(typeof(SpikeProfiler), nameof(Prefix)) { priority = Priority.First };
            var postfix = new HarmonyMethod(typeof(SpikeProfiler), nameof(Postfix)) { priority = Priority.Last };
            var watch = Stopwatch.StartNew();
            foreach (MethodInfo method in methods)
            {
                if (Current != State.Patching)
                    yield break; // stopped while patching
                if (watch.Elapsed.TotalMilliseconds > PatchBudgetMs)
                {
                    yield return null;
                    watch.Restart();
                }
                entries[method] = new Entry { Name = $"{method.DeclaringType.FullName}.{method.Name}" };
                try
                {
                    harmony.Patch(method, prefix, postfix);
                }
                catch (Exception e)
                {
                    Failed++;
                    entries.Remove(method);
                    Plugin.Log.LogWarning($"Spike profiler: can't patch {method.DeclaringType.FullName}.{method.Name}: {e.Message}");
                }
                Patched++;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            writer = new StreamWriter(FilePath, append: false);
            writer.WriteLine($"Spike profiler, started {DateTime.Now:yyyy-MM-dd HH:mm:ss}. Threshold {SpikeMs.Value:0.#} ms. " +
                             $"Timed {Patched - Failed} methods (coroutines: {(Coroutines.Value ? "yes" : "no")}, failed: {Failed}).");
            writer.WriteLine("Times are inclusive: a timed method called from another timed method counts in both.");
            writer.WriteLine();
            writer.Flush();
            StartRecorders();
            skipFrames = 2; // the frame that finished patching is slow itself
            Current = State.On;
            recording = true;
        }

        private IEnumerator Stop()
        {
            recording = false;
            Current = State.Unpatching;
            yield return null;
            WriteSummary();
            Unpatch();
            Current = State.Off;
            Menu.Notify($"Spike profiler stopped: {Spikes} slow frames in spikes.txt");
        }

        private void Unpatch()
        {
            recording = false;
            harmony?.UnpatchSelf();
            harmony = null;
            entries.Clear();
            touched.Clear();
            StopRecorders();
            writer?.Dispose();
            writer = null;
        }

        private void OnDestroy()
        {
            if (Current != State.Off)
            {
                WriteSummary();
                Unpatch();
            }
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            if (Current != State.On)
                return;
            // unscaledDeltaTime is the last frame's length; the entries hold what ran since this Update in that frame.
            float ms = Time.unscaledDeltaTime * 1000f;
            if (skipFrames > 0)
                skipFrames--;
            else if (ms > SpikeMs.Value && Spikes < MaxSpikes)
                WriteSpike(ms);
            foreach (Entry e in touched)
            {
                e.Ticks = 0;
                e.Calls = 0;
                e.Touched = false;
            }
            touched.Clear();
        }

        private void WriteSpike(float frameMs)
        {
            Spikes++;
            double tickMs = 1000.0 / Stopwatch.Frequency;
            double scripts = touched.Sum(e => e.Ticks) * tickMs;
            int calls = touched.Sum(e => e.Calls);
            List<Entry> top = touched.OrderByDescending(e => e.Ticks).Take(TopPerFrame).ToList();
            foreach (Entry e in top)
            {
                e.SpikeMs += e.Ticks * tickMs;
                e.SpikeFrames++;
            }
            writer.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] frame {frameMs:0.0} ms, scene {SceneManager.GetActiveScene().name}; " +
                             $"timed scripts {scripts:0.0} ms in {calls} calls; {Recorders()}");
            foreach (Entry e in top)
                writer.WriteLine($"  {e.Ticks * tickMs,8:0.00} ms  x{e.Calls,-5} {e.Name}");
            writer.WriteLine();
            writer.Flush();
            Entry first = top.FirstOrDefault();
            LastSpike = $"{frameMs:0} ms, scripts {scripts:0} ms" + (first != null ? $"; top: {Short(first.Name)} {first.Ticks * tickMs:0.0} ms" : "");
        }

        private void WriteSummary()
        {
            if (writer == null)
                return;
            writer.WriteLine($"Summary: {Spikes} slow frames. Methods by time summed over the slow frames (top {TopPerFrame} of each frame):");
            foreach (Entry e in entries.Values.Where(e => e.SpikeFrames > 0).OrderByDescending(e => e.SpikeMs).Take(30))
                writer.WriteLine($"  {e.SpikeMs,9:0.0} ms  in {e.SpikeFrames,4} frames  (avg {e.SpikeMs / e.SpikeFrames:0.00})  {e.Name}");
            writer.Flush();
        }

        private static string Short(string name)
        {
            int dot = name.LastIndexOf('.', name.Length - 1);
            int prev = dot > 0 ? name.LastIndexOf('.', dot - 1) : -1;
            return prev >= 0 ? name.Substring(prev + 1) : name;
        }

        // Unity's counters that work in a release build; a missing one just shows "?".
        private void StartRecorders()
        {
            drawCalls = Recorder(ProfilerCategory.Render, "Draw Calls Count");
            batches = Recorder(ProfilerCategory.Render, "Batches Count");
            triangles = Recorder(ProfilerCategory.Render, "Triangles Count");
            gcAlloc = Recorder(ProfilerCategory.Memory, "GC Allocated In Frame");
        }

        private static ProfilerRecorder Recorder(ProfilerCategory category, string name)
        {
            try
            {
                return ProfilerRecorder.StartNew(category, name);
            }
            catch (Exception)
            {
                return default;
            }
        }

        private void StopRecorders()
        {
            drawCalls.Dispose();
            batches.Dispose();
            triangles.Dispose();
            gcAlloc.Dispose();
        }

        private string Recorders()
        {
            static string V(ProfilerRecorder r) => r.Valid ? r.LastValue.ToString() : "?";
            string alloc = gcAlloc.Valid ? $"{gcAlloc.LastValue / 1024f:0} KB" : "?";
            return $"draw calls {V(drawCalls)}, batches {V(batches)}, triangles {V(triangles)}, GC alloc {alloc}";
        }

        // The game's assemblies and the third-party ones it ships; not the runtime, BepInEx, Harmony or us.
        private static List<MethodInfo> FindMethods(bool coroutines)
        {
            string managed = Path.GetFullPath(Path.Combine(Application.dataPath, "Managed"));
            var result = new List<MethodInfo>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string name = assembly.GetName().Name;
                if (assembly.IsDynamic || name == "mscorlib" || name == "netstandard" || name.StartsWith("System") || name.StartsWith("Mono."))
                    continue;
                string location;
                try
                {
                    location = assembly.Location;
                }
                catch (Exception)
                {
                    continue;
                }
                if (string.IsNullOrEmpty(location) || !Path.GetFullPath(location).StartsWith(managed, StringComparison.OrdinalIgnoreCase))
                    continue;
                foreach (Type type in Types(assembly))
                {
                    if (type == null || type.ContainsGenericParameters || type.IsInterface)
                        continue;
                    bool behaviour = typeof(MonoBehaviour).IsAssignableFrom(type);
                    bool coroutine = coroutines && typeof(IEnumerator).IsAssignableFrom(type) && type.IsNested && type.Name.StartsWith("<");
                    if (!behaviour && !coroutine)
                        continue;
                    MethodInfo[] declared;
                    try
                    {
                        declared = type.GetMethods(flags);
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                    foreach (MethodInfo m in declared)
                    {
                        bool wanted = behaviour
                            ? (m.Name == "Update" || m.Name == "LateUpdate" || m.Name == "FixedUpdate") && m.ReturnType == typeof(void)
                            : m.Name == "MoveNext" && m.ReturnType == typeof(bool);
                        if (wanted && m.GetParameters().Length == 0 && !m.IsAbstract && !m.IsGenericMethod && HasBody(m))
                            result.Add(m);
                    }
                }
            }
            return result;
        }

        private static IEnumerable<Type> Types(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types;
            }
            catch (Exception)
            {
                return Array.Empty<Type>();
            }
        }

        private static bool HasBody(MethodInfo m)
        {
            try
            {
                return m.GetMethodBody() != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void Prefix(out long __state)
        {
            // Iterators that aren't coroutines may run on worker threads: only the main thread is timed.
            __state = recording && Thread.CurrentThread.ManagedThreadId == mainThread ? Stopwatch.GetTimestamp() : 0;
        }

        private static void Postfix(long __state, MethodBase __originalMethod)
        {
            if (__state == 0 || !entries.TryGetValue(__originalMethod, out Entry e))
                return;
            e.Ticks += Stopwatch.GetTimestamp() - __state;
            e.Calls++;
            if (!e.Touched)
            {
                e.Touched = true;
                touched.Add(e);
            }
        }
    }
}
