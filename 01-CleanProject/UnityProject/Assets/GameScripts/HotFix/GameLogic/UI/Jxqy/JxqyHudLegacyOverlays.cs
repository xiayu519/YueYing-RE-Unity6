using System;
using System.Collections.Generic;
using Jxqy.Domain.Presentation;
using Jxqy.Domain.Simulation;
using Jxqy.Domain.World;
using TEngine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameLogic
{
    [Window(
        UILayer.Bottom,
        location: "jxqy/ui/prefabs/jxqypartnerheadsui.prefab",
        packageName: "JxqyPackage")]
    public sealed class JxqyPartnerHeadsUI : JxqySessionWindow
    {
        private sealed class PartnerHeadView
        {
            public GameObject Root;
            public RectTransform Rect;
            public RawImage Image;
            public Text Level;
            public JxqyUiAnimationBinding Binding;
            public JxqyNpc Npc;
        }

        private readonly List<PartnerHeadView> _partnerHeads = new();
        private GameObject _partnerHeadTemplate;

        protected override void ScriptGenerator()
        {
            Transform template = FindChild("m_item_PartnerHeadTemplate");
            _partnerHeadTemplate = template?.gameObject;
            if (_partnerHeadTemplate == null)
            {
                throw new InvalidOperationException(
                    "JxqyPartnerHeadsUI requires its prefab head template.");
            }
            _partnerHeadTemplate.SetActive(false);
        }

        protected override void RefreshView()
        {
            RefreshLegacyPartners();
        }

        protected override void OnUpdate()
        {
            for (int index = 0; index < _partnerHeads.Count; index++)
                _partnerHeads[index].Binding?.Tick(Time.unscaledDeltaTime);
            RefreshLegacyPartners();
        }

        protected override void OnDestroy()
        {
            DisposeLegacyPartnerHeads();
        }

        private void RefreshLegacyPartners()
        {
            IReadOnlyList<JxqyNpc> npcs = Session?.Npcs;
            int count = 0;
            if (npcs != null)
            {
                for (int index = 0; index < npcs.Count; index++)
                {
                    JxqyNpc npc = npcs[index];
                    if (npc == null || npc.Kind != JxqyCharacterKind.Follower ||
                        !npc.IsVisible || npc.IsDead)
                    {
                        continue;
                    }
                    PartnerHeadView view = GetPartnerHead(count);
                    BindPartnerHead(view, npc, count);
                    count++;
                }
            }
            for (int index = count; index < _partnerHeads.Count; index++)
                _partnerHeads[index].Root.SetActive(false);
        }

        private void DisposeLegacyPartnerHeads()
        {
            for (int index = 0; index < _partnerHeads.Count; index++)
                _partnerHeads[index].Binding?.Dispose();
            _partnerHeads.Clear();
        }

        private PartnerHeadView GetPartnerHead(int index)
        {
            while (_partnerHeads.Count <= index)
            {
                var view = new PartnerHeadView();
                view.Root = UnityEngine.Object.Instantiate(
                    _partnerHeadTemplate,
                    rectTransform,
                    false);
                view.Root.name =
                    $"m_item_PartnerHead{_partnerHeads.Count}";
                view.Root.SetActive(true);
                view.Rect = view.Root.GetComponent<RectTransform>();
                view.Image = view.Root.GetComponent<RawImage>();
                view.Binding = new JxqyUiAnimationBinding(view.Image);
                view.Level = FindChild(
                    view.Root.transform,
                    "m_text_Level")?.GetComponent<Text>();
                JxqyPointerClickRelay clickRelay =
                    view.Root.GetComponent<JxqyPointerClickRelay>();
                if (view.Image == null || view.Level == null ||
                    clickRelay == null)
                {
                    throw new InvalidOperationException(
                        "Partner head template is missing RawImage, level " +
                        "text, or click relay.");
                }
                clickRelay.Clicked =
                    eventData =>
                    {
                        if (eventData.button ==
                            PointerEventData.InputButton.Left)
                        {
                            Session?.OpenPartnerEquipment(view.Npc);
                        }
                    };
                _partnerHeads.Add(view);
            }
            return _partnerHeads[index];
        }

        private static void BindPartnerHead(
            PartnerHeadView view,
            JxqyNpc npc,
            int index)
        {
            view.Root.SetActive(true);
            view.Rect.anchoredPosition = new Vector2(5f, -5f - index * 38f);
            if (!ReferenceEquals(view.Npc, npc))
            {
                view.Npc = npc;
                view.Binding.Set("littlehead", $"{npc.Name}.asf");
            }
            view.Level.text = npc.CanLevelUp > 0
                ? $"LV{npc.Level}"
                : string.Empty;
        }
    }

    [Window(
        UILayer.UI,
        location: "jxqy/ui/prefabs/jxqylittlemapui.prefab",
        packageName: "JxqyPackage")]
    public sealed class JxqyLittleMapUI : JxqySessionWindow
    {
        private const int LittleMapRatio = 4;
        private const float LittleMapWidth = 320f;
        private const float LittleMapHeight = 240f;

        private GameObject _littleMapGroup;
        private RectTransform _littleMapViewport;
        private RawImage _littleMapImage;
        private JxqyUiTextureBinding _littleMapTexture;
        private JxqyUiAnimationBinding _littleMapPanel;
        private readonly List<JxqyUiFrameBinding> _littleMapButtons = new();
        private readonly List<RawImage> _littleMapMarkers = new();
        private readonly List<JxqyUiAnimationBinding>
            _littleMapMarkerBindings = new();
        private readonly RawImage[] _littleMapMarkerSources = new RawImage[4];
        private GameObject _littleMapMarkerTemplate;
        private Text _littleMapName;
        private Text _littleMapTip;
        private int _littleMapViewX;
        private int _littleMapViewY;
        private bool _viewInitialized;

        protected override void ScriptGenerator()
        {
            BindLittleMapView();
        }

        protected override void RefreshView()
        {
            if (Session == null || _littleMapTexture == null)
                return;
            if (!_viewInitialized)
            {
                _littleMapViewX = Math.Max(0, Session.LittleMapViewX);
                _littleMapViewY = Math.Max(0, Session.LittleMapViewY);
                _littleMapTexture.Set(Session.LittleMapTextureAddress);
                SetMapTip("点击小地图进行移动", false);
                _viewInitialized = true;
            }
            _littleMapName.text = Session.LittleMapName ?? string.Empty;
            ClampLittleMapView();
            ApplyLittleMapUv();
            RefreshLittleMapMarkers();
        }

        protected override void OnUpdate()
        {
            float elapsedSeconds = Time.unscaledDeltaTime;
            _littleMapPanel?.Tick(elapsedSeconds);
            for (int index = 0;
                 index < _littleMapMarkerBindings.Count;
                 index++)
            {
                _littleMapMarkerBindings[index].Tick(elapsedSeconds);
            }
#if !UNITY_ANDROID && !UNITY_IOS
            if (Input.GetKey(KeyCode.LeftArrow))
                PanLittleMap(-8, 0);
            if (Input.GetKey(KeyCode.RightArrow))
                PanLittleMap(8, 0);
            if (Input.GetKey(KeyCode.UpArrow))
                PanLittleMap(0, -4);
            if (Input.GetKey(KeyCode.DownArrow))
                PanLittleMap(0, 4);
#endif
            RefreshView();
        }

        protected override void OnDestroy()
        {
            _littleMapTexture?.Dispose();
            _littleMapTexture = null;
            _littleMapPanel?.Dispose();
            _littleMapPanel = null;
            for (int index = 0; index < _littleMapButtons.Count; index++)
                _littleMapButtons[index].Dispose();
            _littleMapButtons.Clear();
            for (int index = 0;
                 index < _littleMapMarkerBindings.Count;
                 index++)
            {
                _littleMapMarkerBindings[index].Dispose();
            }
            _littleMapMarkerBindings.Clear();
            _littleMapMarkers.Clear();
        }

        private void BindLittleMapView()
        {
            Transform group = FindChild("m_group_LittleMap");
            _littleMapGroup = group?.gameObject;
            RawImage panelImage = FindChildComponent<RawImage>(
                "m_group_LittleMap/m_raw_Panel");
            _littleMapViewport = FindChildComponent<RectTransform>(
                "m_group_LittleMap/m_raw_Map");
            _littleMapImage = FindChildComponent<RawImage>(
                "m_group_LittleMap/m_raw_Map");
            _littleMapName = FindChildComponent<Text>(
                "m_group_LittleMap/m_text_MapName");
            _littleMapTip = FindChildComponent<Text>(
                "m_group_LittleMap/m_text_MapTip");
            _littleMapMarkerTemplate = FindChild(
                "m_group_LittleMap/m_raw_Map/m_raw_MarkerTemplate")?
                .gameObject;
            JxqyPointerClickRelay mapClick = _littleMapViewport?
                .GetComponent<JxqyPointerClickRelay>();
            if (_littleMapGroup == null || panelImage == null ||
                _littleMapViewport == null || _littleMapImage == null ||
                _littleMapName == null || _littleMapTip == null ||
                _littleMapMarkerTemplate == null || mapClick == null)
            {
                throw new InvalidOperationException(
                    "JxqyLittleMapUI prefab hierarchy is incomplete.");
            }

            _littleMapPanel = new JxqyUiAnimationBinding(panelImage);
            _littleMapPanel.Set(
                "littlemap", "panel.asf", preserveNativeSize: false);
            _littleMapTexture = new JxqyUiTextureBinding(_littleMapImage);
            mapClick.Clicked = OnLittleMapClicked;
            _littleMapMarkerTemplate.SetActive(false);

            BindLittleMapButton("Left", "btnleft.asf",
                () => PanLittleMap(-8, 0), true);
            BindLittleMapButton("Right", "btnright.asf",
                () => PanLittleMap(8, 0), true);
            BindLittleMapButton("Up", "btnup.asf",
                () => PanLittleMap(0, -4), true);
            BindLittleMapButton("Down", "btndown.asf",
                () => PanLittleMap(0, 4), true);
            BindLittleMapButton("Close", "btnclose.asf",
                CloseLittleMap, false);

            string[] markerFiles =
            {
                "主角坐标.asf",
                "敌人坐标.asf",
                "同伴坐标.asf",
                "路人坐标.asf",
            };
            for (int index = 0; index < markerFiles.Length; index++)
            {
                RawImage source = FindChildComponent<RawImage>(
                    $"m_group_LittleMap/m_raw_MarkerSource{index}");
                if (source == null)
                {
                    throw new InvalidOperationException(
                        $"Little-map marker source {index} is missing.");
                }
                _littleMapMarkerSources[index] = source;
                var binding = new JxqyUiAnimationBinding(source);
                binding.Set("littlemap", markerFiles[index], false);
                _littleMapMarkerBindings.Add(binding);
            }
        }

        private void ApplyLittleMapUv()
        {
            Texture2D texture = _littleMapTexture?.Texture;
            if (texture == null || _littleMapImage == null)
                return;
            float width = Math.Max(1f, texture.width);
            float height = Math.Max(1f, texture.height);
            _littleMapImage.uvRect = new Rect(
                _littleMapViewX / width,
                1f - (_littleMapViewY + LittleMapHeight) / height,
                Math.Min(LittleMapWidth, width) / width,
                Math.Min(LittleMapHeight, height) / height);
        }

        private void ClampLittleMapView()
        {
            Texture2D texture = _littleMapTexture?.Texture;
            if (texture == null)
                return;
            _littleMapViewX = Mathf.Clamp(
                _littleMapViewX, 0,
                Math.Max(0, texture.width - (int)LittleMapWidth));
            _littleMapViewY = Mathf.Clamp(
                _littleMapViewY, 0,
                Math.Max(0, texture.height - (int)LittleMapHeight));
        }

        private void PanLittleMap(int x, int y)
        {
            if (Session?.CurrentScreen != JxqyUiScreen.LittleMap)
                return;
            _littleMapViewX += x;
            _littleMapViewY += y;
            ClampLittleMapView();
            ApplyLittleMapUv();
        }

        private void OnLittleMapClicked(PointerEventData eventData)
        {
            if (_littleMapViewport == null || Session == null ||
                eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _littleMapViewport,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 local);
            float mapX = _littleMapViewX + local.x;
            float mapY = _littleMapViewY - local.y;
            bool accepted = Session.TryMoveFromLittleMap?.Invoke(
                new JxqyFloat2(
                    mapX * LittleMapRatio,
                    mapY * LittleMapRatio),
                Session.IsRunModifierHeld?.Invoke() ?? false) ?? false;
            if (accepted)
            {
                CloseLittleMap();
                return;
            }
            SetMapTip("无法移动到目的地", true);
        }

        private void CloseLittleMap()
        {
            Session?.Open(JxqyUiScreen.Hud);
        }

        private void SetMapTip(string message, bool error)
        {
            if (_littleMapTip == null)
                return;
            _littleMapTip.text = message;
            _littleMapTip.alignment = error
                ? TextAnchor.MiddleRight
                : TextAnchor.MiddleLeft;
            _littleMapTip.color = error
                ? new Color32(220, 30, 30, 230)
                : new Color32(76, 56, 48, 204);
        }

        private void RefreshLittleMapMarkers()
        {
            int markerCount = 0;
            AddLittleMapMarker(Session?.Player, 0, ref markerCount);
            IReadOnlyList<JxqyNpc> npcs = Session?.Npcs;
            if (npcs != null)
            {
                for (int index = 0; index < npcs.Count; index++)
                {
                    JxqyNpc npc = npcs[index];
                    if (npc == null ||
                        npc.Kind == JxqyCharacterKind.GroundAnimal ||
                        npc.Kind == JxqyCharacterKind.Flyer)
                    {
                        continue;
                    }
                    int kind = npc.Relation == JxqyRelationType.Enemy
                        ? 1
                        : npc.Kind == JxqyCharacterKind.Follower ? 2 : 3;
                    AddLittleMapMarker(npc, kind, ref markerCount);
                }
            }
            for (int index = markerCount;
                 index < _littleMapMarkers.Count;
                 index++)
            {
                _littleMapMarkers[index].gameObject.SetActive(false);
            }
        }

        private void AddLittleMapMarker(
            JxqyCharacter character,
            int kind,
            ref int markerCount)
        {
            if (character == null)
                return;
            float mapX = character.PositionInWorld.X / LittleMapRatio -
                         _littleMapViewX;
            float mapY = character.PositionInWorld.Y / LittleMapRatio -
                         _littleMapViewY;
            if (mapX < 0f || mapY < 0f ||
                mapX >= LittleMapWidth || mapY >= LittleMapHeight)
            {
                return;
            }
            RawImage marker = GetLittleMapMarker(markerCount++);
            RawImage source = _littleMapMarkerSources[kind];
            marker.texture = source.texture;
            marker.uvRect = source.uvRect;
            marker.color = source.color;
            marker.rectTransform.anchoredPosition = new Vector2(mapX, -mapY);
            marker.gameObject.SetActive(true);
        }

        private RawImage GetLittleMapMarker(int index)
        {
            while (_littleMapMarkers.Count <= index)
            {
                GameObject markerObject = UnityEngine.Object.Instantiate(
                    _littleMapMarkerTemplate,
                    _littleMapViewport,
                    false);
                markerObject.name =
                    $"m_raw_Marker{_littleMapMarkers.Count}";
                markerObject.SetActive(true);
                RawImage marker = markerObject.GetComponent<RawImage>();
                if (marker == null)
                    throw new InvalidOperationException(
                        "Little-map marker template has no RawImage.");
                _littleMapMarkers.Add(marker);
            }
            return _littleMapMarkers[index];
        }

        private void BindLittleMapButton(
            string name,
            string fileName,
            Action clicked,
            bool repeatWhileHeld)
        {
            GameObject buttonObject = FindChild(
                $"m_group_LittleMap/m_btn_{name}")?.gameObject;
            if (buttonObject == null)
                throw new InvalidOperationException(
                    $"Little-map button {name} is missing.");
            RawImage image = buttonObject.GetComponent<RawImage>();
            Button button = buttonObject.GetComponent<Button>();
            if (image == null || button == null)
                throw new InvalidOperationException(
                    $"Little-map button {name} is incomplete.");
            if (repeatWhileHeld)
            {
                JxqyPointerHoldRelay relay =
                    buttonObject.GetComponent<JxqyPointerHoldRelay>();
                if (relay == null)
                    throw new InvalidOperationException(
                        $"Little-map button {name} has no hold relay.");
                relay.Pressed = () =>
                {
                    Session?.RequestSound(JxqyUiSound.Browse);
                    clicked?.Invoke();
                };
                relay.Held = clicked;
            }
            else
            {
                button.onClick.AddListener(() =>
                {
                    Session?.RequestSound(JxqyUiSound.Browse);
                    clicked?.Invoke();
                });
            }
            var binding = new JxqyUiFrameBinding(image);
            binding.Set("littlemap", fileName);
            _littleMapButtons.Add(binding);
        }
    }
}
