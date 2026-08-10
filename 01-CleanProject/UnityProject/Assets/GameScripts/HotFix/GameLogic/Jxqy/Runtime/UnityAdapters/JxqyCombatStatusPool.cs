using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jxqy.Domain.Simulation;
using Jxqy.Domain.World;
using TEngine;
using UnityEngine;
using UnityEngine.UI;

namespace Jxqy.UnityAdapters
{
    public enum JxqyCombatFloatTextKind
    {
        Miss,
        Damage,
        Healing,
    }

    /// <summary>
    /// A single short-lived combat float text. Instances are owned by the
    /// TEngine object-pool module and contain no combat branching logic.
    /// </summary>
    public sealed class JxqyCombatFloatTextView : MonoBehaviour
    {
        private const float Lifetime = 0.85f;
        private RectTransform _rect;
        private Text _value;
        private Color _baseColor;
        private float _elapsed;

        public JxqyCharacter Target { get; private set; }
        public bool Finished => _elapsed >= Lifetime;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
            _value = FindComponent<Text>("m_text_Value") ??
                     GetComponentInChildren<Text>(true);
        }

        public void Play(
            JxqyCharacter target,
            string value,
            Color color)
        {
            Target = target ??
                     throw new ArgumentNullException(nameof(target));
            _elapsed = 0f;
            _baseColor = color;
            if (_value != null)
            {
                _value.text = value ?? string.Empty;
                _value.color = color;
            }
            gameObject.SetActive(true);
        }

        public void SetPosition(Vector2 anchoredPosition, bool visible)
        {
            if (_rect != null)
                _rect.anchoredPosition = anchoredPosition;
            if (gameObject.activeSelf != visible)
                gameObject.SetActive(visible);
        }

        public void Tick(float elapsedSeconds)
        {
            _elapsed += Mathf.Max(0f, elapsedSeconds);
            if (_value == null)
                return;
            float progress = Mathf.Clamp01(_elapsed / Lifetime);
            _value.rectTransform.anchoredPosition =
                new Vector2(0f, progress * 26f);
            Color color = _baseColor;
            color.a *= 1f - Mathf.Clamp01((progress - 0.55f) / 0.45f);
            _value.color = color;
        }

        public void ResetView()
        {
            Target = null;
            _elapsed = 0f;
            if (_value != null)
            {
                _value.text = string.Empty;
                _value.rectTransform.anchoredPosition = Vector2.zero;
            }
            gameObject.SetActive(false);
        }

