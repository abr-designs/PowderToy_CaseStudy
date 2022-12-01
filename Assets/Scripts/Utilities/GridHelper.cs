using System.Runtime.CompilerServices;

namespace PowderToy.Utilities
{
    public class SurroundingData
    {
        private static bool _ready;
        private static Grid.GridPos[] _gridPosArray;
        private static Particle[] _particles;
        
        public bool IsValid;
        public bool IsOccupied;
        
        public int GridIndex;
        public int ParticleIndex;
        public Grid.GridPos GridPos;
        public Particle Particle;

        public SurroundingData(ref Grid.GridPos[] gridPosArray, ref Particle[] activeParticles)
        {
            if (_ready)
                return;
            
            _gridPosArray = gridPosArray;
            _particles = activeParticles;
            _ready = true;
        }
        

        public void Setup(in int gridIndex)
        {
            IsValid = gridIndex >= 0;
            
            if(IsValid == false)
                return;

            GridIndex = gridIndex;
            GridPos = _gridPosArray[gridIndex];
            IsOccupied = GridPos.IsOccupied;
            
            if (IsOccupied == false)
                return;
                
            ParticleIndex = GridPos.ParticleIndex;
            Particle = _particles[ParticleIndex];
        }
    }
    
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
        
        public static void GetParticleSurroundings(in int originalX, in int originalY, ref SurroundingData[] surroundingArray)
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
            surroundingArray[0].Setup(atLeft || atTop ? -1 : originalIndex + _sizeX - 1);//GridHelper.CoordinateToIndex(originalX - 1, originalY + 1);
            surroundingArray[1].Setup(atTop ? -1 : originalIndex + _sizeX);//GridHelper.CoordinateToIndex(originalX, originalY + 1);
            surroundingArray[2].Setup(atRight || atTop ? -1 : originalIndex + _sizeX + 1);//GridHelper.CoordinateToIndex(originalX + 1, originalY + 1);
            
            //Row[1]
            //------------------------------------------------//
            surroundingArray[3].Setup(atLeft ? -1 : originalIndex - 1);//GridHelper.CoordinateToIndex(originalX - 1, originalY);
            //Original Coordinate
            surroundingArray[4].Setup(originalIndex);
            surroundingArray[5].Setup(atRight ? -1 : originalIndex + 1);//GridHelper.CoordinateToIndex(originalX + 1, originalY);
            
            //Row[2]
            //------------------------------------------------//
            surroundingArray[6].Setup(atLeft || atBottom ? -1 : originalIndex - _sizeX - 1);//GridHelper.CoordinateToIndex(originalX - 1, originalY - 1);
            surroundingArray[7].Setup(atBottom ? -1 : originalIndex - _sizeX);//GridHelper.CoordinateToIndex(originalX, originalY - 1);
            surroundingArray[8].Setup(atRight || atBottom ? -1 : originalIndex - _sizeX + 1);//GridHelper.CoordinateToIndex(originalX + 1, originalY - 1);
        }
    }
}