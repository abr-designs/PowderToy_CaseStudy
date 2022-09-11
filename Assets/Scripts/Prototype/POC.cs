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
        private Texture2D _emptyTexture;

        // Start is called before the first frame update
        private void Start()
        {
            _sharedMaterial = targetRenderer.sharedMaterial;
            
            _emptyTexture = _testTexture = new Texture2D(size.x, size.y, TextureFormat.RGBA4444, false);
            
        }

        private void LateUpdate()
        {
            _testTexture.SetPixels(_emptyTexture.GetPixels());
            _testTexture.SetPixel(coordinate.x, coordinate.y, Color.white);
            
            _sharedMaterial.SetTexture(BaseMapPropertyID, _testTexture);
        }
    }
}
