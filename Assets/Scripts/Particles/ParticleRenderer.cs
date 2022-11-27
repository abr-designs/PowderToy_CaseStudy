using System;
using System.Collections.Generic;
using PowderToy.Utilities;
using Sirenix.OdinInspector;
using UnityEngine;
using Random = UnityEngine.Random;

namespace PowderToy
{
    public class ParticleRenderer : MonoBehaviour
    {
        private static readonly int BaseMapPropertyID = Shader.PropertyToID("_BaseMap");

        //Properties
        //============================================================================================================//
        private static int _sizeX;
        private static int _sizeY;

        [SerializeField, TitleGroup("Render Info")]
        private Renderer targetRenderer;
        private Material _sharedMaterial;
        

        //Texture color array
        //------------------------------------------------//    
        private Texture2D _testTexture;
        private Color32[] _blankTexture;
        private Color32[] _activeTexture;
        
        //Unity Functions
        //============================================================================================================//

        private void OnEnable() => Grid.OnInit += Init;

        private void OnDisable() => Grid.OnInit -= Init;

        //Init Function
        //============================================================================================================//

        private void Init(Vector2Int size)
        {
            //Setup Texture Size
            //------------------------------------------------------------------//

            _sizeX = size.x;
            _sizeY = size.y;
            
            _activeTexture = new Color32[_sizeX * _sizeY];
            _blankTexture = new Color32[_sizeX * _sizeY];
            for (var i = 0; i < _blankTexture.Length; i++)
            {
                _blankTexture[i] = ColorHelper.Black;
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

            UpdateMousePos(ParticleGridMouseInput.SpawnRadius, ColorHelper.Red);
            SetPixels(_activeTexture);
        }

        public void DEBUG_DisplayOccupiedSpace(in Grid.GridPos[] gridPositions)
        {
            var count = gridPositions.Length;

            _blankTexture.CopyTo(_activeTexture, 0);
            
            for (int i = 0; i < count; i++)
            {
                _activeTexture[i] = gridPositions[i].IsOccupied ? ColorHelper.Red : ColorHelper.Green;
            }
            
            SetPixels(_activeTexture);
        }

        //============================================================================================================//

        private void UpdateMousePos(in int radius, in Color32 color)
        {
            //int CoordinateToIndex(in int x, in int y) => (sizeX * y) + x;
            var mouseX = ParticleGridMouseInput.MouseCoordinate.x;
            var mouseY = ParticleGridMouseInput.MouseCoordinate.y;

            if (radius == 0)
            {
                var index = (_sizeX * mouseY) + mouseX;
                _activeTexture[index] = color;
                return;
            }

            var coordinates = RadiusSelection.GetCoordinates(radius);

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
