using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Unity.VisualScripting;
using UnityEngine;

using Random = UnityEngine.Random;

namespace PowderToy.ScriptableObjects
{
    [CreateAssetMenu(fileName = "ParticleData", menuName = "ScriptableObjects/Particle Data")]
    public class ParticleDataScriptableObject : ScriptableObject
    {
        [Serializable]
        public struct ParticleData
        {
            public string name;
            public Particle.TYPE type;

            public Particle.MATERIAL material;
            public Gradient gradient;

            [Space(10f)]
            public bool hasLifetime;
            [Min(0)]
            public int lifetimeMin;
            [Min(0)]
            public int lifetimeMax;
            
            [Space(10f)]
            public bool canBurn;
            [Range(0, 100)]
            public int burnChance;
            
            public Color32 GetRandomColor()
            {
                return gradient.Evaluate(Random.value);
            }

            public uint GetRandomLifetime()
            {
                if (hasLifetime == false)
                    return 0;

                return (uint)Random.Range(lifetimeMin, lifetimeMax + 1);
            }
        }

        [SerializeField]
        private ParticleData[] particleDatas;

        public Dictionary<Particle.TYPE, ParticleData> GetParticleDataDictionary()
        {
            //Set max to be all particle types minus NONE
            var maxCapacity = Enum.GetValues(typeof(Particle.TYPE)).Length - 1;
            var outDictionary = new Dictionary<Particle.TYPE, ParticleData>(maxCapacity);
            for (var i = 0; i < particleDatas.Length; i++)
            {
                outDictionary.Add(particleDatas[i].type, particleDatas[i]);
            }

            return outDictionary;
        }
    }
}