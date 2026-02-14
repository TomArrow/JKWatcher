using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using OpenTK.Compute.OpenCL;
using OpenTK.Graphics.OpenGL;

namespace PCRend
{
    public class OpenTKRend
    {
        public static void exceptIfError(CLResultCode res, string errorMessage = null)
        {
            if (res != CLResultCode.Success)
            {
                throw new Exception($"Error with errorCode {res}. Details if available: {errorMessage}");
            }
        }

        static CLContext context;
        static CLProgram program;
        static CLKernel kernel;
        static CLDevice? deviceToUse = null;
        static CLBuffer bufferFrames;
        static CLBuffer bufferPoints;
        static CLBuffer bufferOutput;
        static CLCommandQueue queue;

        public static void prepareTK(int Length, DeviceType deviceTypeToUse)
        {
            CLResultCode res;

            CLPlatform[] platforms;
            CL.GetPlatformIds(out platforms);


#if DEBUG
            Console.WriteLine("DEBUG BUILD");
#endif

            Console.WriteLine("Devices:");

            bool priorityPicked = false;

            foreach (CLPlatform platform in platforms)
            {


                byte[] platformName;
                CL.GetPlatformInfo(platform, PlatformInfo.Name, out platformName);
                string platformNameString = System.Text.Encoding.Default.GetString(platformName);

                CLDevice[] devices;
                CL.GetDeviceIds(platform, DeviceType.All, out devices);

                foreach (CLDevice device in devices)
                {
                    byte[] deviceName;
                    byte[] deviceType;
                    CL.GetDeviceInfo(device, DeviceInfo.Name, out deviceName);
                    CL.GetDeviceInfo(device, DeviceInfo.Type, out deviceType);
                    string deviceNameString = System.Text.Encoding.Default.GetString(deviceName);
                    UInt64 deviceTypeNum = BitConverter.ToUInt64(deviceType);//TODO Is this portable?
                    Console.Write(platformNameString);
                    Console.Write(": ");
                    Console.Write(deviceNameString);
                    Console.Write(" (");
                    Console.Write((DeviceType)deviceTypeNum);
                    Console.Write(")");

                    if ((DeviceType)deviceTypeNum == deviceTypeToUse && !priorityPicked)
                    {
                        deviceToUse = device;
                        if (deviceNameString.Contains("NVIDIA"))
                        {
                            Console.Write(" - priority pick (for real this time)!\n");

                            priorityPicked = true;
                        }
                    }
                    Console.Write("\n");
                }
            }

            if (deviceToUse == null)
            {
                throw new Exception("OpenCL device selection failure (no GPU found)");
            }

            context = CL.CreateContext(IntPtr.Zero, 1, new CLDevice[] { deviceToUse.Value }, IntPtr.Zero, IntPtr.Zero, out res);

            exceptIfError(res, "Error creating context");

            string kernelCode = @"
                    typedef struct {
                        float4 pos;
                        float4 color;
                    } point_OpenCL;

                    typedef struct {
                        float4 c0;
                        float4 c1;
                        float4 c2;
                        float4 c3;
                        float4 m0;
                        float4 m1;
                        float4 m2;
                        float4 m3;
                        float4 stuff;
                    } frameRenderInfo_OpenCL;
                    __kernel void PaintPoint(__global frameRenderInfo_OpenCL* inputFrames,__global point_OpenCL* inputPoints, __global float* output, int frameCount, int divideCount)
                    {
                        int gid = get_global_id(0);
                        //int i = get_global_id(1);
                        for(int i=0;i<frameCount;i++){
                            //frameRenderInfo_OpenCL* frame = &inputFrames[i];
                            //result.x = 
                            float4 levelshotPos = inputFrames[i].c0 * inputPoints[gid].pos.x;
                            levelshotPos += inputFrames[i].c1 * inputPoints[gid].pos.y;
                            levelshotPos += inputFrames[i].c2 * inputPoints[gid].pos.z;
                            levelshotPos += inputFrames[i].c3 * inputPoints[gid].pos.w;
                            float theZ = levelshotPos.z;
                            levelshotPos /= levelshotPos.w;
                            
                            if (theZ > 0 && levelshotPos.x >= -1.0f && levelshotPos.x <= 1.0f && levelshotPos.y >= -1.0f && levelshotPos.y <= 1.0f)
                            {
                                //float4 color = inputPoints[gid].color;
                                int2 pos;
                                pos.x = (int)(((levelshotPos.x + 1.0f) / 2.0f) * (float)1920);//(float)LevelShotData.levelShotWidth);
                                pos.y = (int)(((levelshotPos.y + 1.0f) / 2.0f) * (float)1080);//(float)LevelShotData.levelShotHeight);
                                //bool isGood = theZ > 0 && levelshotPos.x >= -1.0f && levelshotPos.x <= 1.0f && levelshotPos.y >= -1.0f && levelshotPos.y <= 1.0f && pos.x >= 0 && pos.x < 1920 && pos.y >= 0 && pos.y < 1080;
                                //color = select((float4)(0.0f),color,-(int4)(isGood)); // why - you ask? because MSB needs to be set. and condition cast to int -> 1. but -1 has MSB set. dumb i know.
                                //pos = clamp(pos,(int2)0,(int2)((1920-1),(1080-1)));
                                
                                if (pos.x >= 0 && pos.x < 1920 && pos.y >= 0 && pos.y < 1080)
                                {
                                    output[1920*3*pos.y+pos.x*3] += inputPoints[gid].color.x;
                                    output[1920*3*pos.y+pos.x*3+1] += inputPoints[gid].color.y;
                                    output[1920*3*pos.y+pos.x*3+2] += inputPoints[gid].color.z;
                                }
                            }
                        }

                        //output[0] = 1;
                    }
                ";

            program = CL.CreateProgramWithSource(context, kernelCode, out res);

            exceptIfError(res, "Error creating program");

            res = CL.BuildProgram(program, 1, new CLDevice[] { deviceToUse.Value }, "", (IntPtr)0, (IntPtr)0);

            //exceptIfError(res, "Error building program");

            if (res != CLResultCode.Success)
            {
                byte[] errorLog;
                CL.GetProgramBuildInfo(program, deviceToUse.Value, ProgramBuildInfo.Log, out errorLog);
                string errorLogString = System.Text.Encoding.Default.GetString(errorLog);
                Console.WriteLine(errorLogString);
                throw new Exception("OpenCL Kernel compilation failure");
            }

            kernel = CL.CreateKernel(program, "PaintPoint", out res);

            exceptIfError(res, "Error creating kernel");

            //Console.WriteLine("Wtf it compiled?");

            //buffer = CL.CreateBuffer(context, MemoryFlags.ReadWrite, (nuint)(sizeof(float) * Length * 3), IntPtr.Zero, out res);

            //exceptIfError(res, "Error creating buffer");

            bufferOutput = CL.CreateBuffer(context, MemoryFlags.ReadWrite, (nuint)(sizeof(float) * Length * 3), IntPtr.Zero, out res);

            exceptIfError(res, "Error creating output buffer");


            queue = CL.CreateCommandQueueWithProperties(context, deviceToUse.Value, IntPtr.Zero, out res);


            exceptIfError(res, "Error creating command queue");
        }

