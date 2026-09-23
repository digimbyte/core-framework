using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Core.Registry
{
    public partial class RegistryManager
    {
        private sealed class PendingTexture
        {
            internal readonly TaskCompletionSource<Texture2D> Completion = new TaskCompletionSource<Texture2D>();
            internal UnityWebRequest Request;
        }

        private readonly Dictionary<string, PendingTexture> pendingTextures = new Dictionary<string, PendingTexture>();
        private readonly Dictionary<string, Texture2D> ownedTextures = new Dictionary<string, Texture2D>();

        /// <summary>Hashes the exact URL without changing case, query parameters, or escaping.</summary>
        public static string GetUrlTextureUID(string url)
        {
            if (string.IsNullOrEmpty(url)) throw new ArgumentException("URL is required.", nameof(url));
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(url))).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>
        /// Gets a session texture from a registered Texture cache, downloading only on a miss.
        /// Call on Unity's main thread. Concurrent requests for the same bucket and URL share a task.
        /// The manager owns downloaded textures; callers must not destroy them.
        /// </summary>
        public Task<Texture2D> GetTextureFromUrlAsync(string registryName, string url)
        {
            if (!Application.isPlaying)
                throw new InvalidOperationException("URL texture caching is only available at runtime.");
            var registry = GetRegistry(registryName);
            if (registry == null || registry.RuntimeAccess != RegistryRuntimeAccess.Cache || registry.AssetType != RegistryAssetType.Texture)
                throw new InvalidOperationException("The target must be a registered Texture registry with Cache (Read/Write) runtime access.");
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                throw new ArgumentException("An absolute HTTP or HTTPS URL is required.", nameof(url));

            var key = MakeCompositeKey(registryName, GetUrlTextureUID(url));
            // Inspect the entry itself: the registry's fallback texture is not a cache hit.
            var existing = GetItemByUID(key)?.asset as Texture2D;
            if (existing != null) return Task.FromResult(existing);
            if (pendingTextures.TryGetValue(key, out var pending)) return pending.Completion.Task;

            pending = new PendingTexture();
            pendingTextures.Add(key, pending);
            _ = DownloadTextureAsync(key, url, registry, pending);
            return pending.Completion.Task;
        }

        private async Task DownloadTextureAsync(string key, string url, Registry registry, PendingTexture pending)
        {
            Texture2D texture = null;
            try
            {
                using (var request = UnityWebRequestTexture.GetTexture(url))
                {
                    pending.Request = request;
                    var finished = new TaskCompletionSource<bool>();
                    var operation = request.SendWebRequest();
                    operation.completed += _ => finished.TrySetResult(true);
                    if (operation.isDone) finished.TrySetResult(true);
                    await finished.Task;

                    if (pending.Completion.Task.IsCompleted) return;
                    if (request.result != UnityWebRequest.Result.Success)
                        throw new InvalidOperationException($"Texture download failed: {request.error}");
                    texture = DownloadHandlerTexture.GetContent(request);
                    if (texture == null) throw new InvalidOperationException("The response did not contain a texture.");
                    if (!TryParseCompositeKey(key, out var bucket, out var uid) || GetRegistry(bucket) != registry)
                        throw new InvalidOperationException("The target registry was unregistered during the download.");

                    // A caller may have supplied an override while the request was in flight.
                    var existing = GetItemByUID(key)?.asset as Texture2D;
                    if (existing != null)
                    {
                        pending.Completion.TrySetResult(existing);
                        return;
                    }
                    if (!TryAddOverride(key, new ItemEntry { uid = uid, asset = texture }))
                        throw new InvalidOperationException("The target registry rejected the downloaded texture.");
                    ownedTextures[key] = texture;
                    var result = texture;
                    texture = null;
                    pending.Completion.TrySetResult(result);
                }
            }
            catch (Exception exception)
            {
                pending.Completion.TrySetException(exception);
            }
            finally
            {
                pending.Request = null;
                if (texture != null) Destroy(texture);
                if (pendingTextures.TryGetValue(key, out var current) && current == pending)
                    pendingTextures.Remove(key);
            }
        }

        private void ReleaseOwnedTexture(string key, UnityEngine.Object replacement = null)
        {
            if (!ownedTextures.TryGetValue(key, out var texture) || texture == replacement) return;
            ownedTextures.Remove(key);
            if (texture != null) Destroy(texture);
        }

        private bool RemoveCachedOverride(string key)
        {
            bool removed = overrideItemCache.Remove(key);
            ReleaseOwnedTexture(key);
            if (pendingTextures.TryGetValue(key, out var pending))
            {
                pendingTextures.Remove(key);
                pending.Completion.TrySetCanceled();
                pending.Request?.Abort();
                removed = true;
            }
            return removed;
        }

        private void OnDestroy()
        {
            foreach (var key in new List<string>(pendingTextures.Keys)) RemoveCachedOverride(key);
            foreach (var key in new List<string>(ownedTextures.Keys)) RemoveCachedOverride(key);
            overrideItemCache.Clear();
            if (Instance == this) Instance = null;
        }
    }
}
