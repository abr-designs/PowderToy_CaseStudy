using System.Collections.Generic;
using System.Linq;
using PowderToy.ScriptableObjects;
using UnityEngine;

namespace PowderToy
{
    public class ParticleFactory : MonoBehaviour
    {
        private static bool _isReady;
        private static Dictionary<Particle.TYPE, ParticleDataScriptableObject.ParticleData> _templates;


        [SerializeField]
        private ParticleDataScriptableObject particleDataScriptableObject;


        public static void CreateParticle(ref Particle toSet, in Particle.TYPE particleType, in int index, in int xCoord, in int yCoord)
        {
            
            if (_isReady == false)
            {
                var instance = FindObjectOfType<ParticleFactory>();
                _templates = instance.particleDataScriptableObject.GetParticleDataDictionary();

                _isReady = true;
            }

            var template = _templates[particleType];

            toSet.Type = particleType;
            toSet.Material = template.material;
            toSet.Color = template.GetRandomColor();
            toSet.HasDensity = template.hasDensity;
            toSet.HasLifeSpan = template.hasLifetime;
            toSet.CanBurn = template.canBurn;
            toSet.Index = index;
            toSet.XCoord = xCoord;
            toSet.YCoord = yCoord;
            
            toSet.Density = template.GetDensity();
            toSet.Lifetime = template.GetRandomLifetime();
            toSet.CombustionTemperature = template.combustionTemperature;
            toSet.CurrentTemperature = template.overrideStartTemperature ? template.overrideTemp : Grid.AmbientTemperature;
            toSet.SpreadsHeat = template.spreadHeat;
            toSet.CanCool = template.canCool;

            /*return new Particle(
                particleType,
                template.material,
                template.GetRandomColor(),
                template.hasDensity,
                template.hasLifetime,
                template.canBurn,
                index,
                xCoord,
                yCoord)
            {
                Density = template.GetDensity(),
                Lifetime = template.GetRandomLifetime(),
                ChanceToBurn = (uint)template.burnChance
            };*/
        }

        //============================================================================================================//
        
        public static void ConvertTo(in Particle.TYPE toParticleType, ref Particle particleToConvert, in bool useCurrentTemp = true)
        {
            var newTemplate = _templates[toParticleType];

            particleToConvert.Type = toParticleType;
            particleToConvert.Material = newTemplate.material;
            particleToConvert.Color = newTemplate.GetRandomColor();
            particleToConvert.HasDensity = newTemplate.hasDensity;
            particleToConvert.HasLifeSpan = newTemplate.hasLifetime;
            particleToConvert.CanBurn = newTemplate.canBurn;
            particleToConvert.Density = newTemplate.GetDensity();
            particleToConvert.Lifetime = newTemplate.GetRandomLifetime();
            particleToConvert.CombustionTemperature = newTemplate.combustionTemperature;
            particleToConvert.SpreadsHeat = newTemplate.spreadHeat;
            particleToConvert.CanCool = newTemplate.canCool;
            //FIXME This might not work the way I want
            particleToConvert.CurrentTemperature = useCurrentTemp ? particleToConvert.CurrentTemperature : newTemplate.overrideTemp;
        }

        public static void ConvertToAndMaintainMaterial(in Particle.TYPE newType, ref Particle toConvert)
        {
            var newTemplate = _templates[newType];
            var fromTemplate = _templates[toConvert.Type];

            toConvert.Type = newType;
            toConvert.Color = newTemplate.GetRandomColor();
            toConvert.CanBurn = false;
            toConvert.SpreadsHeat = newTemplate.spreadHeat;
            
            toConvert.HasLifeSpan = newTemplate.hasLifetime;
            
            if(newTemplate.hasLifetime)
                toConvert.Lifetime = newTemplate.GetRandomLifetime(fromTemplate.burnLifeMultiplier);
        }
        
        //============================================================================================================//

#if UNITY_EDITOR
        public static string[] GetParticleNames()
        {
            var instance = FindObjectOfType<ParticleFactory>();
            var templates = instance.particleDataScriptableObject.GetParticleDataDictionary();
            
            var values = templates.Values.ToArray();
            var outArray = new string[values.Length];

            for (int i = 0; i < values.Length; i++)
            {
                outArray[i] = values[i].name;
            }

            return outArray;
        }
#endif
        
    }
}