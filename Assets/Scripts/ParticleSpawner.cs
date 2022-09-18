using System;
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace PowderToy
{
    public class ParticleSpawner : MonoBehaviour
    {
        public static Vector2Int MouseCoordinate { get; private set; }
        public static int SpawnRadius { get; private set; }

        [SerializeField, ReadOnly]
        private int DEBUG_SpawnRadius;

        [SerializeField]
        private Particle.TYPE selectedType;
        
        private Vector2Int _gridSize;
        private Vector2 _screenSize;

        private bool _mouseDown;

        private Grid _particleGrid;
        
        //Unity Functions
        //============================================================================================================//

        private void OnEnable()
        {
            Grid.OnInit += Init;
            WorldTimer.OnTick += OnTick;
        }

        private void Start()
        {
            _particleGrid = FindObjectOfType<Grid>();
            SpawnRadius = 1;
        }

        // Update is called once per frame
        private void Update()
        {
            UpdateScreenPosition();
            
            if (Input.GetKeyDown(KeyCode.Mouse0))
                _mouseDown = true;
            else if(Input.GetKeyUp(KeyCode.Mouse0))
                _mouseDown = false;

            if (Input.GetKeyDown(KeyCode.A) && SpawnRadius > 1)
                SpawnRadius--;
            else if (Input.GetKeyDown(KeyCode.D) && SpawnRadius < 30)
                SpawnRadius++;

            DEBUG_SpawnRadius = SpawnRadius;
        }

        private void OnDisable()
        {
            Grid.OnInit -= Init;
            WorldTimer.OnTick -= OnTick;
        }

        //Init Function
        //============================================================================================================//

        private void Init(Vector2Int size)
        {
            _gridSize = size;
            _screenSize = new Vector2(Screen.width, Screen.height);
        }

        private void OnTick()
        {
            TrySpawnNewParticle();
        }

        private void TrySpawnNewParticle()
        {
            if (_mouseDown == false)
                return;
            
            _particleGrid.SpawnParticle(selectedType, MouseCoordinate);
        }
        
        //UpdateScreenPosition
        //============================================================================================================//
        private void UpdateScreenPosition()
        {
            var mousePosition = Input.mousePosition;

            var x = Mathf.Clamp(Mathf.FloorToInt((mousePosition.x / _screenSize.x) * _gridSize.x), 0, _gridSize.x - 1);
            var y = Mathf.Clamp(Mathf.FloorToInt((mousePosition.y / _screenSize.y) * _gridSize.y), 0, _gridSize.y - 1);

            MouseCoordinate = new Vector2Int(x, y);
        }
        //============================================================================================================//

    }
}
