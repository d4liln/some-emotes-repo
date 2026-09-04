using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SomeEmotesREPO
{
    public class EmoteWheel : MonoBehaviour
    {
        public const int Slots = 8;
        private const float DeadZone = 0.3f;

        private const float RadiusFraction = 0.26f;
        private const float CardWidthFraction = 0.155f;
        private const float CardHeightFraction = 0.052f;

        private static readonly Color Dim = new Color(0f, 0f, 0f, 0.45f);
        private static readonly Color CardFill = new Color(0.07f, 0.08f, 0.09f, 0.92f);
        private static readonly Color CardEdge = new Color(1f, 1f, 1f, 0.13f);
        private static readonly Color PickedFill = new Color(0.18f, 0.62f, 0.42f, 0.96f);
        private static readonly Color PickedEdge = new Color(0.55f, 0.95f, 0.75f, 0.9f);
        private static readonly Color Favourite = new Color(0.93f, 0.72f, 0.25f, 1f);
        private static readonly Color Faded = new Color(0.72f, 0.75f, 0.78f, 1f);

        private static EmoteWheel? instance;
        public static EmoteWheel? Instance => instance;

        private readonly List<string> _page = new List<string>(Slots);

        private bool _open;
        private Vector2 _aim;
        private int _pageIndex;
        private int _picked = -1;
        private bool _wasHeld;

        private Texture2D? _white;
        private GUIStyle? _cardStyle;
        private GUIStyle? _titleStyle;
        private GUIStyle? _hintStyle;

        public bool Visible => _open;

        private void Awake()
        {
            instance = this;
            SomeEmotesREPO.Logger.LogInfo("Emote wheel ready.");
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
            if (_white != null) Destroy(_white);
        }

        public void Close()
        {
            _open = false;
            _picked = -1;
        }

        private void Update()
        {
            var emotes = EmoteSystem.Instance;
            if (emotes == null || !EmoteCatalog.Loaded)
            {
                Close();
                _wasHeld = false;
                return;
            }

            bool allowed = CanOpen(emotes);
            bool held = allowed && Input.GetKey(SomeEmotesREPO.EmoteKeyCode);

            if (held && !_wasHeld)
            {
                Open();
            }
            else if (!held && _wasHeld)
            {

                if (allowed) Release(emotes);
                else Close();
            }

            _wasHeld = held;
            if (!_open) return;

            if (InputManager.instance != null)
            {
                InputManager.instance.disableAimingTimer = 0.1f;
                InputManager.instance.DisableMovement();
            }

            Paging();
            Aim();

            if (_picked >= 0 && Input.GetMouseButtonDown(1))
            {
                emotes.SetFavorite(_page[_picked]);
                Rebuild();
            }
        }

        private static bool CanOpen(EmoteSystem emotes)
        {
            if (emotes.IsDead) return false;

            return SemiFunc.NoTextInputsActive();
        }

        private void Open()
        {
            _open = true;
            _aim = Vector2.zero;
            _picked = -1;

            _pageIndex = Mathf.Clamp(_pageIndex, 0, PageCount() - 1);
            Rebuild();
        }

        private void Release(EmoteSystem emotes)
        {
            bool picked = _open && _picked >= 0 && _picked < _page.Count;
            string name = picked ? _page[_picked] : string.Empty;

            Close();
            if (picked) emotes.PlayEmote(name);
        }

        private void Aim()
        {
            Vector2 delta = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
            _aim = Vector2.ClampMagnitude(_aim + delta * SomeEmotesREPO.WheelSensitivity.Value, 1f);

            if (_aim.magnitude < DeadZone)
            {
                _picked = -1;
                return;
            }
            float degrees = Mathf.Atan2(_aim.x, _aim.y) * Mathf.Rad2Deg;
            int slot = Mathf.RoundToInt(degrees / (360f / Slots));
            slot = (slot % Slots + Slots) % Slots;

            _picked = slot < _page.Count ? slot : -1;
        }

        private void Paging()
        {
            float scroll = Input.mouseScrollDelta.y;
            if (scroll == 0f) return;

            int pages = PageCount();
            if (pages <= 1) return;

            int step = scroll > 0f ? -1 : 1;
            _pageIndex = (_pageIndex + step + pages) % pages;
            Rebuild();
        }

        private static int PageCount()
        {
            return Mathf.Max(1, Mathf.CeilToInt(EmoteCatalog.Count / (float)Slots));
        }

        private void Rebuild()
        {
            _page.Clear();
            _picked = -1;

            var all = EmoteLoader.DisplayOrder();
            int start = _pageIndex * Slots;
            for (int i = start; i < all.Count && _page.Count < Slots; i++)
            {
                _page.Add(all[i]);
            }
        }

        private void OnGUI()
        {
            if (!_open || _page.Count == 0) return;

            EnsureStyles();

            float height = Screen.height;
            var centre = new Vector2(Screen.width * 0.5f, height * 0.5f);
            float radius = height * RadiusFraction;
            float cardWidth = Screen.width * CardWidthFraction;
            float cardHeight = height * CardHeightFraction;

            GUI.color = Dim;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, height), _white);
            GUI.color = Color.white;

            var favourites = new HashSet<string>(EmoteLoader.Favourites());

            for (int i = 0; i < _page.Count; i++)
            {
                float angle = i * (360f / Slots) * Mathf.Deg2Rad;
                var position = new Vector2(
                    centre.x + Mathf.Sin(angle) * radius,
                    centre.y - Mathf.Cos(angle) * radius);

                var card = new Rect(
                    position.x - cardWidth * 0.5f,
                    position.y - cardHeight * 0.5f,
                    cardWidth,
                    cardHeight);

                DrawCard(card, _page[i], i == _picked, favourites.Contains(_page[i]));
            }

            DrawCentre(centre, radius);
        }

        private void DrawCard(Rect card, string emote, bool picked, bool favourite)
        {
            if (picked) card = Grow(card, card.height * 0.14f);

            GUI.color = picked ? PickedFill : CardFill;
            GUI.DrawTexture(card, _white);

            GUI.color = picked ? PickedEdge : CardEdge;
            DrawEdge(card, picked ? 2f : 1f);

            if (favourite)
            {
                GUI.color = Favourite;
                GUI.DrawTexture(new Rect(card.x, card.y, 3f, card.height), _white);
            }

            GUI.color = Color.white;
            _cardStyle!.normal.textColor = picked ? Color.white : Faded;
            GUI.Label(card, Pretty(emote), _cardStyle);
        }

        private void DrawCentre(Vector2 centre, float radius)
        {
            float width = radius * 1.15f;

            var title = new Rect(centre.x - width * 0.5f, centre.y - 26f, width, 34f);
            bool valid = _picked >= 0 && _picked < _page.Count;

            GUI.color = Color.white;
            _titleStyle!.normal.textColor = valid ? Color.white : Faded;
            GUI.Label(title, valid ? Pretty(_page[_picked]) : "Release to cancel", _titleStyle);

            int pages = PageCount();
            string hint = pages > 1
                ? $"Page {_pageIndex + 1}/{pages}   Scroll to turn   -   Right click to favourite"
                : "Right click to favourite";

            var line = new Rect(centre.x - width * 0.5f, centre.y + 10f, width, 22f);
            _hintStyle!.normal.textColor = Faded;
            GUI.Label(line, hint, _hintStyle);
        }

        private static string Pretty(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;

            var text = name.ToCharArray();
            bool startOfWord = true;
            for (int i = 0; i < text.Length; i++)
            {
                if (startOfWord) text[i] = char.ToUpperInvariant(text[i]);
                startOfWord = text[i] == ' ';
            }
            return new string(text);
        }

        private static Rect Grow(Rect r, float by)
        {
            return new Rect(r.x - by, r.y - by, r.width + by * 2f, r.height + by * 2f);
        }

        private void DrawEdge(Rect r, float thickness)
        {
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, thickness), _white);
            GUI.DrawTexture(new Rect(r.x, r.yMax - thickness, r.width, thickness), _white);
            GUI.DrawTexture(new Rect(r.x, r.y, thickness, r.height), _white);
            GUI.DrawTexture(new Rect(r.xMax - thickness, r.y, thickness, r.height), _white);
        }

        private void EnsureStyles()
        {
            if (_white == null)
            {
                _white = new Texture2D(1, 1);
                _white.SetPixel(0, 0, Color.white);
                _white.Apply();
            }

            if (_cardStyle != null) return;

            Font? font = EmoteLoader.GetFont();
            int scale = Mathf.RoundToInt(Screen.height / 54f);

            _cardStyle = new GUIStyle
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = scale,
                wordWrap = false,
                clipping = TextClipping.Clip,
            };
            _titleStyle = new GUIStyle
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.RoundToInt(scale * 1.5f),
            };
            _hintStyle = new GUIStyle
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.RoundToInt(scale * 0.8f),
            };

            if (font != null)
            {
                _cardStyle.font = font;
                _titleStyle.font = font;
                _hintStyle.font = font;
            }
        }
    }

    [HarmonyPatch(typeof(InputManager), nameof(InputManager.KeyDown))]
    internal static class EmoteWheelScrollPatch
    {
        private static bool Prefix(InputKey key, ref bool __result)
        {
            if (key != InputKey.Push && key != InputKey.Pull) return true;
            if (EmoteWheel.Instance == null || !EmoteWheel.Instance.Visible) return true;

            __result = false;
            return false;
        }
    }
}
