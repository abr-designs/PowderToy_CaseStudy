using System.Collections;
using System.Collections.Generic;
using PowderToy;
using UnityEngine;

namespace PowderToy
{
    public struct Command
    {
        public enum TYPE
        {
            NONE,
            SPAWN_PARTICLE,
            SPAWN_MANY_PARTICLES
        }

        public TYPE Type;
        public Particle.TYPE ParticleTypeToSpawn;
        public Vector2Int SpawnCoordinate;
        public uint SpawnRadius;
    }

}