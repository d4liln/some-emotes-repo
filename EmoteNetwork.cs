using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace SomeEmotesREPO
{
    public class EmoteNetwork : MonoBehaviour, IOnEventCallback
    {
        // Photon reserves 200 and above. REPO itself uses 123 and 124 ,
        // so these sit well clear of both.
        private const byte PlayEvent = 171;
        private const byte StopEvent = 172;
        private const byte RequestState = 173;
        private const float PendingLifetime = 5f;

        private static readonly SendOptions Reliable = SendOptions.SendReliable;

        private static readonly RaiseEventOptions ToOthers = new RaiseEventOptions
        {
            Receivers = ReceiverGroup.Others,
        };

        private struct Pending
        {
            public int ViewId;
            public int EmoteIndex;
            public float Yaw;
            public float Expiry;
            public bool Stop;
        }

        private static readonly HashSet<string> Unknown = new HashSet<string>(System.StringComparer.Ordinal);

        private readonly List<Pending> _pending = new List<Pending>();

        public static EmoteNetwork? Instance { get; private set; }

        private static bool listening;

        private void Awake()
        {
            Instance = this;
        }

        public static void Listen()
        {
            if (listening || Instance == null) return;

            try
            {
                PhotonNetwork.AddCallbackTarget(Instance);
                listening = true;
                SomeEmotesREPO.Logger.LogInfo("[Net] Listening for emote events.");
            }
            catch (System.Exception e)
            {
                SomeEmotesREPO.Logger.LogError($"[Net] Could not subscribe to Photon events: {e.Message}");
            }
        }

        private void OnDestroy()
        {
            if (listening)
            {
                PhotonNetwork.RemoveCallbackTarget(this);
                listening = false;
            }
            if (Instance == this) Instance = null;
        }

        public static void SendPlay(PhotonView view, string emote, float yaw)
        {
            if (!Connected(view) || string.IsNullOrEmpty(emote)) return;
            PhotonNetwork.RaiseEvent(PlayEvent, new object[] { view.ViewID, emote, yaw }, ToOthers, Reliable);
        }

        public static void SendStop(PhotonView view)
        {
            if (!Connected(view)) return;
            PhotonNetwork.RaiseEvent(StopEvent, new object[] { view.ViewID }, ToOthers, Reliable);
        }
        public static void SendStateRequest()
        {
            if (!PhotonNetwork.InRoom) return;
            PhotonNetwork.RaiseEvent(RequestState, null, ToOthers, Reliable);
        }

        private static bool Connected(PhotonView view)
        {
            return view != null && PhotonNetwork.InRoom;
        }

        public void OnEvent(EventData photonEvent)
        {
            switch (photonEvent.Code)
            {
                case PlayEvent:
                    if (Unpack(photonEvent, 3, out object[] play))
                    {
                        Apply((int)play[0], play[1] as string ?? string.Empty, (float)play[2], stop: false);
                    }
                    break;

                case StopEvent:
                    if (Unpack(photonEvent, 1, out object[] stop))
                    {
                        Apply((int)stop[0], string.Empty, 0f, stop: true);
                    }
                    break;

                case RequestState:
                    Answer(photonEvent.Sender);
                    break;
            }
        }

        private static bool Unpack(EventData photonEvent, int length, out object[] content)
        {
            content = photonEvent.CustomData as object[] ?? System.Array.Empty<object>();
            if (content.Length >= length) return true;

            SomeEmotesREPO.Logger.LogWarning($"[Net] Ignored a malformed emote event ({photonEvent.Code}).");
            return false;
        }

        private void Apply(int viewId, string emote, float yaw, bool stop)
        {
            int emoteIndex = 0;

            if (!stop)
            {
                emoteIndex = EmoteCatalog.IndexOf(emote);
                if (emoteIndex < 0)
                {
                    if (Unknown.Add(emote))
                    {
                        SomeEmotesREPO.Logger.LogWarning(
                            $"[Net] A player danced '{emote}', which is not in this install " +
                            $"({EmoteCatalog.Count} emotes, signature {EmoteCatalog.Signature}). " +
                            "Their emote set differs from yours, so that one is skipped here.");
                    }
                    return;
                }
            }

            var system = Resolve(viewId);
            if (system != null)
            {
                if (stop) system.ApplyStop();
                else system.ApplyPlay(emoteIndex, yaw);
                return;
            }
            _pending.Add(new Pending
            {
                ViewId = viewId,
                EmoteIndex = emoteIndex,
                Yaw = yaw,
                Stop = stop,
                Expiry = Time.time + PendingLifetime,
            });
        }

        private void Answer(int actorNumber)
        {
            var local = EmoteSystem.Instance;
            if (local == null || !local.IsEmoting) return;

            Player? target = null;
            foreach (var player in PhotonNetwork.PlayerListOthers)
            {
                if (player.ActorNumber == actorNumber) target = player;
            }
            if (target == null) return;

            var options = new RaiseEventOptions { TargetActors = new[] { actorNumber } };
            PhotonNetwork.RaiseEvent(
                PlayEvent,
                new object[] { local.ViewId, local.CurrentEmoteName, local.FrozenYaw },
                options,
                Reliable);
        }

        private static EmoteSystem? Resolve(int viewId)
        {
            PhotonView? view = PhotonView.Find(viewId);
            return view == null ? null : view.GetComponent<EmoteSystem>();
        }

        private void Update()
        {
            if (_pending.Count == 0) return;

            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                Pending item = _pending[i];

                var system = Resolve(item.ViewId);
                if (system != null)
                {
                    if (item.Stop) system.ApplyStop();
                    else system.ApplyPlay(item.EmoteIndex, item.Yaw);
                    _pending.RemoveAt(i);
                }
                else if (Time.time >= item.Expiry)
                {
                    _pending.RemoveAt(i);
                }
            }
        }
    }
}
