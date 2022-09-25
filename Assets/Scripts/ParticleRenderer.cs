using System;
using System.Collections.Generic;
using UnityEngine;

namespace PowderToy
{
    public class ParticleRenderer : MonoBehaviour
    {
        private static readonly int BaseMapPropertyID = Shader.PropertyToID("_BaseMap");

        [Serializable]
        private struct ParticleColor
        {
            public Particle.TYPE Type;
            public Color Color;
        }
        //Properties
        //============================================================================================================//
        private Vector2Int _size;

        [SerializeField]
        private bool USE_DEBUG_VIEW;

        [Min(0), SerializeField]
        private int DEBUG_hightlightIndex;

        [SerializeField] private Renderer targetRenderer;
        private Material _sharedMaterial;

        //[SerializeField] private Color setColor = Color.black;
        [SerializeField]
        private ParticleColor[] particleColors;

        private Texture2D _testTexture;

        //private Texture2D _emptyTexture;
        private Color[] _blankTexture;
        private Color[] _activeTexture;
        private Dictionary<Particle.TYPE, Color> _particleColors;
        
        private Dictionary<int, Vector2Int[]> _mouseRadiusPositions;

        
        //Unity Functions
        //============================================================================================================//

        private void OnEnable() => Grid.OnInit += Init;

        private void OnDisable() => Grid.OnInit -= Init;

        //Init Function
        //============================================================================================================//

