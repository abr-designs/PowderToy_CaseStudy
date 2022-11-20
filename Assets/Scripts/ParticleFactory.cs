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


        public static Particle CreateParticle(in Particle.TYPE particleType, in int index, in int xCoord, in int yCoord)
        {
            if (_isReady == false)
            {
                var instance = FindObjectOfType<ParticleFactory>();
                _templates = instance.particleDataScriptableObject.GetParticleDataDictionary();

                _isReady = true;
            }

            var template = _templates[particleType];

            return new Particle(
                particleType,
                template.material,
                template.GetRandomColor(),
                template.hasLifetime,
                template.canBurn,
                index,
                xCoord,
                yCoord)
            {
                Lifetime = template.GetRandomLifetime(),
                ChanceToBurn = (uint)template.burnChance
            };
        }
        
        //============================================================================================================//

        public static Particle ConvertToFire(in Particle toConvert)
        {
            var fireTemplate = _templates[Particle.TYPE.FIRE];
            var fromTemplate = _templates[toConvert.Type];
            
            return new Particle(
                Particle.TYPE.FIRE,
                toConvert.Material,
                fireTemplate.GetRandomColor(),
                fireTemplate.hasLifetime,
                false,
                toConvert.Index,
                toConvert.XCoord,
                toConvert.YCoord)
            {
                Lifetime = fireTemplate.GetRandomLifetime(fromTemplate.burnLifeMultiplier),
                ChanceToBurn = (uint)fireTemplate.burnChance
            };
            
        }
        
        //============================================================================================================//
    }
}