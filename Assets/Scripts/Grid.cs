using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Sirenix.OdinInspector;
using UnityEngine;

namespace PowderToy
{
    public class Grid : MonoBehaviour
    {
        //Structs
        //============================================================================================================//
        
        /// <summary>
        /// Simple struct that contains a bool for current grid position occupancy, and a pointer to the Active Particle
        /// index
        /// </summary>
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

        /// <summary>
        /// Container for particles 0 -> GridSize.x on a single row. Used to effeciently navigate grid of sorted particles
        /// by Y position
        /// </summary>
        private class ParticleColContainer
        {
            public readonly uint[] ParticleIndices;
            public uint ParticleCount;

            public ParticleColContainer(in uint rowSize)
            {
                ParticleIndices = new uint[rowSize];
                ParticleCount = 0;
            }

            public void AddParticle(in int index) => AddParticle((uint)index);
            public void AddParticle(in uint index)
            {
                ParticleIndices[ParticleCount++] = index;
            }

            public void Clear()
            {
                //If we just set the count back to 0 we can avoid having to clear the entire array
                ParticleCount = 0;
            }
        }

        //Properties
        //============================================================================================================//        
        
        public static Action<Vector2Int> OnInit;
        
        [SerializeField, ReadOnly, TitleGroup("Debugging")]
        private int particleCount;
        
        [SerializeField, Min(0), DisableInPlayMode, TitleGroup("Grid Properties")]
        private Vector2Int size;
        //We save the size as static ints to reduce time it takes to call get from Vector2Int
        private static int _sizeX;
        private static int _sizeY;

        private ParticleRenderer _particleRenderer;

        //Collection of Grid Elements
        //------------------------------------------------//
        private Particle[] _activeParticles;
        private ParticleColContainer[] _particleRowContainers;
        private GridPos[] _gridPositions;

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
            _activeParticles = new Particle[size.x * size.y];

            _sizeX = size.x;
            _sizeY = size.y;

            //Setup column Containers
            //------------------------------------------------//
            _particleRowContainers = new ParticleColContainer[_sizeY];
            var sizeX = (uint)_sizeX;
            for (var i = 0; i < _sizeY; i++)
            {
                _particleRowContainers[i] = new ParticleColContainer(sizeX);
            }

            //------------------------------------------------//

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
            UpdateParticles2();
            UpdateParticleRows();

