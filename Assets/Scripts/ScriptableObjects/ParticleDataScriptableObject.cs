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
        //Common Densities: https://www.efunda.com/Materials/common_matl/common_matl.cfm?MatlPhase=Liquid&MatlProp=Physical
        [Serializable]
        public struct ParticleData
        {
            public string name;
            public Particle.TYPE type;

            public Particle.MATERIAL material;
            public Gradient gradient;

            [Space(10f)]
            public bool hasDensity;
            [Range(0f,2f)]
            public float density;

            [Space(10f)]
            public bool hasLifetime;
            [Min(0)]
            public int lifetimeMin;
            [Min(0)]
            public int lifetimeMax;
            
            [Space(10f)]
            public bool canBurn;
            public int combustionTemperature;
            [Min(0.1f)]
            public float burnLifeMultiplier;

            [Space(10f)]
            public bool hasStartTemperature;
            public int startTemp;
            
            public Color32 GetRandomColor()
            {
                return gradient.Evaluate(Random.value);
            }

            public uint GetRandomLifetime(in float multiplier = 1f)
            {
                if (hasLifetime == false)
                    return 0;

                return (uint)Mathf.RoundToInt(Random.Range(lifetimeMin, lifetimeMax + 1) * multiplier);
            }

            public uint GetDensity()
            {
                if (hasDensity == false)
                    return 0;

                return (uint)Mathf.RoundToInt(density * 10f);
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