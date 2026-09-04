using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using SomeEmotesREPO.Rig;
using UnityEngine;

namespace SomeEmotesREPO
{
    public class EmoteSystem : MonoBehaviourPun
    {
        private const float BindDelay = 0.75f;
        private const float CameraDistanceMin = 0.5f;
        private const float CameraDistanceMax = 4f;

        private static EmoteSystem? instance;
        public static EmoteSystem? Instance => instance;

        public static bool Ready => instance != null && instance.ready;

        private static float camOffset = 3.25f;

        private readonly EmoteRigPlayer _player = new EmoteRigPlayer();

        private PlayerAvatar playerAvatar = null!;
        private PlayerAvatarVisuals? playerVisuals;
        private Transform? camTransform;
        private bool ready;

        private bool isEmoting;
        private int currentEmote = -1;
        private float frozenYaw;

        private bool controlArmed;

        public bool IsEmoting => isEmoting;

        internal bool flashlightHeld;

        public int CurrentEmote => currentEmote;

        public string CurrentEmoteName => EmoteCatalog.NameAt(currentEmote);

        public float FrozenYaw => frozenYaw;

        public int ViewId => photonView != null ? photonView.ViewID : 0;

        public bool IsDead => playerAvatar == null || playerAvatar.deadSet;

        private bool IsMine => photonView != null && photonView.IsMine;

        private void Awake()
        {
            ready = false;
            if (Camera.main != null) camTransform = Camera.main.transform;
        }

        public void SetPlayerAvatar(PlayerAvatar avatar)
        {
            playerAvatar = avatar;
            StartCoroutine(Bind());
        }

        private IEnumerator Bind()
        {
            yield return new WaitForSeconds(BindDelay);

            if (playerAvatar == null) yield break;

            playerVisuals = EmoteRigPlayer.VisualsOf(playerAvatar);
            if (playerVisuals == null)
            {
                SomeEmotesREPO.Logger.LogWarning("[Emote] No PlayerAvatarVisuals on this avatar; emotes are disabled for it.");
                yield break;
            }

            EmoteCatalog.Load();
            if (!EmoteCatalog.Loaded)
            {
                SomeEmotesREPO.Logger.LogError("[Emote] No emote loaded; the system stays off.");
                yield break;
            }

            if (IsMine)
            {
                instance = this;
                EmoteNetwork.SendStateRequest();
            }

            ready = true;
        }

        public void SetFavorite(string favorite) => SetFavorites(new List<string> { favorite });

        public void SetFavorites(List<string> favorites) => EmoteLoader.Instance?.AddFavorites(favorites);
        public void PlayEmote(string emoteName)
        {
            int index = EmoteCatalog.IndexOf(emoteName);
            if (index < 0)
            {
                SomeEmotesREPO.Logger.LogWarning($"[Emote] Unknown emote '{emoteName}'.");
                return;
            }
            PlayEmote(index);
        }

        public void PlayEmote(int emoteIndex)
        {
            if (!ready || !IsMine || !EmoteCatalog.IsValid(emoteIndex)) return;

            EmoteWheel.Instance?.Close();

            float yaw = playerAvatar.transform.eulerAngles.y;

            ApplyPlay(emoteIndex, yaw);
            EmoteNetwork.SendPlay(photonView, EmoteCatalog.NameAt(emoteIndex), yaw);
        }

        public void StopEmote()
        {
            if (!IsMine || !isEmoting) return;

            EmoteWheel.Instance?.Close();
            ApplyStop();
            EmoteNetwork.SendStop(photonView);
        }

        internal void ApplyPlay(int emoteIndex, float yaw)
        {
            if (!ready || playerAvatar == null) return;

            AnimationClip? clip = EmoteCatalog.ClipAt(emoteIndex);
            if (clip == null) return;

            if (!_player.Play(playerAvatar, clip, Quaternion.Euler(0f, yaw, 0f))) return;

            currentEmote = emoteIndex;
            frozenYaw = yaw;
            isEmoting = true;
            controlArmed = false;

            SomeEmotesREPO.Logger.LogInfo($"[Emote] {OwnerName()} plays '{EmoteCatalog.NameAt(emoteIndex)}'.");
        }

        internal void ApplyStop()
        {
            isEmoting = false;
            currentEmote = -1;
            _player.Stop();
        }

        private void Update()
        {
            if (!ready) return;

            if (IsMine)
            {
                if (isEmoting)
                {
                    bool suppressed = InputManager.instance != null
                                   && InputManager.instance.disableMovementTimer > 0f;

                    if (!controlArmed) controlArmed = !suppressed && !TookControlBack();
                    else if (TookControlBack()) StopEmote();
                }

                if (camTransform != null)
                {
                    camTransform.localPosition = new Vector3(0f, 0f, -camOffset * _player.Weight);

                    bool choosing = EmoteWheel.Instance != null && EmoteWheel.Instance.Visible;

                    if (_player.Active && !choosing)
                    {
                        float scroll = Input.mouseScrollDelta.y;
                        if (scroll != 0f)
                        {
                            camOffset = Mathf.Clamp(camOffset - scroll * Time.deltaTime * 20f, CameraDistanceMin, CameraDistanceMax);
                        }
                    }
                }

            }
        }

        private void LateUpdate()
        {
            if (_player.Active)
            {
                if (IsMine && playerVisuals != null) playerVisuals.ShowSelfOverride(0.1f);

                _player.Tick(Time.deltaTime);
            }
            if (isEmoting && !_player.Playing)
            {
                isEmoting = false;
                currentEmote = -1;
                if (IsMine && photonView != null) EmoteNetwork.SendStop(photonView);
            }
        }

        private bool TookControlBack()
        {
            if (InputManager.instance == null) return false;

            if (InputManager.instance.GetMovement().sqrMagnitude > 0.01f) return true;

            return InputManager.instance.KeyDown(InputKey.Jump)
                || InputManager.instance.KeyDown(InputKey.Interact)
                || InputManager.instance.KeyDown(InputKey.Sprint)
                || InputManager.instance.KeyDown(InputKey.Crouch)
                || InputManager.instance.KeyDown(InputKey.Tumble);
        }

        private string OwnerName()
        {
            return photonView != null && photonView.Owner != null ? photonView.Owner.NickName : name;
        }

        private void OnDestroy()
        {
            _player.Dispose();
            if (instance == this) instance = null;
        }
    }
}
