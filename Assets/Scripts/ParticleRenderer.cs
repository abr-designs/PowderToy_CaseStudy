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

        //Init Function
        //============================================================================================================//

        public void Init(in Vector2Int size)
        {
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
                var coordinate = particle.Coordinate;
                _activeTexture[CoordinateToIndex(coordinate.x, coordinate.y)] = _particleColors[particle.Type];
            }

            SetPixels(_activeTexture);
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
