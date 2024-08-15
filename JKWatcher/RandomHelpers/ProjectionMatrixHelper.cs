using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace JKWatcher.RandomHelpers
{
    public static class ProjectionMatrixHelper
    {
        public const int ZCompensationVersion = 1;

        static float[] s_flipMatrix = {
	        // convert from our coordinate system (looking down X)
	        // to OpenGL's coordinate system (looking down -Z)
	        0, 0, -1, 0,
            -1, 0, 0, 0,
            0, 1, 0, 0,
            0, 0, 0, 1
        };

        public static Matrix4x4 createModelProjectionMatrix(Vector3 origin, Vector3 angles, float fovX, float width, float height)
        {

            Matrix4x4 modelMatrtix = createModelMatrix(origin, angles, true);
            Matrix4x4 projectionMatrix = createProjectionMatrix(width, height, fovX);

            return Matrix4x4.Multiply(modelMatrtix, projectionMatrix);
        }

        public static float DEG2RAD(float a)
        {
            return ((a) * (float)(Math.PI / 180.0));
        }
        public static float normalizeAngle180(float angle)
        {
            angle %= 360.0f;
            if (angle < 0.0f)
            {
                angle += 360.0f;
            }
            if (angle > 180.0f)
            {
                angle -= 360.0f;
            }
            return angle;
        }

        public static float GetIlluminationMultiplierPureNoZ(Vector3 modelSpaceOrigin)
        {
            modelSpaceOrigin = Vector3.Normalize(modelSpaceOrigin);
            Vector3 angles = new Vector3();
            Q3MathStuff.vectoangles(modelSpaceOrigin, ref angles);
            angles *= (float)Math.PI / 180.0f;
            float angleX = angles.Y;// normalizeAngle180(angles.Y);
            float angleY = angles.X;// normalizeAngle180(angles.X);
            // 1/cos squared is secant squared, which is the derivative of tangens. so we are calculating the relative increase per angle change here. 
            double xMultiplier = 1.0 / Math.Cos(angleX);
            xMultiplier = xMultiplier * xMultiplier;
            double yMultiplier = 1.0 / Math.Cos(angleY);
            yMultiplier = yMultiplier * yMultiplier;
            return (float)(xMultiplier * yMultiplier);
        }
        //bad logic? need to do z per axis?
        public static float GetIlluminationMultiplier(Vector3 modelSpaceOrigin)
        {
            float z = modelSpaceOrigin.Length();
            z = Math.Max(1.0f, z / 100.0f); // we dont want a point near the camera to get boosted to near infinity.
            z = z * z; // squared cuz area
            modelSpaceOrigin = Vector3.Normalize(modelSpaceOrigin);
            Vector3 angles = new Vector3();
            Q3MathStuff.vectoangles(modelSpaceOrigin, ref angles);
            angles *= (float)Math.PI / 180.0f;
            float angleX = angles.Y;// normalizeAngle180(angles.Y);
            float angleY = angles.X;// normalizeAngle180(angles.X);
            // 1/cos squared is secant squared, which is the derivative of tangens. so we are calculating the relative increase per angle change here. 
            double xMultiplier = 1.0 / Math.Cos(angleX);
            xMultiplier = xMultiplier * xMultiplier;
            double yMultiplier = 1.0 / Math.Cos(angleY);
            yMultiplier = yMultiplier * yMultiplier;
            return (float)(xMultiplier * yMultiplier / (double)z);
        }

        // close stuff gets too bright tbh
        public static float GetIlluminationMultiplier2(Vector3 modelSpaceOrigin)
        {
            //float z = modelSpaceOrigin.Length();
            float z1 = (float)Math.Sqrt(modelSpaceOrigin.X * modelSpaceOrigin.X + modelSpaceOrigin.Y * modelSpaceOrigin.Y);
            float z2 = (float)Math.Sqrt(modelSpaceOrigin.X * modelSpaceOrigin.X + modelSpaceOrigin.Z * modelSpaceOrigin.Z);
            //z = Math.Max(1.0f, z / 100.0f); // we dont want a point near the camera to get boosted to near infinity.
            //z = z * z; // squared cuz area
            modelSpaceOrigin = Vector3.Normalize(modelSpaceOrigin);
            Vector3 angles = new Vector3();
            Q3MathStuff.vectoangles(modelSpaceOrigin, ref angles);
            angles *= (float)Math.PI / 180.0f;
            float angleX = angles.Y;// normalizeAngle180(angles.Y);
            float angleY = angles.X;// normalizeAngle180(angles.X);
            // 1/cos squared is secant squared, which is the derivative of tangens. so we are calculating the relative increase per angle change here. 
            double xMultiplier = Math.Tan(angleX);
            xMultiplier = (1.0 + xMultiplier * xMultiplier) / z1;
            double yMultiplier = Math.Tan(angleY);
            yMultiplier = (1.0 + yMultiplier * yMultiplier) / z2;
            return (float)(xMultiplier * yMultiplier);
        }

        public static float CG_CalcFOVFromX(float fov_x, float width, float height)
        {
            float x;
            //	float	phase;
            //	float	v;
            //	int		contents;
            float fov_y;

            x = width / (float)Math.Tan(fov_x / 360.0f * Math.PI);
            fov_y = (float)Math.Atan2(height, x);
            fov_y = fov_y * 360.0f / (float)Math.PI;


            return fov_y;//(fov_x,fov_y);
        }

        public static Matrix4x4 createProjectionMatrix(float widthScr, float heightScr, float fovX, float zFar = 2000, float zNear = 4)
        {
            float[] projectionMatrix = new float[16];
            Vector3[] axis = new Vector3[3];

            float xmin, xmax, ymin, ymax;
            float width, height, depth;
            //float zNear, zFar;


            //
            // set up projection matrix
            //

            ymax = zNear * (float)Math.Tan(DEG2RAD(CG_CalcFOVFromX(fovX, widthScr, heightScr) * 0.5f));
            ymin = -ymax;

            xmax = zNear * (float)Math.Tan(DEG2RAD(fovX * 0.5f));
            xmin = -xmax;

            width = xmax - xmin;
            height = ymax - ymin;
            depth = zFar - zNear;

            projectionMatrix[0] = 2 * zNear / width;
            projectionMatrix[4] = 0;
            projectionMatrix[8] = (xmax + xmin) / width;   // normally 0
            projectionMatrix[12] = 0;

            projectionMatrix[1] = 0;
            projectionMatrix[5] = 2 * zNear / height;
            projectionMatrix[9] = (ymax + ymin) / height;  // normally 0
            projectionMatrix[13] = 0;

            projectionMatrix[2] = 0;
            projectionMatrix[6] = 0;
            projectionMatrix[10] = -(zFar + zNear) / depth;
            projectionMatrix[14] = -2 * zFar * zNear / depth;

            projectionMatrix[3] = 0;
            projectionMatrix[7] = 0;
            projectionMatrix[11] = -1;
            projectionMatrix[15] = 0;

            return floatArrayToMatrix(projectionMatrix);


        }
        public static Matrix4x4 createModelMatrix(Vector3 origin, Vector3 angles, bool openGLFlip)
        {
            float[] viewerMatrix = new float[16];
            Vector3[] axis = new Vector3[3];
            Q3MathStuff.AngleVectors(angles, out axis[0], out axis[1], out axis[2]);
            viewerMatrix[0] = axis[0].X;
            viewerMatrix[4] = axis[0].Y;
            viewerMatrix[8] = axis[0].Z;
            viewerMatrix[12] = -origin.X * viewerMatrix[0] + -origin.Y * viewerMatrix[4] + -origin.Z * viewerMatrix[8];

            viewerMatrix[1] = axis[1].X;
            viewerMatrix[5] = axis[1].Y;
            viewerMatrix[9] = axis[1].Z;
            viewerMatrix[13] = -origin.X * viewerMatrix[1] + -origin.Y * viewerMatrix[5] + -origin.Z * viewerMatrix[9];

            viewerMatrix[2] = axis[2].X;
            viewerMatrix[6] = axis[2].Y;
            viewerMatrix[10] = axis[2].Z;
            viewerMatrix[14] = -origin.X * viewerMatrix[2] + -origin.Y * viewerMatrix[6] + -origin.Z * viewerMatrix[10];

            viewerMatrix[3] = 0;
            viewerMatrix[7] = 0;
            viewerMatrix[11] = 0;
            viewerMatrix[15] = 1;


            if (openGLFlip) viewerMatrix = myGlMultMatrix(viewerMatrix, s_flipMatrix);



            return floatArrayToMatrix(viewerMatrix);


        }

        static Matrix4x4 floatArrayToMatrix(float[] matrix)
        {
            return new Matrix4x4()
            {
                M11 = matrix[0],
                M12 = matrix[1],
                M13 = matrix[2],
                M14 = matrix[3],
                M21 = matrix[4],
                M22 = matrix[5],
                M23 = matrix[6],
                M24 = matrix[7],
                M31 = matrix[8],
                M32 = matrix[9],
                M33 = matrix[10],
                M34 = matrix[11],
                M41 = matrix[12],
                M42 = matrix[13],
                M43 = matrix[14],
                M44 = matrix[15],
            };
        }

        static float[] myGlMultMatrix(float[] a, float[] b)
        {
            int i, j;
            float[] retVal = new float[16];

            for (i = 0; i < 4; i++)
            {
                for (j = 0; j < 4; j++)
                {
                    retVal[i * 4 + j] =
                      a[i * 4 + 0] * b[0 * 4 + j]
                      + a[i * 4 + 1] * b[1 * 4 + j]
                      + a[i * 4 + 2] * b[2 * 4 + j]
                      + a[i * 4 + 3] * b[3 * 4 + j];
                }
            }
            return retVal;
        }

    }
}
