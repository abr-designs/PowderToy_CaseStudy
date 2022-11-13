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
    }
}