        private T FindComponent<T>(string childName)
            where T : Component
        {
            foreach (T component in GetComponentsInChildren<T>(true))
            {
                if (component.name == childName)
                    return component;
            }
            return null;
        }
    }

    /// <summary>
    /// Unified combat-float-text presentation. Damage and healing arrive via
    /// character events; misses use the same Show entry point. Each displayed
    /// text is spawned and returned through TEngine's object-pool module.
    /// </summary>
    public sealed class JxqyCombatFloatTextPool : IDisposable
    {
        private const string PrefabAddress =
            "jxqy/ui/prefabs/jxqycombatfloattextview.prefab";
        private const int PoolCapacity = 32;
        private const float VerticalOffset = 58f;
        private static int _nextPoolId;

        private readonly List<FloatTextObject> _active = new();
        private readonly HashSet<JxqyCharacter> _subscribed = new();
        private readonly HashSet<JxqyCharacter> _visibleCharacters = new();
        private readonly List<JxqyCharacter> _releaseBuffer = new();
        private RectTransform _canvasRect;
        private Transform _poolRoot;
        private IObjectPoolModule _objectPoolModule;
        private IObjectPool<FloatTextObject> _pool;
        private IResourceModule _resourceModule;
        private CancellationToken _lifetimeToken;
        private bool _disposed;

        public UniTask InitializeAsync(
            Transform owner,
            CancellationToken cancellationToken)
        {
            if (owner == null)
                throw new ArgumentNullException(nameof(owner));
            var canvasObject = new GameObject(
                "JxqyCombatFloatTextCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));
            canvasObject.layer = LayerMask.NameToLayer("UI");
            canvasObject.transform.SetParent(owner, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = -10;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(640f, 480f);
            scaler.screenMatchMode =
                CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            _canvasRect = canvasObject.GetComponent<RectTransform>();

            var poolObject = new GameObject(
                "CombatFloatTextPool",
                typeof(RectTransform));
            poolObject.layer = canvasObject.layer;
            poolObject.transform.SetParent(canvasObject.transform, false);
            _poolRoot = poolObject.transform;
            RectTransform poolRect = poolObject.GetComponent<RectTransform>();
            poolRect.anchorMin = Vector2.zero;
            poolRect.anchorMax = Vector2.one;
            poolRect.offsetMin = Vector2.zero;
            poolRect.offsetMax = Vector2.zero;
            _lifetimeToken = poolObject.GetCancellationTokenOnDestroy();

            _objectPoolModule =
                ModuleSystem.GetModule<IObjectPoolModule>() ??
                throw new InvalidOperationException(
                    "TEngine object-pool module is unavailable.");
            _resourceModule =
                ModuleSystem.GetModule<IResourceModule>() ??
                throw new InvalidOperationException(
                    "TEngine resource module is unavailable.");
            _pool =
                _objectPoolModule.CreateSingleSpawnObjectPool<
                    FloatTextObject>(
                    "Jxqy Combat Float Text " +
                    Interlocked.Increment(ref _nextPoolId),
                    PoolCapacity,
                    30f);
            return UniTask.CompletedTask;
        }

        public void Synchronize(
            JxqyPlayer player,
            IReadOnlyList<JxqyNpc> npcs)
        {
            if (_disposed)
                return;
            _visibleCharacters.Clear();
            if (player != null)
                _visibleCharacters.Add(player);
            if (npcs != null)
            {
                foreach (JxqyNpc npc in npcs)
                {
                    if (npc != null && npc.IsVisible)
                        _visibleCharacters.Add(npc);
                }
            }

            _releaseBuffer.Clear();
            foreach (JxqyCharacter character in _subscribed)
            {
                if (!_visibleCharacters.Contains(character))
                    _releaseBuffer.Add(character);
            }
            foreach (JxqyCharacter character in _releaseBuffer)
                Unsubscribe(character);

            foreach (JxqyCharacter character in _visibleCharacters)
            {
                if (_subscribed.Add(character))
                {
                    character.Damaged += OnDamaged;
                    character.Healed += OnHealed;
                }
            }
        }

        public void UpdateVisuals(Camera worldCamera, float elapsedSeconds)
        {
            if (_disposed || worldCamera == null || _canvasRect == null)
                return;
            for (int index = _active.Count - 1; index >= 0; index--)
            {
                FloatTextObject pooled = _active[index];
                JxqyCombatFloatTextView view = pooled.View;
                if (view == null || view.Target == null)
                {
                    RecycleAt(index);
                    continue;
                }
                JxqyFloat2 position = view.Target.PositionInWorld;
                Vector3 screen = worldCamera.WorldToScreenPoint(
                    new Vector3(position.X, -position.Y, 0f));
                bool visible = screen.z > 0f &&
                               screen.x >= -32f &&
                               screen.y >= -32f &&
                               screen.x <= Screen.width + 32f &&
                               screen.y <= Screen.height + 96f;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvasRect,
                    screen,
                    null,
                    out Vector2 local);
                view.SetPosition(
                    local + new Vector2(0f, VerticalOffset),
                    visible);
                view.Tick(elapsedSeconds);
                if (view.Finished)
                    RecycleAt(index);
            }
        }

        public void ShowMiss(JxqyCharacter target)
        {
            Show(target, JxqyCombatFloatTextKind.Miss, 0);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            foreach (JxqyCharacter character in _subscribed)
            {
                character.Damaged -= OnDamaged;
                character.Healed -= OnHealed;
            }
            _subscribed.Clear();
            _visibleCharacters.Clear();
            if (_pool != null)
            {
                for (int index = _active.Count - 1; index >= 0; index--)
                    _pool.Unspawn(_active[index]);
            }
            _active.Clear();
            if (_pool != null && _objectPoolModule != null)
                _objectPoolModule.DestroyObjectPool(_pool);
            _pool = null;
            _objectPoolModule = null;
            _resourceModule = null;
            if (_canvasRect != null)
                UnityEngine.Object.Destroy(_canvasRect.gameObject);
            _canvasRect = null;
            _poolRoot = null;
        }

        private void OnDamaged(
            JxqyCharacter target,
            int amount,
            JxqyCharacter attacker)
        {
            if (amount > 0)
                Show(target, JxqyCombatFloatTextKind.Damage, amount);
        }

        private void OnHealed(JxqyCharacter target, int amount)
        {
            if (amount > 0)
                Show(target, JxqyCombatFloatTextKind.Healing, amount);
        }

        private void Show(
            JxqyCharacter target,
            JxqyCombatFloatTextKind kind,
            int amount)
        {
            if (_disposed || target == null || _pool == null)
                return;
            bool friendlyTarget =
                target.Kind == JxqyCharacterKind.Player ||
                target.Kind == JxqyCharacterKind.Follower ||
                target.Relation == JxqyRelationType.Friend;
            string value;
            Color color;
            switch (kind)
            {
                case JxqyCombatFloatTextKind.Damage:
                    value = $"-{Math.Max(0, amount)}";
                    color = friendlyTarget
                        ? new Color32(235, 48, 42, 255)
                        : new Color32(255, 178, 38, 255);
                    break;
                case JxqyCombatFloatTextKind.Healing:
                    value = $"+{Math.Max(0, amount)}";
                    color = friendlyTarget
                        ? new Color32(52, 210, 82, 255)
                        : new Color32(193, 105, 255, 255);
                    break;
                default:
                    value = "MISS";
                    color = friendlyTarget
                        ? Color.white
                        : new Color32(82, 213, 255, 255);
                    break;
            }
            ShowAsync(target, value, color).Forget();
        }

        private async UniTaskVoid ShowAsync(
            JxqyCharacter target,
            string value,
            Color color)
        {
            try
            {
                FloatTextObject pooled = _pool.CanSpawn()
                    ? _pool.Spawn()
                    : await LoadAndRegisterAsync(_lifetimeToken);
                if (_disposed || pooled == null)
                    return;
                pooled.View.Play(target, value, color);
                _active.Add(pooled);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"JXQY combat float text failed: {exception.Message}");
            }
        }

        private async UniTask<FloatTextObject> LoadAndRegisterAsync(
            CancellationToken cancellationToken)
        {
            GameObject instance =
                await _resourceModule.LoadGameObjectAsync(
                    PrefabAddress,
                    _poolRoot,
                    cancellationToken,
                    JxqyResourceLocations.PackageName);
            if (instance == null)
                throw new InvalidOperationException(
                    $"Could not load {PrefabAddress}.");
            JxqyCombatFloatTextView view =
                instance.GetComponent<JxqyCombatFloatTextView>() ??
                instance.AddComponent<JxqyCombatFloatTextView>();
            FloatTextObject pooled = FloatTextObject.Create(view);
            _pool.Register(pooled, spawned: true);
            return pooled;
        }

        private void RecycleAt(int index)
        {
            FloatTextObject pooled = _active[index];
            _active.RemoveAt(index);
            _pool?.Unspawn(pooled);
        }

        private void Unsubscribe(JxqyCharacter character)
        {
            if (character == null || !_subscribed.Remove(character))
                return;
            character.Damaged -= OnDamaged;
            character.Healed -= OnHealed;
        }

        private sealed class FloatTextObject : ObjectBase
        {
            public JxqyCombatFloatTextView View { get; private set; }

            public static FloatTextObject Create(
                JxqyCombatFloatTextView view)
            {
                FloatTextObject result =
                    MemoryPool.Acquire<FloatTextObject>();
                result.Initialize(view.gameObject);
                result.View = view;
                return result;
            }

            public override void Clear()
            {
                base.Clear();
                View = null;
            }

            protected override void OnSpawn()
            {
                View?.gameObject.SetActive(true);
            }

            protected override void OnUnspawn()
            {
                View?.ResetView();
            }

            protected override void Release(bool isShutdown)
            {
                if (View == null || View.gameObject == null)
                    return;
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(View.gameObject);
                else
                    UnityEngine.Object.DestroyImmediate(View.gameObject);
                View = null;
            }
        }
    }
}
