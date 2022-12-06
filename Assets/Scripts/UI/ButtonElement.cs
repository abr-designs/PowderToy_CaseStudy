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

        private Enum particleType;

        public void Init<E>(in string displayText, in E type, Action<E> buttonPressCallback) where E: Enum
        {
            particleType = type;
            buttonText = gameObject.GetComponentInChildren<TMP_Text>();
            buttonText.text = displayText;

            button = GetComponent<Button>();
            button.onClick.AddListener(() =>
            {
                buttonPressCallback.Invoke((E)particleType);
            });
        }
    }
}
