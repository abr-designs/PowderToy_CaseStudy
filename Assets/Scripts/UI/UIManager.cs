using System;
using System.Collections;
using System.Collections.Generic;
using PowderToy;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using Grid = PowderToy.Grid;

public class UIManager : MonoBehaviour
{
    //Properties
    //============================================================================================================//
    
    [SerializeField, TitleGroup("Label Fields")]
    private TMP_Text selectedTypeText;
    [SerializeField]
    private TMP_Text particleCountText;
    [SerializeField]
    private TMP_Text frameRateText;

    [SerializeField, Min(0f), TitleGroup("Update Rate")]
    private float tickTime;
    private float _tickTimer;

    private int _maxParticleCount;
    private Grid _particleGrid;

    [SerializeField, ReadOnly]
    private string[] particleNames;

    //Unity Functions
    //============================================================================================================//

    private void OnEnable()
    {
        Grid.OnInit += OnInit;
        ParticleGridMouseInput.OnParticleTypeSelected += OnNewSelectedType;
    }

    // Start is called before the first frame update
    private void Start()
    {
        _particleGrid = FindObjectOfType<Grid>();
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

        UpdateParticleCount();
        UpdateFrameRate(dt);
        _tickTimer = 0f;
    }

    private void OnDisable()
    {
        Grid.OnInit -= OnInit;
        ParticleGridMouseInput.OnParticleTypeSelected -= OnNewSelectedType;
    }

    //============================================================================================================//

    private void OnInit(Vector2Int gridSize)
    {
        _maxParticleCount = gridSize.x * gridSize.y;
    }

    private void OnNewSelectedType(Particle.TYPE type)
    {
        //TODO Might want to store the particle type names somewhere
        selectedTypeText.text = $"Spawn Type: {particleNames[(int)type]}";
    }

    private void UpdateParticleCount()
    {
        //FIXME That allocated some garbage, want to find a better way to do this. Maybe StringBuilder
        particleCountText.text =
            $"Particles: {_particleGrid.ParticleCount:N0}/{_maxParticleCount:N0}";
    }

    private void UpdateFrameRate(in float deltaTime)
    {
        var fps = Mathf.FloorToInt(1f / deltaTime);

        frameRateText.text = $"{fps.ToString()}fps";
    }

    //============================================================================================================//
#if  UNITY_EDITOR

    private void OnValidate()
    {
        particleNames = Enum.GetNames(typeof(Particle.TYPE));
    }

#endif
}
