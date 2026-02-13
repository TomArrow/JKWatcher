using System;
using System.Drawing;
using System.IO;
using FFmpeg.AutoGen;

namespace PCRend.FFmpegStuff
{

    public sealed unsafe class MagicYUVVideoStreamEncoder : IDisposable
    {
        private readonly Size _frameSize;
        private readonly int _linesize;
        private readonly AVCodec* _pCodec;
        private readonly AVCodecContext* _pCodecContext;
        private readonly AVFormatContext* _pFormatContext;
        private readonly AVStream* _stream;
        //private readonly Stream _stream;
        private readonly int _size;

        public MagicYUVVideoStreamEncoder(string filename, AVRational timebase, Size frameSize)
        {
            //_stream = stream;
            _frameSize = frameSize;

            var codecId = AVCodecID.AV_CODEC_ID_MAGICYUV;
            _pCodec = ffmpeg.avcodec_find_encoder(codecId);
            if (_pCodec == null) throw new InvalidOperationException("Codec not found.");

            fixed(AVFormatContext** formatContextPtr = &_pFormatContext)
            {
                ffmpeg.avformat_alloc_output_context2(formatContextPtr, null, "avi", filename).ThrowExceptionIfError();
            }
            if (_pFormatContext == null) throw new InvalidOperationException("Output context failed to initialize.");

            _stream = ffmpeg.avformat_new_stream(_pFormatContext,_pCodec);

            if (_stream == null) throw new InvalidOperationException("Output stream failed to initialize.");


            _pCodecContext = ffmpeg.avcodec_alloc_context3(_pCodec);
            _pCodecContext->codec_id = _pCodec->id;
            _pCodecContext->codec_type = AVMediaType.AVMEDIA_TYPE_VIDEO;
            _pCodecContext->width = frameSize.Width;
            _pCodecContext->height = frameSize.Height;
            //_pCodecContext->time_base = new AVRational { num = 1, den = fps };
            //_pCodecContext->framerate = new AVRational { num = fps, den = 1 };
            _pCodecContext->time_base = timebase;
            _pCodecContext->framerate = new AVRational { num = timebase.den, den = timebase.num };
            _pCodecContext->pix_fmt = AVPixelFormat.AV_PIX_FMT_GBRP;

            ffmpeg.avcodec_open2(_pCodecContext, _pCodec, null).ThrowExceptionIfError();

            ffmpeg.avcodec_parameters_from_context(_stream->codecpar, _pCodecContext);

            _stream->time_base = timebase;

            ffmpeg.avio_open(&_pFormatContext->pb, filename, ffmpeg.AVIO_FLAG_WRITE).ThrowExceptionIfError();

            ffmpeg.avformat_write_header(_pFormatContext, null);


            _linesize = frameSize.Width;

            _size = _linesize * frameSize.Height;
        }

        bool _disposed = false;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            //ffmpeg.avcodec_close(_pCodecContext);
            ffmpeg.av_free(_pCodecContext);
            ffmpeg.avio_close(_pFormatContext->pb);
            ffmpeg.avformat_free_context(_pFormatContext);
        }

