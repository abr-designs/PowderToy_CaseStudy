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
            OIL,
            STONE,
            MOLTEN_STONE,
            METAL,
            MOLTEN_METAL,
            ICE,
            ACID,
            SMOKE
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
        public int CombustionTemperature;

        public int CurrentTemperature;

        /// <summary>
        /// This value is used when two particles swap positions, and that counts as both of their updates for that tick
        /// </summary>
        public bool IsSwapLocked;

        public bool SpreadsHeat;
        public bool CanCool;
        public bool HasChangedTemp;

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
            CombustionTemperature = copyFrom.CombustionTemperature;
            CurrentTemperature = copyFrom.CurrentTemperature;
            SpreadsHeat = copyFrom.SpreadsHeat;
            CanCool = copyFrom.CanCool;
        }
    }
}
