using UnityEngine;
using UnityEngine.Experimental.Rendering.Universal;


namespace PowderToy
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
            PixelPerfectCamera.refResolutionX = gridSize.x;
            PixelPerfectCamera.refResolutionY = gridSize.y;
        }
    }
}
