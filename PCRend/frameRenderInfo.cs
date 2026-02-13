using JKWatcher.RandomHelpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vim.Math3d;
namespace PCRend
{
    class frameRenderInfo
    {
        public frameRenderInfo oldFrame = null;
        public System.Numerics.Vector3 pos;
        public System.Numerics.Vector3 angles;
        public float fov;
        public System.Numerics.Matrix4x4 modelMatrix;
        public System.Numerics.Matrix4x4 camTransform;
        public LevelShotData lsData = null;
        public frameRenderInfo[] blendStates = null;
        public float multiplier = 1.0f;
        public frameRenderInfo GetBlendedFrame(float progress)
        {
            if (oldFrame == null)
            {
                return this;
            }
            Quaternion quat1 = Quaternion.CreateFromEulerAngles(new Vector3(oldFrame.angles.Y*(float)Math.PI/180.0f, oldFrame.angles.X * (float)Math.PI / 180.0f, oldFrame.angles.Z * (float)Math.PI / 180.0f));
            Quaternion quat2 = Quaternion.CreateFromEulerAngles(new Vector3(angles.Y*(float)Math.PI/180.0f, angles.X * (float)Math.PI / 180.0f, angles.Z * (float)Math.PI / 180.0f));
            Quaternion quatres = Quaternion.Slerp(quat1, quat2, progress);
            Vector3 slerpangs = quatres.ToEulerAngles(); // yaw pitch roll?

            return new frameRenderInfo()
            {
                pos = oldFrame.pos * (1.0f - progress) + progress * pos,
                angles = new System.Numerics.Vector3(slerpangs.Y* 180.0f / (float)Math.PI, slerpangs.X * 180.0f / (float)Math.PI, slerpangs.Z * 180.0f / (float)Math.PI),
                fov = oldFrame.fov * (1.0f - progress) + progress * fov,
            };

        }
        public void CalcMatrices(int blendFrames)
        {
            modelMatrix = ProjectionMatrixHelper.createModelMatrix(pos, angles, false);
            camTransform = ProjectionMatrixHelper.createModelProjectionMatrix(pos, angles, fov, LevelShotData.levelShotWidth, LevelShotData.levelShotHeight);
            if(blendFrames > 1 && oldFrame != null)
            {
                blendStates = new frameRenderInfo[blendFrames]; // we do them extra on top of the normal one
                float step = 1.0f/(float)blendFrames;
                for(int i = 0; i < blendFrames - 1; i++)
                {
                    blendStates[i] = GetBlendedFrame(((float)i + 1.0f) * step);
                    blendStates[i].CalcMatrices(0);
                    blendStates[i].multiplier = step;
                }
                this.multiplier = step;
                blendStates[blendFrames - 1] = this;
            }
            else
            {
                blendStates = new frameRenderInfo[1];
                blendStates[0] = this;
            }
        }
    }
}
