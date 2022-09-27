using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;
using Random = UnityEngine.Random;

namespace PowderToy
{
    public class ParticleRenderer : MonoBehaviour
    {
        private static readonly int BaseMapPropertyID = Shader.PropertyToID("_BaseMap");
        
        private static readonly Color32 black = new Color32(0, 0, 0, 255);
        private static readonly Color32 red = new Color32(255, 0, 0, 255);
        private static readonly Color32 green = new Color32(0,255,0,255);
        private static readonly Color32 magenta = new Color32(255, 0, 255, 255);

        //Structs
        //============================================================================================================//
        
        /// <summary>
        /// Stores gradient for Particle type color
        /// </summary>
        [Serializable]
        private struct ParticleColor
        {
            public Particle.TYPE Type;
            public Gradient Gradient;

            public Color32 GetColor()
            {
                return Gradient.Evaluate(Random.value);
            }
        }
        
        //Properties
        //============================================================================================================//
        private static int _sizeX;
        private static int _sizeY;

        [SerializeField, TitleGroup("Render Info")]
        private Renderer targetRenderer;
        private Material _sharedMaterial;

        [SerializeField, TitleGroup("Particle Colors")]
        private ParticleColor[] particleColors;
        private Dictionary<Particle.TYPE, ParticleColor> _particleColors;

        //Texture color array
        //------------------------------------------------//    
        private Texture2D _testTexture;
        private Color32[] _blankTexture;
        private Color32[] _activeTexture;
        
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

            _particleColors = new Dictionary<Particle.TYPE, ParticleColor>();
            for (int i = 0; i < particleColors.Length; i++)
            {
                var data = particleColors[i];
                _particleColors.Add(data.Type, data);
            }
            
            //Setup Texture Size
            //------------------------------------------------------------------//

            _sizeX = size.x;
            _sizeY = size.y;
            
            _activeTexture = new Color32[_sizeX * _sizeY];
            _blankTexture = new Color32[_sizeX * _sizeY];
            for (var i = 0; i < _blankTexture.Length; i++)
            {
                _blankTexture[i] = black;
            }
            
            //------------------------------------------------------------------//

            _blankTexture.CopyTo(_activeTexture, 0);

            _sharedMaterial = targetRenderer.sharedMaterial;

            _testTexture = new Texture2D(_sizeX, _sizeY, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            SetPixels(_blankTexture);
            _sharedMaterial.SetTexture(BaseMapPropertyID, _testTexture);
        }

        public void UpdateTexture(in Particle[] particles, in int count)
        {
            int CoordinateToIndex(in int x, in int y) => (_sizeX * y) + x;
            
            _blankTexture.CopyTo(_activeTexture, 0);
            
            //var count = particles.Count;
            for (int i = 0; i < count; i++)
            {
                var particle = particles[i];
                var xCoord = particle.XCoord;
                var yCoord = particle.YCoord;

                _activeTexture[CoordinateToIndex(xCoord, yCoord)] = particle.Color;
            }

            UpdateMousePos(ParticleSpawner.SpawnRadius, red);
            SetPixels(_activeTexture);
        }

        public void DEBUG_DisplayOccupiedSpace(in Grid.GridPos[] gridPositions)
        {
            var count = gridPositions.Length;

            _blankTexture.CopyTo(_activeTexture, 0);
            
            for (int i = 0; i < count; i++)
            {
                _activeTexture[i] = gridPositions[i].IsOccupied ? red : green;
            }
            
            SetPixels(_activeTexture);
        }

        //============================================================================================================//

        public Color32 GetParticleColor(in Particle.TYPE type)
        {
            return _particleColors[type].GetColor();
        }
        
        //============================================================================================================//

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
                
                _mouseRadiusPositions.Add(i, 
                    coordinates
                    .Distinct()
                    .ToArray());
            }

        }

        private void UpdateMousePos(in int radius, in Color32 color)
        {
            //int CoordinateToIndex(in int x, in int y) => (sizeX * y) + x;
            var mouseX = ParticleSpawner.MouseCoordinate.x;
            var mouseY = ParticleSpawner.MouseCoordinate.y;

            if (radius == 0)
            {
                var index = (_sizeX * mouseY) + mouseX;
                _activeTexture[index] = color;
                return;
            }

            var coordinates = _mouseRadiusPositions[radius];

            for (int i = 0; i < coordinates.Length; i++)
            {
                var coord = coordinates[i];
                var newX = coord.x + mouseX;
                var newY = coord.y + mouseY;
                
                if (newX >= _sizeX || newX < 0)
                    continue;
                if (newY>= _sizeY || newY < 0)
                    continue;
                var index = (_sizeX * newY) + newX;
                _activeTexture[index] = color;
            }
        }

        private void SetPixels(in Color32[] colors)
        {
            //Set the new change
            _testTexture.SetPixels32(colors);
            //Push to the texture
            _testTexture.Apply();
        }

        //============================================================================================================//

    }
}
