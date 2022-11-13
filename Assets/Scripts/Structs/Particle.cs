using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PowderToy
{
    public struct Particle
    {
        public const int WAIT_TO_SLEEP = 50;
        public static readonly Particle Empty = new Particle();

        //FIXME Does this make sense to exist somewhere else?
        private static readonly Dictionary<TYPE, MATERIAL> ParticleMaterials = new Dictionary<TYPE, MATERIAL>()
        {
            [TYPE.SAND] = MATERIAL.POWDER,
            [TYPE.WATER] = MATERIAL.LIQUID,
            [TYPE.WOOD] = MATERIAL.SOLID,
            [TYPE.STEAM] = MATERIAL.GAS,
        };

        public enum TYPE
        {
            NONE,
            SAND,
            WATER,
            WOOD,
            STEAM
        }

        public enum MATERIAL
        {
            NONE,
            SOLID,
            POWDER,
            LIQUID,
            GAS
        }

        public readonly TYPE Type;
        public readonly MATERIAL Material;
        public readonly Color32 Color; 
        
        public int Index;
        public int SleepCounter;
        public bool Asleep;
        public bool WillBeKilled;
        public int XCoord;
        public int YCoord;

        public Particle(in TYPE type, in Color32 color, in int index, in int x, in int y)
        {
            SleepCounter = 0;
            Asleep = false;
            WillBeKilled = false;

            Type = type;
            Material = ParticleMaterials[type];
            Color = color;
            Index = index;

            XCoord = x;
            YCoord = y;
        }
    }
}
