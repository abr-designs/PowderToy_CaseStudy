using UnityEngine;

namespace PowderToy
{
    public class ParticleSpawner : MonoBehaviour
    {
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

            if (SpawnRadius == 0)
                _particleGrid.SpawnParticle(selectedType, MouseCoordinate);
            else
                TrySpawnNewParticlesInRadius(SpawnRadius);
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
