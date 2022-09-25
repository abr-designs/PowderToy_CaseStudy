using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PowderToy
{
    public struct Particle
    {
        public const int WAIT_TO_SLEEP = 50;
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
        //public bool IsOccupied;
        public bool Asleep;
        //public Vector2Int Coordinate;
        public int XCoord;
        public int YCoord;
    }
    
    /*public class CompareParticleAscending : IComparer<Particle>
    {
        public int Compare(Particle particleA, Particle particleB)
        {
            return particleA.Coordinate.y - particleB.Coordinate.y;

            /*switch (dif)
            {
                case > 0:
                    return 1;
                case < 0:
                    return -1;
                default:
                    return 0;
            }#1#
        }
    }*/
}
