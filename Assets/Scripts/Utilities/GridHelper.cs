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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsLegalIndex(in int index) => index >= 0 && index < _maxSize;
    }
}