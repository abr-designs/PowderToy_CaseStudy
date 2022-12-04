using System;
using PowderToy.UI;
using UnityEngine;

namespace PowderToy
{
    //TODO This needs to be a queue, race conditions causing issues with Updates
    public class ParticleGridMouseInput : MonoBehaviour
    {
        [SerializeField]
        private MeshRenderer meshRenderer;
        [SerializeField]
        private Camera camera;
        
        private const int MIN_RADIUS = 0;
        private const int MAX_RADIUS = 7;
        public static Vector2Int MouseCoordinate { get; private set; }
        public static int SpawnRadius { get; private set; }

        [SerializeField] private bool usePressAndHold = true;

        private Particle.TYPE selectedParticleType;

        private Vector2Int _gridSize;
        private Vector2 _screenSize;

        private bool _mouseDown;

        private bool _mouseOnGrid;
        private Rect gridRect;

        //Unity Functions
        //============================================================================================================//

        private void OnEnable()
        {
            Grid.OnInit += Init;
            WorldTimer.OnTick += OnTick;
            UIManager.OnParticleTypeSelected += OnParticleTypeSelected;
        }

        private void Start()
        {
            SpawnRadius = 0;

            var min = meshRenderer.bounds.min;
            var max = meshRenderer.bounds.max;

            var minScreenPos = camera.WorldToScreenPoint(min);
            var maxScreenPos = camera.WorldToScreenPoint(max);
            

            gridRect = new Rect
            {
                xMin = minScreenPos.x,
                xMax = maxScreenPos.x,

                yMin = minScreenPos.y,
                yMax = maxScreenPos.y
            };
            
            _screenSize = new Vector2(gridRect.width, gridRect.height);


            //Make sure that we announce the selected type on start
            //OnParticleTypeSelected?.Invoke(selectedParticleType);
        }

        // Update is called once per frame
        private void Update()
        {
            UpdateScreenPosition();

            if (Input.GetKeyDown(KeyCode.Mouse0))
                _mouseDown = true;
            else if (Input.GetKeyUp(KeyCode.Mouse0))
                _mouseDown = false;

            if (Input.GetKeyDown(KeyCode.A) && SpawnRadius > MIN_RADIUS)
                SpawnRadius--;
            else if (Input.GetKeyDown(KeyCode.D) && SpawnRadius < MAX_RADIUS)
                SpawnRadius++;

            /*if (Input.GetKeyDown(KeyCode.Tab))
                ToggleSpawnType();*/
        }

        private void OnDisable()
        {
            Grid.OnInit -= Init;
            WorldTimer.OnTick -= OnTick;
            UIManager.OnParticleTypeSelected -= OnParticleTypeSelected;

        }

        //Init Function
        //============================================================================================================//

        private void Init(Vector2Int size)
        {
            _gridSize = size;
        }

        private void OnTick()
        {
            if (_mouseOnGrid == false)
                return;
            if (_mouseDown == false)
                return;

            switch (selectedParticleType)
            {
                case Particle.TYPE.NONE:
                    Grid.QueuedCommand = new Command
                    {
                        Type = Command.TYPE.KILL_PARTICLE,
                        InteractionCoordinate = MouseCoordinate,
                        InteractionRadius = (uint)SpawnRadius
                    };
                    break;
                default:
                    Grid.QueuedCommand = new Command
                    {
                        Type = Command.TYPE.SPAWN_PARTICLE,
                        ParticleTypeToSpawn = selectedParticleType,
                        InteractionCoordinate = MouseCoordinate,
                        InteractionRadius = (uint)SpawnRadius
                    };
                    break;
            }

            if (usePressAndHold == false)
                _mouseDown = false;
        }

        /*private void ToggleSpawnType()
        {
            var newType = (int)selectedParticleType;
            newType++;
            if (newType > 8)
                newType = 0;


            selectedParticleType = (Particle.TYPE)newType;
            OnParticleTypeSelected?.Invoke(selectedParticleType);
        }*/

        private void OnParticleTypeSelected(Particle.TYPE particleType)
        {
            selectedParticleType = particleType;
        }

    //UpdateScreenPosition
        //============================================================================================================//
        private void UpdateScreenPosition()
        {
            //------------------------------------------------//
            
            void UpdateGridRect()
            {
                
                var meshBounds = meshRenderer.bounds;
                var min = meshBounds.min;
                var max = meshBounds.max;

                var minScreenPos = camera.WorldToScreenPoint(min);
                var maxScreenPos = camera.WorldToScreenPoint(max);
            
                gridRect.xMin = minScreenPos.x;
                gridRect.xMax = maxScreenPos.x;
                gridRect.yMin = minScreenPos.y;
                gridRect.yMax = maxScreenPos.y;
            
                _screenSize.x = gridRect.width;
                _screenSize.y = gridRect.height;
            }

            //------------------------------------------------//
            
            var mousePosition = (Vector2)Input.mousePosition;

            UpdateGridRect();
            _mouseOnGrid = gridRect.Contains(mousePosition);

            if (_mouseOnGrid == false)
                return;

            var x = Mathf.Clamp(Mathf.FloorToInt(((mousePosition.x - gridRect.xMin) / _screenSize.x) * _gridSize.x), 0, _gridSize.x - 1);
            var y = Mathf.Clamp(Mathf.FloorToInt(((mousePosition.y - gridRect.yMin) / _screenSize.y) * _gridSize.y), 0, _gridSize.y - 1);

            MouseCoordinate = new Vector2Int(x, y);
        }
        //============================================================================================================//

    }
}
