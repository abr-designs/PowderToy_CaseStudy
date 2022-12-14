using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PowderToy;
using PowderToy.ScriptableObjects;
using PowderToy.UI;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using Grid = PowderToy.Grid;

namespace PowderToy.UI
{
    public class UIManager : MonoBehaviour
    {
        public static Action<Particle.TYPE> OnParticleTypeSelected;
        public static Action<ParticleRenderer.DISPLAY> OnDisplayTypeSelected;

        //Properties
        //============================================================================================================//

        [SerializeField, TitleGroup("Label Fields")]
        private TMP_Text selectedTypeText;

        [SerializeField] private TMP_Text particleCountText;
        [SerializeField] private TMP_Text frameRateText;

        [SerializeField, Min(0f), TitleGroup("Update Rate")]
        private float tickTime;

        private float _tickTimer;

        private int _maxParticleCount;
        private Grid _particleGrid;

        [SerializeField, ReadOnly] private string[] particleNames;

        [SerializeField, TitleGroup("Particle Button List")]
        private ButtonElement buttonPrefab;
        private ButtonElement[] _activeParticleButtons;

        [SerializeField]
        private RectTransform particleButtonContainer;
        [SerializeField]
        private ParticleDataScriptableObject particleDataScriptableObject;
        [SerializeField]
        private Particle.TYPE startSelection;
        
        [SerializeField, TitleGroup("Display Button List")]
        private RectTransform displayButtonContainer;
        private ButtonElement[] _activeDisplayButtons;



        [SerializeField, TitleGroup("Debug Info")]
        private TMP_Text debugText;
        private StringBuilder sb;


        //Unity Functions
        //============================================================================================================//

        private void OnEnable()
        {
            Grid.OnInit += OnInit;
            //ParticleGridMouseInput.OnParticleTypeSelected += OnParticleButtonPressed;
        }

        // Start is called before the first frame update
        private void Start()
        {
            _particleGrid = FindObjectOfType<Grid>();
            SetupParticleButtons();
            SetupDisplayButtons();
        }

        // Update is called once per frame
        private void Update()
        {
            var dt = Time.deltaTime;
            if (_tickTimer < tickTime)
            {
                _tickTimer += dt;
                return;
            }

            UpdateDebugInfo();
            UpdateParticleCount();
            UpdateFrameRate(dt);
            _tickTimer = 0f;
        }

        private void OnDisable()
        {
            Grid.OnInit -= OnInit;
            //ParticleGridMouseInput.OnParticleTypeSelected -= OnParticleButtonPressed;
        }

        //============================================================================================================//

        private void OnInit(Vector2Int gridSize)
        {
            _maxParticleCount = gridSize.x * gridSize.y;
            OnParticleButtonPressed(startSelection);
        }

        private void OnParticleButtonPressed(Particle.TYPE type)
        {
            //TODO Might want to store the particle type names somewhere
            selectedTypeText.text = $"Spawn Type: {particleNames[(int)type]}";
            
            OnParticleTypeSelected?.Invoke(type);
        }
        private void OnDisplayButtonPressed(ParticleRenderer.DISPLAY type)
        {
            OnDisplayTypeSelected?.Invoke(type);
        }

        private void UpdateParticleCount()
        {
            //FIXME That allocated some garbage, want to find a better way to do this. Maybe StringBuilder
            particleCountText.text = $"Particles: {_particleGrid.ParticleCount:N0}/{_maxParticleCount:N0}";
        }

        private void UpdateFrameRate(in float deltaTime)
        {
            var fps = Mathf.FloorToInt(1f / deltaTime);

            frameRateText.text = $"{fps.ToString()}fps";
        }

        private void UpdateDebugInfo()
        {
            if (sb == null)
                sb = new StringBuilder();

            sb.Clear();
            var coordinate = ParticleGridMouseInput.MouseCoordinate;
            
            var data = _particleGrid.GetParticleAtCoordinate(coordinate.x, coordinate.y);
            //debugText

            sb.AppendLine($"GridIndex: [{data.gridIndex.ToString()}]");
            sb.AppendLine($"Occupied: {data.gridPos.IsOccupied.ToString()}");
            sb.AppendLine();
            if (data.particle == null)
            {
                sb.AppendLine("Particle: null");
                debugText.text = sb.ToString();
                return;
            }
            sb.AppendLine("Particle:");
            sb.AppendLine($"Type: {particleNames[(int)data.particle.Type]}");
            sb.AppendLine($"Material: {data.particle.Material}");
            sb.AppendLine($"Lifetime: {data.particle.Lifetime.ToString()}");
            sb.AppendLine($"Temp: {data.particle.CurrentTemperature.ToString()}");
            

            //hasWarmed = data.particle.HasChangedTemp;
            debugText.text = sb.ToString();
        }

        //Buttons Setup
        //============================================================================================================//

        private void SetupParticleButtons()
        {
            var particleDatas = particleDataScriptableObject.GetParticleDataDictionary().Values.ToArray();
            _activeParticleButtons = new ButtonElement[particleDatas.Length];
            
            for (var i = 0; i < particleDatas.Length; i++)
            {
                var particleData = particleDatas[i];
                var particleType = particleData.type;
                
                
                var buttonElement = Instantiate(buttonPrefab, particleButtonContainer, false);
                buttonElement.Init(particleData.name, particleType, OnParticleButtonPressed);
                buttonElement.gameObject.name = $"{particleData.name}Button";
                _activeParticleButtons[i] = buttonElement;
            }
        }
        
        private void SetupDisplayButtons()
        {
            var displayTypes = (ParticleRenderer.DISPLAY[])Enum.GetValues(typeof(ParticleRenderer.DISPLAY));
            var displayTypeNames = Enum.GetNames(typeof(ParticleRenderer.DISPLAY));
            
            _activeDisplayButtons = new ButtonElement[displayTypes.Length];

            for (var i = 0; i < displayTypes.Length; i++)
            {
                var type = displayTypes[i];
                var displayTypeName = displayTypeNames[i];
                
                var buttonElement = Instantiate(buttonPrefab, displayButtonContainer, false);
                buttonElement.Init(displayTypeName, type, OnDisplayButtonPressed);
                buttonElement.gameObject.name = $"{displayTypeName}Button";
                _activeDisplayButtons[i] = buttonElement;
            }
        }

        //============================================================================================================//
#if UNITY_EDITOR

        private void OnValidate()
        {
            
            particleNames = ParticleFactory.GetParticleNames();
        }

#endif
    }
}
