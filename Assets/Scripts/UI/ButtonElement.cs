using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PowderToy.UI
{
    [RequireComponent(typeof(Button))]
    public class ButtonElement : MonoBehaviour
    {
        private Button button;
        private TMP_Text buttonText;

        private Particle.TYPE particleType;

        public void Init(in string displayText, in Particle.TYPE type, Action<Particle.TYPE> buttonPressCallback)
        {
            particleType = type;
            buttonText = gameObject.GetComponentInChildren<TMP_Text>();
            buttonText.text = displayText;

            button = GetComponent<Button>();
            button.onClick.AddListener(() =>
            {
                buttonPressCallback.Invoke(particleType);
            });
        }
    }
}