        public void Encode(AVFrame frame)
        {
            if (_disposed)
            {
                return;
            }
            if (frame.format != (int)_pCodecContext->pix_fmt)
                throw new ArgumentException("Invalid pixel format.", nameof(frame));
            if (frame.width != _frameSize.Width) throw new ArgumentException("Invalid width.", nameof(frame));
            if (frame.height != _frameSize.Height) throw new ArgumentException("Invalid height.", nameof(frame));
            if (frame.linesize[0] < _linesize) throw new ArgumentException("Invalid linesize 1.", nameof(frame));
            if (frame.linesize[1] < _linesize) throw new ArgumentException("Invalid linesize 2.", nameof(frame));
            if (frame.linesize[2] < _linesize) throw new ArgumentException("Invalid linesize 3.", nameof(frame));
            var pPacket = ffmpeg.av_packet_alloc();

            try
            {
                // Basic encoding loop explained: 
                // https://ffmpeg.org/doxygen/4.1/group__lavc__encdec.html

                // Give the encoder a frame to encode
                ffmpeg.avcodec_send_frame(_pCodecContext, &frame).ThrowExceptionIfError();

                // From https://ffmpeg.org/doxygen/4.1/group__lavc__encdec.html:
                // For encoding, call avcodec_receive_packet().  On success, it will return an AVPacket with a compressed frame.
                // Repeat this call until it returns AVERROR(EAGAIN) or an error.
                // The AVERROR(EAGAIN) return value means that new input data is required to return new output.
                // In this case, continue with sending input.
                // For each input frame/packet, the codec will typically return 1 output frame/packet, but it can also be 0 or more than 1.
                bool hasFinishedWithThisFrame;

                do
                {
                    // Clear/wipe the receiving packet
                    // (not sure if this is needed, since docs for avcoded_receive_packet say that it will call that first-thing
                    ffmpeg.av_packet_unref(pPacket);

                    // Receive back a packet; there might be 0, 1 or many packets to receive for an input frame.
                    var response = ffmpeg.avcodec_receive_packet(_pCodecContext, pPacket);

                    bool isPacketValid;

                    if (response == 0)
                    {
                        // 0 on success; as in, successfully retrieved a packet, and expecting us to retrieve another one.
                        isPacketValid = true;
                        hasFinishedWithThisFrame = false;
                    }
                    else if (response == ffmpeg.AVERROR(ffmpeg.EAGAIN))
                    {
                        // EAGAIN: there's no more output is available in the current state - user must try to send more input
                        isPacketValid = false;
                        hasFinishedWithThisFrame = true;
                    }
                    else if (response == ffmpeg.AVERROR(ffmpeg.AVERROR_EOF))
                    {
                        // EOF: the encoder has been fully flushed, and there will be no more output packets
                        isPacketValid = false;
                        hasFinishedWithThisFrame = true;
                    }
                    else
                    {
                        // AVERROR(EINVAL): codec not opened, or it is a decoder other errors: legitimate encoding errors
                        // , otherwise negative error code:
                        throw new InvalidOperationException($"error from avcodec_receive_packet: {response}");
                    }

                    if (isPacketValid)
                    {
                        pPacket->stream_index = _stream->index;
                        ffmpeg.av_interleaved_write_frame(_pFormatContext, pPacket);
                        //using var packetStream = new UnmanagedMemoryStream(pPacket->data, pPacket->size);
                        //packetStream.CopyTo(_stream);
                    }
                } while (!hasFinishedWithThisFrame);
            }
            finally
            {
                ffmpeg.av_packet_free(&pPacket);
            }
        }

        public void Drain()
        {
            // From https://ffmpeg.org/doxygen/4.1/group__lavc__encdec.html:
            // End of stream situations. These require "flushing" (aka draining) the codec, as the codec might buffer multiple frames or packets internally for performance or out of necessity (consider B-frames). This is handled as follows:
            // Instead of valid input, send NULL to the avcodec_send_packet() (decoding) or avcodec_send_frame() (encoding) functions. This will enter draining mode.
            // 	Call avcodec_receive_frame() (decoding) or avcodec_receive_packet() (encoding) in a loop until AVERROR_EOF is returned. The functions will not return AVERROR(EAGAIN), unless you forgot to enter draining mode.

            var pPacket = ffmpeg.av_packet_alloc();

            try
            {
                // Send a null frame to enter draining mode
                ffmpeg.avcodec_send_frame(_pCodecContext, null).ThrowExceptionIfError();

                bool hasFinishedDraining;

                do
                {
                    // Clear/wipe the receiving packet
                    // (not sure if this is needed, since docs for avcoded_receive_packet say that it will call that first-thing
                    ffmpeg.av_packet_unref(pPacket);

                    var response = ffmpeg.avcodec_receive_packet(_pCodecContext, pPacket);

                    bool isPacketValid;

                    if (response == 0)
                    {
                        // 0 on success; as in, successfully retrieved a packet, and expecting us to retrieve another one.
                        isPacketValid = true;
                        hasFinishedDraining = false;
                    }
                    else if (response == ffmpeg.AVERROR(ffmpeg.AVERROR_EOF))
                    {
                        // EOF: the encoder has been fully flushed, and there will be no more output packets
                        isPacketValid = false;
                        hasFinishedDraining = true;
                    }
                    else
                    {
                        // Some other error.
                        // Should probably throw here, but in testing we get error -541478725
                        isPacketValid = false;
                        hasFinishedDraining = true;
                    }

                    if (isPacketValid)
                    {
                        pPacket->stream_index = _stream->index;
                        ffmpeg.av_interleaved_write_frame(_pFormatContext, pPacket);
                        //using var packetStream = new UnmanagedMemoryStream(pPacket->data, pPacket->size);
                        //packetStream.CopyTo(_stream);
                    }
                } while (!hasFinishedDraining);
            }
            finally
            {
                ffmpeg.av_packet_free(&pPacket);
            }
            ffmpeg.av_write_trailer(_pFormatContext);
            Dispose();
        }
    }
}