using System;
using System.Drawing;
using System.Runtime.InteropServices;
using FFmpeg.AutoGen;

namespace PCRend.FFmpegStuff {


    public sealed unsafe class VideoConverter : IDisposable
    {
        private readonly SwsContext* _pConvertContext;
        private readonly byte_ptrArray4 _dstData; 
        private readonly int_array4 _dstLinesize;
        private readonly IntPtr _convertedFrameBufferPtr;

        public VideoConverter(Size sourceSize, AVPixelFormat sourcePixelFormat,
            Size destinationSize, AVPixelFormat destinationPixelFormat)
        {
            _pConvertContext = ffmpeg.sws_getContext(sourceSize.Width,
                sourceSize.Height,
                sourcePixelFormat,
                destinationSize.Width,
                destinationSize.Height,
                destinationPixelFormat,
                (int)SwsFlags.SWS_FAST_BILINEAR,
                null,
                null,
                null);
            if (_pConvertContext == null)
                throw new ApplicationException("Could not initialize the conversion context.");

            var convertedFrameBufferSize = ffmpeg.av_image_get_buffer_size(destinationPixelFormat,
                destinationSize.Width,
                destinationSize.Height,
                1);
            _convertedFrameBufferPtr = Marshal.AllocHGlobal(convertedFrameBufferSize);
            _dstData = new byte_ptrArray4();
            _dstLinesize = new int_array4();

            ffmpeg.av_image_fill_arrays(ref _dstData,
                ref _dstLinesize,
                (byte*)_convertedFrameBufferPtr,
                destinationPixelFormat,
                destinationSize.Width,
                destinationSize.Height,
                1).ThrowExceptionIfError(); ;
        }

        public void Dispose()
        {
            ffmpeg.sws_freeContext(_pConvertContext);
            Marshal.FreeHGlobal(_convertedFrameBufferPtr);
        }

        public AVFrame Convert(AVFrame sourceFrame)
        {
            //var dstData = new byte_ptrArray4();
            //var dstLinesize = new int_array4();
            AVFrame newFrame = new AVFrame();
            for (uint i = 0; i < 4; i++)
            {
                newFrame.data[i] = _dstData[i];
                newFrame.linesize[i] = _dstLinesize[i];
            }

            newFrame.width = sourceFrame.width;
            newFrame.height = sourceFrame.height;

            ffmpeg.sws_scale(_pConvertContext,
                sourceFrame.data,
                sourceFrame.linesize,
                0,
                sourceFrame.height,
                newFrame.data,
                newFrame.linesize).ThrowExceptionIfError();

            return newFrame;
        }
    }
}

