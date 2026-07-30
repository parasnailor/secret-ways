using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using SecretHistories.UI;
using UnityEngine;

namespace BookOfHoursSecretWays
{
	/// <summary>
	/// Thumbnails of saved views, written next to the config as PNGs under a folder
	/// per playthrough, matching how the locations themselves are scoped.
	///
	/// Capture happens at bind time on purpose: the live camera is already at the
	/// target, so the zoom-dependent scene state the game maintains - wall
	/// translucency, darkening, interior/exterior audio, all driven off camera z by
	/// ZoomEffectController - is already correct for that height. Rendering the same
	/// spot later from a differently-positioned camera would show the wrong state.
	/// </summary>
	public class LocationPreviews
	{
		private const string DirectoryName = "secret_ways_previews";

		private readonly Dictionary<string, Texture2D> _cache = new Dictionary<string, Texture2D>();

		/// <summary>Slots we've already looked for and not found, so we don't retry the disk every frame.</summary>
		private readonly HashSet<string> _missing = new HashSet<string>();

		private readonly HotkeyConfig _config;

		private readonly MonoBehaviour _coroutineHost;

		public LocationPreviews(HotkeyConfig config, MonoBehaviour coroutineHost)
		{
			_config = config;
			_coroutineHost = coroutineHost;
		}

		public static string RootDirectory => Path.Combine(Application.persistentDataPath, DirectoryName);

		/// <summary>The playthrough id is a tick count, so it's already a safe folder name.</summary>
		private static string FileFor(string playthroughId, string slotAction)
		{
			return Path.Combine(RootDirectory, playthroughId, slotAction + ".png");
		}

		private static string CacheKey(string playthroughId, string slotAction)
		{
			return playthroughId + "/" + slotAction;
		}

		/// <summary>The thumbnail for a slot, or null if it has none.</summary>
		public Texture2D Get(string playthroughId, string slotAction)
		{
			string key = CacheKey(playthroughId, slotAction);
			if (_cache.TryGetValue(key, out Texture2D cached) && cached != null)
			{
				return cached;
			}

			if (_missing.Contains(key))
			{
				return null;
			}

			try
			{
				string file = FileFor(playthroughId, slotAction);
				if (!File.Exists(file))
				{
					_missing.Add(key);
					return null;
				}

				Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
				{
					hideFlags = HideFlags.HideAndDontSave,
					wrapMode = TextureWrapMode.Clamp
				};
				texture.LoadImage(File.ReadAllBytes(file));
				texture.Apply();

				_cache[key] = texture;
				return texture;
			}
			catch (Exception e)
			{
				NoonUtility.LogWarning("Secret Ways: couldn't read the preview for " + slotAction + ": " + e.Message);
				_missing.Add(key);
				return null;
			}
		}

		/// <summary>
		/// Renders the current view to this slot's thumbnail. Never throws - a slot
		/// without a picture is a cosmetic loss, and it must not fail a bind.
		/// </summary>
		public void Capture(string playthroughId, string slotAction)
		{
			CamOperator cam = Watchman.Get<CamOperator>();
			Camera source = (cam == null) ? null : cam.GetAttachedCamera();
			if (source == null)
			{
				return;
			}

			int width = Mathf.Clamp(_config.PreviewWidth, 32, 1024);
			int height = Mathf.Clamp(_config.PreviewHeight, 32, 1024);

			RenderTexture target = null;
			RenderTexture previouslyActive = RenderTexture.active;
			GameObject cloneObject = null;
			Texture2D shot = null;

			try
			{
				target = RenderTexture.GetTemporary(width, height, 24);

				// Render through a copy rather than the live camera: no risk of leaving
				// the real one pointed at a texture, and screen-space-overlay canvases
				// (the HUD, and this mod's own UI) aren't drawn by any camera, so they
				// stay out of the shot for free.
				cloneObject = new GameObject("lhpreviewcamera");
				Camera clone = cloneObject.AddComponent<Camera>();
				clone.CopyFrom(source);
				cloneObject.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);

				// After CopyFrom - it copies targetTexture along with everything else.
				clone.targetTexture = target;
				clone.enabled = false;
				clone.Render();

				RenderTexture.active = target;
				shot = new Texture2D(width, height, TextureFormat.RGB24, false);
				shot.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
				shot.Apply();

				string file = FileFor(playthroughId, slotAction);
				System.IO.Directory.CreateDirectory(Path.GetDirectoryName(file));
				File.WriteAllBytes(file, shot.EncodeToPNG());

				Forget(playthroughId, slotAction);
			}
			catch (Exception e)
			{
				NoonUtility.LogWarning("Secret Ways: couldn't capture a preview for " + slotAction + ": " + e.Message);
			}
			finally
			{
				RenderTexture.active = previouslyActive;

				if (cloneObject != null)
				{
					UnityEngine.Object.Destroy(cloneObject);
				}

				if (target != null)
				{
					RenderTexture.ReleaseTemporary(target);
				}

				if (shot != null)
				{
					UnityEngine.Object.Destroy(shot);
				}
			}
		}

		/// <summary>
		/// Flies to a saved location and captures it on arrival, for slots bound
		/// before previews existed or whose room has since changed. Leaves the camera
		/// there, which is the useful outcome anyway.
		/// </summary>
		public void RefreshOnArrival(string playthroughId, string slotAction, SavedLocation location)
		{
			CamOperator cam = Watchman.Get<CamOperator>();
			if (cam == null || location == null)
			{
				return;
			}

			cam.StopAllMovement();
			cam.PointAtTableLevelAtHeight(
				location.TablePosition,
				location.Z,
				_config.ClampedTravelSeconds,
				() => _coroutineHost.StartCoroutine(CaptureAfterSettling(playthroughId, slotAction)));
		}

		/// <summary>
		/// The camera arrives before the zoom-driven fades finish, so give the scene
		/// a moment to settle or the thumbnail catches walls mid-dissolve.
		/// </summary>
		private IEnumerator CaptureAfterSettling(string playthroughId, string slotAction)
		{
			yield return new WaitForSecondsRealtime(0.35f);
			Capture(playthroughId, slotAction);
		}

		public void Delete(string playthroughId, string slotAction)
		{
			try
			{
				string file = FileFor(playthroughId, slotAction);
				if (File.Exists(file))
				{
					File.Delete(file);
				}
			}
			catch (Exception e)
			{
				NoonUtility.LogWarning("Secret Ways: couldn't delete the preview for " + slotAction + ": " + e.Message);
			}

			Forget(playthroughId, slotAction);
		}

		/// <summary>Drops the cached texture so the next Get re-reads from disk.</summary>
		private void Forget(string playthroughId, string slotAction)
		{
			string key = CacheKey(playthroughId, slotAction);
			if (_cache.TryGetValue(key, out Texture2D cached) && cached != null)
			{
				UnityEngine.Object.Destroy(cached);
			}

			_cache.Remove(key);
			_missing.Remove(key);
		}

		public void Destroy()
		{
			foreach (KeyValuePair<string, Texture2D> entry in _cache)
			{
				if (entry.Value != null)
				{
					UnityEngine.Object.Destroy(entry.Value);
				}
			}

			_cache.Clear();
			_missing.Clear();
		}
	}
}
