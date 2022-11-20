using UnityEngine;

namespace PowderToy
{
    //TODO Need to investigate whether this is better as a class to reduce constant memory copy calls
    public class Particle
    {
        public const int WAIT_TO_SLEEP = 50;
        public static readonly Particle Empty = new Particle();

        public enum TYPE
        {
            NONE,
            SAND,
            WATER,
            WOOD,
            STEAM,
            FIRE,
            OIL
        }

        public enum MATERIAL
        {
            NONE,
            SOLID,
            POWDER,
            LIQUID,
            GAS
        }

        public TYPE Type;
        public MATERIAL Material;
        public Color32 Color; 
        
        public int Index;
        public int SleepCounter;
        public bool Asleep;
        public int XCoord;
        public int YCoord;
        
        public bool KillNextTick;

        public bool HasDensity;
        public uint Density;

        public bool HasLifeSpan;
        public uint Lifetime;

        public bool CanBurn;
        public uint ChanceToBurn;

        /*public Particle(
            in TYPE type, 
            in MATERIAL material, 
            in Color32 color, 
            
            in bool hasDensity,
            in bool hasLifeSpan,
            in bool canBurn,
            
            in int index, 
            in int x, 
            in int y)
        {
            SleepCounter = 0;
            Asleep = false;
            KillNextTick = false;

            Type = type;
            Material = material;
            Color = color;
            Index = index;

            XCoord = x;
            YCoord = y;

            HasDensity = hasDensity;
            Density = 10;

            HasLifeSpan = hasLifeSpan;
            Lifetime = 0;

            CanBurn = canBurn;
            ChanceToBurn = 0;
        }*/

        public void CopyFrom(in Particle copyFrom)
        {
            SleepCounter = copyFrom.SleepCounter;
            Asleep = copyFrom.Asleep;
            KillNextTick = copyFrom.KillNextTick;
            Type = copyFrom.Type;
            Material = copyFrom.Material;
            Color = copyFrom.Color;
            Index = copyFrom.Index;
            XCoord = copyFrom.XCoord;
            YCoord = copyFrom.YCoord;
            HasDensity = copyFrom.HasDensity;
            Density = copyFrom.Density;
            HasLifeSpan = copyFrom.HasLifeSpan;
            Lifetime = copyFrom.Lifetime;
            CanBurn = copyFrom.CanBurn;
            ChanceToBurn = copyFrom.ChanceToBurn;
        }
    }
}
