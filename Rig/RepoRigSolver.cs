using UnityEngine;

namespace SomeEmotesREPO.Rig
{
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

        public float Weight { get; set; } = 1f;

        public RepoRigSolver(RepoRigBinder rig, EmoteProxyRig proxy)
        {
            _rig = rig;
            _proxy = proxy;

            _restAxis[(int)RigBone.ArmLeft] = Vector3.forward;
            _restAxis[(int)RigBone.ArmRight] = Vector3.forward;
            _restAxis[(int)RigBone.LegLeftTop] = Vector3.down;
            _restAxis[(int)RigBone.LegRightTop] = Vector3.down;
        }

        public void Solve()
        {
            if (Weight <= 0f) return;

            EnsureRestAxes();

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

            Aim(RigBone.ArmLeft, _proxy.Direction(ProxyBone.ArmLeft, ProxyBone.HandLeft));
            Aim(RigBone.ArmRight, _proxy.Direction(ProxyBone.ArmRight, ProxyBone.HandRight));
            Aim(RigBone.LegLeftTop, _proxy.Direction(ProxyBone.UpLegLeft, ProxyBone.FootLeft));
            Aim(RigBone.LegRightTop, _proxy.Direction(ProxyBone.UpLegRight, ProxyBone.FootRight));
        }

        private void Crouch()
        {
            Transform? root = _rig[RigBone.Root];
            if (root == null || _legScale <= 0f) return;

            float lowestFoot = Mathf.Min(_proxy.Position(ProxyBone.FootLeft).y, _proxy.Position(ProxyBone.FootRight).y);
            float lift = _proxy.Position(ProxyBone.Hips).y - lowestFoot;

            float drop = Mathf.Clamp((_proxyLegLength - lift) * _legScale, 0f, MaxCrouch);

            Vector3 target = _rootRestPosition + Vector3.down * drop;
            root.localPosition = Vector3.Lerp(_rootRestPosition, target, Weight);
        }

        public void ReleaseRoot()
        {
            if (!_restAxesMeasured) return;

            Transform? root = _rig[RigBone.Root];
            if (root != null) root.localPosition = _rootRestPosition;
        }

        private static Quaternion Frame(Vector3 up, Vector3 rightHint)
        {
            if (up.sqrMagnitude < 1e-6f || rightHint.sqrMagnitude < 1e-6f) return Quaternion.identity;

            up = up.normalized;
            Vector3 forward = Vector3.Cross(rightHint.normalized, up);
            if (forward.sqrMagnitude < 1e-6f) return Quaternion.identity;

            return Quaternion.LookRotation(forward.normalized, up);
        }

        private void OrientTo(RigBone bone, Quaternion orientationInCharacterSpace)
        {
            Transform? t = _rig[bone];
            if (t == null || t.parent == null) return;

            Quaternion world = _rig.Visuals.transform.rotation * orientationInCharacterSpace;
            Write(t, Quaternion.Inverse(t.parent.rotation) * world);
        }

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

        private void MeasureLegScale()
        {
            Transform? root = _rig[RigBone.Root];
            Transform? hip = _rig[RigBone.LegLeftTop];
            if (root == null || hip == null) return;

            _rootRestPosition = root.localPosition;

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

        private static bool IsOtherLimb(string name)
        {
            return name.StartsWith("ANIM ", System.StringComparison.Ordinal)
                && !name.EndsWith(" SCALE", System.StringComparison.Ordinal);
        }

        private void Write(Transform t, Quaternion target)
        {
            t.localRotation = Weight >= 1f ? target : Quaternion.Slerp(t.localRotation, target, Weight);
        }
    }
}
