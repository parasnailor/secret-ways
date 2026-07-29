using System.Collections.Generic;
using SecretHistories.Entities;
using SecretHistories.Enums;
using SecretHistories.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BookOfHoursLocationHotkeys
{
	/// <summary>
	/// Everything the mod knows about looking like Book of Hours: which canvas to
	/// build on, which font the current culture wants, the game's palette, its UI
	/// sounds, and the handful of sprites we have to draw ourselves.
	/// </summary>
	public static class NativeUi
	{
		/// <summary>
		/// Dark, warm and translucent, like the game's own text panels. The game's
		/// real panel colours are serialised onto prefabs rather than written in
		/// code - there are only two Color32 literals in the whole of
		/// SecretHistories.Main - so there's nothing to read at runtime. This is the
		/// value the mod's old IMGUI panel used, which was already matched by eye.
		/// </summary>
		public static readonly Color PanelBackground = new Color(0.05f, 0.04f, 0.03f, 0.88f);

		/// <summary>Same plate, dimmer, for slots with nothing saved in them.</summary>
		public static readonly Color PanelBackgroundDim = new Color(0.05f, 0.04f, 0.03f, 0.55f);

		/// <summary>Resting border. Hover swaps it for UIStyle.warmWhite.</summary>
		public static readonly Color PanelBorder = new Color(UIStyle.gold.r, UIStyle.gold.g, UIStyle.gold.b, 0.38f);

		public static readonly Color Scrim = new Color(0f, 0f, 0f, 0.45f);

		/// <summary>
		/// Sorting orders for the mod's own layers. The game never sets a sorting
		/// order anywhere in its code, so everything of its own sits at zero and
		/// these can't collide with it. The banner sits above the wheel so that a
		/// confirmation raised from the wheel isn't hidden behind its own scrim.
		/// </summary>
		public const int SortOrderWheel = 30000;

		public const int SortOrderBanner = 30010;

		private static Transform _overlayParent;

		private static readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>();

		/// <summary>
		/// The transform the game parents its own full-screen overlays to. It's a
		/// private field on Meniscate, but DisplayInOverlayAtScreenCentre reparents
		/// whatever we hand it, so a throwaway object tells us where that is.
		/// </summary>
		public static Transform GetOverlayParent()
		{
			if (_overlayParent != null)
			{
				return _overlayParent;
			}

			Meniscate meniscate = Watchman.Get<Meniscate>();
			if (meniscate != null)
			{
				GameObject probe = new GameObject("lhoverlayprobe", typeof(RectTransform));
				try
				{
					meniscate.DisplayInOverlayAtScreenCentre(probe);
					_overlayParent = probe.transform.parent;
				}
				finally
				{
					Object.Destroy(probe);
				}
			}

			// No playfield, or a Meniscate without an overlay wired up: stand up our
			// own canvas. The game never sets a sorting order anywhere, so a high one
			// can't collide with anything of its own.
			if (_overlayParent == null)
			{
				GameObject canvasObject = new GameObject("LocationHotkeysCanvas");
				Object.DontDestroyOnLoad(canvasObject);

				Canvas canvas = canvasObject.AddComponent<Canvas>();
				canvas.renderMode = RenderMode.ScreenSpaceOverlay;
				canvas.sortingOrder = 30000;

				CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
				scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
				scaler.referenceResolution = new Vector2(1920f, 1080f);
				scaler.matchWidthOrHeight = 0.5f;

				canvasObject.AddComponent<GraphicRaycaster>();
				_overlayParent = canvasObject.transform;
			}

			return _overlayParent;
		}

		/// <summary>
		/// The TMP font the current culture wants for this role. Null when the game
		/// hasn't loaded a real language manager yet - callers leave TMP's default in
		/// place rather than blanking the text.
		/// </summary>
		public static TMP_FontAsset GetFont(LanguageManager.eFontStyle style)
		{
			ILocStringProvider loc = Watchman.Get<ILocStringProvider>();
			if (loc == null)
			{
				return null;
			}

			Culture culture = loc.GetCurrentCulture();
			return loc.GetFont(style, (culture == null) ? null : culture.FontScript);
		}

		/// <summary>
		/// Solid rounded rectangle with a 9-slice border, for panel backgrounds.
		/// Sprites loaded through ModManager come back with a zero border and so
		/// can't slice, which is why these are drawn rather than shipped.
		/// </summary>
		public static Sprite RoundedRect(int radius)
		{
			return RoundedRectSprite("fill" + radius, radius, 0f);
		}

		/// <summary>The same shape as an outline, to frame a panel or a photo.</summary>
		public static Sprite RoundedRectOutline(int radius, float thickness)
		{
			return RoundedRectSprite("line" + radius + "x" + thickness, radius, thickness);
		}

		/// <summary>Thickness of zero fills the shape; anything else draws a band just
		/// inside its edge. Both slice off the same corner radius.</summary>
		private static Sprite RoundedRectSprite(string key, int radius, float thickness)
		{
			if (_sprites.TryGetValue(key, out Sprite cached) && cached != null)
			{
				return cached;
			}

			// One pixel of stretchable middle is all a 9-slice needs.
			int border = radius + Mathf.CeilToInt(thickness);
			int size = border * 2 + 2;

			Texture2D texture = NewTexture(size, size);
			Color[] pixels = new Color[size * size];
			float centre = (size - 1) / 2f;
			float extent = centre - radius;

			for (int y = 0; y < size; y++)
			{
				for (int x = 0; x < size; x++)
				{
					float distance = RoundedRectDistance(x - centre, y - centre, extent, radius);

					// Half a pixel either side of the edge gives a clean antialiased line.
					float inside = Mathf.Clamp01(0.5f - distance);
					float alpha = (thickness <= 0f)
						? inside
						: inside - Mathf.Clamp01(0.5f - (distance + thickness));

					pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
				}
			}

			texture.SetPixels(pixels);
			texture.Apply();

			Sprite sprite = Sprite.Create(
				texture,
				new Rect(0f, 0f, size, size),
				new Vector2(0.5f, 0.5f),
				100f,
				0,
				SpriteMeshType.FullRect,
				new Vector4(border, border, border, border));

			_sprites[key] = sprite;
			return sprite;
		}

		/// <summary>Signed distance to a rounded rectangle: negative inside, zero on the edge.</summary>
		private static float RoundedRectDistance(float x, float y, float extent, float radius)
		{
			float dx = Mathf.Abs(x) - extent;
			float dy = Mathf.Abs(y) - extent;
			float outside = new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dy, 0f)).magnitude;
			return outside + Mathf.Min(Mathf.Max(dx, dy), 0f) - radius;
		}

		/// <summary>An outlined circle, for the ring and the wedge frames.</summary>
		public static Sprite Ring(int size, float thickness)
		{
			string key = "ring" + size + "x" + thickness;
			if (_sprites.TryGetValue(key, out Sprite cached) && cached != null)
			{
				return cached;
			}

			Texture2D texture = NewTexture(size, size);
			Color[] pixels = new Color[size * size];
			float centre = (size - 1) / 2f;
			float outer = centre;
			float inner = centre - thickness;

			for (int y = 0; y < size; y++)
			{
				for (int x = 0; x < size; x++)
				{
					float distance = Mathf.Sqrt((x - centre) * (x - centre) + (y - centre) * (y - centre));
					// Two soft edges a pixel wide, so the curve doesn't stair-step.
					float alpha = Mathf.Clamp01(outer - distance) * Mathf.Clamp01(distance - inner);
					pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
				}
			}

			texture.SetPixels(pixels);
			texture.Apply();

			Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
			_sprites[key] = sprite;
			return sprite;
		}

		/// <summary>A filled circle, for the wheel's hub.</summary>
		public static Sprite Disc(int size)
		{
			string key = "disc" + size;
			if (_sprites.TryGetValue(key, out Sprite cached) && cached != null)
			{
				return cached;
			}

			Texture2D texture = NewTexture(size, size);
			Color[] pixels = new Color[size * size];
			float centre = (size - 1) / 2f;

			for (int y = 0; y < size; y++)
			{
				for (int x = 0; x < size; x++)
				{
					float distance = Mathf.Sqrt((x - centre) * (x - centre) + (y - centre) * (y - centre));
					pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(centre - distance));
				}
			}

			texture.SetPixels(pixels);
			texture.Apply();

			Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
			_sprites[key] = sprite;
			return sprite;
		}

		private static Texture2D NewTexture(int width, int height)
		{
			return new Texture2D(width, height, TextureFormat.RGBA32, false)
			{
				hideFlags = HideFlags.HideAndDontSave,
				filterMode = FilterMode.Bilinear,
				wrapMode = TextureWrapMode.Clamp
			};
		}

		/// <summary>
		/// Gives a rect its own nested canvas so it draws above the game's windows
		/// rather than only above its siblings. The raycaster has to come with it:
		/// graphics register against their nearest canvas, so the parent canvas's
		/// raycaster stops seeing them the moment this one is added.
		/// </summary>
		public static void SortAbove(RectTransform rect, int sortingOrder)
		{
			Canvas canvas = rect.gameObject.AddComponent<Canvas>();
			canvas.overrideSorting = true;
			canvas.sortingOrder = sortingOrder;
			rect.gameObject.AddComponent<GraphicRaycaster>();
		}

		/// <summary>A dark plate with a gold border, the mod's one panel shape.</summary>
		public class Panel
		{
			public RectTransform Root;

			public Image Fill;

			public Image Border;

			/// <summary>Put the frame back on top after adding content between the two.</summary>
			public void RaiseBorder()
			{
				Border.rectTransform.SetAsLastSibling();
			}
		}

		public static Panel MakePanel(Transform parent, string name, int radius, float borderThickness)
		{
			Panel panel = new Panel { Root = MakeRect(parent, name) };

			panel.Fill = MakeImage(panel.Root, "Fill", RoundedRect(radius), PanelBackground);
			Fill(panel.Fill.rectTransform);

			panel.Border = MakeImage(panel.Root, "Border", RoundedRectOutline(radius, borderThickness), PanelBorder);
			Fill(panel.Border.rectTransform);

			return panel;
		}

		/// <summary>
		/// A rounded window that clips whatever is put inside it, for a photo that
		/// would otherwise square off the corners of the panel it sits in. The shape
		/// is only ever stencilled, never drawn, so its colour doesn't matter.
		/// </summary>
		public static RectTransform MakeMask(Transform parent, string name, int radius)
		{
			Image shape = MakeImage(parent, name, RoundedRect(radius), Color.white);
			Mask mask = shape.gameObject.AddComponent<Mask>();
			mask.showMaskGraphic = false;
			return shape.rectTransform;
		}

		public static RectTransform MakeRect(Transform parent, string name)
		{
			GameObject go = new GameObject(name, typeof(RectTransform));
			RectTransform rect = (RectTransform)go.transform;
			rect.SetParent(parent, false);
			rect.localScale = Vector3.one;
			return rect;
		}

		public static Image MakeImage(Transform parent, string name, Sprite sprite, Color colour)
		{
			Image image = MakeRect(parent, name).gameObject.AddComponent<Image>();
			image.sprite = sprite;
			image.color = colour;
			image.type = (sprite != null && sprite.border != Vector4.zero) ? Image.Type.Sliced : Image.Type.Simple;
			image.raycastTarget = false;
			return image;
		}

		public static TextMeshProUGUI MakeText(Transform parent, string name, LanguageManager.eFontStyle style, float fontSize, Color colour)
		{
			TextMeshProUGUI text = MakeRect(parent, name).gameObject.AddComponent<TextMeshProUGUI>();

			TMP_FontAsset font = GetFont(style);
			if (font != null)
			{
				text.font = font;
			}

			text.fontSize = fontSize;
			text.color = colour;
			text.alignment = TextAlignmentOptions.Center;
			text.raycastTarget = false;
			text.enableWordWrapping = false;
			return text;
		}

		/// <summary>Stretch a rect to fill its parent.</summary>
		public static void Fill(RectTransform rect)
		{
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.offsetMin = Vector2.zero;
			rect.offsetMax = Vector2.zero;
		}

		/// <summary>Anchor a rect to a point in its parent, sized in pixels.</summary>
		public static void Anchor(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
		{
			rect.anchorMin = anchor;
			rect.anchorMax = anchor;
			rect.pivot = new Vector2(0.5f, 0.5f);
			rect.anchoredPosition = position;
			rect.sizeDelta = size;
		}

		public static void Sfx(AudioEvent audioEvent)
		{
			try
			{
				SoundManager.PlaySfxUnrefined(audioEvent);
			}
			catch
			{
				// Sound is decoration; never let a missing mixer break the menu.
			}
		}
	}

	/// <summary>
	/// Fades a CanvasGroup on unscaled time, like the rest of the mod's timing.
	///
	/// The game ships CanvasGroupFader, but this keeps raycast blocking tied to
	/// intent rather than to alpha: a scrim that stops blocking a frame late is a
	/// cosmetic glitch, one that keeps blocking after close is a soft-lock.
	/// </summary>
	public class UnscaledFader : MonoBehaviour
	{
		public float FadeInSeconds = 0.12f;

		public float FadeOutSeconds = 0.09f;

		private CanvasGroup _group;

		private float _target;

		public bool IsHidden => _group != null && _target <= 0f && _group.alpha <= 0f;

		public void Awake()
		{
			_group = gameObject.GetComponent<CanvasGroup>();
			if (_group == null)
			{
				_group = gameObject.AddComponent<CanvasGroup>();
			}

			_group.alpha = 0f;
			SetRaycasts(false);
		}

		/// <summary>Fade in. Raycast blocking starts immediately, not when the fade ends.</summary>
		public void Show(bool blockRaycasts)
		{
			_target = 1f;
			SetRaycasts(blockRaycasts);
		}

		/// <summary>Fade out. Raycast blocking stops immediately, before the fade ends.</summary>
		public void Hide()
		{
			_target = 0f;
			SetRaycasts(false);
		}

		public void HideImmediately()
		{
			Hide();
			if (_group != null)
			{
				_group.alpha = 0f;
			}
		}

		public void Update()
		{
			if (_group == null || Mathf.Approximately(_group.alpha, _target))
			{
				return;
			}

			float duration = (_target > _group.alpha) ? FadeInSeconds : FadeOutSeconds;
			_group.alpha = (duration <= 0f)
				? _target
				: Mathf.MoveTowards(_group.alpha, _target, Time.unscaledDeltaTime / duration);
		}

		private void SetRaycasts(bool state)
		{
			if (_group != null)
			{
				_group.blocksRaycasts = state;
				_group.interactable = state;
			}
		}

		public void OnDisable()
		{
			// Never leave a full-screen raycast blocker behind.
			SetRaycasts(false);
		}
	}
}
