using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace AfetUyanisi.Chapters.Earthquake
{
    /// <summary>The existing kitchen sprite has electric plates: show heat, never gas flames.
    /// RoomInteractor owns input; this component owns only the stove's committed shutdown.</summary>
    [DisallowMultipleComponent]
    public sealed class StoveFireController : MonoBehaviour
    {
        public const string TaskId = "utilities_location";
        public enum StoveState { On, TurningOff, Off }
        [SerializeField] private Vector3 plateOffset = new Vector3(-2f / 64, 29f / 64, 0);
        [SerializeField] private Vector3 knobOffset = new Vector3(5f / 64, 16f / 64, 0);
        [SerializeField, Min(1)] private float framesPerSecond = 8;
        [SerializeField, Range(.35f, .6f)] private float shutdownSeconds = .45f;
        [SerializeField] private float knobOffAngle = -75;
        [SerializeField] private AudioClip runningClip;
        [SerializeField] private AudioClip clickClip;
        private Interactable point;
        private SpriteRenderer stove, heat, knob;
        private Sprite[] frames;
        private Texture2D[] textures;
        private PreparationQuestManager quests;
        private EmergencyCountdownManager countdown;
        private EarthquakeModeController mode;
        private EarthquakeRoom room;
        private Action completed;
        private AudioSource hum, click;
        private SortingGroup createdGroup;
        private float elapsed;
        public StoveState State { get; private set; }
        public SpriteRenderer HeatRenderer => heat;
        public SpriteRenderer StoveRenderer => stove;

        public bool Configure(InspectableObject source, EarthquakeModeController chapter,
            PreparationQuestManager manager, EmergencyCountdownManager timer, Action onCompleted)
        {
            if (stove != null) return true;
            // Resolve once, scoped to this loaded scene. Never search or allocate in Update.
            float nearest = float.PositiveInfinity;
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var candidate in root.GetComponentsInChildren<SpriteRenderer>(true))
                    if (candidate.sprite != null && candidate.sprite.name == "PROP_Kitchen_Stove")
                    {
                        float distance = (candidate.transform.position - transform.position).sqrMagnitude;
                        if (distance < nearest) { nearest = distance; stove = candidate; }
                    }
            if (stove == null || manager == null || timer == null) return false;
            point = source.Point; mode = chapter; quests = manager; countdown = timer; completed = onCompleted;
            transform.position = stove.transform.TransformPoint(new Vector3(.15f, -.1f, 0));
            point.PromptOffset = new Vector3(0, .95f, 0);
            room = source.GetComponentInParent<EarthquakeRoom>();
            quests.ConfigureStoveTask();
            quests.BeforeLock += ResolveCommittedShutdown;
            quests.TaskCompleted += OnTaskCompleted;
            // Keep all overlays in the stove's external depth position, including player occlusion.
            if (stove.GetComponentInParent<SortingGroup>() == null)
            {
                var group = stove.gameObject.AddComponent<SortingGroup>(); createdGroup = group;
                group.sortingLayerID = stove.sortingLayerID; group.sortingOrder = stove.sortingOrder;
            }
            BuildFrames();
            heat = Overlay("Electric plate heat", plateOffset, 1);
            knob = Overlay("Stove knob indicator", knobOffset, 2);
            var texture = new Texture2D(1, 3) { filterMode = FilterMode.Point };
            texture.SetPixels(new[] { Color.white, Color.white, Color.white }); texture.Apply();
            knob.sprite = Sprite.Create(texture, new Rect(0, 0, 1, 3), new Vector2(.5f, .5f), 64);
            if (runningClip != null) { hum = stove.gameObject.AddComponent<AudioSource>(); hum.clip = runningClip; hum.loop = true; hum.playOnAwake = false; hum.volume = .08f; }
            if (clickClip != null) { click = stove.gameObject.AddComponent<AudioSource>(); click.playOnAwake = false; click.volume = .25f; }
            point.Setup("Ocağı kapat", "", false, true);
            if (quests.IsDone(TaskId)) SetOff();
            RefreshVisuals();
            return true;
        }

        public IEnumerator Use()
        {
            if (State != StoveState.On || !isActiveAndEnabled || quests.IsLocked || countdown.HasExpired
                || mode.Phase != EarthquakePhase.Prep) yield break;
            // Turning the control is the irreversible logical commit. Lock/disable flushes it to OFF.
            State = StoveState.TurningOff; elapsed = 0;
            point.SetAvailable(false);
            if (click != null) click.PlayOneShot(clickClip);
            yield break; // No player lock and no countdown pause during the visual transition.
        }

        private void Update()
        {
            if (stove == null) return;
            if (State == StoveState.TurningOff)
            {
                elapsed += Time.deltaTime;
                if (elapsed >= shutdownSeconds) ResolveCommittedShutdown();
            }
            RefreshVisuals();
        }

        private void ResolveCommittedShutdown()
        {
            if (State != StoveState.TurningOff) return;
            SetOff();
            // BeforeLock runs before the snapshot freezes; no delayed coroutine can award twice.
            if (quests != null && quests.CompleteTask(TaskId)) completed?.Invoke();
        }

        private void OnTaskCompleted(string id) { if (id == TaskId) SetOff(); }
        private void SetOff()
        {
            State = StoveState.Off;
            if (point != null) { point.Setup("Ocak kapalı", "", false, true); point.SetAvailable(false); }
            if (hum != null) hum.Stop();
            RefreshVisuals();
        }

        private void OnEnable() { if (quests != null && quests.IsDone(TaskId)) SetOff(); }
        private void OnDisable()
        {
            ResolveCommittedShutdown();
            if (heat != null) heat.enabled = false;
            if (knob != null) knob.enabled = false;
            if (hum != null) hum.Stop();
            if (click != null) click.Stop();
        }

        private void RefreshVisuals()
        {
            if (heat == null) return;
            point.SetAvailable(isActiveAndEnabled && State == StoveState.On && mode.Phase == EarthquakePhase.Prep
                && !quests.IsLocked && !countdown.HasExpired);
            bool visible = isActiveAndEnabled && stove.enabled && stove.gameObject.activeInHierarchy
                && (room == null || mode.CurrentRoom == room);
            float power = State == StoveState.On ? 1 : State == StoveState.Off ? 0 : 1 - Mathf.Clamp01(elapsed / shutdownSeconds);
            int frame = State == StoveState.On ? Mathf.FloorToInt(Time.time * framesPerSecond) % frames.Length
                : Mathf.Clamp(Mathf.FloorToInt((1 - power) * frames.Length), 0, frames.Length - 1);
            heat.sprite = frames[frame]; heat.enabled = visible && power > 0;
            // Copy room tint/material rather than adding an unlit glow that leaks through darkness.
            heat.color = new Color(stove.color.r, stove.color.g, stove.color.b, stove.color.a * power);
            knob.enabled = visible; knob.color = stove.color;
            if (!visible && click != null) click.Stop();
            knob.transform.localRotation = Quaternion.Euler(0, 0, knobOffAngle * (1 - power));
            if (hum != null)
            {
                hum.volume = .08f * power * Mathf.Min(stove.color.r, stove.color.g, stove.color.b);
                if (visible && power > 0) { if (!hum.isPlaying) hum.Play(); }
                else hum.Stop();
            }
        }

        private SpriteRenderer Overlay(string title, Vector3 offset, int order)
        {
            var obj = new GameObject(title); obj.transform.SetParent(stove.transform, false); obj.transform.localPosition = offset;
            var renderer = obj.AddComponent<SpriteRenderer>(); renderer.sharedMaterial = stove.sharedMaterial;
            renderer.sortingLayerID = stove.sortingLayerID; renderer.sortingOrder = stove.sortingOrder + order;
            return renderer;
        }

        private void BuildFrames()
        {
            frames = new Sprite[8]; textures = new Texture2D[8];
            for (int frame = 0; frame < frames.Length; frame++)
            {
                var pixels = new Color[13 * 7];
                for (int y = 0; y < 7; y++) for (int x = 0; x < 13; x++)
                {
                    float radius = Mathf.Pow((x - 6) / 5.5f, 2) + Mathf.Pow((y - 3) / 2.5f, 2);
                    if (radius <= 1 && radius >= .2f)
                    {
                        float pulse = .5f + .5f * Mathf.Sin((frame + x + y) * Mathf.PI / 4);
                        pixels[y * 13 + x] = new Color(1, .16f + .15f * pulse, .035f, .65f + .2f * pulse);
                    }
                }
                var texture = new Texture2D(13, 7) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                texture.SetPixels(pixels); texture.Apply(); textures[frame] = texture;
                frames[frame] = Sprite.Create(texture, new Rect(0, 0, 13, 7), new Vector2(.5f, .5f), 64);
            }
        }

        private void OnDestroy()
        {
            if (quests != null) { quests.BeforeLock -= ResolveCommittedShutdown; quests.TaskCompleted -= OnTaskCompleted; }
            if (heat != null) Destroy(heat.gameObject);
            if (knob != null) { Destroy(knob.sprite.texture); Destroy(knob.sprite); Destroy(knob.gameObject); }
            if (frames != null) foreach (var sprite in frames) Destroy(sprite);
            if (textures != null) foreach (var texture in textures) Destroy(texture);
            if (hum != null) Destroy(hum); if (click != null) Destroy(click);
            if (createdGroup != null) Destroy(createdGroup);
        }
    }
}
