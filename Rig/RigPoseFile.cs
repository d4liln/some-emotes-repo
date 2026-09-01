using System;
using System.Collections.Generic;
using UnityEngine;

namespace SomeEmotesREPO.Rig
{
    /// <summary>
    /// One bone of a hand-authored pose. Angles are local eulers, in the bone's own
    /// space, applied on top of the rest pose (which is identity for every ANIM node).
    /// </summary>
    [Serializable]
    public class RigBonePose
    {
        public string bone = string.Empty;
        public Vector3 euler = Vector3.zero;
        public Vector3 scale = Vector3.one;

        /// <summary>False leaves this bone to the game Animator, so you can A/B a single joint.</summary>
        public bool driven = true;

        /// <summary>Scale is only meaningful on the *_SCALE and *_TOP nodes; off by default.</summary>
        public bool applyScale = false;
    }

    /// <summary>
    /// The whole pose, as it lives in rigpose.json next to the plugin DLL.
    /// This is a phase 1 tuning file, not a shipped asset: it exists so the rig can be
    /// explored in-game without a rebuild, and so the angles found here can be reused
    /// as the starting calibration of the solver in phase 3.
    /// </summary>
    [Serializable]
    public class RigPoseFile
    {
        public string note = "SomeEmotesREPO rig probe. Local euler angles applied on top of the rest pose.";
        public List<RigBonePose> bones = new List<RigBonePose>();

        public static RigPoseFile Default()
        {
            var file = new RigPoseFile();
            for (int i = 0; i < RepoRigBinder.BoneCount; i++)
            {
                file.bones.Add(new RigBonePose { bone = RepoRigBinder.NameOf((RigBone)i) });
            }
            return file;
        }

        /// <summary>
        /// Reads the file into the flat arrays the driver uses. Unknown or missing bone
        /// names are skipped rather than shifting everything by one.
        /// </summary>
        public void CopyTo(Vector3[] euler, Vector3[] scale, bool[] driven, bool[] applyScale)
        {
            foreach (var entry in bones)
            {
                int index = IndexOf(entry.bone);
                if (index < 0) continue;

                euler[index] = entry.euler;
                scale[index] = entry.scale;
                driven[index] = entry.driven;
                applyScale[index] = entry.applyScale;
            }
        }

        public static RigPoseFile From(Vector3[] euler, Vector3[] scale, bool[] driven, bool[] applyScale)
        {
            var file = new RigPoseFile();
            for (int i = 0; i < RepoRigBinder.BoneCount; i++)
            {
                file.bones.Add(new RigBonePose
                {
                    bone = RepoRigBinder.NameOf((RigBone)i),
                    euler = euler[i],
                    scale = scale[i],
                    driven = driven[i],
                    applyScale = applyScale[i],
                });
            }
            return file;
        }

        private static int IndexOf(string boneName)
        {
            for (int i = 0; i < RepoRigBinder.BoneCount; i++)
            {
                if (string.Equals(RepoRigBinder.NameOf((RigBone)i), boneName, StringComparison.Ordinal)) return i;
            }
            return -1;
        }
    }
}
