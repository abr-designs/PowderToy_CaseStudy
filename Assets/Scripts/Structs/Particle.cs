using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PowderToy
{
    public struct Particle
    {
        public const int WAIT_TO_SLEEP = 3;
        public static readonly Particle Empty = new Particle();

        public enum TYPE
        {
            NONE,
            SAND,
            WATER
        }

        public TYPE Type;
        public int Index;
        public int SleepCounter;
        public bool IsOccupied;
        public bool Asleep;
        public Vector2Int Coordinate;
    }
}
