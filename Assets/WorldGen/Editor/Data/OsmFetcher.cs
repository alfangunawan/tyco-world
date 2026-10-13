using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Non-blocking Overpass download driven by EditorApplication.update; tries endpoints in order.</summary>
    public sealed class OsmFetcher
    {
        public const string UserAgent = "TycoWorld-WorldGen/1.0 (Unity editor; personal project)";

        public static OsmFetcher Active { get; private set; }

        public string Status { get; private set; } = "";
        public bool IsDone { get; private set; }
        public bool Succeeded { get; private set; }
        public string Error { get; private set; }

        readonly string query;
        readonly string[] endpoints;
        readonly int timeoutSeconds;
        readonly string rawPath;
        readonly GeoBounds bounds;
        readonly Action<OsmFetcher> onDone;
        readonly List<string> failures = new List<string>();
        int endpointIndex = -1;
        UnityWebRequest request;

        OsmFetcher(WorldGenSettings settings, Action<OsmFetcher> onDone)
        {
            bounds = settings.Bounds;
            query = OverpassQuery.Build(bounds, settings.timeoutSeconds);
            endpoints = settings.overpassEndpoints ?? new string[0];
            timeoutSeconds = settings.timeoutSeconds;
            rawPath = WorldGenPaths.ToAbsolute(settings.rawDataPath);
            this.onDone = onDone;
        }

        public static OsmFetcher Start(WorldGenSettings settings, Action<OsmFetcher> onDone = null)
        {
            if (Active != null && !Active.IsDone) throw new InvalidOperationException("Download sedang berjalan.");
            var fetcher = new OsmFetcher(settings, onDone);
            Active = fetcher;
            EditorApplication.update += fetcher.Tick;
            fetcher.Next();
            return fetcher;
        }

        public static UnityWebRequest CreateRequest(string endpoint, string query, int timeoutSeconds)
        {
            var form = new WWWForm();
            form.AddField("data", query);
            var request = UnityWebRequest.Post(endpoint, form);
            request.timeout = timeoutSeconds;
            request.SetRequestHeader("User-Agent", UserAgent);
            request.SetRequestHeader("Accept", "application/json");
            return request;
        }

        void Next()
        {
            DisposeRequest();
            endpointIndex++;
            if (endpointIndex >= endpoints.Length)
            {
                Finish(false, "Semua endpoint gagal:\n" + string.Join("\n", failures));
                return;
            }
            Status = "Download dari " + endpoints[endpointIndex] + " ...";
            request = CreateRequest(endpoints[endpointIndex], query, timeoutSeconds);
            request.SendWebRequest();
        }

        void Tick()
        {
            if (IsDone || request == null || !request.isDone) return;
            string endpoint = endpoints[endpointIndex];
            if (request.result != UnityWebRequest.Result.Success)
            {
                failures.Add(endpoint + ": " + request.error);
                Next();
                return;
            }
            string body = request.downloadHandler.text;
            if (!OverpassResponseValidator.Validate(body, out string error))
            {
                failures.Add(endpoint + ": " + error);
                Next();
                return;
            }
            try
            {
                CacheWriter.WriteAtomic(rawPath, body);
                CacheWriter.WriteInfo(rawPath, endpoint, bounds, OverpassResponseValidator.CountElements(body), DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                Finish(false, "Gagal menyimpan cache: " + ex.Message);
                return;
            }
            Finish(true, null);
        }

        public void Cancel()
        {
            if (IsDone) return;
            if (request != null) request.Abort();
            Finish(false, "Dibatalkan.");
        }

        void Finish(bool ok, string error)
        {
            IsDone = true;
            Succeeded = ok;
            Error = error;
            Status = ok ? "Selesai." : "Gagal: " + error;
            EditorApplication.update -= Tick;
            DisposeRequest();
            if (ok) Debug.Log("[WorldGen] Download selesai: " + rawPath);
            else Debug.LogWarning("[WorldGen] Download gagal: " + error);
            onDone?.Invoke(this);
        }

        void DisposeRequest()
        {
            if (request == null) return;
            request.Dispose();
            request = null;
        }
    }
}
