using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CombatSystem.Tests
{
    public class Day7PerformanceSmokeTests
    {
        private const float WarmupSeconds = 2f;
        private const float SampleSeconds = 8f;

        [UnityTest]
        public IEnumerator FieldScene_PerfSnapshot_WritesReport()
        {
            yield return RunSceneSnapshot("Field");
        }

        [UnityTest]
        public IEnumerator BossScene_PerfSnapshot_WritesReport()
        {
            yield return RunSceneSnapshot("Boss");
        }

        private static IEnumerator RunSceneSnapshot(string sceneName)
        {
            var load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            Assert.IsNotNull(load, $"Failed to load scene '{sceneName}'.");
            while (!load.isDone)
            {
                yield return null;
            }

            yield return null;
            yield return WaitForRealtime(WarmupSeconds);

            ProfilerRecorder mainThreadRecorder;
            ProfilerRecorder gcAllocRecorder;
            ProfilerRecorder systemMemoryRecorder;
            var hasMainThread = TryStartRecorder(ProfilerCategory.Internal, "Main Thread", out mainThreadRecorder);
            var hasGcAlloc = TryStartRecorder(ProfilerCategory.Memory, "GC Allocated In Frame", out gcAllocRecorder);
            var hasSystemMemory = TryStartRecorder(ProfilerCategory.Memory, "System Used Memory", out systemMemoryRecorder);

            var mainThreadSamples = hasMainThread ? new List<long>(1024) : null;
            var gcAllocSamples = hasGcAlloc ? new List<long>(1024) : null;
            var systemMemorySamples = hasSystemMemory ? new List<long>(1024) : null;

            var start = Time.realtimeSinceStartup;
            var sampledFrames = 0;
            while (Time.realtimeSinceStartup - start < SampleSeconds)
            {
                sampledFrames++;
                if (hasMainThread)
                {
                    mainThreadSamples.Add(mainThreadRecorder.LastValue);
                }

                if (hasGcAlloc)
                {
                    gcAllocSamples.Add(gcAllocRecorder.LastValue);
                }

                if (hasSystemMemory)
                {
                    systemMemorySamples.Add(systemMemoryRecorder.LastValue);
                }

                yield return null;
            }

            if (hasMainThread)
            {
                mainThreadRecorder.Dispose();
            }

            if (hasGcAlloc)
            {
                gcAllocRecorder.Dispose();
            }

            if (hasSystemMemory)
            {
                systemMemoryRecorder.Dispose();
            }

            var snapshot = BuildSnapshot(
                sceneName,
                sampledFrames,
                mainThreadSamples,
                gcAllocSamples,
                systemMemorySamples);

            var summary = snapshot.ToSummary();
            Debug.Log(summary);
            WriteSnapshotLog(snapshot);

            Assert.Greater(snapshot.SampledFrames, 10, $"Scene '{sceneName}' sampled too few frames.");
            if (snapshot.AvgMainThreadMs >= 0f)
            {
                Assert.Greater(snapshot.AvgMainThreadMs, 0.01f, $"Scene '{sceneName}' has invalid main thread average.");
            }
        }

        private static Snapshot BuildSnapshot(
            string sceneName,
            int sampledFrames,
            List<long> mainThreadSamples,
            List<long> gcAllocSamples,
            List<long> systemMemorySamples)
        {
            return new Snapshot
            {
                SceneName = sceneName,
                TimestampUtc = DateTime.UtcNow,
                SampledFrames = sampledFrames,
                AvgMainThreadMs = ToScaledAverage(mainThreadSamples, 1e-6f),
                P95MainThreadMs = ToScaledPercentile(mainThreadSamples, 0.95f, 1e-6f),
                AvgGcAllocBytes = ToScaledAverage(gcAllocSamples, 1f),
                P95GcAllocBytes = ToScaledPercentile(gcAllocSamples, 0.95f, 1f),
                AvgSystemMemoryMb = ToScaledAverage(systemMemorySamples, 1f / (1024f * 1024f)),
                P95SystemMemoryMb = ToScaledPercentile(systemMemorySamples, 0.95f, 1f / (1024f * 1024f))
            };
        }

        private static float ToScaledAverage(List<long> samples, float scale)
        {
            if (samples == null || samples.Count == 0)
            {
                return -1f;
            }

            double sum = 0d;
            for (var i = 0; i < samples.Count; i++)
            {
                sum += samples[i];
            }

            return (float)(sum / samples.Count) * scale;
        }

        private static float ToScaledPercentile(List<long> samples, float percentile, float scale)
        {
            if (samples == null || samples.Count == 0)
            {
                return -1f;
            }

            var sorted = new List<long>(samples);
            sorted.Sort();
            var p = Mathf.Clamp01(percentile);
            var index = Mathf.Clamp(Mathf.CeilToInt(sorted.Count * p) - 1, 0, sorted.Count - 1);
            return sorted[index] * scale;
        }

        private static IEnumerator WaitForRealtime(float seconds)
        {
            var end = Time.realtimeSinceStartup + Mathf.Max(0f, seconds);
            while (Time.realtimeSinceStartup < end)
            {
                yield return null;
            }
        }

        private static bool TryStartRecorder(ProfilerCategory category, string name, out ProfilerRecorder recorder)
        {
            try
            {
                recorder = ProfilerRecorder.StartNew(category, name, 1024);
                return recorder.Valid;
            }
            catch
            {
                recorder = default;
                return false;
            }
        }

        private static void WriteSnapshotLog(Snapshot snapshot)
        {
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var logsPath = Path.Combine(root, "Logs");
            Directory.CreateDirectory(logsPath);

            var filePath = Path.Combine(logsPath, $"Day7_Perf_{snapshot.SceneName}.log");
            var line = snapshot.ToLogLine();
            File.AppendAllText(filePath, line + Environment.NewLine);
        }

        private struct Snapshot
        {
            public string SceneName;
            public DateTime TimestampUtc;
            public int SampledFrames;
            public float AvgMainThreadMs;
            public float P95MainThreadMs;
            public float AvgGcAllocBytes;
            public float P95GcAllocBytes;
            public float AvgSystemMemoryMb;
            public float P95SystemMemoryMb;

            public string ToSummary()
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "[Day7Perf] Scene={0}, Frames={1}, MainThreadAvgMs={2:F3}, MainThreadP95Ms={3:F3}, GCAllocAvgB={4:F1}, GCAllocP95B={5:F1}, SysMemAvgMB={6:F2}, SysMemP95MB={7:F2}",
                    SceneName,
                    SampledFrames,
                    AvgMainThreadMs,
                    P95MainThreadMs,
                    AvgGcAllocBytes,
                    P95GcAllocBytes,
                    AvgSystemMemoryMb,
                    P95SystemMemoryMb);
            }

            public string ToLogLine()
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "{0:o}\tScene={1}\tFrames={2}\tMainThreadAvgMs={3:F3}\tMainThreadP95Ms={4:F3}\tGCAllocAvgB={5:F1}\tGCAllocP95B={6:F1}\tSysMemAvgMB={7:F2}\tSysMemP95MB={8:F2}",
                    TimestampUtc,
                    SceneName,
                    SampledFrames,
                    AvgMainThreadMs,
                    P95MainThreadMs,
                    AvgGcAllocBytes,
                    P95GcAllocBytes,
                    AvgSystemMemoryMb,
                    P95SystemMemoryMb);
            }
        }
    }
}