        public void Init(Vector2Int size)
        {
            PreWarmMouseRadius();
            
            //Setup Color references
            //------------------------------------------------------------------//

            _particleColors = new Dictionary<Particle.TYPE, Color>();
            for (int i = 0; i < particleColors.Length; i++)
            {
                var data = particleColors[i];
                _particleColors.Add(data.Type, data.Color);
            }
            
            //Setup Texture Size
            //------------------------------------------------------------------//

            _size = size;
            
            _activeTexture = new Color[_size.x * _size.y];
            _blankTexture = new Color[_size.x * _size.y];
            for (var i = 0; i < _blankTexture.Length; i++)
            {
                _blankTexture[i] = Color.black;
            }
            
            //------------------------------------------------------------------//

            _blankTexture.CopyTo(_activeTexture, 0);

            _sharedMaterial = targetRenderer.sharedMaterial;

            _testTexture = new Texture2D(_size.x, _size.y, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            SetPixels(_blankTexture);
            _sharedMaterial.SetTexture(BaseMapPropertyID, _testTexture);
        }

        public void UpdateTexture(in IReadOnlyList<Particle> particles)
        {
            int CoordinateToIndex(in int x, in int y) => (_size.x * y) + x;
            
            _blankTexture.CopyTo(_activeTexture, 0);
            
            var count = particles.Count;
            for (int i = 0; i < count; i++)
            {
                var particle = particles[i];
                var xCoord = particle.XCoord;
                var yCoord = particle.YCoord;

                /*if (USE_DEBUG_VIEW)
                    _activeTexture[CoordinateToIndex(coordinate.x, coordinate.y)] =
                        Color.Lerp(Color.green, Color.red, i / (float)count);
                else*/
                if(DEBUG_hightlightIndex == i)
                    _activeTexture[CoordinateToIndex(xCoord, yCoord)] = Color.magenta;
                else
                    _activeTexture[CoordinateToIndex(xCoord, yCoord)] = _particleColors[particle.Type];

            }

            UpdateMousePos(ParticleSpawner.SpawnRadius, Color.red);
            SetPixels(_activeTexture);
        }

        private void PreWarmMouseRadius()
        {
            _mouseRadiusPositions = new Dictionary<int, Vector2Int[]>();
            //Radius 1 => 12
            //Radius 2 => 28
            //Radius 3 => 52
            //Radius 4 => 80
            //Radius 5 => 112
            //Radius 6 => 160
            //Radius 7 => 204
            
            //FIXME This is what I want to do
            /*var counts = new byte[] { 12, 28, 52, 80, 112, 160, 204 };
            
            for (int i = 1; i <= 7; i++)
            {
                var rSqr = i * i;
                
                var coordinateCount = counts[i - 1];
                var coordinates = new Vector2Int[coordinateCount];
                var counter = 0;
                
                coordinates[counter++] = Vector2Int.zero;

                for (var x = 1; x <= i; x++)
                {
                    var d = (int)Mathf.Ceil(Mathf.Sqrt(rSqr - x * x));
                    for (var y = 1; y <= d; y++, counter += 4)
                    {
                        coordinates[counter] = new Vector2Int(x, y);
                        coordinates[counter + 1] = new Vector2Int(-x, y);
                        coordinates[counter + 2] = new Vector2Int(x, -y);
                        coordinates[counter + 3] = new Vector2Int(-x, -y);
                    }
                }

                _mouseRadiusPositions.Add(i, coordinates);
            }*/

            for (int i = 1; i <= 7; i++)
            {
                var coordinates = new List<Vector2Int>();
                var rSqr = i * i;

                for (var x = 0; x <= i; x++)
                {
                    var d = (int)Mathf.Ceil(Mathf.Sqrt(rSqr - x * x));
                    for (var y = 0; y <= d; y++)
                    {
                        //FIXME Move this to pre-made array to avoid alloc issues
                        coordinates.Add(new Vector2Int(x, y));
                        coordinates.Add(new Vector2Int(-x, y));
                        coordinates.Add(new Vector2Int(x, -y));
                        coordinates.Add(new Vector2Int(-x, -y));
                    }
                }
                
                _mouseRadiusPositions.Add(i, coordinates.ToArray());
            }

        }

        private void UpdateMousePos(in int radius, in Color color)
        {
            var sizeX = _size.x;
            
            //int CoordinateToIndex(in int x, in int y) => (sizeX * y) + x;
            var mouseX = ParticleSpawner.MouseCoordinate.x;
            var mouseY = ParticleSpawner.MouseCoordinate.y;

            if (radius == 0)
            {
                var index = (sizeX * mouseY) + mouseX;
                _activeTexture[index] = color;
                return;
            }

            /*int px, nx, py, ny, d;
            var coordinates = new List<Vector2Int>();
            var mouseCoord = ParticleSpawner.MouseCoordinate;
            var rSqr = radius * radius;
            
            

            
            for (var x = 0; x <= radius; x++)
            {
                d = (int)Mathf.Ceil(Mathf.Sqrt(rSqr - x * x));
                for (var y = 0; y <= d; y++)
                {
                    px = mouseCoord.x + x;
                    nx = mouseCoord.x - x;
                    
                    py = mouseCoord.y + y;
                    ny = mouseCoord.y - y;
                    
                    //FIXME Move this to pre-made array to avoid alloc issues
                    coordinates.Add(new Vector2Int(px, py));
                    coordinates.Add(new Vector2Int(nx, py));
                    coordinates.Add(new Vector2Int(px, ny));
                    coordinates.Add(new Vector2Int(nx, ny));
                }
            }*/

            var coordinates = _mouseRadiusPositions[radius];

            //Debug.Log($"{radius} => {coordinates.Count}");
            for (int i = 0; i < coordinates.Length; i++)
            {
                var coord = coordinates[i];
                var newX = coord.x + mouseX;
                var newY = coord.y + mouseY;
                
                if (newX >= _size.x || newX < 0)
                    continue;
                if (newY>= _size.y || newY < 0)
                    continue;
                var index = (sizeX * newY) + newX;
                _activeTexture[index] = color;
            }
        }

        private void SetPixels(in Color[] colors)
        {
            //Set the new change
            _testTexture.SetPixels(colors);
            //Push to the texture
            _testTexture.Apply();
        }

        //============================================================================================================//

    }
}
