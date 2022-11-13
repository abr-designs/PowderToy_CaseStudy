using UnityEngine;

namespace PowderToy
{
    public struct Command
    {
        public enum TYPE
        {
            NONE,
            SPAWN_PARTICLE,
        }

        public static readonly Command Empty = new Command(); 

        public TYPE Type;
        public Particle.TYPE TypeToSpawn;
        public Vector2Int mouseCoordinate;
        public uint SpawnRadius;
    }

}