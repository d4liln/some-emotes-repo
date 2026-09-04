using System;
using System.Collections.Generic;
using UnityEngine;

namespace SomeEmotesREPO.Rig
{
    [Serializable]
    public class RigBonePose
    {
        public string bone = string.Empty;
        public Vector3 euler = Vector3.zero;
        public Vector3 scale = Vector3.one;

        public bool driven = true;

        public bool applyScale = false;
    }

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
