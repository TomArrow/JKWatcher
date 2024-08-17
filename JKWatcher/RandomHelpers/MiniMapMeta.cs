using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace JKWatcher.RandomHelpers
{
    class MiniMapMeta
    {
        public float minX { get; set; }
        public float minY { get; set; }
        public float minZ { get; set; }
        public float maxX { get; set; }
        public float maxY { get; set; }
        public float maxZ { get; set; }
        /*
        public Vector2 GetTexturePositionXY(Vector3 position)
        {
            return new Vector2() { X = (position.X - minX) / (maxX - minX), Y = (position.Y - minY) / (maxY - minY) };
        }
        public Vector2 GetTexturePosition(Vector3 position, int xIndex, int yIndex)
        {
            float minXHere = xIndex == 0 ? minX : (xIndex == 1 ? minY : minZ);
            float maxXHere = xIndex == 0 ? maxX : (xIndex == 1 ? maxY : maxZ);
            float minYHere = yIndex == 0 ? minX : (yIndex == 1 ? minY : minZ);
            float maxYHere = yIndex == 0 ? maxX : (yIndex == 1 ? maxY : maxZ);
            return new Vector2() { X = (position[xIndex] - minXHere) / (maxXHere - minXHere), Y = (position[yIndex] - minYHere) / (maxYHere - minYHere) };
        }
        public Vector2 GetTexturePosition(Vector2 position, int xIndex, int yIndex)
        {
            float minXHere = xIndex == 0 ? minX : (xIndex == 1 ? minY : minZ);
            float maxXHere = xIndex == 0 ? maxX : (xIndex == 1 ? maxY : maxZ);
            float minYHere = yIndex == 0 ? minX : (yIndex == 1 ? minY : minZ);
            float maxYHere = yIndex == 0 ? maxX : (yIndex == 1 ? maxY : maxZ);
            return new Vector2() { X = (position.X - minXHere) / (maxXHere - minXHere), Y = (position.Y - minYHere) / (maxYHere - minYHere) };
        }
        public Vector3 GetPositionFromTexturePosition(Vector2 texturePosition, int xIndex, int yIndex)
        {
            Vector3 retVal = new Vector3();
            float minXHere = xIndex == 0 ? minX : (xIndex == 1 ? minY : minZ);
            float maxXHere = xIndex == 0 ? maxX : (xIndex == 1 ? maxY : maxZ);
            float minYHere = yIndex == 0 ? minX : (yIndex == 1 ? minY : minZ);
            float maxYHere = yIndex == 0 ? maxX : (yIndex == 1 ? maxY : maxZ);
            retVal[xIndex] = minXHere + texturePosition.X * (maxXHere - minXHere);
            retVal[yIndex] = minYHere + texturePosition.Y * (maxYHere - minYHere);
            return retVal;
        }*/
    }
}
