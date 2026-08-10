using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Jxqy.Ports;
using TEngine;
using UnityEngine;

namespace Jxqy.UnityAdapters
{
    /// <summary>
    /// Renders dynamic world actors with pooled SpriteRenderers.
    /// Static map tiles belong to generated Tilemaps in the map scene.
    /// </summary>
    public sealed class JxqySpriteRendererWorldRenderer :
        MonoBehaviour,
        IJxqyRenderPort
    {
        private const int RendererPoolCapacity = 2048;
        private const int FirstSortingOrder = -16000;
        private static int _nextPoolId;
        private readonly List<OrderedCommand> _sorted = new();
        private readonly List<RendererObject> _activeRenderers = new();
        private readonly Dictionary<SpriteKey, Sprite> _sprites = new();
        private JxqyTextureRegistry _textures;
        private JxqyMaterialCache _materials;
        private IObjectPoolModule _objectPoolModule;
        private IObjectPool<RendererObject> _rendererPool;
        private Camera _camera;
        private int _logicalWidth = 640;
        private int _logicalHeight = 480;

        public int LastSubmittedCommandCount { get; private set; }
        public int LastVisibleCommandCount { get; private set; }
        public int LastWeatherCommandCount { get; private set; }
        public int LastFadeCommandCount { get; private set; }
        public int LastPoolSpawnCount { get; private set; }
        public int LastPoolUnspawnCount { get; private set; }
        public int ActiveRendererCount => _activeRenderers.Count;

        public void Initialize(
            Camera targetCamera,
            JxqyTextureRegistry textures,
            IReadOnlyDictionary<string, Material> materials)
        {
            _camera = targetCamera != null
                ? targetCamera
                : throw new ArgumentNullException(nameof(targetCamera));
            _textures = textures ??
                        throw new ArgumentNullException(nameof(textures));
            _materials ??= new JxqyMaterialCache(materials);
            if (_rendererPool == null)
            {
                _objectPoolModule =
                    ModuleSystem.GetModule<IObjectPoolModule>() ??
                    throw new InvalidOperationException(
                        "TEngine object-pool module is unavailable.");
                _rendererPool =
                    _objectPoolModule.CreateSingleSpawnObjectPool<
                        RendererObject>(
                        "Jxqy Dynamic Renderers " +
                        Interlocked.Increment(ref _nextPoolId),
                        RendererPoolCapacity,
                        30f);
            }
            SetLogicalResolution(_logicalWidth, _logicalHeight);
        }

        public void SetLogicalResolution(int width, int height)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException(nameof(width));
            _logicalWidth = width;
            _logicalHeight = height;
            if (_camera == null)
                return;
            _camera.orthographic = true;
            _camera.orthographicSize = height * 0.5f;
            _camera.nearClipPlane = 0.01f;
            _camera.farClipPlane = 200f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.black;
            _camera.transparencySortMode =
                TransparencySortMode.CustomAxis;
            _camera.transparencySortAxis = new Vector3(0f, 1f, 0f);
        }

        public void SetCameraPosition(float worldX, float worldY)
        {
            if (_camera == null)
                throw new InvalidOperationException(
                    "Jxqy dynamic renderer has not been initialized.");
            _camera.transform.position = new Vector3(
                worldX,
                -worldY,
                -10f);
        }

