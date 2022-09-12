using System;
using UnityEngine;

namespace PowderToy
{
    public class WorldTimer : MonoBehaviour
    {
        public static Action OnTick;
        
        [SerializeField, Min(0f)]
        private float tickTime;
        private float _tickTimer;

        // Update is called once per frame
        private void Update()
        {
            if (_tickTimer < tickTime)
            {
                _tickTimer += Time.deltaTime;
                return;
            }

            _tickTimer = 0f;
            OnTick?.Invoke();
        }
    }
}
