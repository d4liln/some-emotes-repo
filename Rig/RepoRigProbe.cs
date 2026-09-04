using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx.Configuration;
using UnityEngine;

namespace SomeEmotesREPO.Rig
{
    [DefaultExecutionOrder(10000)]
    public class RepoRigProbe : MonoBehaviour
    {
        internal static ConfigEntry<bool> ProbeEnabled { get; private set; } = null!;
        internal static ConfigEntry<KeyCode> ProbeKey { get; private set; } = null!;

        private const float StepDefault = 5f;
        private const float StepFine = 1f;
        private const float StepCoarse = 25f;
        private const string PoseFileName = "rigpose.json";

        private static RepoRigProbe? _instance;
        public static RepoRigProbe? Instance => _instance;

        private readonly Vector3[] _euler = new Vector3[RepoRigBinder.BoneCount];
        private readonly Vector3[] _scale = new Vector3[RepoRigBinder.BoneCount];
        private readonly bool[] _driven = new bool[RepoRigBinder.BoneCount];
        private readonly bool[] _applyScale = new bool[RepoRigBinder.BoneCount];

        private RepoRigBinder? _binder;
        private PlayerAvatar? _target;
        private bool _active;
        private int _selectedBone;
        private int _selectedAxis;
        private string _status = "Inactive";
        private float _rebindCooldown;
        private const float SweepAmplitude = 60f;
        private const float SweepPeriod = 1.5f;
        private const float SweepAxisDuration = 3f;
        private bool _sweep;
        private float _sweepTime;
        private const float CameraDistanceMin = 0.5f;
        private const float CameraDistanceMax = 6f;
        private Transform? _camera;
        private float _cameraDistance = 3.25f;
        private readonly EmoteRigPlayer _player = new EmoteRigPlayer();
        private EmoteProxyRig? _reference;
        private List<AnimationClip> _clips = new List<AnimationClip>();
        private int _clipIndex;

        private GUIStyle? _style;
        private Texture2D? _panel;

        private void Awake()
        {
            _instance = this;
            for (int i = 0; i < RepoRigBinder.BoneCount; i++)
            {
                _scale[i] = Vector3.one;
                _driven[i] = true;
            }
            LoadPose(silent: true);

            ProbeEnabled = SomeEmotesREPO.Instance.Config.Bind(
                "Debug", "RigProbe", false,
                "Enables the rig probe: an in-game tool that binds the real player avatar bones and forces a pose onto them. Used to develop the clone-free emote system.");
            ProbeKey = SomeEmotesREPO.Instance.Config.Bind(
                "Debug", "RigProbeKey", KeyCode.F8,
                "Opens and closes the rig probe overlay.");

            if (ProbeEnabled.Value)
            {
                SomeEmotesREPO.Logger.LogInfo($"[RigProbe] Enabled. Press [{ProbeKey.Value}] in a level to bind the avatar rig.");
            }
            else
            {
                SomeEmotesREPO.Logger.LogInfo("[RigProbe] Disabled (Debug/RigProbe = false). Set it to true and restart the game.");
            }
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            if (_panel != null) Destroy(_panel);
            _player.Dispose();
            if (_reference != null) Destroy(_reference.gameObject);
        }

        private void Update()
        {
            if (!ProbeEnabled.Value) return;
            if (ChatManager.instance != null && ChatManager.instance.chatActive) return;

            if (Input.GetKeyDown(ProbeKey.Value)) SetActive(!_active);
            if (!_active) return;

            if (Input.GetKeyDown(KeyCode.PageUp)) CycleTarget(1);
            if (Input.GetKeyDown(KeyCode.PageDown)) CycleTarget(-1);

            if (Input.GetKeyDown(KeyCode.F7))
            {
                _sweep = !_sweep;
                _sweepTime = 0f;
            }
            if (_sweep) _sweepTime += Time.unscaledDeltaTime;

            float scroll = Input.mouseScrollDelta.y;
            if (scroll != 0f)
            {
                _cameraDistance = Mathf.Clamp(_cameraDistance - scroll * 0.4f, CameraDistanceMin, CameraDistanceMax);
            }

            if (Input.GetKeyDown(KeyCode.DownArrow)) _selectedBone = Wrap(_selectedBone + 1, RepoRigBinder.BoneCount);
            if (Input.GetKeyDown(KeyCode.UpArrow)) _selectedBone = Wrap(_selectedBone - 1, RepoRigBinder.BoneCount);
            if (Input.GetKeyDown(KeyCode.Tab)) _selectedAxis = Wrap(_selectedAxis + 1, 3);

            float step = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? StepCoarse
                       : Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) ? StepFine
                       : StepDefault;