        public void Submit(IReadOnlyList<JxqyDrawCommand> commands)
        {
            if (_textures == null || _camera == null)
                throw new InvalidOperationException(
                    "Jxqy dynamic renderer has not been initialized.");

            _sorted.Clear();
            if (commands != null)
            {
                for (int index = 0; index < commands.Count; index++)
                {
                    _sorted.Add(new OrderedCommand(
                        commands[index],
                        index));
                }
            }
            _sorted.Sort(OrderedCommand.Compare);
            LastSubmittedCommandCount = _sorted.Count;
            LastWeatherCommandCount = 0;
            LastFadeCommandCount = 0;
            LastPoolSpawnCount = 0;
            LastPoolUnspawnCount = 0;
            for (int index = 0; index < _sorted.Count; index++)
            {
                int depth = _sorted[index].Command.Depth;
                if (depth >= JxqyPresentationDrawCommandBuilder.WeatherDepth &&
                    depth < JxqyPresentationDrawCommandBuilder.FadeDepth)
                {
                    LastWeatherCommandCount++;
                }
                else if (depth ==
                         JxqyPresentationDrawCommandBuilder.FadeDepth)
                {
                    LastFadeCommandCount++;
                }
            }
            EnsurePoolSize(_sorted.Count);

            int visibleCount = 0;
            for (int index = 0; index < _sorted.Count; index++)
            {
                JxqyDrawCommand command = _sorted[index].Command;
                if (!_textures.TryGet(
                        command.TextureAddress,
                        out Texture2D texture))
                {
                    continue;
                }

                RendererObject pooledObject;
                if (visibleCount < _activeRenderers.Count)
                {
                    pooledObject = _activeRenderers[visibleCount];
                }
                else
                {
                    pooledObject = _rendererPool.Spawn();
                    if (pooledObject == null)
                    {
                        throw new InvalidOperationException(
                            "TEngine renderer pool could not provide an object.");
                    }
                    _activeRenderers.Add(pooledObject);
                    LastPoolSpawnCount++;
                }
                PooledRenderer pooled = pooledObject.Renderer;
                visibleCount++;
                Vector2 spritePosition = GetSpritePosition(
                    command.Position,
                    command.Source,
                    command.Anchor);
                pooled.Transform.localPosition = new Vector3(
                    spritePosition.x,
                    -spritePosition.y,
                    0f);
                pooled.Renderer.sprite = GetOrCreateSprite(
                    texture,
                    command.Source,
                    command.Anchor);
                pooled.Renderer.color = command.Color;
                pooled.Renderer.sharedMaterial = _materials.Get(
                    command.MaterialKey);
                pooled.Renderer.sortingOrder =
                    GetSortingOrderForSubmissionIndex(index);
            }
            ReleaseSurplusRenderers(visibleCount);
            LastVisibleCommandCount = visibleCount;
        }

        public static int GetSortingOrderForSubmissionIndex(int index)
        {
            if (index < 0 || index >= RendererPoolCapacity)
                throw new ArgumentOutOfRangeException(nameof(index));
            return FirstSortingOrder + index;
        }

        public static Vector2 GetSpritePosition(
            Vector2 commandPosition,
            Rect source,
            Vector2 anchor)
        {
            float clampedAnchorX = Mathf.Clamp(
                anchor.x,
                0f,
                Mathf.Max(0f, source.width));
            float clampedAnchorY = Mathf.Clamp(
                anchor.y,
                0f,
                Mathf.Max(0f, source.height));
            return commandPosition + new Vector2(
                clampedAnchorX - anchor.x,
                clampedAnchorY - anchor.y);
        }

        private Sprite GetOrCreateSprite(
            Texture2D texture,
            Rect source,
            Vector2 anchor)
        {
            var key = new SpriteKey(texture, source, anchor);
            if (_sprites.TryGetValue(key, out Sprite sprite))
                return sprite;
            float pivotX = source.width <= 0
                ? 0f
                : Mathf.Clamp01(anchor.x / source.width);
            float pivotY = source.height <= 0
                ? 0f
                : Mathf.Clamp01(1f - anchor.y / source.height);
            sprite = Sprite.Create(
                texture,
                source,
                new Vector2(pivotX, pivotY),
                1f,
                0,
                SpriteMeshType.FullRect,
                Vector4.zero,
                false);
            sprite.name =
                $"Jxqy-{texture.name}-{source.x}-{source.y}-" +
                $"{source.width}-{source.height}";
            _sprites.Add(key, sprite);
            return sprite;
        }

        private void EnsurePoolSize(int count)
        {
            while (_rendererPool.Count < count)
            {
                int previousCount = _rendererPool.Count;
                var child = new GameObject(
                    $"DynamicSprite-{_rendererPool.Count:D4}");
                child.transform.SetParent(transform, false);
                SpriteRenderer renderer =
                    child.AddComponent<SpriteRenderer>();
                renderer.receiveShadows = false;
                renderer.shadowCastingMode =
                    UnityEngine.Rendering.ShadowCastingMode.Off;
                _rendererPool.Register(
                    RendererObject.Create(
                        new PooledRenderer(
                            child,
                            child.transform,
                            renderer)),
                    spawned: false);
                if (_rendererPool.Count <= previousCount)
                {
                    Destroy(child);
                    throw new InvalidOperationException(
                        $"TEngine renderer pool stopped growing at " +
                        $"{previousCount} objects while {count} were " +
                        $"required. Capacity={RendererPoolCapacity}.");
                }
            }
        }

