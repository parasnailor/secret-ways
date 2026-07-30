using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BookOfHoursSecretWays
{
	/// <summary>
	/// The bind-mode banner and the confirmation toasts, drawn on the game's own
	/// overlay canvas in the game's font so they don't look like a mod.
	///
	/// Deliberately not AureateNotifier: that instantiates a whole notification
	/// window, hides whatever notification is already showing, and runs for ten
	/// seconds plus. Far too heavy for "Saved this view to K".
	/// </summary>
	public class ToastBanner
	{
		private const float BottomOffset = 64f;

		private const float FontSize = 22f;

		private const float HorizontalPadding = 34f;

		private const float VerticalPadding = 18f;

		private RectTransform _root;

		private TextMeshProUGUI _text;

		private UnscaledFader _fader;

		/// <summary>Unscaled time the current toast stops being shown; 0 while persistent.</summary>
		private float _expiresAt;

		private bool _persistent;

		/// <summary>Show for a few seconds, then fade out on its own.</summary>
		public void Show(string message, float seconds)
		{
			if (!Build())
			{
				return;
			}

			_persistent = false;
			_expiresAt = Time.unscaledTime + seconds;
			SetText(message);
			_fader.Show(false);
		}

		/// <summary>Show until Hide is called - the bind-mode banner.</summary>
		public void ShowPersistent(string message)
		{
			if (!Build())
			{
				return;
			}

			_persistent = true;
			_expiresAt = 0f;
			SetText(message);
			_fader.Show(false);
		}

		public void Hide()
		{
			_persistent = false;
			_expiresAt = 0f;
			if (_fader != null)
			{
				_fader.Hide();
			}
		}

		/// <summary>Drives the timed fade-out. Call every frame.</summary>
		public void Tick()
		{
			if (_persistent || _expiresAt <= 0f || Time.unscaledTime < _expiresAt)
			{
				return;
			}

			_expiresAt = 0f;
			_fader.Hide();
		}

		public void Destroy()
		{
			if (_root != null)
			{
				Object.Destroy(_root.gameObject);
				_root = null;
			}
		}

		private void SetText(string message)
		{
			_text.text = message;

			// Size the plate to the text rather than guessing, the way the old IMGUI
			// panel used CalcSize.
			Vector2 preferred = _text.GetPreferredValues(message);
			_root.sizeDelta = new Vector2(
				Mathf.Min(preferred.x + HorizontalPadding * 2f, Screen.width - 80f),
				preferred.y + VerticalPadding * 2f);
		}

		/// <summary>Builds the widget on first use. False if the canvas isn't up yet.</summary>
		private bool Build()
		{
			if (_root != null)
			{
				return true;
			}

			Transform parent = NativeUi.GetOverlayParent();
			if (parent == null)
			{
				return false;
			}

			_root = NativeUi.MakeRect(parent, "SecretWaysBanner");
			NativeUi.Anchor(_root, new Vector2(0.5f, 0f), new Vector2(0f, BottomOffset), new Vector2(520f, 60f));

			// Above the game's windows, and above the wheel too - binding a slot from
			// the wheel raises a toast while the wheel is still up.
			NativeUi.SortAbove(_root, NativeUi.SortOrderBanner);

			_root.gameObject.AddComponent<CanvasGroup>();
			_fader = _root.gameObject.AddComponent<UnscaledFader>();

			NativeUi.Panel plate = NativeUi.MakePanel(_root, "Plate", 14, 2f);
			NativeUi.Fill(plate.Root);

			_text = NativeUi.MakeText(_root, "Text", LanguageManager.eFontStyle.BodyText, FontSize, UIStyle.gold);
			NativeUi.Fill(_text.rectTransform);

			return true;
		}
	}
}
