using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;

namespace PowderToy
{
    public class Grid : MonoBehaviour
    {
        //Structs
        //============================================================================================================//
        
        public struct GridPos
        {
            public static readonly GridPos Empty = new GridPos
            {
                IsOccupied = false,
                particleIndex = -1
            };
            
            public bool IsOccupied;
            public int particleIndex;
        }

        //============================================================================================================//        
        
        public static Action<Vector2Int> OnInit;
        
        [SerializeField, ReadOnly]
        private int particleCount;

        private ParticleRenderer _particleRenderer;
        
        [SerializeField, Min(0), DisableInPlayMode]
        private Vector2Int size;

        //FIXME This probably needs to be a list of pointers, not the data
        private GridPos[] _gridPositions;
        private List<Particle> _activeParticles;

        //Unity Functions
        //============================================================================================================//

        private void OnEnable()
        {
            WorldTimer.OnTick += OnTick;
        }

        // Start is called before the first frame update
        private void Start()
        {
            _gridPositions = new GridPos[size.x * size.y];
            
            _activeParticles = new List<Particle>();

            _particleRenderer = FindObjectOfType<ParticleRenderer>();
            OnInit?.Invoke(size);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.R) == false)
                return;
            
            ClearGrid();
        }

        private void OnDisable()
        {
            WorldTimer.OnTick -= OnTick;
        }

        //============================================================================================================//
        
        private void OnTick()
        {
            UpdateParticles();

            _particleRenderer.UpdateTexture(_activeParticles);
        }

        public void SpawnParticle(in Particle.TYPE type, in Vector2Int coordinate)
        {
            if(IsSpaceOccupied(coordinate.x, coordinate.y))
                return;
            
            var newParticle = new Particle
            {
                Coordinate = coordinate,
                Type = type,
                Index =  _activeParticles.Count
            };
            
            _activeParticles.Add(newParticle);

            _gridPositions[CoordinateToIndex(coordinate)] = new GridPos
            {
                IsOccupied = true,
                particleIndex = newParticle.Index
            };

            particleCount++;
        }

        private void UpdateParticles()
        {
            var count = _activeParticles.Count;
            //_activeParticles = _activeParticles.OrderBy(p => p.Coordinate.y).ToList();
            for (int i = 0; i < count; i++)
            {
                var particle = _activeParticles[i];

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

                /*if(particle.SleepCounter++ >= Particle.WAIT_TO_SLEEP)
                    particle.Asleep = true;
                
                _activeParticles[i] = particle;*/
            }
        }

        private void ClearGrid()
        {
            _activeParticles.Clear();
            var count = _gridPositions.Length;

            for (int i = 0; i < count; i++)
            {
                _gridPositions[i] = GridPos.Empty;
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
            
            var gridIndex = CoordinateToIndex(x, y);
            var particleIndex = _gridPositions[gridIndex].particleIndex;

            if (_gridPositions[gridIndex].IsOccupied == false)
                return false;
            
            occupier = _activeParticles[particleIndex];

            return true;
        }
        private bool IsSpaceOccupied(in int x, in int y)
        {
            if (x >= size.x || x < 0)
                return true;
            if (y >= size.y || y < 0)
                return true;
            
            var index = CoordinateToIndex(x, y);
            return _gridPositions[index].IsOccupied;
        }
        
        private int CoordinateToIndex(in Vector2Int c) => CoordinateToIndex(c.x, c.y);
        private int CoordinateToIndex(in int x, in int y) => (size.x * y) + x;
        
        //============================================================================================================//

        private bool UpdateSandParticle(ref Particle particle)
        {
            var originalCoordinate = particle.Coordinate;

            if(particle.Asleep)
                return false;

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
                    _gridPositions[CoordinateToIndex(originalCoordinate)] = new GridPos
                    {
                        IsOccupied = true,
                        particleIndex = occupier.Index
                    };
                    _activeParticles[occupier.Index] = occupier;
                }
                else
                    _gridPositions[CoordinateToIndex(originalCoordinate)] = GridPos.Empty;
                //------------------------------------------------------------------//


                _gridPositions[CoordinateToIndex(myParticle.Coordinate)] = new GridPos
                {
                    IsOccupied = true,
                    particleIndex = myParticle.Index
                };
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

                _gridPositions[CoordinateToIndex(coordinate)] = GridPos.Empty;
                
                _gridPositions[CoordinateToIndex(myParticle.Coordinate)] = new GridPos
                {
                    IsOccupied = true,
                    particleIndex = myParticle.Index
                };
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
