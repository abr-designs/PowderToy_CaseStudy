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

            public void RemoveParticle(in uint indexToRemove)
            {
                if (ParticleCount == 0)
                    return;
                
                var shiftCount = 0;
                var i = 0;
                try
                {
                    for (i = 0; i < ParticleCount; i++)
                    {
                        var hasFoundItem = ParticleIndices[i] != indexToRemove;
                        if (shiftCount > 0 && hasFoundItem == false)
                        {
                            ParticleIndices[i - shiftCount] = ParticleIndices[i];
                        }
                        if(hasFoundItem == false)
                            continue;

                        shiftCount++;
                    }

                    if (shiftCount == 0 && ParticleCount > 1)
                        throw new Exception("Expect to kill Particle from Array");

                    ParticleCount--;
                }
                catch (IndexOutOfRangeException e)
                {
                    Debug.LogError($"(i){i} - (shift){shiftCount} == {i - shiftCount}\n[{string.Join(", ", ParticleIndices)}]");
                    throw e;
                }
            }

            public void RemoveParticles(in HashSet<uint> indices)
            {
                var originalCount = indices.Count;
                
                var shiftCount = 0;
                for (int i = 0; i < ParticleCount; i++)
                {
                    var index = ParticleIndices[i];
                    var hasFoundItem = indices.Contains(index);
                    if (shiftCount > 0 && hasFoundItem == false)
                    {
                        ParticleIndices[i - shiftCount] = index;
                    }
                    if(hasFoundItem == false)
                        continue;

                    shiftCount++;
                    indices.Remove(index);
                }

                ParticleCount -= (uint)originalCount;
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

        //TODO This might need to be a list at some point
        public static Command QueuedCommand;

        public int ParticleCount => _particleCount;
        
        [SerializeField, ReadOnly, TitleGroup("Debug Info")]
        private int _particleCount;

        [SerializeField, TitleGroup("Debug Info")]
        private bool DebugView;

        [SerializeField, TitleGroup("Particle Properties")]
        private bool allowSleeping = true;
        
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
            var count = size.x * size.y;
            _gridPositions = new GridPos[count];
            _activeParticles = new Particle[count];

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
            ExecuteQueuedCommand();
            UpdateParticles2();
            UpdateParticleRows();

            if(DebugView == false)
                _particleRenderer.UpdateTexture(_activeParticles, _particleCount);
            else
                _particleRenderer.DEBUG_DisplayOccupiedSpace(_gridPositions);
        }

        //============================================================================================================//

        public static void QueueNewCommand(in Command command) => QueuedCommand = command;
        
        private void ExecuteQueuedCommand()
        {

            switch (QueuedCommand.Type)
            {
                //------------------------------------------------//
                case Command.TYPE.NONE:
                    return;
                //Remove Particles
                //------------------------------------------------//
                case Command.TYPE.SPAWN_PARTICLE when QueuedCommand.TypeToSpawn == Particle.TYPE.NONE && QueuedCommand.SpawnRadius == 0:
                    RemoveParticle(QueuedCommand.mouseCoordinate);
                    break;
                case Command.TYPE.SPAWN_PARTICLE when QueuedCommand.TypeToSpawn == Particle.TYPE.NONE && QueuedCommand.SpawnRadius > 0:
                    var toRemove = TryRemoveParticlesInRadius(QueuedCommand.mouseCoordinate, (int)QueuedCommand.SpawnRadius);
                    RemoveParticles(toRemove);
                    break;
                //Spawn Particles
                //------------------------------------------------//
                case Command.TYPE.SPAWN_PARTICLE when QueuedCommand.SpawnRadius == 0:
                    SpawnParticle(QueuedCommand.TypeToSpawn, QueuedCommand.mouseCoordinate);
                    break;
                case Command.TYPE.SPAWN_PARTICLE when QueuedCommand.SpawnRadius > 0:
                    TrySpawnNewParticlesInRadius(QueuedCommand.mouseCoordinate, 
                        QueuedCommand.TypeToSpawn,
                        (int)QueuedCommand.SpawnRadius);
                    break;
                //------------------------------------------------//
                default:
                    throw new ArgumentOutOfRangeException();
            }
            
            QueuedCommand = Command.Empty;
        }
        
        private void TrySpawnNewParticlesInRadius(in Vector2Int coord, in Particle.TYPE particleType, in int radius)
        {
            int x, y, px, nx, py, ny, d;
            
            var rSqr = radius * radius;

            for (x = 0; x <= radius; x++)
            {
                d = (int)Mathf.Ceil(Mathf.Sqrt(rSqr - x * x));
                for (y = 0; y <= d; y++)
                {
                    px = coord.x + x;
                    nx = coord.x - x;
                    
                    py = coord.y + y;
                    ny = coord.y - y;

                    SpawnParticle(particleType, new Vector2Int(px, py));
                    SpawnParticle(particleType, new Vector2Int(nx, py));
                    SpawnParticle(particleType, new Vector2Int(px, ny));
                    SpawnParticle(particleType, new Vector2Int(nx, ny));
                }
            }

            //_mouseDown = false;
        }
        
        //FIXME I should be saving this radius size array elsewhere
        private Vector2Int[] TryRemoveParticlesInRadius(in Vector2Int coord, in int radius)
        {
            int x, y, px, nx, py, ny, d;
            
            var rSqr = radius * radius;
            var coordinates = new List<Vector2Int>();

            for (x = 0; x <= radius; x++)
            {
                d = (int)Mathf.Ceil(Mathf.Sqrt(rSqr - x * x));
                for (y = 0; y <= d; y++)
                {
                    px = coord.x + x;
                    nx = coord.x - x;
                    
                    py = coord.y + y;
                    ny = coord.y - y;

                    coordinates.Add(new Vector2Int(px, py));
                    coordinates.Add(new Vector2Int(nx, py));
                    coordinates.Add(new Vector2Int(px, ny));
                    coordinates.Add(new Vector2Int(nx, ny));
                }
            }

            //_mouseDown = false;
            return coordinates.Distinct().ToArray();
        }

        //============================================================================================================//

        private void SpawnParticle(in Particle.TYPE type, in Vector2Int coordinate)
        {
            var newX = coordinate.x;
            var newY = coordinate.y;
            
            if(IsSpaceOccupied(newX, newY))
                return;
            
            var newIndex = _particleCount++;
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

        private bool RemoveParticle(in Vector2Int coordinate, in bool updateActiveParticles = true)
        {
            var delX = coordinate.x;
            var delY = coordinate.y;

            if (IsSpaceOccupied(delX, delY, out Particle toDelete) == false || toDelete.Type == Particle.TYPE.NONE)
                return false;
            
            _activeParticles[toDelete.Index] = Particle.Empty;
            _gridPositions[CoordinateToIndex(delX, delY)] = GridPos.Empty;
            //FIXME Need a nice way of call this from a group, current issue is potential allocation size
            _particleRowContainers[delY].RemoveParticle((uint)toDelete.Index);

            if (updateActiveParticles)
            {
                _particleCount--;
                CompressActiveParticles();
                //UpdateParticleRows();
            }
            
            return true;
        }
        
        //TODO Add RemoveParticles() collection removal
        private void RemoveParticles(in Vector2Int[] coordinates)
        {
            var toDeleteCount = coordinates.Length;
            var confirmedDeletedCount = 0;
            for (int i = 0; i < toDeleteCount; i++)
            {
                if (RemoveParticle(coordinates[i], false) == false)
                    continue;
                
                confirmedDeletedCount++;
            }
            
            _particleCount -= confirmedDeletedCount;
            if (_particleCount < 0)
            {
                throw new Exception("PARTICLES FELL BELOW 0");
            }
            
            CompressActiveParticles();
            //_particleRowContainers[delY].RemoveParticles((uint)toDelete.Index);
            //UpdateParticleRows();
        }

        /// <summary>
        /// WARNING: Its very important that the particle count is adjusted before calling this function
        /// </summary>
        private void CompressActiveParticles()
        {
            //FIXME This is the brute force version of this, nonononononono
            //------------------------------------------------//

            var liveParticles = _activeParticles
                .Where(x => x.Type != Particle.TYPE.NONE)
                .ToArray();

            for (int i = 0; i < _activeParticles.Length; i++)
            {
                _activeParticles[i] = Particle.Empty;
            }
            for (var i = 0; i < _gridPositions.Length; i++)
            {
                _gridPositions[i] = GridPos.Empty;
            }

            for (int i = 0; i < liveParticles.Length; i++)
            {
                _activeParticles[i] = liveParticles[i];
                _gridPositions[i] = new GridPos
                {
                    particleIndex = i,
                    IsOccupied = true
                };
            }

            return;
            //------------------------------------------------//
            
            //TODO Navigate list in one direction
            //TODO Check for empty positions, if empty add to count
            //TODO If found non-empty, after empty, shift by count
            //TODO Change index of the particle
            //TODO Count up until having reached the particle count

            ushort particleFoundCount = 0;
            ushort emptyPositionCount = 0;
            var count = _activeParticles.Length;
            for (var i = 0; i < count && particleFoundCount < _particleCount; i++)
            {
                var particle = _activeParticles[i];
                var originalIndex = particle.Index;
                
                if (particle.Type == Particle.TYPE.NONE)
                {
                    emptyPositionCount++;
                    continue;
                }

                particleFoundCount++;
                
                if (emptyPositionCount == 0)
                    continue;

                var newIndex = originalIndex - emptyPositionCount;
                particle.Index = newIndex;
                
                var gridIndex = CoordinateToIndex(particle.XCoord, particle.YCoord);
                _gridPositions[gridIndex] = new GridPos
                {
                    IsOccupied = true,
                    particleIndex = newIndex
                };

                try
                {
                    _activeParticles[newIndex] = particle;
                }
                catch (Exception e)
                {
                    Debug.Log($"INdex: [{newIndex}]");
                    throw;
                }
            }
            
            //[X, X, X, O, O, X, X]
        }
        
        //==================================================================================//
        
        private void UpdateParticles2()
        {
            for (var i = 0; i < _sizeY; i++)
            {
                var container = _particleRowContainers[i].ParticleIndices;
                var count = _particleRowContainers[i].ParticleCount;
                for (var ii = 0; ii < count; ii++)
                {
                    var particle = _activeParticles[(int)container[ii]];

                    //Check for napping particles OR ones that were marked as empty
                    if(particle.Asleep || particle.Type == Particle.TYPE.NONE)
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
                    
                    
                    if(allowSleeping && particle.SleepCounter++ >= Particle.WAIT_TO_SLEEP)
                        particle.Asleep = true;
                }
                
                _particleRowContainers[i].Clear();
            }
        }

        private void UpdateParticleRows()
        {
            //---------------------------------------------------//

            /*//FIXME Instead of doing this, I should be able to apply a compression move on the array using the changed rows
            void ForceClearRowContainers()
            {
                var count = _particleRowContainers.Length;
                for (var i = 0; i < count; i++)
                {
                    _particleRowContainers[i].Clear();
                }
            }
            
            //---------------------------------------------------//
            
            if (forceClearContainers)
                ForceClearRowContainers();*/
            
            for (int i = 0; i < _particleCount; i++)
            {
                var particle = _activeParticles[i];
                
                if(particle.Type == Particle.TYPE.NONE)
                    continue;
                if(particle.Material == Particle.MATERIAL.SOLID)
                    continue;
                if(particle.Asleep)
                    continue;
                
                _particleRowContainers[particle.YCoord].AddParticle(particle.Index);
            }
        }

        private void RemoveIndexFromParticleRow(in int yPos, in int particleIndex)
        {
            
        }

        //============================================================================================================//

        private void ClearGrid()
        {
            _particleCount = 0;
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
