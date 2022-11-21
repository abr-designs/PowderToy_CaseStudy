using System.Collections.Generic;
using PowderToy.ScriptableObjects;
using UnityEngine;

namespace PowderToy
{
    public class ParticleFactory : MonoBehaviour
    {
        //private static ParticleFactory _instance;
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
            toSet.ChanceToBurn = (uint)template.burnChance;

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

        public static void ConvertToFire(ref Particle toConvert)
        {
            var fireTemplate = _templates[Particle.TYPE.FIRE];
            var fromTemplate = _templates[toConvert.Type];

            toConvert.Type = Particle.TYPE.FIRE;
            toConvert.Color = fireTemplate.GetRandomColor();
            toConvert.CanBurn = false;
            
            toConvert.HasLifeSpan = fireTemplate.hasLifetime;
            toConvert.Lifetime = fireTemplate.GetRandomLifetime(fromTemplate.burnLifeMultiplier);
            toConvert.ChanceToBurn = (uint)fireTemplate.burnChance;
        }
        
        //============================================================================================================//
        
    }
}