            if (Input.GetKeyDown(KeyCode.RightArrow)) Nudge(step);
            if (Input.GetKeyDown(KeyCode.LeftArrow)) Nudge(-step);

            if (Input.GetKeyDown(KeyCode.Space)) _driven[_selectedBone] = !_driven[_selectedBone];

            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                {
                    for (int i = 0; i < RepoRigBinder.BoneCount; i++)
                    {
                        _euler[i] = Vector3.zero;
                        _scale[i] = Vector3.one;
                        _driven[i] = true;
                    }
                    _binder?.ResetToRest();
                }
                else
                {
                    _euler[_selectedBone] = Vector3.zero;
                    _scale[_selectedBone] = Vector3.one;
                }
            }

            if (Input.GetKeyDown(KeyCode.F5)) TogglePlayback();
            if (Input.GetKeyDown(KeyCode.F2) || Input.GetKeyDown(KeyCode.Home)) CycleClip(-1);
            if (Input.GetKeyDown(KeyCode.F3) || Input.GetKeyDown(KeyCode.End)) CycleClip(1);

            // A/B the camera-driven transforms, to see for yourself what they cost.
            if (Input.GetKeyDown(KeyCode.F4)) EmoteSuppression.HoldCodeTransforms = !EmoteSuppression.HoldCodeTransforms;

            if (Input.GetKeyDown(KeyCode.F1)) ToggleReference();

