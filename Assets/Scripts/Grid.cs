using System;
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace PowderToy
{
    public class Grid : MonoBehaviour
    {
        [SerializeField]
        private ParticleRenderer particleRenderer;
        
        [SerializeField, Min(0)]
        private Vector2Int size;

        [SerializeField, Min(0)]
        private Vector2Int generationCoordinate;
        
        
        [SerializeField, Min(0f)]
        private float tickTime;
        private float _tickTimer;
        
        [SerializeField, Min(0), SuffixLabel("Ticks", true)]
        private int spawnDelay;
        private int _spawnTimer;

        //FIXME This probably needs to be a list of pointers, not the data
        private Particle[] _particlePositions;
        private List<Particle> _activeParticles;

        //Unity Functions
        //============================================================================================================//
        // Start is called before the first frame update
        private void Start()
        {
            _particlePositions = new Particle[size.x * size.y];
            _activeParticles = new List<Particle>();

            particleRenderer.Init(size);
        }

        // Update is called once per frame
        private void Update()
        {
            if (_tickTimer < tickTime)
            {
                _tickTimer += Time.deltaTime;
                return;
            }

            _tickTimer = 0f;
            
            UpdateParticles();
            TrySpawnParticle();
            particleRenderer.UpdateTexture(_activeParticles);
        }
        
        
        //============================================================================================================//

        private void TrySpawnParticle()
        {
            if (_spawnTimer < spawnDelay)
            {
                _spawnTimer++;
                return;
            }

            _spawnTimer = 0;

            var newParticle = new Particle
            {
                Coordinate = generationCoordinate,
                IsOccupied = true
            };
            _activeParticles.Add(newParticle);

            _particlePositions[CoordinateToIndex(generationCoordinate)] = newParticle;
        }

        private void UpdateParticles()
        {
            var count = _activeParticles.Count;
            for (int i = 0; i < count; i++)
            {
                var particle = _activeParticles[i];

                if (particle.IsOccupied == false)
                    throw new Exception();
                
                if(particle.Asleep)
                    continue;

                var coordinate = particle.Coordinate;

                if (IsSpaceOccupied(coordinate.x, coordinate.y - 1) == false)
                {
                    particle.Coordinate += Vector2Int.down;
                    _particlePositions[CoordinateToIndex(coordinate)] = Particle.Empty;
                    _particlePositions[CoordinateToIndex(particle.Coordinate)] = particle;
                    _activeParticles[i] = particle;
                    continue;
                }
                if (IsSpaceOccupied(coordinate.x - 1, coordinate.y - 1) == false)
                {
                    particle.Coordinate += new Vector2Int(-1, -1);
                    _particlePositions[CoordinateToIndex(coordinate)] = Particle.Empty;
                    _particlePositions[CoordinateToIndex(particle.Coordinate)] = particle;
                    _activeParticles[i] = particle;
                    continue;
                }
                if (IsSpaceOccupied(coordinate.x + 1, coordinate.y - 1) == false)
                {
                    particle.Coordinate += new Vector2Int(1, -1);
                    _particlePositions[CoordinateToIndex(coordinate)] = Particle.Empty;
                    _particlePositions[CoordinateToIndex(particle.Coordinate)] = particle;
                    _activeParticles[i] = particle;
                    continue;
                }

                if(particle.SleepCounter++ >= Particle.WAIT_TO_SLEEP)
                    particle.Asleep = true;
                
                _activeParticles[i] = particle;
            }
        }
        
        //Grid Calculations
        //============================================================================================================//

        private bool IsSpaceOccupied(in int x, in int y)
        {
            if (x >= size.x || x < 0)
                return true;
            if (y >= size.y || y < 0)
                return true;
            
            var index = CoordinateToIndex(x, y);

            return _particlePositions[index].IsOccupied;
        }
        
        private int CoordinateToIndex(in Vector2Int c) => CoordinateToIndex(c.x, c.y);
        private int CoordinateToIndex(in int x, in int y) => (size.x * y) + x;
        
        //============================================================================================================//

    }

    public struct Particle
    {
        public const int WAIT_TO_SLEEP = 3;
        public static readonly Particle Empty = new Particle();

        public int SleepCounter;
        public bool IsOccupied;
        public bool Asleep;
        public Vector2Int Coordinate;
    }
}
