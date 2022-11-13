using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PowderToy.Utilities
{
    public static class RadiusSelection
    {
        private static Dictionary<uint, Vector2Int[]> _coordinates;
        private static bool _preWarmed;

        public static Vector2Int[] GetCoordinates(in int radius) => GetCoordinates((uint)radius);
        
        public static Vector2Int[] GetCoordinates(in uint radius)
        {
            if (_preWarmed == false)
            {
                PreWarmCollections();
            }

            return _coordinates[radius];
        }

        private static void PreWarmCollections()
        {
            _coordinates = new Dictionary<uint, Vector2Int[]>(7);
            
            for (uint i = 1; i <= 7; i++)
            {
                var coordinates = new List<Vector2Int>();
                var rSqr = i * i;

                for (var x = 0; x <= i; x++)
                {
                    var d = (int)Mathf.Ceil(Mathf.Sqrt(rSqr - x * x));
                    for (var y = 0; y <= d; y++)
                    {
                        //FIXME Move this to pre-made array to avoid alloc issues
                        coordinates.Add(new Vector2Int(x, y));
                        coordinates.Add(new Vector2Int(-x, y));
                        coordinates.Add(new Vector2Int(x, -y));
                        coordinates.Add(new Vector2Int(-x, -y));
                    }
                }
                
                _coordinates.Add(i, 
                    coordinates
                        .Distinct()
                        .ToArray());
            }

            _preWarmed = true;
        }
        
    }
}