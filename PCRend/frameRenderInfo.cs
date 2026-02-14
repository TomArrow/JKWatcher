using JKWatcher.RandomHelpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Vim.Math3d;
namespace PCRend
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct frameRenderInfo_OpenCL { 
        public unsafe fixed float camTransform[16];
        public unsafe fixed float modelMatrix[16];
        public unsafe fixed float stuff[4]; // idk maybe we need more stuff :P
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct point_OpenCL
    {
        public unsafe fixed float pos[4];
        public unsafe fixed float color[4];
    }

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
        public unsafe frameRenderInfo_OpenCL[] getOpenCLFrames()
        {
            List<frameRenderInfo_OpenCL> retVal = new List<frameRenderInfo_OpenCL>();
            foreach(var subframe in blendStates)
            {
                frameRenderInfo_OpenCL newFrame = new frameRenderInfo_OpenCL();
                newFrame.camTransform[0] = subframe.camTransform.M11;
                newFrame.camTransform[1] = subframe.camTransform.M12;
                newFrame.camTransform[2] = subframe.camTransform.M13;
                newFrame.camTransform[3] = subframe.camTransform.M14;
                newFrame.camTransform[4] = subframe.camTransform.M21;
                newFrame.camTransform[5] = subframe.camTransform.M22;
                newFrame.camTransform[6] = subframe.camTransform.M23;
                newFrame.camTransform[7] = subframe.camTransform.M24;
                newFrame.camTransform[8] = subframe.camTransform.M31;
                newFrame.camTransform[9] = subframe.camTransform.M32;
                newFrame.camTransform[10] = subframe.camTransform.M33;
                newFrame.camTransform[11] = subframe.camTransform.M34;
                newFrame.camTransform[12] = subframe.camTransform.M41;
                newFrame.camTransform[13] = subframe.camTransform.M42;
                newFrame.camTransform[14] = subframe.camTransform.M43;
                newFrame.camTransform[15] = subframe.camTransform.M44;
                newFrame.modelMatrix[0] = subframe.modelMatrix.M11;
                newFrame.modelMatrix[1] = subframe.modelMatrix.M12;
                newFrame.modelMatrix[2] = subframe.modelMatrix.M13;
                newFrame.modelMatrix[3] = subframe.modelMatrix.M14;
                newFrame.modelMatrix[4] = subframe.modelMatrix.M21;
                newFrame.modelMatrix[5] = subframe.modelMatrix.M22;
                newFrame.modelMatrix[6] = subframe.modelMatrix.M23;
                newFrame.modelMatrix[7] = subframe.modelMatrix.M24;
                newFrame.modelMatrix[8] = subframe.modelMatrix.M31;
                newFrame.modelMatrix[9] = subframe.modelMatrix.M32;
                newFrame.modelMatrix[10] = subframe.modelMatrix.M33;
                newFrame.modelMatrix[11] = subframe.modelMatrix.M34;
                newFrame.modelMatrix[12] = subframe.modelMatrix.M41;
                newFrame.modelMatrix[13] = subframe.modelMatrix.M42;
                newFrame.modelMatrix[14] = subframe.modelMatrix.M43;
                newFrame.modelMatrix[15] = subframe.modelMatrix.M44;
                newFrame.stuff[0] = subframe.multiplier;
                retVal.Add(newFrame);
            }
            return retVal.ToArray();
        }
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