            if (Input.GetKeyDown(KeyCode.F6)) DumpAxes();
            if (Input.GetKeyDown(KeyCode.F9)) SavePose();
            if (Input.GetKeyDown(KeyCode.F10)) LoadPose(silent: false);
        }

        private void Nudge(float degrees)
        {
            Vector3 e = _euler[_selectedBone];
            if (_selectedAxis == 0) e.x += degrees;
            else if (_selectedAxis == 1) e.y += degrees;
            else e.z += degrees;
            _euler[_selectedBone] = e;
        }

        private static int Wrap(int value, int length) => (value % length + length) % length;

        private int SweepAxis() => Wrap(Mathf.FloorToInt(_sweepTime / SweepAxisDuration), 3);

        private Vector3 SweepEuler()
        {
            float angle = SweepAmplitude * Mathf.Sin(_sweepTime / SweepPeriod * 2f * Mathf.PI);
            int axis = SweepAxis();
            return axis == 0 ? new Vector3(angle, 0f, 0f)
                 : axis == 1 ? new Vector3(0f, angle, 0f)
                             : new Vector3(0f, 0f, angle);
        }

        private void LateUpdate()
        {
            if (!_active) return;

            if (_binder == null || _target == null || _target.playerAvatarVisuals == null)
            {
                _rebindCooldown -= Time.unscaledDeltaTime;
                if (_rebindCooldown <= 0f)
                {
                    _rebindCooldown = 0.5f;
                    Rebind(_target);
                }
                return;
            }

            if (_target == PlayerAvatar.instance)
            {
                _binder.Visuals.ShowSelfOverride(0.1f);

                if (_camera == null && Camera.main != null) _camera = Camera.main.transform;
                if (_camera != null) _camera.localPosition = new Vector3(0f, 0f, -_cameraDistance);
            }

            PlaceReference();

            if (_player.Active)
            {
                _player.Tick(Time.deltaTime);
                return;
            }

            for (int i = 0; i < RepoRigBinder.BoneCount; i++)
            {
                if (!_driven[i]) continue;

                Transform bone = _binder[(RigBone)i];
                if (bone == null) continue;

                Vector3 euler = _euler[i];
                if (_sweep && i == _selectedBone)
                {
                    euler = SweepEuler();
                }

                bone.localRotation = Quaternion.Euler(euler);
                if (_applyScale[i]) bone.localScale = _scale[i];
            }
        }

        private void SetActive(bool value)
        {
            if (_active == value) return;
            _active = value;

            if (_active)
            {
                SomeEmotesREPO.Logger.LogInfo("[RigProbe] Opening.");
                Rebind(PlayerAvatar.instance);
            }
            else
            {
                _player.Cancel("the probe was closed");
                _binder?.ResetToRest();
                _binder = null;
                _target = null;
                _sweep = false;
                _status = "Inactive";

                if (_camera != null) _camera.localPosition = Vector3.zero;
                _camera = null;
            }
        }

        private void CycleTarget(int direction)
        {
            var avatars = LiveAvatars();
            if (avatars.Count == 0)
            {
                _status = "No avatar in the scene";
                return;
            }

            int index = _target != null ? avatars.IndexOf(_target) : -1;
            index = Wrap(index + direction, avatars.Count);

            _binder?.ResetToRest();
            Rebind(avatars[index]);
        }

        private static List<PlayerAvatar> LiveAvatars()
        {
            var result = new List<PlayerAvatar>();
            foreach (var avatar in Object.FindObjectsOfType<PlayerAvatar>())
            {
                var visuals = EmoteRigPlayer.VisualsOf(avatar);
                if (visuals == null || visuals.isMenuAvatar) continue;
                result.Add(avatar);
            }
            return result;
        }

        private void Rebind(PlayerAvatar? avatar)
        {
            _player.Cancel("the probe changed target");
            _target = avatar;
            _binder = null;

            var visuals = EmoteRigPlayer.VisualsOf(avatar);
            if (visuals == null)
            {
                _status = "Avatar has no PlayerAvatarVisuals yet";
                return;
            }

            if (!RepoRigBinder.TryBind(visuals, out var binder, out string error))
            {
                _binder = null;
                _status = "BIND FAILED - " + error;
                SomeEmotesREPO.Logger.LogError($"[RigProbe] Could not bind the avatar rig: {error}");
                return;
            }

            _binder = binder;
            _status = $"Bound ({RepoRigBinder.BoneCount} bones)";
            SomeEmotesREPO.Logger.LogInfo("[RigProbe] " + binder!.Describe());
        }

        private void ToggleReference()
        {
            if (_reference != null)
            {
                Destroy(_reference.gameObject);
                _reference = null;
                _status = "Reference dancer off";
                return;
            }

            if (_clips.Count == 0) LoadClips();
            if (_clips.Count == 0)
            {
                _status = "No emote clip in the bundle";
                return;
            }

            _reference = EmoteProxyRig.Create(visible: true);
            if (_reference == null)
            {
                _status = "Reference dancer could not be created, see the log";
                return;
            }

            Material? paint = FindLiveMaterial();
            if (paint != null) _reference.ApplyMaterial(paint);
            else SomeEmotesREPO.Logger.LogWarning("[Solver] No game material found; the reference may be invisible.");

            _reference.Play(_clips[_clipIndex]);
            PlaceReference();
            _status = "Reference dancer on";
            SomeEmotesREPO.Logger.LogInfo($"[Solver] Reference dancer at {_reference.transform.position}.");
        }

        private Material? FindLiveMaterial()
        {
            if (_binder == null) return null;

            foreach (var renderer in _binder.Visuals.GetComponentsInChildren<Renderer>(includeInactive: true))
            {
                if (renderer is MeshRenderer && renderer.sharedMaterial != null) return renderer.sharedMaterial;
            }
            return null;
        }

        private void PlaceReference()
        {
            if (_reference == null || _target == null) return;

            Quaternion facing = _binder != null ? _binder.Visuals.transform.rotation : _target.transform.rotation;
            _reference.PlaceAt(_target.transform.position + facing * (Vector3.right * 1.6f), facing);
        }

        private void TogglePlayback()
        {
            if (_player.Playing)
            {
                _player.Stop();
                _status = "Fading out";
                return;
            }

            if (_target == null)
            {
                _status = "No target avatar";
                return;
            }

            if (_clips.Count == 0) LoadClips();
            if (_clips.Count == 0)
            {
                _status = "No emote clip in the bundle";
                return;
            }

            _sweep = false;
            if (!_player.Play(_target, _clips[_clipIndex]))
            {
                _status = "The emote could not start, see the log";
                return;
            }

            _reference?.Play(_clips[_clipIndex]);
            _status = "Playing " + _clips[_clipIndex].name;
            SomeEmotesREPO.Logger.LogInfo($"[Solver] Playing '{_clips[_clipIndex].name}' on the real avatar.");
        }

        private void CycleClip(int direction)
        {
            if (_clips.Count == 0) LoadClips();
            if (_clips.Count == 0) return;

            _clipIndex = Wrap(_clipIndex + direction, _clips.Count);
            if (_player.Playing && _target != null)
            {
                _player.Play(_target, _clips[_clipIndex]);
                _status = "Playing " + _clips[_clipIndex].name;
            }
            _reference?.Play(_clips[_clipIndex]);
        }

        private void LoadClips()
        {
            _clips = new List<AnimationClip>();
            foreach (string path in EmoteBundleLoader.GetAllAnimNames())
            {
                var clip = EmoteBundleLoader.LoadAsset<AnimationClip>(path);
                if (clip != null) _clips.Add(clip);
            }
            _clips.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            SomeEmotesREPO.Logger.LogInfo($"[Solver] {_clips.Count} emote clips available.");
        }

        private void DumpAxes()
        {
            if (_binder == null)
            {
                _status = "No rig bound, nothing to measure";
                return;
            }

            _binder.ResetToRest();

            Transform root = _binder.Visuals.transform;
            var sb = new StringBuilder();
            sb.AppendLine($"[RigProbe] Bone axes measured in '{root.name}' space (+X right, +Y up, +Z forward).");

            for (int i = 0; i < RepoRigBinder.BoneCount; i++)
            {
                Transform bone = _binder[(RigBone)i];
                if (bone == null) continue;

                sb.AppendLine($"  {RepoRigBinder.NameOf((RigBone)i)}");
                sb.AppendLine($"      axes   X={V(root.InverseTransformDirection(bone.right))}" +
                              $"  Y={V(root.InverseTransformDirection(bone.up))}" +
                              $"  Z={V(root.InverseTransformDirection(bone.forward))}");

                Transform? hips = _binder[RigBone.Root];
                if (hips != null && bone != hips)
                {
                    sb.AppendLine($"      joint  at {V(hips.InverseTransformPoint(bone.position))} in ANIM BOT space");
                }
                var meshes = MeshesBelow(bone);
                if (meshes.Count == 0)
                {
                    sb.AppendLine("      mesh   none below this bone");
                }
                foreach (Renderer mesh in meshes)
                {
                    Bounds b = mesh.bounds;
                    Vector3 local = bone.InverseTransformPoint(b.center);
                    sb.AppendLine($"      mesh   '{mesh.name}' center={V(local)} size={V(b.size)}");
                }
            }

            SomeEmotesREPO.Logger.LogInfo(sb.ToString());
            _status = "Axes measured, see the log";
        }

        private static string V(Vector3 v) => $"({v.x,6:0.00},{v.y,6:0.00},{v.z,6:0.00})";

        private static List<Renderer> MeshesBelow(Transform bone)
        {
            var result = new List<Renderer>();
            Collect(bone);
            return result;

            void Collect(Transform t)
            {
                for (int i = 0; i < t.childCount; i++)
                {
                    Transform child = t.GetChild(i);
                    if (child.name.StartsWith("ANIM ", System.StringComparison.Ordinal)
                        && !child.name.EndsWith(" SCALE", System.StringComparison.Ordinal)) continue;

                    if (child.name.StartsWith("mesh_", System.StringComparison.OrdinalIgnoreCase))
                    {
                        var renderer = child.GetComponent<Renderer>();
                        if (renderer != null) result.Add(renderer);
                    }
                    Collect(child);
                }
            }
        }

        private static string PoseFilePath()
        {
            string folder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".";
            return Path.Combine(folder, PoseFileName);
        }

        private void SavePose()
        {
            string path = PoseFilePath();
            try
            {
                var file = RigPoseFile.From(_euler, _scale, _driven, _applyScale);
                File.WriteAllText(path, JsonUtility.ToJson(file, prettyPrint: true));
                _status = "Saved " + PoseFileName;
                SomeEmotesREPO.Logger.LogInfo($"[RigProbe] Pose saved to {path}");
            }
            catch (IOException e)
            {
                _status = "Save failed - " + e.Message;
                SomeEmotesREPO.Logger.LogError($"[RigProbe] Could not write {path}: {e.Message}");
            }
        }

        private void LoadPose(bool silent)
        {
            string path = PoseFilePath();
            if (!File.Exists(path))
            {
                if (!silent) _status = PoseFileName + " not found";
                return;
            }

            try
            {
                var file = JsonUtility.FromJson<RigPoseFile>(File.ReadAllText(path));
                if (file?.bones == null)
                {
                    if (!silent) _status = PoseFileName + " is empty";
                    return;
                }

                file.CopyTo(_euler, _scale, _driven, _applyScale);
                if (!silent) _status = "Reloaded " + PoseFileName;
            }
            catch (IOException e)
            {
                _status = "Load failed - " + e.Message;
                SomeEmotesREPO.Logger.LogError($"[RigProbe] Could not read {path}: {e.Message}");
            }
        }

        private void OnGUI()
        {
            if (!_active || !ProbeEnabled.Value) return;

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    richText = true,
                    wordWrap = false,
                    padding = new RectOffset(10, 10, 6, 6),
                };
                _style.normal.textColor = Color.white;
            }

            if (_panel == null)
            {
                _panel = new Texture2D(1, 1);
                _panel.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.78f));
                _panel.Apply();
            }

            string text = BuildOverlay();
            Vector2 size = _style.CalcSize(new GUIContent(text));
            var rect = new Rect(12f, 12f, size.x + 8f, size.y + 4f);

            GUI.DrawTexture(rect, _panel);
            GUI.Label(rect, text, _style);
        }

        private string BuildOverlay()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"<b>SomeEmotesREPO - rig probe</b>   <color=#9aa>[{ProbeKey.Value}] close</color>");

            string targetName = _target != null ? DisplayName(_target) : "none";
            string localTag = _target != null && _target == PlayerAvatar.instance ? "  <color=#4fc58c>(local)</color>" : string.Empty;
            string interrupted = _player.LastInterruption.Length > 0
                ? $"   <color=#e07a6b>Stopped: {_player.LastInterruption}</color>"
                : string.Empty;
            sb.AppendLine($"Target: <b>{targetName}</b>{localTag}   <color=#9aa>PgUp/PgDn</color>{interrupted}");

            bool ok = _binder != null;
            sb.AppendLine($"Bind  : <color={(ok ? "#4fc58c" : "#e07a6b")}>{_status}</color>");

            sb.AppendLine(VoiceLine());

            if (_player.Active && _clips.Count > 0)
            {
                string hold = EmoteSuppression.HoldCodeTransforms
                    ? "<color=#4fc58c>code_* held</color>"
                    : "<color=#d9a441>code_* free (the camera bends the dance)</color>";
                string reference = _reference != null
                    ? "  <color=#4fc58c>Reference shown</color>"
                    : "  <color=#9aa>F1 reference</color>";
                string phase = _player.Current == EmoteRigPlayer.State.FadingIn ? "<color=#d9a441>Fading in</color>"
                             : _player.Current == EmoteRigPlayer.State.FadingOut ? "<color=#d9a441>Fading out</color>"
                             : "<color=#4fc58c>Holding</color>";

                sb.AppendLine($"<color=#4fc58c><b>EMOTE</b>  {_clips[_clipIndex].name}  " +
                              $"<color=#9aa>({_clipIndex + 1}/{_clips.Count})  F2/F3 to change</color></color>   {hold}  <color=#9aa>F4</color>{reference}");
                sb.AppendLine($"Blend : {phase}  <b>{_player.Weight * 100f,4:0}%</b>  {Bar(_player.Weight)}");
            }

            if (_sweep)
            {
                string axis = SweepAxis() == 0 ? "X" : SweepAxis() == 1 ? "Y" : "Z";
                sb.AppendLine($"<color=#4fc58c><b>SWEEP</b>  {RepoRigBinder.NameOf((RigBone)_selectedBone)}  axis <b>{axis}</b>  +-{SweepAmplitude:0}&#176;  " +
                              $"<color=#9aa>The bone must swing</color></color>");
            }

            sb.AppendLine("<color=#667>--------------------------------------------------</color>");

            for (int i = 0; i < RepoRigBinder.BoneCount; i++)
            {
                bool selected = i == _selectedBone;
                string marker = selected ? "<color=#4fc58c>&gt;</color>" : " ";
                string name = RepoRigBinder.NameOf((RigBone)i);
                string state = _driven[i] ? "  " : "<color=#d9a441>~ </color>";
                Vector3 e = _euler[i];

                sb.AppendLine($"{marker} {state}<color={(selected ? "#fff" : "#aab")}>{name,-20}</color> " +
                              $"{Axis(e.x, selected && _selectedAxis == 0)} " +
                              $"{Axis(e.y, selected && _selectedAxis == 1)} " +
                              $"{Axis(e.z, selected && _selectedAxis == 2)}");
            }

            sb.AppendLine("<color=#667>--------------------------------------------------</color>");
            sb.AppendLine("<color=#4fc58c>F5 play an emote</color>   <color=#9aa>F7</color> Sweep   <color=#9aa>Up/Down</color> bone   <color=#9aa>Tab</color> axis   <color=#9aa>Left/Right</color> +-5 deg (Shift 25, Ctrl 1)");
            sb.Append("<color=#9aa>Space</color> hand the bone back   <color=#9aa>Backspace</color> reset (Shift: all)   " +
                      $"<color=#9aa>scroll</color> camera pull-back ({_cameraDistance:0.0}m)   " +
                      "<color=#4fc58c>F6 measure the axes</color>   <color=#9aa>F9</color> save   <color=#9aa>F10</color> reload");
            return sb.ToString();
        }

        private static string Bar(float weight)
        {
            const int Width = 20;
            int filled = Mathf.Clamp(Mathf.RoundToInt(weight * Width), 0, Width);
            return "<color=#4fc58c>" + new string('=', filled) + "</color>"
                 + "<color=#556>" + new string('-', Width - filled) + "</color>";
        }

        private static string Axis(float value, bool selected)
        {
            string text = $"{value,7:0.0}";
            return selected ? $"<color=#4fc58c><b>{text}</b></color>" : $"<color=#889>{text}</color>";
        }
        private string VoiceLine()
        {
            if (_binder == null) return "Voice : <color=#889>Waiting for the bind</color>";

            Transform? witness = _binder.TalkWitness;
            if (witness == null) return "Voice : <color=#e07a6b>code_head_top not found</color>";

            float pitch = witness.localEulerAngles.x;
            if (pitch > 180f) pitch -= 360f;

            string loudness = "n/a";
            if (_target != null && _target.voiceChatFetched && _target.voiceChat != null)
            {
                loudness = _target.voiceChat.clipLoudness.ToString("0.000");
            }

            return $"Voice : clipLoudness <b>{loudness}</b>   code_head_top.x <b>{pitch,6:0.0}</b>  <color=#9aa>Must move when the target talks</color>";
        }

        private static string DisplayName(PlayerAvatar avatar)
        {
            if (avatar.photonView != null && avatar.photonView.Owner != null)
            {
                return avatar.photonView.Owner.NickName;
            }
            return avatar.name;
        }
    }
}