            _particleRenderer.UpdateTexture(_activeParticles, particleCount);
        }

        
        public void SpawnParticle(in Particle.TYPE type, in Vector2Int coordinate)
        {
            var newX = coordinate.x;
            var newY = coordinate.y;
            
            if(IsSpaceOccupied(newX, newY))
                return;
            
            var newIndex = particleCount++;
            var newColor = _particleRenderer.GetParticleColor(type);
            var newParticle = new Particle(type, newColor, newIndex, newX, newY);

            _activeParticles[newIndex] = newParticle;

            _gridPositions[CoordinateToIndex(newX, newY)] = new GridPos
            {
                IsOccupied = true,
                particleIndex = newParticle.Index
            };

            switch (newParticle.Material)
            {
                //Don't want to track things that will never move
                case Particle.MATERIAL.SOLID:
                    break;
                case Particle.MATERIAL.POWDER:
                case Particle.MATERIAL.LIQUID:
                case Particle.MATERIAL.GAS:
                    _particleRowContainers[newY].AddParticle(newIndex);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
        private void UpdateParticles2()
        {
            for (var i = 0; i < _sizeY; i++)
            {
                var container = _particleRowContainers[i].ParticleIndices;
                var count = _particleRowContainers[i].ParticleCount;
                for (var ii = 0; ii < count; ii++)
                {
                    var particle = _activeParticles[(int)container[ii]];

                    if(particle.Asleep)
                        continue;

                    bool didUpdate;

                    switch (particle.Material)
                    {
                        case Particle.MATERIAL.SOLID:
                            continue;
                        case Particle.MATERIAL.POWDER:
                            didUpdate = UpdatePowderParticle(ref particle);
                            break;
                        case Particle.MATERIAL.LIQUID:
                            didUpdate = UpdateLiquidParticle(ref particle);
                            break;
                        case Particle.MATERIAL.GAS:
                            didUpdate = UpdateGasParticle(ref particle);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    if (didUpdate)
                    {
                        _activeParticles[particle.Index] = particle;
                        continue;
                    }
                    
                    
                    if(particle.SleepCounter++ >= Particle.WAIT_TO_SLEEP)
                        particle.Asleep = true;
                }
                
                _particleRowContainers[i].Clear();
            }
        }

        private void UpdateParticleRows()
        {
            for (int i = 0; i < particleCount; i++)
            {
                var particle = _activeParticles[i];
                
                if(particle.Material == Particle.MATERIAL.SOLID)
                    continue;
                if(particle.Asleep)
                    continue;
                
                _particleRowContainers[particle.YCoord].AddParticle(particle.Index);
            }
        }

        private void ClearGrid()
        {
            particleCount = 0;
            var count = _gridPositions.Length;

            for (int i = 0; i < count; i++)
            {
                _gridPositions[i] = GridPos.Empty;
            }

            count = _particleRowContainers.Length;
            for (int i = 0; i < count; i++)
            {
                _particleRowContainers[i].Clear();
            }
        }
        
        //Grid Calculations
        //============================================================================================================//
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsSpaceOccupied(in int x, in int y, out Particle occupier)
        {
            occupier = Particle.Empty;
            
            if (x >= _sizeX || x < 0)
                return true;
            if (y >= _sizeY || y < 0)
                return true;
            
            var gridIndex = CoordinateToIndex(x, y);

            if (_gridPositions[gridIndex].IsOccupied == false)
                return false;

            var particleIndex = _gridPositions[gridIndex].particleIndex;
            
            occupier = _activeParticles[particleIndex];

            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsSpaceOccupied(in int x, in int y)
        {
            if (x >= _sizeX || x < 0)
                return true;
            if (y >= _sizeY || y < 0)
                return true;
            
            var index = CoordinateToIndex(x, y);
            return _gridPositions[index].IsOccupied;
        }
        
        //private static int CoordinateToIndex(in Vector2Int c) => CoordinateToIndex(c.x, c.y);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CoordinateToIndex(in int x, in int y) => (_sizeX * y) + x;
        
        //============================================================================================================//

        private bool UpdatePowderParticle(ref Particle particle)
        {
            //var originalCoordinate = particle.Coordinate;
            var originalX = particle.XCoord;
            var originalY = particle.YCoord;

            if(particle.Asleep)
                return false;

            //------------------------------------------------------------------//
            
            bool TrySetNewPosition(in int xOffset, in int yOffset, ref Particle myParticle)
            {
                //var testCoordinate = myParticle.Coordinate + offset;
                var newX = originalX + xOffset;
                var newY = originalY + yOffset;
                
                //IF the space is occupied, leave early
                var spaceOccupied = IsSpaceOccupied(newX, newY, out var occupier);

                if (spaceOccupied && occupier.Type != Particle.TYPE.WATER)
                    return false;

                myParticle.XCoord = newX;
                myParticle.YCoord = newY;
                
                //Test for water
                //------------------------------------------------------------------//
                if (occupier.Type == Particle.TYPE.WATER)
                {
                    //occupier.Coordinate = originalCoordinate;
                    occupier.XCoord = originalX;
                    occupier.YCoord = originalY;
                    _gridPositions[CoordinateToIndex(originalX, originalY)] = new GridPos
                    {
                        IsOccupied = true,
                        particleIndex = occupier.Index
                    };
                    _activeParticles[occupier.Index] = occupier;
                }
                else
                    _gridPositions[CoordinateToIndex(originalX, originalY)] = GridPos.Empty;
                //------------------------------------------------------------------//


                _gridPositions[CoordinateToIndex(newX, newY)] = new GridPos
                {
                    IsOccupied = true,
                    particleIndex = myParticle.Index
                };
                _activeParticles[myParticle.Index] = myParticle;
                return true;
            }
            
            //------------------------------------------------------------------//
            
            if (TrySetNewPosition(0 ,-1, ref particle))
                return true;
            if (TrySetNewPosition(-1, -1, ref particle))
                return true;
            if (TrySetNewPosition(1, -1, ref particle))
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
        
        private bool UpdateLiquidParticle(ref Particle particle)
        {
            //var coordinate = particle.Coordinate;
            var originalX = particle.XCoord;
            var originalY = particle.YCoord;

            //------------------------------------------------------------------//
            
            bool TrySetNewPosition(in int xOffset, in int yOffset, ref Particle myParticle)
            {
                //var testCoordinate = myParticle.Coordinate + offset;
                var newX = originalX + xOffset;
                var newY = originalY + yOffset;
                //IF the space is occupied, leave early
                if (IsSpaceOccupied(newX, newY)) 
                    return false;
                
                //myParticle.Coordinate += offset;
                myParticle.XCoord = newX;
                myParticle.YCoord = newY;

                _gridPositions[CoordinateToIndex(originalX, originalY)] = GridPos.Empty;
                
                _gridPositions[CoordinateToIndex(newX, newY)] = new GridPos
                {
                    IsOccupied = true,
                    particleIndex = myParticle.Index
                };
                return true;
            }
            
            //------------------------------------------------------------------//
            
            if (TrySetNewPosition(0, -1, ref particle))
                return true;
            if (TrySetNewPosition(-1, -1, ref particle))
                return true;
            if (TrySetNewPosition(1, -1, ref particle))
                return true;
            if (TrySetNewPosition(-1, 0, ref particle))
                return true;
            if (TrySetNewPosition(1, 0, ref particle))
                return true;
            
            return false;
        }
        
        private bool UpdateGasParticle(ref Particle particle)
        {
            //var coordinate = particle.Coordinate;
            var originalX = particle.XCoord;
            var originalY = particle.YCoord;

            //------------------------------------------------------------------//
            
            bool TrySetNewPosition(in int xOffset, in int yOffset, ref Particle myParticle)
            {
                //var testCoordinate = myParticle.Coordinate + offset;
                var newX = originalX + xOffset;
                var newY = originalY + yOffset;
                //IF the space is occupied, leave early
                if (IsSpaceOccupied(newX, newY)) 
                    return false;
                
                //myParticle.Coordinate += offset;
                myParticle.XCoord = newX;
                myParticle.YCoord = newY;

                _gridPositions[CoordinateToIndex(originalX, originalY)] = GridPos.Empty;
                
                _gridPositions[CoordinateToIndex(newX, newY)] = new GridPos
                {
                    IsOccupied = true,
                    particleIndex = myParticle.Index
                };
                return true;
            }
            
            //------------------------------------------------------------------//
            
            if (TrySetNewPosition(0, 1, ref particle))
                return true;
            if (TrySetNewPosition(-1, 1, ref particle))
                return true;
            if (TrySetNewPosition(1, 1, ref particle))
                return true;
            if (TrySetNewPosition(-1, 0, ref particle))
                return true;
            if (TrySetNewPosition(1, 0, ref particle))
                return true;
            
            return false;
        }

        //============================================================================================================//
        
    }

    
}
