using UnityEngine;

namespace PowderToy
{
    public struct Command
    {
        public enum TYPE
        {
            NONE,
            SPAWN_PARTICLE,
            KILL_PARTICLE
        }

        public TYPE Type;
        public Particle.TYPE ParticleTypeToSpawn;
        public Vector2Int InteractionCoordinate;
        public uint InteractionRadius;
    }

}