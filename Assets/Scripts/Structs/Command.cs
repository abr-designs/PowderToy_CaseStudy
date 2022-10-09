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

        public Particle.TYPE TypeToSpawn;
        public Vector2Int spawnCoordinate;
        public uint SpawnRadius;
    }

}