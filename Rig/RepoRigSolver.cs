using UnityEngine;

namespace SomeEmotesREPO.Rig
{
    /// <summary>
    /// Turns a human pose into a REPO robot pose.
    ///
    /// The robot has no elbow, no knee and no spine, so there is nothing to retarget
    /// joint to joint. What survives the translation is, for each limb, the *direction*
    /// it points: shoulder to hand, hip to foot. That is exactly the constraint the 37
    /// emotes were picked under, so aiming is not an approximation of the dance here,
    /// it is the dance.
    ///
    /// Every mapping below comes from measurements taken on the live rig, not from
    /// guesses. Two of them contradicted the original plan:
    ///
    ///   The legs read upside down. ANIM LEG * TOP sits at the hip (y 0.55) and ANIM
    ///   LEG * BOT at the floor (y 0.00), yet BOT is TOP's parent. So TOP is the joint
    ///   that swings a leg from the hip, and BOT swings the whole leg about the ground
    ///   contact. Only TOP is driven; BOT stays with the game.
    ///
    ///   The arms point forward at rest, not down. Their local +Z runs along the limb
    ///   and their frames are rolled 75 degrees about it, mirrored left to right. The
    ///   arms-by-the-side pose belongs to the idle clip, not to the rig.
    ///
    /// Two bones are deliberately left to the game. ANIM HEAD TOP is the jaw lid that
    /// PlayerAvatarTalkAnimation opens from the microphone, through its code_head_top
    /// child; driving it would trade the talking mouth for a stiffer head. ANIM LEG *
    /// BOT would fight the hip swing for no gain.
    /// </summary>
    public sealed class RepoRigSolver
    {
        private static readonly RigBone[] LimbBones =
        {
            RigBone.ArmLeft, RigBone.ArmRight, RigBone.LegLeftTop, RigBone.LegRightTop,
        };

        private readonly RepoRigBinder _rig;
        private readonly EmoteProxyRig _proxy;
        private readonly Vector3[] _restAxis = new Vector3[RepoRigBinder.BoneCount];
        private bool _restAxesMeasured;

        /// <summary>Deepest squat the avatar will take, in metres.</summary>
        private const float MaxCrouch = 0.45f;

        private Vector3 _rootRestPosition;
        private float _proxyLegLength;
        private float _legScale;

        /// <summary>
        /// 0 leaves the pose the game Animator produced, 1 is the emote alone. Anything
        /// between is a genuine blend, because the solver runs after the Animator and so
        /// reads the game's own pose as its starting point. Phase 4's crossfade is just
        /// this value moving.
        /// </summary>
        public float Weight { get; set; } = 1f;

        public RepoRigSolver(RepoRigBinder rig, EmoteProxyRig proxy)
        {
            _rig = rig;
            _proxy = proxy;

            // Fallbacks, used only if a limb has no mesh to measure against.
            _restAxis[(int)RigBone.ArmLeft] = Vector3.forward;
            _restAxis[(int)RigBone.ArmRight] = Vector3.forward;
            _restAxis[(int)RigBone.LegLeftTop] = Vector3.down;
            _restAxis[(int)RigBone.LegRightTop] = Vector3.down;
        }

        /// <summary>Call from LateUpdate, so it lands after the game Animator has written.</summary>
        public void Solve()
        {
            if (Weight <= 0f) return;

            EnsureRestAxes();

            // The torso is built from anatomy, not from bone rotations.
            //
            // Two earlier attempts failed here, and both failed for the same underlying
            // reason. Aiming a bone's +Y at the next joint welds the torso into a block,
            // because a spine is near vertical everywhere and aiming an axis discards the
            // twist about it. Reading the bones' rotations instead measures them against
            // the prefab's saved pose, and that prefab was saved mid-animation: its torso
            // already leans 34 degrees, so every rotation inherited that lean.
            //
            // Joint *positions* carry no such reference. The hip line, the shoulder line
            // and the spine give a complete, unambiguous frame, including the twist, and
            // it does not matter what pose the prefab was saved in.
            Vector3 hipLine = _proxy.Position(ProxyBone.UpLegRight) - _proxy.Position(ProxyBone.UpLegLeft);
            Vector3 shoulderLine = _proxy.Position(ProxyBone.ArmRight) - _proxy.Position(ProxyBone.ArmLeft);

            Quaternion pelvis = Frame(_proxy.Position(ProxyBone.Spine1) - _proxy.Position(ProxyBone.Hips), hipLine);
            Quaternion chest = Frame(_proxy.Position(ProxyBone.Neck) - _proxy.Position(ProxyBone.Spine1), shoulderLine);
            Quaternion head = Frame(_proxy.Position(ProxyBone.HeadTop) - _proxy.Position(ProxyBone.Head), shoulderLine);

            OrientTo(RigBone.Root, pelvis);
            OrientTo(RigBone.BodyBottom, pelvis);
            OrientTo(RigBone.BodyTop, chest);
            OrientTo(RigBone.HeadBottom, head);

            Crouch();

            // Limbs point at the far joint. The elbow and the knee are absorbed: the
            // capsule simply aims at the hand, or at the foot.
            Aim(RigBone.ArmLeft, _proxy.Direction(ProxyBone.ArmLeft, ProxyBone.HandLeft));
            Aim(RigBone.ArmRight, _proxy.Direction(ProxyBone.ArmRight, ProxyBone.HandRight));
            Aim(RigBone.LegLeftTop, _proxy.Direction(ProxyBone.UpLegLeft, ProxyBone.FootLeft));
            Aim(RigBone.LegRightTop, _proxy.Direction(ProxyBone.UpLegRight, ProxyBone.FootRight));
        }

