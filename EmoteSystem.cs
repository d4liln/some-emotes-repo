using Photon.Pun;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using UnityEngine;
using System;

namespace SomeEmotesREPO
{
    public class EmoteSystem : MonoBehaviourPun
    {
        private bool isEmoting = false;
        private int emoteId = -1;
        private float emoteTime = 0f;
        private float initialRot = 0f;

        bool ready = false;

        public static bool Ready
        {
            get
            {
                if (!instance) return false;
                return instance.ready;
            }
        }

        public bool IsEmoting
        {
            get
            {
                return isEmoting;
            }
            set
            {
                isEmoting = value;

                if (isEmoting)
                {
                    emoteTime = 0f;
                }
            }
        }

        private Transform camTransform;

        private PlayerAvatar playerAvatar;
        private PlayerAvatarVisuals playerVisuals;

        private static float initalCamOffset = 3.25f;
        private static float camOffset = initalCamOffset;

        private static EmoteSystem instance;
        public static EmoteSystem Instance => instance;

        private EmoteLauncher emoteLauncher;
        private Animator animator;

        private PhotonView PV
        {
            get
            {
                return playerVisuals.playerAvatar.photonView;
            }
        }

        private void Awake()
        {
            ready = false;
            if (Camera.main != null) camTransform = Camera.main.transform;
        }

        public List<string> FetchEmotes(int startIndex, int count)
        {
            return emoteLauncher.EmoteNames
                .Skip(Math.Max(startIndex, 0))
                .Take(Math.Max(count, 0))
                .ToList();
        }

        public void SetFavorite(string fav)
        {
            SetFavorites(new List<string>() { fav });
        }
        public void SetFavorites(List<string> favs)
        {
            emoteLauncher.SetFavorites(favs);
        }

        public void SetPlayerAvatar(PlayerAvatar pa)
        {
            playerAvatar = pa;
            StartCoroutine(SetVisuals());
        }

        private IEnumerator SetVisuals()
        {
            yield return new WaitForSeconds(0.75f);

            if (playerAvatar != null)
            {
                playerVisuals = playerAvatar.playerAvatarVisuals;
                if (playerVisuals == null)
                {
                    playerVisuals = playerAvatar.transform.parent.GetComponentInChildren<PlayerAvatarVisuals>();
                }

                if (playerVisuals != null)
                {
                    animator = playerVisuals.GetComponentInChildren<Animator>();

                    if (animator == null)
                    {
                        SomeEmotesREPO.Logger.LogWarning("Animator object not found on player visuals. Emote system visual features disabled.");
                    }
                }
            }

            if (PV.IsMine)
            {
                instance = this;
            }

            if (GameManager.Multiplayer())
            {
                emoteLauncher = EmoteLoader.Instance.LoadEmote(playerAvatar);
                emoteLauncher.emoteSystem = this;
            }

            ready = true;
        }

        public void PlayEmote(string emoteId)
        {
            if (!PV.IsMine) return;

            EmoteSelectionManager.Instance.SetVisible(false);
            IsEmoting = true;
            photonView.RPC(nameof(RPC_PlayEmote), RpcTarget.All, emoteId, initialRot);
        }

        public void StopEmote()
        {
            if (!PV.IsMine) return;

            EmoteSelectionManager.Instance.SetVisible(false);
            IsEmoting = false;
            photonView.RPC(nameof(RPC_StopEmote), RpcTarget.All);
        }

        [PunRPC]
        private void RPC_PlayEmote(string emoteId, float _initialRot)
        {
            IsEmoting = true;

            if (emoteLauncher == null) return;

            emoteLauncher.SetRotation(_initialRot);
            emoteLauncher.Animate(emoteId);
            SomeEmotesREPO.Logger.LogInfo($"[{photonView.Owner.NickName}] played emote {emoteId}.");
        }

        [PunRPC]
        private void RPC_StopEmote()
        {
            IsEmoting = false;

            if (emoteLauncher != null)
            {
                emoteLauncher.StopEmote();
            }
        }

        void Update()
        {
            if (!ready) return;

            if (IsEmoting)
            {
                emoteTime += Time.deltaTime;

                if (IsPlayerTakeControlBack())
                {
                    IsEmoting = false;
                    StopEmote();
                }
            }

            if (PV.IsMine && camTransform)
            {
                if (isEmoting) camTransform.localPosition = new Vector3(0f, 0f, -camOffset);
                else camTransform.localPosition = Vector3.zero;

                float scroll = Input.mouseScrollDelta.y;
                if (scroll != 0f)
                {
                    camOffset -= scroll * Time.deltaTime * 20f;
                    camOffset = Mathf.Clamp(camOffset, 0.5f, 4f); 
                }
            }

            if (playerVisuals)
            {
                if (PV.IsMine && !playerAvatar.deadSet && Input.GetKeyDown(EmoteLoader.PanelKey) && !ChatManager.instance.chatActive)
                {
                    EmoteSelectionManager.Instance.SetVisible(!EmoteSelectionManager.Instance.Visible);
                }

                if (!PV.IsMine)
                {
                    if (animator != null)
                    {
                        animator.enabled = !IsEmoting;
                    }
                    playerVisuals.meshParent.SetActive(!IsEmoting);
                }
                else
                {
                    //according to repo PlayerAvatarVisuals.Start(), ignores the visuals for the client (always hidden)

                    initialRot = playerAvatar.transform.eulerAngles.y;
                }
            }
        }

        public bool IsPlayerTakeControlBack()
        {
            if (!PV.IsMine)
                return false;

            Vector2 move = InputManager.instance.GetMovement();
            if (move.sqrMagnitude > 0.01f)
                return true;

            if (InputManager.instance.KeyDown(InputKey.Jump) ||
                InputManager.instance.KeyDown(InputKey.Interact) ||
                InputManager.instance.KeyDown(InputKey.Sprint) ||
                InputManager.instance.KeyDown(InputKey.Crouch) ||
                InputManager.instance.KeyDown(InputKey.Tumble))
            {
                return true;
            }

            return false;
        }

        public void OnDestroy()
        {
            if (emoteLauncher != null)
            {
                Destroy(emoteLauncher.gameObject);
            }
        }
    }
}