        private void OnDestroy()
        {
            ReleaseActiveRenderers();
            if (_rendererPool != null &&
                _objectPoolModule != null)
            {
                _objectPoolModule.DestroyObjectPool(
                    _rendererPool);
            }
            _rendererPool = null;
            _objectPoolModule = null;
            foreach (Sprite sprite in _sprites.Values)
                Destroy(sprite);
            _sprites.Clear();
            _materials = null;
        }

        private void ReleaseActiveRenderers()
        {
            if (_rendererPool == null)
            {
                _activeRenderers.Clear();
                return;
            }
            for (int index = 0;
                 index < _activeRenderers.Count;
                 index++)
            {
                _rendererPool.Unspawn(
                    _activeRenderers[index]);
            }
            _activeRenderers.Clear();
        }

        private void ReleaseSurplusRenderers(int requiredCount)
        {
            for (int index = _activeRenderers.Count - 1;
                 index >= requiredCount;
                 index--)
            {
                _rendererPool.Unspawn(_activeRenderers[index]);
                _activeRenderers.RemoveAt(index);
                LastPoolUnspawnCount++;
            }
        }

        private readonly struct OrderedCommand
        {
            public OrderedCommand(
                JxqyDrawCommand command,
                int submissionIndex)
            {
                Command = command;
                SubmissionIndex = submissionIndex;
            }

            public JxqyDrawCommand Command { get; }
            private int SubmissionIndex { get; }

            public static int Compare(
                OrderedCommand left,
                OrderedCommand right)
            {
                int depth = left.Command.Depth.CompareTo(
                    right.Command.Depth);
                return depth != 0
                    ? depth
                    : left.SubmissionIndex.CompareTo(
                        right.SubmissionIndex);
            }
        }

        private sealed class RendererObject : ObjectBase
        {
            private PooledRenderer _renderer;

            public PooledRenderer Renderer => _renderer;

            public static RendererObject Create(
                PooledRenderer renderer)
            {
                RendererObject result =
                    MemoryPool.Acquire<RendererObject>();
                result.Initialize(renderer.GameObject);
                result._renderer = renderer;
                return result;
            }

            public override void Clear()
            {
                base.Clear();
                _renderer = default;
            }

            protected override void OnSpawn()
            {
                _renderer.GameObject.SetActive(true);
            }

            protected override void OnUnspawn()
            {
                _renderer.GameObject.SetActive(false);
            }

            protected override void Release(
                bool isShutdown)
            {
                if (_renderer.GameObject == null)
                    return;
                if (Application.isPlaying)
                    Destroy(_renderer.GameObject);
                else
                    DestroyImmediate(_renderer.GameObject);
                _renderer = default;
            }
        }

        private readonly struct PooledRenderer
        {
            public PooledRenderer(
                GameObject gameObject,
                Transform transform,
                SpriteRenderer renderer)
            {
                GameObject = gameObject;
                Transform = transform;
                Renderer = renderer;
            }

            public GameObject GameObject { get; }
            public Transform Transform { get; }
            public SpriteRenderer Renderer { get; }
        }

        private readonly struct SpriteKey : IEquatable<SpriteKey>
        {
            public SpriteKey(
                Texture2D texture,
                Rect source,
                Vector2 anchor)
            {
                Texture = texture;
                Source = source;
                Anchor = anchor;
            }

            private Texture2D Texture { get; }
            private Rect Source { get; }
            private Vector2 Anchor { get; }

            public bool Equals(SpriteKey other)
            {
                return Texture == other.Texture &&
                       Source.Equals(other.Source) &&
                       Anchor.Equals(other.Anchor);
            }

            public override bool Equals(object obj)
            {
                return obj is SpriteKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = Texture != null
                        ? RuntimeHelpers.GetHashCode(Texture)
                        : 0;
                    hash = hash * 397 ^ Source.GetHashCode();
                    hash = hash * 397 ^ Anchor.GetHashCode();
                    return hash;
                }
            }
        }
    }
}
