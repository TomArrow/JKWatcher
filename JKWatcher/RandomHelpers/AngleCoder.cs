using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace JKWatcher.RandomHelpers
{
    // Note: This isn't meant to be cryptographically secure or anything
    // Don't use this for anything sensitive or security relevant
    // I just use this to transport some mildly relevant messages.
    // You can ofc layer another layer of encryption on top of this in principle.

    public class AngleCoder
    {
        protected const int maxBytes = 1024;
        protected const uint startSequence = 'J' | 'K' << 8 | 'W' << 16 | 'A' << 24; // 4 bytes for identifier, then 4 bytes for size of message 
        protected const uint endSequence = 'T' | 'C' << 8 | 'H' << 16 | 'R' << 24; // 4 bytes for identifier, then 4 bytes for xor checksum

        protected static float normalizeAngle(float angle)
        {
            angle %= 360.0f;
            if (angle < 0.0f)
            {
                angle += 360.0f;
            }
            return angle;
        }

    }

    public class AngleEncoder : AngleCoder {
        
        //pitch,yaw
        static public Vector2[] CreateAngleSequence(byte[] data)
        {
            if (data == null) return null;

            List<Vector2> list = new List<Vector2>();

            uint msgLen = (uint)data.Length;
            uint xorNum = 0;
            for (int i = 0; i < msgLen; i++)
            {
                int shift2 = (i % 4) * 8;
                xorNum ^= ((uint)data[i] << shift2);
            }

            Vector2 newVec = new Vector2();
            // encode start sequence
            for(int i = 0; i < 4; i++)
            {
                byte byteHere = (byte)((startSequence & (255 << i*8)) >> i*8);
                newVec.X = i;
                newVec.Y = 255 + byteHere;
                list.Add(newVec);
            }
            // encode message length as part of start sequence
            for (int i = 0; i < 4; i++)
            {
                byte byteHere = (byte)((msgLen & (255 << i*8)) >> i*8);
                newVec.X = i+4;
                newVec.Y = byteHere;
                list.Add(newVec);
            }


            for (int i = 0; i < msgLen; i++)
            {
                if (i % 8 == 0)
                {
                    newVec.X = 45;
                    newVec.Y = 45;
                    list.Add(newVec); // commit.
                }
                byte byteHere = data[i];
                newVec.X = i % 8;
                newVec.Y = byteHere;
                list.Add(newVec);
            }

            newVec.X = 45;
            newVec.Y = 45;
            list.Add(newVec); // commit.


            // encode end sequence
            for (int i = 0; i < 4; i++)
            {
                byte byteHere = (byte)((endSequence & (255 << i * 8)) >> i * 8);
                newVec.X = i;
                newVec.Y = 255 + byteHere;
                list.Add(newVec);
            }
            // encode xor checksum as part of start sequence
            for (int i = 0; i < 4; i++)
            {
                byte byteHere = (byte)((xorNum & (255 << i * 8)) >> i * 8);
                newVec.X = i + 4;
                newVec.Y = byteHere;
                list.Add(newVec);
            }
            newVec.X = 45;
            newVec.Y = 45;
            list.Add(newVec); // commit.

            List<Vector2> adjustedList = new List<Vector2>();
            for(int i = 0; i < list.Count; i++)
            {
                Vector2 entry = list[i];
                entry.X += 0.25f;
                entry.Y += 0.25f;
                adjustedList.Add(entry);
            }

            return adjustedList.ToArray();
        }
    }


    public class AngleDecoder : AngleCoder
    {
        object multiThreadLock = new object();

        UInt64 buffer = 0;
        uint specialBuffer = 0; // for start and end sequence. will be encoided as angles > 255.
        byte[] fullBuffer = new byte[maxBytes];

        bool decodingActive = false;
        int decodingMessageSize = 0;
        int decodedChunks = 0;
        byte receivedBits = 0;
        int nonsenseReceived = 0;

        public byte[] GiveAngleMaybeReturnResult(float pitch, float yaw)
        {
            lock (multiThreadLock) { 

                pitch = normalizeAngle(pitch);
                yaw = normalizeAngle(yaw);

                if (float.IsNaN(pitch) || float.IsNaN(yaw)) return null;

                uint pitchInt = (uint)(pitch+0.1f); // + 0.1 just in case rounded float numbers end up 0.99999
                uint yawInt = (uint)(yaw+0.1f);
                int shift = 8 * (int)pitchInt;

                if (pitchInt >= 0 && pitchInt <= 7 && yawInt >= 0 && yawInt <= 255)
                {
                    buffer &= ~((UInt64)255U << shift);
                    buffer |= (UInt64)yawInt << shift;
                    receivedBits |= (byte)(1 << (int)pitchInt);
                } 
                else if (pitchInt >= 0 && pitchInt <= 3 && yawInt >= 256 && yawInt <= 359)
                {
                    specialBuffer &= ~(255U << shift);
                    specialBuffer |= (yawInt-255) << shift;
                } 
                else if(pitchInt == 45 && yawInt == 45)
                {
                    // This is the 'confirm chunk' angle combination
                    if (decodingActive)
                    {
                        if(specialBuffer == endSequence)
                        {
                            // Ok I guess we're done receiving the message.
                            // check length and xor
                            if(decodedChunks*8 >= decodingMessageSize)
                            {
                                // good.
                                uint xorNum = 0;
                                for(int i = 0; i < decodingMessageSize; i++)
                                {
                                    int shift2 = (i % 4) * 8;
                                    xorNum ^= ((uint)fullBuffer[i] << shift2);
                                }

                                uint control = (uint)(buffer >> 32);

                                if(xorNum == control)
                                {
                                    // successfully decoded
                                    byte[] result = new byte[decodingMessageSize];
                                    Array.Copy(fullBuffer,result,decodingMessageSize);
                                    resetDecoding();
                                    return result;
                                }
                            }
                        } else
                        {
                            if(decodedChunks * 8 < decodingMessageSize)
                            {
                                int bytesInThisChunk = Math.Min(8,decodingMessageSize-decodedChunks*8);
                                byte receivedBitsRequired = 0;
                                for(int i = 0; i < bytesInThisChunk; i++)
                                {
                                    receivedBitsRequired |= (byte)(1 << i);
                                }
                                if((receivedBits & receivedBitsRequired) == receivedBitsRequired)
                                {
                                    byte[] chunk = BitConverter.GetBytes(buffer);
                                    Array.Copy(chunk, 0, fullBuffer, decodedChunks * 8, 8);
                                    decodedChunks++;
                                    receivedBits = 0;
                                }
                            }
                        }
                    } else
                    {
                        if(specialBuffer == startSequence)
                        {
                            uint messageLength = (uint)(buffer >> 32);
                            if(messageLength <= maxBytes)
                            {
                                resetDecoding();
                                decodingMessageSize = (int)messageLength;
                                decodingActive = true;
                            }
                        }
                    }
                } else if(decodingActive)
                {
                    nonsenseReceived++;

                    if(nonsenseReceived > 100)
                    {
                        // ok it's some nonsense or too much noise
                        // reset everything
                        resetDecoding();
                    }
                }

                return null;
            }

        }
        private void resetDecoding()
        {
            decodingActive = false;
            decodingMessageSize = 0;
            decodedChunks = 0;
            buffer = 0;
            specialBuffer = 0;
            fullBuffer = new byte[maxBytes];
            receivedBits = 0;
            nonsenseReceived = 0;
        }

    }
}
