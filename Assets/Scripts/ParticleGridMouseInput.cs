using System;
using UnityEngine;

namespace PowderToy
{
    //TODO This needs to be a queue, race conditions causing issues with Updates
    public class ParticleGridMouseInput : MonoBehaviour
    {
        public static Action<Particle.TYPE> OnParticleTypeSelected;
        
        private const int MIN_RADIUS = 0;
        private const int MAX_RADIUS = 7;
        public static Vector2Int MouseCoordinate { get; private set; }
        public static int SpawnRadius { get; private set; }

        [SerializeField]
        private bool usePressAndHold = true;

        [SerializeField]
        private Particle.TYPE selectedParticleType;
        
        private Vector2Int _gridSize;
        private Vector2 _screenSize;

        private bool _mouseDown;

        //Unity Functions
        //============================================================================================================//

        private void OnEnable()
        {
            Grid.OnInit += Init;
            WorldTimer.OnTick += OnTick;
        }

        private void Start()
        {
            SpawnRadius = 0;
            
            //Make sure that we announce the selected type on start
            OnParticleTypeSelected?.Invoke(selectedParticleType);
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
                case Particle.TYPE.SAND:
                case Particle.TYPE.WATER:
                case Particle.TYPE.WOOD:
                case Particle.TYPE.STEAM:
                case Particle.TYPE.FIRE:
                    Grid.QueuedCommand = new Command
                    {
                        Type = Command.TYPE.SPAWN_PARTICLE,
                        ParticleTypeToSpawn = selectedParticleType,
                        InteractionCoordinate = MouseCoordinate,
                        InteractionRadius = (uint)SpawnRadius
                    };
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if(usePressAndHold == false)
                _mouseDown = false;
        }

        private void ToggleSpawnType()
        {
            var newType = (int)selectedParticleType;
            newType++;
            if (newType > 5)
                newType = 0;


            selectedParticleType = (Particle.TYPE)newType;
            OnParticleTypeSelected?.Invoke(selectedParticleType);
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
