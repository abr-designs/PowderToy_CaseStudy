using System.Runtime.CompilerServices;

namespace PowderToy.Utilities
{
    public static class GridHelper
    {
        private static int _sizeX;
        private static int _sizeY;
        private static int _maxSize;
        
        public static void InitGridData(in int width, in int height)
        {
            _sizeX = width;
            _sizeY = height;
            _maxSize = _sizeX * _sizeY;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CoordinateToIndex(in int x, in int y) => (_sizeX * y) + x;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CoordinateToIndex(in Particle particle) => (_sizeX * particle.YCoord) + particle.XCoord;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsLegalCoordinate(in int x, in int y)
        {
            if (x >= _sizeX || x < 0)
                return false;
            if (y >= _sizeY || y < 0)
                return false;

            return true;
        }

        /*[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsLegalIndex(in int index) => index >= 0 && index < _maxSize;*/
        
        public static void GetParticleSurroundings(in int originalX, in int originalY, ref int[] surroundingArray)
        {
            //[0 1 2]
            //[3 x 5]
            //[6 7 8]
            //TODO I can probably simplify the math here by just adding index values

            var originalIndex = CoordinateToIndex(originalX, originalY);
            var atLeft = originalX == 0;
            var atRight = originalX == _sizeX - 1;
            var atTop = originalY == _sizeY - 1;
            var atBottom = originalY == 0;
            
            //Row[0]
            //------------------------------------------------//
            //FIXME There are issues with this corner conversion. Top-left corner minus one should not be row down right side
            surroundingArray[0] = atLeft || atTop ? -1 : originalIndex + _sizeX - 1;//GridHelper.CoordinateToIndex(originalX - 1, originalY + 1);
            surroundingArray[1] = atTop ? -1 : originalIndex + _sizeX;//GridHelper.CoordinateToIndex(originalX, originalY + 1);
            surroundingArray[2] = atRight || atTop ? -1 : originalIndex + _sizeX + 1;//GridHelper.CoordinateToIndex(originalX + 1, originalY + 1);
            
            //Row[1]
            //------------------------------------------------//
            surroundingArray[3] = atLeft ? -1 : originalIndex - 1;//GridHelper.CoordinateToIndex(originalX - 1, originalY);
            //Original Coordinate
            surroundingArray[4] = originalIndex;
            surroundingArray[5] = atRight ? -1 : originalIndex + 1;//GridHelper.CoordinateToIndex(originalX + 1, originalY);
            
            //Row[2]
            //------------------------------------------------//
            surroundingArray[6] = atLeft || atBottom ? -1 : originalIndex - _sizeX - 1;//GridHelper.CoordinateToIndex(originalX - 1, originalY - 1);
            surroundingArray[7] = atBottom ? -1 : originalIndex - _sizeX;//GridHelper.CoordinateToIndex(originalX, originalY - 1);
            surroundingArray[8] = atRight || atBottom ? -1 : originalIndex - _sizeX + 1;//GridHelper.CoordinateToIndex(originalX + 1, originalY - 1);
        }
    }
}