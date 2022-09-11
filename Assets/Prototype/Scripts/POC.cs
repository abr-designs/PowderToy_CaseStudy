using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PowderToy.Prototype
{
    public class POC : MonoBehaviour
    {
        private static readonly int BaseMapPropertyID = Shader.PropertyToID("_BaseMap");
        
        [SerializeField, Min(0)]
        private Vector2Int size;
        [SerializeField]
        private Renderer targetRenderer;
        private Material _sharedMaterial;

        [SerializeField]
        private Color setColor = Color.black;
        
        [SerializeField]
        private Vector2Int coordinate;
        
        private Texture2D _testTexture;
        //private Texture2D _emptyTexture;
        private Color[] _blankTexture;

        // Start is called before the first frame update
        private void Start()
        {
            _blankTexture = new Color[size.x * size.y];
            for (int i = 0; i < _blankTexture.Length; i++)
            {
                _blankTexture[i] = Color.black;
            }
            
            _sharedMaterial = targetRenderer.sharedMaterial;
            
            _testTexture = new Texture2D(size.x, size.y, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            _sharedMaterial.SetTexture(BaseMapPropertyID, _testTexture);
        }

        private void Update()
        {
            //TODO This needs to only happen if something changes
            //Clear the buffer
            _testTexture.SetPixels(_blankTexture);
            //Set the new change
            _testTexture.SetPixel(coordinate.x, coordinate.y, setColor, 0);
            //Push to the texture
            _testTexture.Apply();
        }
    }
}
