using UnityEngine;

namespace SomeEmotesREPO.Rig
{
    public sealed class EmoteRigPlayer
    {
        public const float DefaultFadeIn = 0.25f;
        public const float DefaultFadeOut = 0.18f;

        public enum State
        {
            Idle,
            FadingIn,
            Holding,
            FadingOut,
        }

        private PlayerAvatar? _avatar;
        private RepoRigBinder? _rig;
        private EmoteProxyRig? _proxy;
        private RepoRigSolver? _solver;
        private EmoteSuppression? _suppression;

        private State _state = State.Idle;
        private float _weight;

        public float FadeInDuration { get; set; } = DefaultFadeIn;
        public float FadeOutDuration { get; set; } = DefaultFadeOut;

        public State Current => _state;

        public bool Active => _state != State.Idle;

        public bool Playing => _state == State.FadingIn || _state == State.Holding;

        public float Weight => Ease(_weight);

        public string ClipName { get; private set; } = string.Empty;

        public PlayerAvatar? Avatar => _avatar;

        public string LastInterruption { get; private set; } = string.Empty;

        public bool Play(PlayerAvatar avatar, AnimationClip clip, Quaternion? heading = null)
        {
            if (avatar == null || clip == null) return false;

            if (_avatar != avatar)
            {
                Cancel("the target changed");
                _avatar = avatar;
                _rig = null;
                _solver = null;
            }
            else if (_rig != null && _rig.Visuals == null)
            {
                _rig = null;
                _solver = null;
            }

            RepoRigBinder? rig = _rig;
            if (rig == null)
            {
                var visuals = VisualsOf(avatar);
                if (visuals == null)
                {
                    SomeEmotesREPO.Logger.LogWarning("[Emote] That avatar has no PlayerAvatarVisuals yet.");
                    return false;
                }

                if (!RepoRigBinder.TryBind(visuals, out var binder, out string error) || binder == null)
                {
                    SomeEmotesREPO.Logger.LogError($"[Emote] Could not bind the avatar rig: {error}");
                    return false;
                }
                rig = binder;
                _rig = binder;
            }

            EmoteProxyRig? proxy = _proxy;
            if (proxy == null)
            {
                proxy = EmoteProxyRig.Create();
                if (proxy == null) return false;
                _proxy = proxy;
            }

            _solver ??= new RepoRigSolver(rig, proxy);
            _suppression ??= EmoteSuppression.Begin(rig.Visuals, heading);

            _avatar = avatar;
            proxy.Play(clip);
            ClipName = clip.name;
            LastInterruption = string.Empty;
            _state = State.FadingIn;
            return true;
        }

        public void Stop()
        {
            if (_state == State.Idle || _state == State.FadingOut) return;
            _state = State.FadingOut;
        }

        public void Cancel(string reason)
        {
            if (_state == State.Idle) return;

            LastInterruption = reason;
            _proxy?.Stop();

            _rig?.ResetToRest();
            Release();
        }

        public void Tick(float deltaTime)
        {
            if (_state == State.Idle) return;

            string? interruption = InterruptReason();
            if (interruption != null)
            {
                SomeEmotesREPO.Logger.LogInfo($"[Emote] Stopped: {interruption}.");
                Cancel(interruption);
                return;
            }

            switch (_state)
            {
                case State.FadingIn:
                    _weight = Advance(_weight, 1f, FadeInDuration, deltaTime);
                    if (_weight >= 1f) _state = State.Holding;
                    break;

                case State.Holding:
                    _weight = 1f;
                    break;

                case State.FadingOut:
                    _weight = Advance(_weight, 0f, FadeOutDuration, deltaTime);
                    if (_weight <= 0f)
                    {
                        _solver?.ReleaseRoot();
                        Release();
                        return;
                    }
                    break;
            }

            float eased = Ease(_weight);
            if (_suppression != null) _suppression.Weight = eased;
            if (_solver != null)
            {
                _solver.Weight = eased;
                _solver.Solve();
            }
        }

        public void Dispose()
        {
            Cancel("disposed");
            if (_proxy != null) Object.Destroy(_proxy.gameObject);
            _proxy = null;
            _solver = null;
        }

        private string? InterruptReason()
        {
            if (_avatar == null) return "the avatar is gone";
            if (_rig == null || _rig.Visuals == null) return "the avatar visuals are gone";
            if (_avatar.deadSet) return "the player died";
            if (_avatar.isTumbling) return "the player is tumbling";
            if (_avatar.isDisabled) return "the player was disabled";
            return null;
        }

        private void Release()
        {
            _suppression?.End();
            _suppression = null;
            _weight = 0f;
            _state = State.Idle;
            ClipName = string.Empty;
        }

        private static float Advance(float weight, float target, float duration, float deltaTime)
        {
            return duration <= 0f ? target : Mathf.MoveTowards(weight, target, deltaTime / duration);
        }

        private static float Ease(float t) => t * t * (3f - 2f * t);

        internal static PlayerAvatarVisuals? VisualsOf(PlayerAvatar? avatar)
        {
            if (avatar == null) return null;
            if (avatar.playerAvatarVisuals != null) return avatar.playerAvatarVisuals;
            // PlayerAvatar and PlayerAvatarVisuals are siblings under a shared parent.
            return avatar.transform.parent != null
                ? avatar.transform.parent.GetComponentInChildren<PlayerAvatarVisuals>()
                : null;
        }
    }
}
