using System;
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace PowderToy
{
    public class Grid : MonoBehaviour
    {
        public static Action<Vector2Int> OnInit;
        
        [SerializeField, ReadOnly]
        private int particleCount;

        private ParticleRenderer _particleRenderer;
        
        [SerializeField, Min(0), DisableInPlayMode]
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

            _particleRenderer = FindObjectOfType<ParticleRenderer>();
            OnInit?.Invoke(size);
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
            _particleRenderer.UpdateTexture(_activeParticles);
        }
        
        
        //============================================================================================================//

        private bool everyOther;
        private void TrySpawnParticle()
        {
            if (_spawnTimer < spawnDelay)
            {
                _spawnTimer++;
                return;
            }

            _spawnTimer = 0;
            
            if(IsSpaceOccupied(generationCoordinate.x, generationCoordinate.y))
                return;
            
            var newParticle = new Particle
            {
                Coordinate = generationCoordinate,
                IsOccupied = true,
                Type = everyOther ? Particle.TYPE.WATER : Particle.TYPE.SAND,
                Index =  _activeParticles.Count
            };
            
            _activeParticles.Add(newParticle);

            _particlePositions[CoordinateToIndex(generationCoordinate)] = newParticle;

            everyOther = !everyOther;
            particleCount++;
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

                bool didUpdate;

                switch (particle.Type)
                {
                    case Particle.TYPE.SAND:
                        didUpdate = UpdateSandParticle(ref particle);
                        break;
                    case Particle.TYPE.WATER:
                        didUpdate = UpdateWaterParticle(ref particle);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                if (didUpdate)
                {
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

        private bool IsSpaceOccupied(in int x, in int y, out Particle occupier)
        {
            occupier = Particle.Empty;
            
            if (x >= size.x || x < 0)
                return true;
            if (y >= size.y || y < 0)
                return true;
            
            var index = CoordinateToIndex(x, y);
            occupier = _particlePositions[index];

            return occupier.IsOccupied;
        }
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

        private bool UpdateSandParticle(ref Particle particle)
        {
            var originalCoordinate = particle.Coordinate;

            //------------------------------------------------------------------//
            
            bool TrySetNewPosition(in Vector2Int offset, ref Particle myParticle)
            {
                var testCoordinate = myParticle.Coordinate + offset;
                
                //IF the space is occupied, leave early
                var spaceOccupied = IsSpaceOccupied(testCoordinate.x, testCoordinate.y, out var occupier);

                if (spaceOccupied && occupier.Type != Particle.TYPE.WATER)
                    return false;
                
                myParticle.Coordinate += offset;
                //Test for water
                //------------------------------------------------------------------//
                if (occupier.Type == Particle.TYPE.WATER)
                {
                    occupier.Coordinate = originalCoordinate;
                    _particlePositions[CoordinateToIndex(originalCoordinate)] = occupier;
                    _activeParticles[occupier.Index] = occupier;
                }
                else
                    _particlePositions[CoordinateToIndex(originalCoordinate)] = Particle.Empty;
                //------------------------------------------------------------------//
                
                _particlePositions[CoordinateToIndex(myParticle.Coordinate)] = myParticle;
                _activeParticles[myParticle.Index] = myParticle;
                return true;
            }
            
            //------------------------------------------------------------------//
            
            if (TrySetNewPosition(new Vector2Int(0, -1), ref particle))
                return true;
            if (TrySetNewPosition(new Vector2Int(-1, -1), ref particle))
                return true;
            if (TrySetNewPosition(new Vector2Int(1, -1), ref particle))
                return true;
            
            return false;

            /*if (IsSpaceOccupied(coordinate.x, coordinate.y - 1, out var occupier) == false)
            {
                particle.Coordinate += Vector2Int.down;
                if (occupier.Type == Particle.TYPE.WATER)
                {
                    occupier.Coordinate = coordinate;
                    _particlePositions[CoordinateToIndex(coordinate)] = occupier;
                }
                else
                    _particlePositions[CoordinateToIndex(coordinate)] = Particle.Empty;
                
                _particlePositions[CoordinateToIndex(particle.Coordinate)] = particle;
                return true;
            }
            if (IsSpaceOccupied(coordinate.x - 1, coordinate.y - 1, out occupier) == false)
            {
                particle.Coordinate += new Vector2Int(-1, -1);
                _particlePositions[CoordinateToIndex(coordinate)] = Particle.Empty;
                _particlePositions[CoordinateToIndex(particle.Coordinate)] = particle;
                return true;
            }
            if (IsSpaceOccupied(coordinate.x + 1, coordinate.y - 1, out occupier) == false)
            {
                particle.Coordinate += new Vector2Int(1, -1);
                _particlePositions[CoordinateToIndex(coordinate)] = Particle.Empty;
                _particlePositions[CoordinateToIndex(particle.Coordinate)] = particle;
                return true;
            }*/
        }
        
        private bool UpdateWaterParticle(ref Particle particle)
        {
            var coordinate = particle.Coordinate;

            //------------------------------------------------------------------//
            
            bool TrySetNewPosition(in Vector2Int offset, ref Particle myParticle)
            {
                var testCoordinate = myParticle.Coordinate + offset;
                
                //IF the space is occupied, leave early
                if (IsSpaceOccupied(testCoordinate.x, testCoordinate.y)) 
                    return false;
                
                myParticle.Coordinate += offset;

                _particlePositions[CoordinateToIndex(coordinate)] = Particle.Empty;
                
                _particlePositions[CoordinateToIndex(myParticle.Coordinate)] = myParticle;
                return true;
            }
            
            //------------------------------------------------------------------//
            
            if (TrySetNewPosition(new Vector2Int(0, -1), ref particle))
                return true;
            if (TrySetNewPosition(new Vector2Int(-1, -1), ref particle))
                return true;
            if (TrySetNewPosition(new Vector2Int(1, -1), ref particle))
                return true;
            if (TrySetNewPosition(new Vector2Int(-1, 0), ref particle))
                return true;
            if (TrySetNewPosition(new Vector2Int(1, 0), ref particle))
                return true;
            
            return false;
        }

        //============================================================================================================//
        
    }

    
}