        public unsafe static float[] RunFrame(frameRenderInfo_OpenCL[] inputFrames, point_OpenCL[] inputPoints, int divideCount, int workGroupSize)
        {

            CLResultCode res;

            var watch = new System.Diagnostics.Stopwatch();

            float[] resultData = new float[1920*1080*3];

            int frameCount = inputFrames.Length;


            CLEvent eventWhatever;

            watch.Start();
            bufferFrames = CL.CreateBuffer(context, MemoryFlags.ReadWrite, (nuint)(sizeof(frameRenderInfo_OpenCL) * inputFrames.Length), IntPtr.Zero, out res);
            exceptIfError(res, "Error creating frame buffer");
            watch.Stop();

            watch.Start();
            bufferPoints = CL.CreateBuffer(context, MemoryFlags.ReadWrite, (nuint)(sizeof(point_OpenCL) * inputPoints.Length), IntPtr.Zero, out res);
            exceptIfError(res, "Error creating point buffer");
            watch.Stop();

            watch.Start();
            res = CL.EnqueueWriteBuffer(queue, bufferFrames, true, (UIntPtr)0, inputFrames, null, out eventWhatever);
            watch.Stop();
            //Console.WriteLine($"TK write buffer: {watch.Elapsed.TotalMilliseconds}");

            exceptIfError(res, "Error enqueueing frame buffer write.");

            watch.Restart();
            res = CL.EnqueueWriteBuffer(queue, bufferPoints, true, (UIntPtr)0, inputPoints, null, out eventWhatever);
            watch.Stop();
            //Console.WriteLine($"TK write buffer: {watch.Elapsed.TotalMilliseconds}");

            exceptIfError(res, "Error enqueueing point buffer write.");

            watch.Restart();
            res = CL.SetKernelArg(kernel, 0, bufferFrames);
            watch.Stop();

            watch.Restart();
            res = CL.SetKernelArg(kernel, 1, bufferPoints);
            watch.Stop();
            //Console.WriteLine($"TK set kernel arg: {watch.Elapsed.TotalMilliseconds}");

            exceptIfError(res, "Error setting kernel argument.");

            watch.Restart();
            res = CL.SetKernelArg(kernel, 2, bufferOutput);
            watch.Stop();
            //Console.WriteLine($"TK set kernel arg: {watch.Elapsed.TotalMilliseconds}");

            exceptIfError(res, "Error setting kernel argument 2.");

            watch.Restart();
            int* frameCountPtr = &frameCount;
            res = CL.SetKernelArg(kernel, 3, (UIntPtr)sizeof(int), (IntPtr)frameCountPtr);

            watch.Stop();
            //Console.WriteLine($"TK set kernel arg: {watch.Elapsed.TotalMilliseconds}");

            exceptIfError(res, "Error setting kernel argument 3.");

            watch.Restart();
            res = CL.SetKernelArg(kernel, 4, (UIntPtr)sizeof(int), (IntPtr)(&divideCount));
            watch.Stop();
            //Console.WriteLine($"TK set kernel arg: {watch.Elapsed.TotalMilliseconds}");

            exceptIfError(res, "Error setting kernel argument 4.");

            watch.Restart();
            //new nuint[] { (nuint)workGroupSize }
            //res = CL.EnqueueNDRangeKernel(queue, kernel, 2, new nuint[] { 0,0 }, new nuint[] { (nuint)(inputPoints.Length),(nuint)(inputFrames.Length) }, null, 0, null, out eventWhatever);
            res = CL.EnqueueNDRangeKernel(queue, kernel, 1, new nuint[] { 0 }, new nuint[] { (nuint)(inputPoints.Length) }, new nuint[] { (nuint)workGroupSize }, 0, null, out eventWhatever);
            watch.Stop();
            //Console.WriteLine($"TK execute: {watch.Elapsed.TotalMilliseconds}");

            exceptIfError(res, "Error kernel execution.");

            watch.Restart();
            CL.Finish(queue);
            watch.Stop();
            //Console.WriteLine($"TK finish: {watch.Elapsed.TotalMilliseconds}");

            watch.Restart();
            res = CL.EnqueueReadBuffer(queue, bufferOutput, true, (UIntPtr)0, resultData, null, out eventWhatever);
            watch.Stop();

            exceptIfError(res, "Error enqueueing buffer read.");

            watch.Restart();
            res = CL.ReleaseMemoryObject(bufferFrames);
            watch.Stop();
            //Console.WriteLine($"TK read buffer: {watch.Elapsed.TotalMilliseconds}");

            exceptIfError(res, "Error destroying frames buffer.");

            watch.Restart();
            res = CL.ReleaseMemoryObject(bufferPoints);
            watch.Stop();
            //Console.WriteLine($"TK read buffer: {watch.Elapsed.TotalMilliseconds}");

            exceptIfError(res, "Error destroying points buffer.");

            watch.Restart();
            float zero = 0.0f;
            res = CL.EnqueueFillBuffer(
                queue,
                bufferOutput,
                (IntPtr)(&zero),
                (UIntPtr)sizeof(float),
                (UIntPtr)0,
                (UIntPtr)(1920 * 1080 * 3 * sizeof(float)),
                0,
                null, out CLEvent blah);
            watch.Stop();
            //Console.WriteLine($"TK read buffer: {watch.Elapsed.TotalMilliseconds}");

            exceptIfError(res, "Error clearing output buffer.");

            return resultData;
        }
    }
}
