using UnityEngine;

namespace SomeEmotesREPO.Rig
{
    /// <summary>
    /// Phase 4: one emote, from start to finish, on one avatar.
    ///
    /// Everything phases 1 to 3 built is a still photograph: bind the rig, silence the
    /// game, read the dancer, write the pose. This is the part that has a beginning and
    /// an end, and it exists because both of those were, until now, instantaneous:
    ///
    ///   Starting cut the avatar from its walk cycle to the dance in a single frame.
    ///   Ending cut it back, from a raised arm to whatever the game happened to be
    ///   playing. Both read as a glitch rather than as a move.
    ///
    ///   Worse, nothing ended an emote except asking for it. A player who died, tumbled
    ///   down a stairwell or got extracted mid-dance kept dancing, holding a pose the
    ///   ragdoll was supposed to own.
    ///
    /// A weight ramp answers the first, because the solver and the suppression both
    /// blend against the pose the game wrote this frame, so a weight between 0 and 1 is a
    /// genuine crossfade rather than a dimmer. A state check each frame answers the
    /// second.
    ///
    /// One instance per dancing avatar. Nothing here is networked: phase 5 decides who
    /// dances and when, and calls Play and Stop.
    /// </summary>
    public sealed class EmoteRigPlayer
    {
        /// <summary>
        /// Long enough to read as a move, short enough that the dance is not late.
        /// Out is quicker than in: getting control back has to feel immediate.
        /// </summary>
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

        /// <summary>True while the avatar is under our control, fades included.</summary>
        public bool Active => _state != State.Idle;

        /// <summary>True while the emote is meant to keep going, so a caller can toggle.</summary>
        public bool Playing => _state == State.FadingIn || _state == State.Holding;

        /// <summary>The eased blend actually applied, for the overlay.</summary>
        public float Weight => Ease(_weight);

        public string ClipName { get; private set; } = string.Empty;

        public PlayerAvatar? Avatar => _avatar;

        /// <summary>Why the last emote ended by itself, empty if it was asked to stop.</summary>
        public string LastInterruption { get; private set; } = string.Empty;

        /// <summary>
        /// Starts, or switches, an emote on this avatar.
        ///
        /// Switching while one is already running keeps the current weight instead of
        /// dropping through rest: the two dances crossfade into each other, since the
        /// solver simply starts reading a different proxy pose.
        /// </summary>
        public bool Play(PlayerAvatar avatar, AnimationClip clip, Quaternion? heading = null)
        {
            if (avatar == null || clip == null) return false;

            // The rig, the solver and the proxy survive between emotes on purpose: they
            // carry the measured rest axes and the leg scale, which cost a log line and a
            // pose reading each time. They are only invalid if the body they describe has
            // changed or been destroyed.
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

            // A scene load destroys the proxy, so re-create rather than assume.
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

        /// <summary>Ends the emote gracefully: the pose blends back into the game.</summary>
        public void Stop()
        {
            if (_state == State.Idle || _state == State.FadingOut) return;
            _state = State.FadingOut;
        }

        /// <summary>
        /// Ends the emote now, with no fade. For the cases where blending out is worse
        /// than cutting: the ragdoll has taken the body, or there is no body left.
        /// </summary>
        public void Cancel(string reason)
        {
            if (_state == State.Idle) return;

            LastInterruption = reason;
            _proxy?.Stop();

            // Unlike a fade-out, this leaves a full-weight pose on the bones, so the rig
            // has to be cleared. The Animator overwrites it on the next frame anyway,
            // which is why one frame of rest pose is the right price here and the wrong
            // one at the end of a fade.
            _rig?.ResetToRest();
            Release();
        }

        /// <summary>Call from LateUpdate, before nothing else: it drives the solver itself.</summary>
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
                        // No ResetToRest here on purpose. The bones have just blended into
                        // the game pose, so the only thing that would not have unwound by
                        // itself is the height.
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

        /// <summary>Drops the proxy skeleton. Call when the owner is destroyed.</summary>
        public void Dispose()
        {
            Cancel("disposed");
            if (_proxy != null) Object.Destroy(_proxy.gameObject);
            _proxy = null;
            _solver = null;
        }

        /// <summary>
        /// The states where holding a dance pose is wrong rather than merely late.
        ///
        /// Death and tumbling both hand the body to something else, physics in one case
        /// and the death sequence in the other, and both would be fighting us for the
        /// same bones. isDisabled covers extraction and the end of a level.
        /// </summary>
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

        /// <summary>
        /// Smoothstep. A linear ramp starts and stops abruptly even though the numbers
        /// are continuous, because the eye reads the change in speed, not the position.
        /// </summary>
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
