using System;
using UnityEngine;

namespace PowderToy.Utilities
{
    [Obsolete("Now built into UI")]
    public class GridDebugger : MonoBehaviour
    {

#if UNITY_EDITOR
        [SerializeField, Header("Position Information")]
        private Vector2Int coordinate;
        [SerializeField]private int gridIndex;
        [SerializeField]private bool isOccupied;
        
        [SerializeField,Header("Particle Information")]private bool hasParticle;
        [SerializeField]private Particle.TYPE particleType;
        [SerializeField]private Particle.MATERIAL materialType;
        [SerializeField]private int lifeTime;
        [SerializeField]private int currentTemperature;
        [SerializeField]private bool hasWarmed;
        
        private Grid grid;
        private void OnEnable()
        {
            WorldTimer.OnTick += TryUpdateData;
        }

        private void Start()
        {
            grid = FindObjectOfType<Grid>();
        }

        private void OnDisable()
        {
            WorldTimer.OnTick -= TryUpdateData;
        }

        private void TryUpdateData()
        {
            coordinate = ParticleGridMouseInput.MouseCoordinate;
            
            gridIndex = -1;
            isOccupied = default;
            hasParticle = default;
            particleType = default;
            materialType = default;
            lifeTime = default;
            
            var data = grid.GetParticleAtCoordinate(coordinate.x, coordinate.y);

            gridIndex = data.gridIndex;
            isOccupied = data.gridPos.IsOccupied;

            if (data.particle == null)
            {
                hasParticle = false;
                return;
            }

            hasParticle = true;
            particleType = data.particle.Type;
            materialType = data.particle.Material;
            lifeTime = (int)data.particle.Lifetime;
            currentTemperature = data.particle.CurrentTemperature;
            hasWarmed = data.particle.HasChangedTemp;

        }
#endif
    }
}