        /// <summary>
        /// Lowers the whole body when the dancer bends their legs.
        ///
        /// Only the vertical part of the root motion is taken. Horizontal travel is
        /// dropped on purpose: an emote has to stay where the player is standing, or it
        /// walks through walls and drifts away from where the network thinks the player
        /// is. So the avatar squats and rises, but never leaves its spot.
        ///
        /// The amount comes from how far the dancer's hips have dropped below a straight
        /// leg, converted with the ratio of the two skeletons' leg lengths. Leg length is
        /// used rather than any pose because it is a fixed property of the rig, and this
        /// prefab's saved pose has already proven untrustworthy once.
        /// </summary>
        private void Crouch()
        {
            Transform? root = _rig[RigBone.Root];
            if (root == null || _legScale <= 0f) return;

            float lowestFoot = Mathf.Min(_proxy.Position(ProxyBone.FootLeft).y, _proxy.Position(ProxyBone.FootRight).y);
            float lift = _proxy.Position(ProxyBone.Hips).y - lowestFoot;

            // Clamped so an upside-down dance, where the feet end up above the hips,
            // cannot bury the avatar in the floor.
            float drop = Mathf.Clamp((_proxyLegLength - lift) * _legScale, 0f, MaxCrouch);

            // Blended from the measured rest, not from the bone's current value. Every
            // other channel here blends against whatever the game Animator wrote this
            // frame, which is a real crossfade because the Animator rewrites it every
            // frame. Position is the exception: not every game clip animates ANIM BOT
            // position, so the current value is often just our own previous frame, and
            // blending against it would never come back to standing.
            Vector3 target = _rootRestPosition + Vector3.down * drop;
            root.localPosition = Vector3.Lerp(_rootRestPosition, target, Weight);
        }

        /// <summary>
        /// Hands the height back. Called when an emote ends: at that point the rotations
        /// have already blended into the game pose and the Animator owns them again, but
        /// the crouch would keep whatever residue the last blended frame left behind.
        /// </summary>
        public void ReleaseRoot()
        {
            if (!_restAxesMeasured) return;

            Transform? root = _rig[RigBone.Root];
            if (root != null) root.localPosition = _rootRestPosition;
        }

        /// <summary>
        /// Builds an orientation from an up axis and a sideways hint, the way a torso is
        /// described by its spine and its shoulder line. Returns identity for a
        /// degenerate pair rather than a wild rotation.
        /// </summary>
        private static Quaternion Frame(Vector3 up, Vector3 rightHint)
        {
            if (up.sqrMagnitude < 1e-6f || rightHint.sqrMagnitude < 1e-6f) return Quaternion.identity;

            up = up.normalized;
            Vector3 forward = Vector3.Cross(rightHint.normalized, up);
            if (forward.sqrMagnitude < 1e-6f) return Quaternion.identity;

            return Quaternion.LookRotation(forward.normalized, up);
        }

        /// <summary>
        /// Gives a bone an absolute orientation expressed in character space. Routed
        /// through world space so the parent chain, which by then already carries the
        /// pelvis and everything above, cancels exactly.
        /// </summary>
        private void OrientTo(RigBone bone, Quaternion orientationInCharacterSpace)
        {
            Transform? t = _rig[bone];
            if (t == null || t.parent == null) return;

            // Every ANIM bone rests axis-aligned with the avatar, so a character-space
            // orientation is exactly what the bone should end up carrying.
            Quaternion world = _rig.Visuals.transform.rotation * orientationInCharacterSpace;
            Write(t, Quaternion.Inverse(t.parent.rotation) * world);
        }

