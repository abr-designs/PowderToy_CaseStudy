using UnityEngine;
using UnityEngine.Experimental.Rendering.Universal;
using Grid = PowderToy.Grid;

namespace PowderToy.Utilities
{
    [RequireComponent(typeof(Camera), typeof(PixelPerfectCamera))]
    public class CameraResizer : MonoBehaviour
    {
        private PixelPerfectCamera PixelPerfectCamera
        {
            get
            {
                if (_pixelPerfectCamera == null)
                    _pixelPerfectCamera = GetComponent<PixelPerfectCamera>();

                return _pixelPerfectCamera;
            }
        }
        private PixelPerfectCamera _pixelPerfectCamera;
        private void OnEnable()
        {
            Grid.OnInit += OnInit;
        }

        private void OnDisable()
        {
            Grid.OnInit -= OnInit;
        }

        private void OnInit(Vector2Int gridSize)
        {
            PixelPerfectCamera.refResolutionX = Mathf.CeilToInt(gridSize.x * 1.5f);
            PixelPerfectCamera.refResolutionY = gridSize.y;
        }
    }
}
