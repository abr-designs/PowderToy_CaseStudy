using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using PowderToy.Utilities;
using Sirenix.OdinInspector;
using UnityEngine;
using Random = UnityEngine.Random;

namespace PowderToy
{
    //Rules:
    //#1. If an index is Illegal, then it will be equal to -1
    
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
                ParticleIndex = -1
            };
            
            public bool IsOccupied;
            public int ParticleIndex;
        }

        /// <summary>
        /// Container for particles 0 -> GridSize.x on a single row. Used to effeciently navigate grid of sorted particles
        /// by Y position
        /// </summary>
        private class ParticleRow
        {
            public readonly uint[] ParticleIndices;
            public uint ParticleCount;

            public ParticleRow(in uint rowSize)
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

        /// <summary>
        /// Used exclusively by IsSpaceOccupiedDetailed() to return information foregoing the use of an anonymous type/Tuple.
        /// Do not use for anything other than fast obtaining of information on a grid location.
        /// </summary>
        private struct GridLocationDetails
        {
            public bool IsLegal;
            public bool IsOccupied;
            public uint OccupierIndex;
        }
        //Properties
        //============================================================================================================//        
        
        public static Action<Vector2Int> OnInit;

        //TODO This might need to be a list at some point, or exist somewhere else
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

        private bool gridRequiresCleaning;

        private GridLocationDetails _gridLocationDetails;

        //Collection of Grid Elements
        //------------------------------------------------//
        
        //Master list of Active Particles
        private Particle[] _activeParticles;
        
        //Row organized particles to help with bottom-up update order
        private ParticleRow[] _particleRowContainers;
        
        //Complete Grid Layout to track who is where
        private GridPos[] _gridPositions;
        
        private int[] _particleSurroundings;


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
            //Fill default particle data
            _activeParticles = new Particle[count];
            for (var i = 0; i < count; i++)
            {
                _activeParticles[i] = new Particle();
            }
            

            _sizeX = size.x;
            _sizeY = size.y;

            GridHelper.InitGridData(_sizeX, _sizeY);

            //Setup column Containers
            //------------------------------------------------//
            _particleRowContainers = new ParticleRow[_sizeY];
            var sizeX = (uint)_sizeX;
            for (var i = 0; i < _sizeY; i++)
            {
                _particleRowContainers[i] = new ParticleRow(sizeX);
            }

            //------------------------------------------------//
            
            _particleSurroundings = new int[9];


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
            UpdateParticles();
            
            if(gridRequiresCleaning)
                CleanActiveParticles();
            
            QueueParticleRowsForNextTick();

            if(DebugView == false)
                _particleRenderer.UpdateTexture(_activeParticles, _particleCount);
            else
                _particleRenderer.DEBUG_DisplayOccupiedSpace(_gridPositions);
        }

        //GridCommandBuffer
        //============================================================================================================//
        
        private void ExecuteQueuedCommand()
        {
            switch (QueuedCommand.Type)
            {
                //------------------------------------------------//
                case Command.TYPE.NONE:
                    return;
                //------------------------------------------------//
                case Command.TYPE.SPAWN_PARTICLE when QueuedCommand.InteractionRadius == 0:
                    SpawnParticle(
                        QueuedCommand.ParticleTypeToSpawn, 
                        QueuedCommand.InteractionCoordinate);
                    break;
                case Command.TYPE.SPAWN_PARTICLE:
                    TrySpawnNewParticlesInRadius(
                        QueuedCommand.ParticleTypeToSpawn, 
                        QueuedCommand.InteractionCoordinate,
                        QueuedCommand.InteractionRadius);
                    break;
                //------------------------------------------------//
                case Command.TYPE.KILL_PARTICLE when QueuedCommand.InteractionRadius > 0:
                    TryMarkParticleForDeathInRadius(
                        QueuedCommand.InteractionCoordinate, 
                        QueuedCommand.InteractionRadius);
                    break;
                case Command.TYPE.KILL_PARTICLE:
                    MarkParticleForDeath(QueuedCommand.InteractionCoordinate);
                    break;
                //------------------------------------------------//
                default:
                    throw new ArgumentOutOfRangeException();
            }

            //Reset command buffer after executing
            QueuedCommand = default;
        }

        //Spawning Particles
        //============================================================================================================//

        private void SpawnParticle(in Particle.TYPE particleType, in Vector2Int coordinate)
        {
            if (particleType == Particle.TYPE.NONE)
                return;
            
            var newX = coordinate.x;
            var newY = coordinate.y;
            
            if(IsSpaceOccupied(newX, newY))
                return;
            
            var newIndex = _particleCount++;
            //var newColor = _particleRenderer.GetParticleColor(particleType);
            //var newParticle = new Particle(particleType, newColor, newIndex, newX, newY);
            ref var newParticle = ref _activeParticles[newIndex];
            ParticleFactory.CreateParticle(ref newParticle, particleType, newIndex, newX, newY);

            _gridPositions[GridHelper.CoordinateToIndex(newX, newY)] = new GridPos
            {
                IsOccupied = true,
                ParticleIndex = newParticle.Index
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
        
        private void TrySpawnNewParticlesInRadius(in Particle.TYPE particleType, in Vector2Int originCoordinate, in uint radius)
        {
            var coordinates = RadiusSelection.GetCoordinates(radius);

            for (var i = 0; i < coordinates.Length; i++)
            {
                var targetCoordinate = originCoordinate + coordinates[i];
                
                SpawnParticle(particleType, targetCoordinate);
            }
        }

        //Killing Particles
        //============================================================================================================//

        private void MarkParticleForDeath(in Vector2Int coordinate)
        {
            var x = coordinate.x;
            var y = coordinate.y;
            
            if (GridHelper.IsLegalCoordinate(x, y) == false)
                return;
            
            if(IsSpaceOccupied(x, y) == false)
                return;

            var gridIndex = GridHelper.CoordinateToIndex(x, y);
            var gridPos = _gridPositions[gridIndex];

            ref var particle = ref _activeParticles[gridPos.ParticleIndex];
            //We only want to queue deletion for things that move, solid things are safe to be deleted now.
            //This is because solid particles do not get updated the same way
            //FIXME once fire is added the above statement may need to change
            switch (particle.Material)
            {
                case Particle.MATERIAL.NONE:
                    break;
                case Particle.MATERIAL.SOLID:
                case Particle.MATERIAL.POWDER:
                case Particle.MATERIAL.LIQUID:
                case Particle.MATERIAL.GAS:
                    particle.KillNextTick = true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            //gridPos.IsOccupied = false;
            _gridPositions[gridIndex] = GridPos.Empty;
            
            gridRequiresCleaning = true;
        }
        private void MarkParticleForDeath(ref Particle particle)
        {
            var x = particle.XCoord;
            var y = particle.YCoord;

            if(IsSpaceOccupied(x, y) == false)
                return;

            var gridIndex = GridHelper.CoordinateToIndex(x, y);
            //var gridPos = _gridPositions[gridIndex];

            //var particle = _activeParticles[gridPos.ParticleIndex];
            //We only want to queue deletion for things that move, solid things are safe to be deleted now.
            //This is because solid particles do not get updated the same way
            //FIXME once fire is added the above statement may need to change
            switch (particle.Material)
            {
                case Particle.MATERIAL.SOLID:
                case Particle.MATERIAL.POWDER:
                case Particle.MATERIAL.LIQUID:
                case Particle.MATERIAL.GAS:
                    particle.KillNextTick = true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            _gridPositions[gridIndex] = GridPos.Empty;
            
            gridRequiresCleaning = true;
        }
        
        private void TryMarkParticleForDeathInRadius(in Vector2Int originCoordinate, in uint radius)
        {
            var coordinates = RadiusSelection.GetCoordinates(radius);

            for (var i = 0; i < coordinates.Length; i++)
            {
                MarkParticleForDeath(originCoordinate + coordinates[i]);
            }
        }

        private void KillParticle(ref Particle particle)
        {
            particle.CopyFrom(Particle.Empty);
            _particleCount--;
        }

        //==================================================================================//

        private void UpdateParticles()
        {
            for (var i = 0; i < _sizeY; i++)
            {
                var container = _particleRowContainers[i].ParticleIndices;
                var count = _particleRowContainers[i].ParticleCount;
                for (var ii = 0; ii < count; ii++)
                {
                    ref var particle = ref _activeParticles[(int)container[ii]];

                    //Check for napping particles OR ones that were marked as empty
                    if(particle.Asleep || particle.Type == Particle.TYPE.NONE)
                        continue;
                    
                    if (particle.KillNextTick)
                    {
                        KillParticle(ref particle);
                        continue;
                    }
                    
                    GridHelper.GetParticleSurroundings(particle.XCoord, particle.YCoord, ref _particleSurroundings);

                    switch (particle.Material)
                    {
                        case Particle.MATERIAL.SOLID when particle.Type == Particle.TYPE.FIRE:
                            break;
                        case Particle.MATERIAL.SOLID:
                            continue;
                        case Particle.MATERIAL.POWDER:
                            UpdatePowderParticle(_particleSurroundings, ref particle);
                            break;
                        case Particle.MATERIAL.LIQUID:
                            UpdateLiquidParticle(_particleSurroundings, ref particle);
                            break;
                        case Particle.MATERIAL.GAS:
                            UpdateGasParticle(_particleSurroundings, ref particle);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                    
                    if (particle.HasLifeSpan)
                    {
                        if (particle.Lifetime-- <= 0)
                        {
                            var gridIndex = GridHelper.CoordinateToIndex(particle);
                            
                            particle.CopyFrom(Particle.Empty);
                            _gridPositions[gridIndex] = GridPos.Empty;
                            gridRequiresCleaning = true;
                            _particleCount--;
                            
                            continue;
                        }
                    }
                    
                    if (particle.Type == Particle.TYPE.FIRE)
                        FireParticleBurnCheck(_particleSurroundings);
                    
                    /*if(allowSleeping && particle.SleepCounter++ >= Particle.WAIT_TO_SLEEP)
                        particle.Asleep = true;*/
                }
                
                _particleRowContainers[i].Clear();
            }
        }

        private void CleanActiveParticles()
        {
            var expectedParticles = _particleCount;
            var maxCount = _activeParticles.Length;
            
            var foundParticles = 0;
            var indexOffset = 0;
            for (var i = 0; i < maxCount; i++)
            {
                if (foundParticles == expectedParticles)
                    break;
                
                var particle = _activeParticles[i];

                if (particle.Type == Particle.TYPE.NONE)
                {
                    indexOffset++;
                    continue;
                }

                foundParticles++;

                if (indexOffset == 0)
                    continue;

                var newIndex = i - indexOffset;

                var gridPosIndex = GridHelper.CoordinateToIndex(particle);

                //Update particle index in grid positions
                var gridPos = _gridPositions[gridPosIndex];
                gridPos.ParticleIndex = newIndex;
                _gridPositions[gridPosIndex] = gridPos;

                //particle.Index = newIndex;
                _activeParticles[newIndex].CopyFrom(particle);
                _activeParticles[newIndex].Index = newIndex;
                
                _activeParticles[i].CopyFrom(Particle.Empty);
            }

            gridRequiresCleaning = false;
        }

        private void QueueParticleRowsForNextTick()
        {
            //---------------------------------------------------//

            for (var i = 0; i < _particleCount; i++)
            {
                var particle = _activeParticles[i];
                
                //We can't move nothing
                if(particle.Type == Particle.TYPE.NONE)
                    continue;
                //Solid Particles do not move thus do not need to be updated. Unless they're on fire.
                if(particle.Material == Particle.MATERIAL.SOLID && particle.Type != Particle.TYPE.FIRE)
                    continue;
                //If we aren't updating the particle, don't queue it for updates
                if(particle.Asleep)
                    continue;

                try
                {
                    _particleRowContainers[particle.YCoord].AddParticle(particle.Index);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }
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

        /// <summary>
        /// Gets information about the grid position such as if the location exists on the grid, whether it is occupied,
        /// and if occupied by whom (by Active Particle index).
        /// </summary>
        /// <param name="index"></param>
        /// <param name="gridLocationDetails"></param>
        /// <returns></returns>
        private void IsIndexOccupiedDetailed(in int index, ref GridLocationDetails gridLocationDetails)
        {
            if (index < 0)
            {
                gridLocationDetails.IsLegal = false;
                return;
            }

            if (_gridPositions[index].IsOccupied == false)
            {
                gridLocationDetails.IsLegal = true;
                gridLocationDetails.IsOccupied = false;
                return;
            }

            gridLocationDetails.IsLegal = true;
            gridLocationDetails.IsOccupied = true;
            gridLocationDetails.OccupierIndex = (uint)_gridPositions[index].ParticleIndex;
        }

        private bool IsSpaceOccupied(in int x, in int y)
        {
            //TODO Might need to apply the same fix as IsSpaceOccupiedDetailed() for grid space legality
            if (GridHelper.IsLegalCoordinate(x, y) == false)
                return true;

            var gridIndex = GridHelper.CoordinateToIndex(x, y);

            return _gridPositions[gridIndex].IsOccupied;
        }
        
        private bool IsIndexOccupied(in int index)
        {
            //TODO Might need to apply the same fix as IsSpaceOccupiedDetailed() for grid space legality
            if (index < 0)
                return true;

            return _gridPositions[index].IsOccupied;
        }

        //============================================================================================================//

        private void UpdatePowderParticle(in int[] particleSurroundings, ref Particle particle)
        {
            //var originalCoordinate = particle.Coordinate;
            var originalX = particle.XCoord;
            var originalY = particle.YCoord;

            if (particle.Asleep)
                return;

            //------------------------------------------------------------------//
            //[MethodImpl(MethodImplOptions.AggressiveInlining)]
            bool TrySetNewPosition(
                in int newX,
                in int newY,
                in int newGridIndex,
                in int currentGridIndex,
                ref Particle myParticle)
            {
                /*if (GridHelper.IsLegalIndex(newGridIndex) == false)
                    return false;*/
                //IF the space is occupied, leave early
                IsIndexOccupiedDetailed(newGridIndex, ref _gridLocationDetails);

                if (_gridLocationDetails.IsLegal == false)
                    return false;
                
                var spaceOccupied = _gridLocationDetails.IsOccupied;
                var occupierIndex = _gridLocationDetails.OccupierIndex;
                ref var occupier = ref _activeParticles[occupierIndex];
                

                if (spaceOccupied)
                {
                    if (occupier.Type != Particle.TYPE.WATER)
                        return false;
                }
                
                myParticle.XCoord = newX;
                myParticle.YCoord = newY;


                //Test for water
                //------------------------------------------------------------------//
                //FIXME I need a new behaviour for this
                if (occupier.Type == Particle.TYPE.WATER)
                {
                    //occupier.Coordinate = originalCoordinate;
                    occupier.XCoord = originalX;
                    occupier.YCoord = originalY;
                    _gridPositions[currentGridIndex] = new GridPos
                    {
                        IsOccupied = true,
                        ParticleIndex = (int)occupierIndex
                    };
                    //_activeParticles[occupierIndex] = occupier;
                }
                else
                    _gridPositions[currentGridIndex] = GridPos.Empty;
                //------------------------------------------------------------------//


                _gridPositions[newGridIndex] = new GridPos
                {
                    IsOccupied = true,
                    ParticleIndex = myParticle.Index
                };
                
                return true;
            }

            //------------------------------------------------------------------//
            //[0 1 2]
            //[3 x 5]
            //[6 7 8]
            var currentGridIndex = particleSurroundings[4];
            if (particleSurroundings[7] >= 0 && TrySetNewPosition(originalX, originalY - 1, particleSurroundings[7], currentGridIndex, ref particle))
                return;
            if (particleSurroundings[6] >= 0 && TrySetNewPosition(originalX - 1, originalY - 1, particleSurroundings[6], currentGridIndex,
                    ref particle))
                return;
            if (particleSurroundings[8] >= 0 && TrySetNewPosition(originalX + 1, originalY - 1, particleSurroundings[8], currentGridIndex,
                    ref particle))
                return;
        }

        //TODO Need to implement a position resolving function when the liquid particle needs to move
        private void UpdateLiquidParticle(in int[] particleSurroundings, ref Particle particle)
        {
            //var coordinate = particle.Coordinate;
            var originalX = particle.XCoord;
            var originalY = particle.YCoord;

            //------------------------------------------------------------------//

            bool TrySetNewPosition(
                in int newX,
                in int newY,
                in int newGridIndex,
                in int currentGridIndex,
                ref Particle myParticle)
            {
                if (myParticle.HasDensity)
                {
                    IsIndexOccupiedDetailed(newGridIndex, ref _gridLocationDetails);

                    if (_gridLocationDetails.IsLegal == false)
                        return false;

                    if (_gridLocationDetails.IsOccupied)
                    {
                        var occupierParticle = _activeParticles[_gridLocationDetails.OccupierIndex];

                        if (occupierParticle.Material == Particle.MATERIAL.LIQUID &&
                             occupierParticle.Type != myParticle.Type &&
                             occupierParticle.HasDensity)
                        {
                            var didSwap = LiquidParticleDensityCheck(
                                currentGridIndex, 
                                newGridIndex, 
                                ref myParticle,
                                ref occupierParticle);

                            if (didSwap)
                                return true;
                        }
                    }
                }
                //If the particle doesn't care about density, don't go through the expensive dance above
                if (IsIndexOccupied(newGridIndex))
                    return false;

                //myParticle.Coordinate += offset;
                myParticle.XCoord = newX;
                myParticle.YCoord = newY;

                _gridPositions[currentGridIndex] = GridPos.Empty;

                _gridPositions[newGridIndex] = new GridPos
                {
                    IsOccupied = true,
                    ParticleIndex = myParticle.Index
                };
                return true;
            }

            //------------------------------------------------------------------//
            //[0 1 2]
            //[3 x 5]
            //[6 7 8]
            var originalIndex = particleSurroundings[4];
            if (particleSurroundings[7] >= 0 && TrySetNewPosition(originalX, originalY - 1, particleSurroundings[7], originalIndex, ref particle))
                return;
            if (particleSurroundings[6] >= 0 && TrySetNewPosition(originalX - 1, originalY - 1, particleSurroundings[6], originalIndex, ref particle))
                return;
            if (particleSurroundings[8] >= 0 && TrySetNewPosition(originalX + 1, originalY - 1, particleSurroundings[8], originalIndex, ref particle))
                return;
            if (particleSurroundings[3] >= 0 && TrySetNewPosition(originalX - 1, originalY, particleSurroundings[3], originalIndex, ref particle))
                return;
            if (particleSurroundings[5] >= 0 && TrySetNewPosition(originalX + 1, originalY, particleSurroundings[5], originalIndex, ref particle))
                return;
        }

        private void UpdateGasParticle(in int[] particleSurroundings, ref Particle particle)
        {
            //var coordinate = particle.Coordinate;
            var originalX = particle.XCoord;
            var originalY = particle.YCoord;

            //------------------------------------------------------------------//

            bool TrySetNewPosition(
                in int newX,
                in int newY,
                in int newGridIndex,
                in int currentGridIndex,
                ref Particle myParticle)
            {
                /*if (GridHelper.IsLegalIndex(newGridIndex) == false)
                    return false;*/
                //IF the space is occupied, leave early
                if (IsIndexOccupied(newGridIndex))
                    return false;

                //myParticle.Coordinate += offset;
                myParticle.XCoord = newX;
                myParticle.YCoord = newY;

                _gridPositions[currentGridIndex] = GridPos.Empty;

                _gridPositions[newGridIndex] = new GridPos
                {
                    IsOccupied = true,
                    ParticleIndex = myParticle.Index
                };
                return true;
            }

            //------------------------------------------------------------------//
            //[0 1 2]
            //[3 x 5]
            //[6 7 8]
            var originalIndex = particleSurroundings[4];
            if (particleSurroundings[1] >= 0 && TrySetNewPosition(originalX, originalY + 1, particleSurroundings[1], originalIndex, ref particle))
                return;
            if (particleSurroundings[0] >= 0 && TrySetNewPosition(originalX - 1, originalY + 1, particleSurroundings[0], originalIndex, ref particle))
                return;
            if (particleSurroundings[2] >= 0 && TrySetNewPosition(originalX + 1, originalY + 1, particleSurroundings[2], originalIndex, ref particle))
                return;
            if (particleSurroundings[3] >= 0 && TrySetNewPosition(originalX - 1, originalY, particleSurroundings[3], originalIndex, ref particle))
                return;
            if (particleSurroundings[5] >= 0 && TrySetNewPosition(originalX + 1, originalY, particleSurroundings[5], originalIndex, ref particle))
                return;
        }

        //============================================================================================================//

        private void FireParticleBurnCheck(in int[] particleSurroundings)
        {
            //Check for things to burn
            //------------------------------------------------------------------//
            //[0 1 2]
            //[3 x 5]
            //[6 7 8]
            for (var i = 0; i < 8; i++)
            {
                var index = particleSurroundings[i];
                if (index < 0)
                    continue;

                var gridPos = _gridPositions[index];
                if (gridPos.IsOccupied == false)
                    continue;

                ref var posParticle = ref _activeParticles[gridPos.ParticleIndex];

                if (posParticle.CanBurn == false)
                    continue;

                //TODO Should probably increase the chance of burning over time, when back-to-back calls occur
                if (Random.Range(0, 100) > posParticle.ChanceToBurn)
                    continue;

                ParticleFactory.ConvertToFire(ref posParticle);
            }
        }

        private bool LiquidParticleDensityCheck(
            in int fromGridIndex, 
            in int toGridIndex, 
            ref Particle fromParticle,
            ref Particle toParticle)
        {
            //Compare densities, and swap if necessary
            //------------------------------------------------------------------//
            //[0 1 2]
            //[3 x 5]
            //[6 7 8]
            //TODO Determine if just checking the one below is enough

            if (toGridIndex < 0)
                return false;

            var fromGridPos = _gridPositions[fromGridIndex];
            var toGridPos = _gridPositions[toGridIndex];

            if (toGridPos.IsOccupied == false || fromGridPos.IsOccupied == false)
                return false;

            /*//Make sure that both particles want to compare Density
            if ((toParticle.HasDensity && fromParticle.HasDensity) == false)
                return;*/

            //If the one below already has a greater density
            if (toParticle.Density >= fromParticle.Density)
                return false;

            SwapParticlePositions(fromGridIndex, toGridIndex,
                ref fromGridPos, ref toGridPos,
                ref fromParticle, ref toParticle);
            
            return true;
        }

        /*private void SwapParticlePositions(in int fromGridIndex, in int toGridIndex, in bool updateParticles = true)
        {
            var currentGridPos = _gridPositions[fromGridIndex];
            var newGridPos = _gridPositions[toGridIndex];
            
            var currentParticle = _activeParticles[currentGridPos.ParticleIndex];
            var belowParticle = _activeParticles[newGridPos.ParticleIndex];
            
            //Otherwise we swap the two particles, and update the grid
            currentGridPos.ParticleIndex = belowParticle.Index;
            newGridPos.ParticleIndex = currentParticle.Index;

            _gridPositions[fromGridIndex] = currentGridPos;
            _gridPositions[toGridIndex] = newGridPos;
            
            //and Update the particle Coordinates
            var currentX = currentParticle.XCoord;
            var currentY = currentParticle.YCoord;

            currentParticle.XCoord = belowParticle.XCoord;
            currentParticle.YCoord = belowParticle.YCoord;

            belowParticle.XCoord = currentX;
            belowParticle.YCoord = currentY;

            if (updateParticles == false)
                return;

            _activeParticles[currentParticle.Index] = currentParticle;
            _activeParticles[belowParticle.Index] = belowParticle;
        }*/
        
        private void SwapParticlePositions(in int fromGridIndex, in int toGridIndex,
            ref GridPos fromGridPos, ref GridPos toGridPos,
            ref Particle fromParticle, ref Particle toParticle)
        {
            //Otherwise we swap the two particles, and update the grid
            fromGridPos.ParticleIndex = toParticle.Index;
            toGridPos.ParticleIndex = fromParticle.Index;

            _gridPositions[fromGridIndex] = fromGridPos;
            _gridPositions[toGridIndex] = toGridPos;
            
            //and Update the particle Coordinates
            var fromX = fromParticle.XCoord;
            var fromY = fromParticle.YCoord;

           fromParticle.XCoord = toParticle.XCoord;
           fromParticle.YCoord = toParticle.YCoord;

           toParticle.XCoord = fromX;
           toParticle.YCoord = fromY;

        }

        //============================================================================================================//
        
    }

    
}