        /// <summary>
        /// Rotates a bone so the limb it carries points along a direction given in
        /// character space. Same world-space route as Orient, for the same reason.
        /// </summary>
        private void Aim(RigBone bone, Vector3 directionInCharacterSpace)
        {
            if (directionInCharacterSpace.sqrMagnitude < 1e-6f) return;

            Transform? t = _rig[bone];
            if (t == null || t.parent == null) return;

            Vector3 world = _rig.Visuals.transform.TransformDirection(directionInCharacterSpace);
            Vector3 inParent = t.parent.InverseTransformDirection(world);
            if (inParent.sqrMagnitude < 1e-6f) return;

            Write(t, Quaternion.FromToRotation(_restAxis[(int)bone], inParent.normalized));
        }

        /// <summary>
        /// Finds, once, which way each limb actually points at rest.
        ///
        /// These used to be hard-coded as +Z for arms and -Y for legs, read off the
        /// measurement pass. That was right for the left arm and both legs, and wrong by
        /// 90 degrees for the right arm, whose mesh hangs below two extra code
        /// transforms and a scale node. Measuring removes the guess: put the bone back
        /// to rest, look at where its mesh sits in the bone's own frame, and use that.
        ///
        /// Safe to do here rather than at construction: this runs in LateUpdate, so the
        /// code_* transforms have already been held at rest by EmoteSuppression this
        /// frame, and the reading describes the rig instead of a live pose.
        /// </summary>
        private void EnsureRestAxes()
        {
            if (_restAxesMeasured) return;
            _restAxesMeasured = true;

            foreach (RigBone bone in LimbBones)
            {
                Transform? t = _rig[bone];
                if (t == null) continue;

                Quaternion previous = t.localRotation;
                t.localRotation = Quaternion.identity;

                Renderer? mesh = FindMesh(t);
                Vector3 axis = mesh != null ? t.InverseTransformPoint(mesh.bounds.center) : Vector3.zero;

                t.localRotation = previous;

                if (axis.sqrMagnitude < 1e-6f)
                {
                    SomeEmotesREPO.Logger.LogWarning($"[Solver] No mesh under {RepoRigBinder.NameOf(bone)}; keeping its fallback axis.");
                    continue;
                }

                _restAxis[(int)bone] = axis.normalized;
                SomeEmotesREPO.Logger.LogInfo($"[Solver] {RepoRigBinder.NameOf(bone)} rest axis {_restAxis[(int)bone]}");
            }

            MeasureLegScale();
        }

        /// <summary>
        /// Works out how a metre of dancer converts into a metre of robot, from the two
        /// leg lengths. Both are pose-independent, unlike anything read off a saved pose.
        /// </summary>
        private void MeasureLegScale()
        {
            Transform? root = _rig[RigBone.Root];
            Transform? hip = _rig[RigBone.LegLeftTop];
            if (root == null || hip == null) return;

            _rootRestPosition = root.localPosition;

            // REPO's hip joint sits directly above the foot end of the rig's root.
            float repoLegLength = Mathf.Abs(root.InverseTransformPoint(hip.position).y);

            _proxyLegLength =
                _proxy.Length(ProxyBone.UpLegLeft, ProxyBone.KneeLeft) +
                _proxy.Length(ProxyBone.KneeLeft, ProxyBone.FootLeft);

            _legScale = _proxyLegLength > 0.01f ? repoLegLength / _proxyLegLength : 0f;
            SomeEmotesREPO.Logger.LogInfo(
                $"[Solver] Leg length: REPO {repoLegLength:0.000}, dancer {_proxyLegLength:0.000}, scale {_legScale:0.000}");
        }

        private static Renderer? FindMesh(Transform bone)
        {
            for (int i = 0; i < bone.childCount; i++)
            {
                Transform child = bone.GetChild(i);
                // Stop at the next limb, but not at this limb's own scale node. The right
                // arm's mesh hangs under ANIM ARM R SCALE, so refusing to cross any name
                // starting with "ANIM " hid it entirely and left the arm on its hard-coded
                // fallback axis, 90 degrees out.
                if (IsOtherLimb(child.name)) continue;

                if (child.name.StartsWith("mesh_", System.StringComparison.OrdinalIgnoreCase))
                {
                    var renderer = child.GetComponent<Renderer>();
                    if (renderer != null) return renderer;
                }

                var deeper = FindMesh(child);
                if (deeper != null) return deeper;
            }
            return null;
        }

        /// <summary>A different bone, as opposed to this one's own "* SCALE" node.</summary>
        private static bool IsOtherLimb(string name)
        {
            return name.StartsWith("ANIM ", System.StringComparison.Ordinal)
                && !name.EndsWith(" SCALE", System.StringComparison.Ordinal);
        }

        private void Write(Transform t, Quaternion target)
        {
            // At weight 1 this is a plain assignment; below it, the bone's current value
            // is whatever the game Animator wrote this frame, which is what we blend from.
            t.localRotation = Weight >= 1f ? target : Quaternion.Slerp(t.localRotation, target, Weight);
        }
    }
}
