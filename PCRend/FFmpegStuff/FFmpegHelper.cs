using FFmpeg.AutoGen;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace PCRend.FFmpegStuff
{

    internal static class FFmpegHelper
    {
        public static string findFFmpegLibs()
        {
            string test = Path.Combine(AppContext.BaseDirectory, "runtimes", RuntimeInformation.ProcessArchitecture == Architecture.X64 ? "win-x64" : "win-x86", "native");
            if(File.Exists(Path.Combine(test, "avcodec-62.dll")))
            {
                return test;
            }
            else
            {
                return AppContext.BaseDirectory;
            }
        }
        public static unsafe string av_strerror(int error)
        {
            var bufferSize = 1024;
            var buffer = stackalloc byte[bufferSize];
            ffmpeg.av_strerror(error, buffer, (ulong)bufferSize);
            var message = Marshal.PtrToStringAnsi((IntPtr)buffer);
            return message;
        }

        public static int ThrowExceptionIfError(this int error)
        {
            if (error < 0) throw new ApplicationException(av_strerror(error));
            return error;
        }

        public static unsafe byte[] getpixel(this AVFrame frame, int x, int y)
        {

            int stride = frame.linesize[0];
            byte* data = frame.data[0];
            return new byte[3] {
                *(data+y*stride + x*3),
                *(data+y*stride + x*3+1),
                *(data+y*stride + x*3+2),
            };
        }
    }
}
