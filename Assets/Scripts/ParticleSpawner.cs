using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PowderToy
{
    public class ParticleSpawner : MonoBehaviour
    {
        public static Action<Particle.TYPE> OnParticleTypeSelected;
        
        private const int MIN_RADIUS = 0;
        private const int MAX_RADIUS = 7;
        public static Vector2Int MouseCoordinate { get; private set; }
        public static int SpawnRadius { get; private set; }

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
            SpawnRadius = 0;
            
            //Make sure that we announce the selected type on start
            OnParticleTypeSelected?.Invoke(selectedType);
        }

        // Update is called once per frame
        private void Update()
        {
            UpdateScreenPosition();
            
            if (Input.GetKeyDown(KeyCode.Mouse0))
                _mouseDown = true;
            else if(Input.GetKeyUp(KeyCode.Mouse0))
                _mouseDown = false;

            if (Input.GetKeyDown(KeyCode.A) && SpawnRadius > MIN_RADIUS)
                SpawnRadius--;
            else if (Input.GetKeyDown(KeyCode.D) && SpawnRadius < MAX_RADIUS)
                SpawnRadius++;

            if (Input.GetKeyDown(KeyCode.Tab))
                ToggleSpawnType();
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

        //TODO Should this be passed as a queued request?
        //Currently seems to be fighting with the other update methods
        private void TrySpawnNewParticle()
        {
            if (_mouseDown == false)
                return;
            
            //If the eraser is selected
            if (selectedType == Particle.TYPE.NONE)
            {
                if (SpawnRadius == 0)
                    _particleGrid.RemoveParticle(MouseCoordinate);
                else
                {
                    var removeRadius = TryRemoveParticlesInRadius(SpawnRadius);
                    _particleGrid.RemoveParticles(removeRadius);
                }
                
            }
            else
            {
                if (SpawnRadius == 0)
                    _particleGrid.SpawnParticle(selectedType, MouseCoordinate);
                else
                    TrySpawnNewParticlesInRadius(SpawnRadius);
            }

            
        }

        private void TrySpawnNewParticlesInRadius(in int radius)
        {
            int x, y, px, nx, py, ny, d;
            
            var coord = MouseCoordinate;
            var rSqr = radius * radius;

            for (x = 0; x <= radius; x++)
            {
                d = (int)Mathf.Ceil(Mathf.Sqrt(rSqr - x * x));
                for (y = 0; y <= d; y++)
                {
                    px = coord.x + x;
                    nx = coord.x - x;
                    
                    py = coord.y + y;
                    ny = coord.y - y;

                    _particleGrid.SpawnParticle(selectedType, new Vector2Int(px, py));
                    _particleGrid.SpawnParticle(selectedType, new Vector2Int(nx, py));
                    _particleGrid.SpawnParticle(selectedType, new Vector2Int(px, ny));
                    _particleGrid.SpawnParticle(selectedType, new Vector2Int(nx, ny));
                }
            }

            //_mouseDown = false;
        }
        //FIXME I should be saving this radius size array elsewhere
        private Vector2Int[] TryRemoveParticlesInRadius(in int radius)
        {
            int x, y, px, nx, py, ny, d;
            
            var coord = MouseCoordinate;
            var rSqr = radius * radius;
            var coordinates = new List<Vector2Int>();

            for (x = 0; x <= radius; x++)
            {
                d = (int)Mathf.Ceil(Mathf.Sqrt(rSqr - x * x));
                for (y = 0; y <= d; y++)
                {
                    px = coord.x + x;
                    nx = coord.x - x;
                    
                    py = coord.y + y;
                    ny = coord.y - y;

                    coordinates.Add(new Vector2Int(px, py));
                    coordinates.Add(new Vector2Int(nx, py));
                    coordinates.Add(new Vector2Int(px, ny));
                    coordinates.Add(new Vector2Int(nx, ny));
                }
            }

            //_mouseDown = false;
            return coordinates.Distinct().ToArray();
        }

        private void ToggleSpawnType()
        {
            var newType = (int)selectedType;
            newType++;
            if (newType > 4)
                newType = 0;


            selectedType = (Particle.TYPE)newType;
            OnParticleTypeSelected?.Invoke(selectedType);
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
