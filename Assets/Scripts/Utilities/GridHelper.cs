using System.Runtime.CompilerServices;

namespace PowderToy.Utilities
{
    public static class GridHelper
    {
        private static int _sizeX;
        private static int _sizeY;
        
        public static void InitGridData(in int width, in int height)
        {
            _sizeX = width;
            _sizeY = height;
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
    }
}