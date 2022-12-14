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

        [SerializeField, TitleGroup("Particle Properties")]
        private bool allowSleeping = true;
        
        [SerializeField, Min(0), DisableInPlayMode, TitleGroup("Grid Properties")]
        private Vector2Int size;
        //We save the size as static ints to reduce time it takes to call get from Vector2Int
        private static int _sizeX;
        private static int _sizeY;

        [SerializeField, DisableInPlayMode, TitleGroup("Properties")]
        private int ambientTemperature;
        [SerializeField, DisableInPlayMode, TitleGroup("Properties"), Range(0,4)]
        private int coolingThresholdCount = 2;

        public static int AmbientTemperature { get; private set; }
        private float minTemp;
        private float maxTemp;

        private ParticleRenderer _particleRenderer;

        private bool gridRequiresCleaning;

        //private GridLocationDetails _gridLocationDetails;

        //Collection of Grid Elements
        //------------------------------------------------//
        
        //Master list of Active Particles
        private Particle[] _activeParticles;
        
        //Row organized particles to help with bottom-up update order
        private ParticleRow[] _particleRowContainers;
        
        //Complete Grid Layout to track who is where
        private GridPos[] _gridPositions;
        
        private SurroundingData[] _particleSurroundings;


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
            
            _particleSurroundings = new SurroundingData[9];
            for (var i = 0; i < 9; i++)
            {
                _particleSurroundings[i] = new SurroundingData(ref _gridPositions, ref _activeParticles);
            }


            _particleRenderer = FindObjectOfType<ParticleRenderer>();

            AmbientTemperature = ambientTemperature;
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

            //------------------------------------------------//
            switch (_particleRenderer.displayType)
            {
                case ParticleRenderer.DISPLAY.DEFAULT:
                    _particleRenderer.UpdateTextureDefault(_activeParticles, _particleCount);
                    break;
                case ParticleRenderer.DISPLAY.HEAT:
                    var dif = Mathf.Max(maxTemp - minTemp, ambientTemperature * 2f);
                    var low = minTemp - (dif * 0.10f);
                    var high = maxTemp + (dif * 0.05f);
                    _particleRenderer.UpdateTextureHeat(_activeParticles, _particleCount, low, high);
                    break;
                case ParticleRenderer.DISPLAY.DEBUG:
                    _particleRenderer.DEBUG_DisplayOccupiedSpace(_gridPositions);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            //------------------------------------------------//
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

            newParticle.HasChangedTemp = true;
            CheckShouldChangeState(true, ref newParticle);
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
                    KillParticle(ref particle);
                    break;
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
        
        private void KillParticleImmediate(ref Particle particle)
        {
            var gridIndex = GridHelper.CoordinateToIndex(particle);
                            
            particle.CopyFrom(Particle.Empty);
            _gridPositions[gridIndex] = GridPos.Empty;
            gridRequiresCleaning = true;
            _particleCount--;
        }

        //==================================================================================//

        private void UpdateParticles()
        {
            minTemp = 999;
            maxTemp = -999;
            
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
                        case Particle.MATERIAL.SOLID/* when particle.SpreadsHeat*/:
                            break;
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

                    //Life Span Updates
                    //------------------------------------------------//
                    if (particle.HasLifeSpan)
                    {
                        if (particle.Lifetime-- <= 0)
                        {
                            if(CheckShouldKillParticle(ref particle))
                                KillParticleImmediate(ref particle);
                            continue;
                        }
                    }

                    //Particle Temperature Updates
                    //------------------------------------------------//
                    CheckParticleMaterialState(ref particle);
                    
                    //Check Acid
                    //------------------------------------------------//

                    if (particle.Type == Particle.TYPE.ACID)
                        CheckAcidSurroundings(_particleSurroundings);

                    //------------------------------------------------//
                    /*if(allowSleeping && particle.SleepCounter++ >= Particle.WAIT_TO_SLEEP)
                        particle.Asleep = true;*/
                }
                
                _particleRowContainers[i].Clear();
            }
        }

        //Used to see if the particle has changed from Solid <=> Liquid <=> Gas
        private void CheckParticleMaterialState(ref Particle particle)
        {
            bool shouldCheckMaterialState = false;
            if (particle.SpreadsHeat)
            {
                //TODO Need to determine if this is the best way of cooling
                if (particle.CanCool && HeatCountAtCardinals(_particleSurroundings) < coolingThresholdCount)
                {
                    EqualizeParticleTemperature(ref particle);
                    shouldCheckMaterialState = true;
                }
                        
                SpreadParticleHeatToSurroundings(_particleSurroundings);
            }
            //FIXME Might need a way of slowing the pace of this
            else if (particle.HasChangedTemp == false && particle.CurrentTemperature != ambientTemperature)
            {
                EqualizeParticleTemperature(ref particle);
                shouldCheckMaterialState = true;
            }
                    
            if(shouldCheckMaterialState)
                CheckShouldChangeState(_particleSurroundings, ref particle);

            if (particle.CurrentTemperature < minTemp)
                minTemp = particle.CurrentTemperature;
            else if(particle.CurrentTemperature > maxTemp)
                maxTemp = particle.CurrentTemperature;
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
                ref var particle = ref _activeParticles[i];
                
                //We can't move nothing
                if(particle.Type == Particle.TYPE.NONE)
                    continue;
                
                particle.HasChangedTemp = false;
                particle.IsSwapLocked = false;

                /*//Solid Particles do not move thus do not need to be updated. Unless they're on fire.
                if(particle.Material == Particle.MATERIAL.SOLID && particle.Type != Particle.TYPE.FIRE)
                    continue;*/
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
        /*private void IsIndexOccupiedDetailed(in int index, ref GridLocationDetails gridLocationDetails)
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
        }*/

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

        private void UpdatePowderParticle(in SurroundingData[] particleSurroundings, ref Particle particle)
        {
            //var originalCoordinate = particle.Coordinate;
            var originalX = particle.XCoord;
            var originalY = particle.YCoord;

            //------------------------------------------------------------------//
            bool TrySetNewPosition(
                in int newX,
                in int newY,
                in SurroundingData newParticlePosition,
                in int currentGridIndex,
                ref Particle myParticle)
            {
                //IF the space is occupied, leave early
                //IsIndexOccupiedDetailed(newGridIndex, ref _gridLocationDetails);

                if (newParticlePosition.IsValid == false)
                    return false;
                
                var spaceOccupied = newParticlePosition.IsOccupied;
                var occupierIndex = newParticlePosition.ParticleIndex;
                ref var occupier = ref _activeParticles[occupierIndex];
                

                if (spaceOccupied)
                {
                    if (occupier.Material == Particle.MATERIAL.SOLID || occupier.Material == Particle.MATERIAL.POWDER)
                        return false;
                    
                    SwapParticlePositions(currentGridIndex, newParticlePosition.GridIndex,
                        ref myParticle, ref occupier);

                    myParticle.IsSwapLocked = true;
                    occupier.IsSwapLocked = true;
                        
                    return true;
                }
                
                myParticle.XCoord = newX;
                myParticle.YCoord = newY;


                //Test for water
                //------------------------------------------------------------------//
                /*//FIXME I need a new behaviour for this
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
                else*/
                _gridPositions[currentGridIndex] = GridPos.Empty;
                //------------------------------------------------------------------//


                _gridPositions[newParticlePosition.GridIndex] = new GridPos
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
            var currentGridIndex = particleSurroundings[4].GridIndex;
            if (particleSurroundings[7].IsValid && TrySetNewPosition(originalX, originalY - 1, particleSurroundings[7], currentGridIndex, ref particle))
                return;
            if (particleSurroundings[6].IsValid && TrySetNewPosition(originalX - 1, originalY - 1, particleSurroundings[6], currentGridIndex,
                    ref particle))
                return;
            if (particleSurroundings[8].IsValid && TrySetNewPosition(originalX + 1, originalY - 1, particleSurroundings[8], currentGridIndex,
                    ref particle))
                return;
        }
        /*private void UpdatePowderParticle(in int[] particleSurroundings, ref Particle particle)
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
                    return false;#1#
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
        }*/

        //TODO Need to implement a position resolving function when the liquid particle needs to move
        private void UpdateLiquidParticle(in SurroundingData[] particleSurroundings, ref Particle particle)
        {
            //var coordinate = particle.Coordinate;
            var originalX = particle.XCoord;
            var originalY = particle.YCoord;

            //------------------------------------------------------------------//

            bool TrySetNewPosition(
                in int newX,
                in int newY,
                in SurroundingData newParticlePosition,
                in int currentGridIndex,
                in bool useSwapLock,
                ref Particle myParticle)
            {
                //IsIndexOccupiedDetailed(newGridIndex, ref _gridLocationDetails);
                
                if (newParticlePosition.IsValid == false)
                    return false;

                Particle occupyingParticle = null;
                var occupied = newParticlePosition.IsOccupied;
                if (newParticlePosition.IsOccupied)
                    occupyingParticle = newParticlePosition.Particle;
                
                //FIXME I feel like this needs to be somewhere else...
                //Water extinguishes fire.
                //------------------------------------------------//
                
                //Putting out fire overrides the need to check density
                if (occupied && myParticle.Type == Particle.TYPE.WATER && occupyingParticle.SpreadsHeat)
                    return TryExtinguish(ref myParticle, ref occupyingParticle);

                //Check material densities
                //------------------------------------------------//
                
                if (CheckDidSwapParticleDensity(useSwapLock, occupied, currentGridIndex, newParticlePosition.GridIndex, ref myParticle,
                        ref occupyingParticle))
                    return true;

                //------------------------------------------------//
                
                //If the particle doesn't care about density, don't go through the expensive dance above
                if (newParticlePosition.IsOccupied)
                    return false;

                //myParticle.Coordinate += offset;
                myParticle.XCoord = newX;
                myParticle.YCoord = newY;

                _gridPositions[currentGridIndex] = GridPos.Empty;

                _gridPositions[newParticlePosition.GridIndex] = new GridPos
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
            //Currently Density checks only occur on the absolute target direction, DOWN[7]
            var originalIndex = particleSurroundings[4].GridIndex;
            if (particleSurroundings[7].IsValid && TrySetNewPosition(originalX, originalY - 1, particleSurroundings[7], originalIndex, true, ref particle))
                return;
            if (particleSurroundings[6].IsValid && TrySetNewPosition(originalX - 1, originalY - 1, particleSurroundings[6], originalIndex, true, ref particle))
                return;
            if (particleSurroundings[8].IsValid && TrySetNewPosition(originalX + 1, originalY - 1, particleSurroundings[8], originalIndex, true, ref particle))
                return;
            if (particleSurroundings[3].IsValid && TrySetNewPosition(originalX - 1, originalY, particleSurroundings[3], originalIndex,false, ref particle))
                return;
            if (particleSurroundings[5].IsValid && TrySetNewPosition(originalX + 1, originalY, particleSurroundings[5], originalIndex,false, ref particle))
                return;
        }

        private void UpdateGasParticle(in SurroundingData[] particleSurroundings, ref Particle particle)
        {
            //var coordinate = particle.Coordinate;
            var originalX = particle.XCoord;
            var originalY = particle.YCoord;

            //------------------------------------------------------------------//

            bool TrySetNewPosition(
                in int newX,
                in int newY,
                in SurroundingData newParticlePosition,
                in int currentGridIndex,
                in bool useSwapLock,
                ref Particle myParticle)
            {
                /*if (GridHelper.IsLegalIndex(newGridIndex) == false)
                    return false;*/
                //IsIndexOccupiedDetailed(newGridIndex, ref _gridLocationDetails);
                
                if (newParticlePosition.IsValid == false)
                    return false;

                Particle occupyingParticle = null;
                var occupied = newParticlePosition.IsOccupied;
                if (occupied)
                    occupyingParticle = newParticlePosition.Particle;
                
                //Check material densities
                //------------------------------------------------//

                if (CheckDidSwapParticleDensity(useSwapLock, occupied, currentGridIndex, newParticlePosition.GridIndex, ref myParticle,
                        ref occupyingParticle))
                    return true;
                
                //------------------------------------------------//
                //IF the space is occupied, leave early
                if (newParticlePosition.IsOccupied)
                    return false;

                //myParticle.Coordinate += offset;
                myParticle.XCoord = newX;
                myParticle.YCoord = newY;

                _gridPositions[currentGridIndex] = GridPos.Empty;

                _gridPositions[newParticlePosition.GridIndex] = new GridPos
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
            //Currently Density checks only occur on the absolute target direction, UP[1]
            var originalIndex = particleSurroundings[4].GridIndex;
            if (particleSurroundings[1].IsValid && TrySetNewPosition(originalX, originalY + 1, particleSurroundings[1], originalIndex, true, ref particle))
                return;
            if (particleSurroundings[0].IsValid && TrySetNewPosition(originalX - 1, originalY + 1, particleSurroundings[0], originalIndex, true, ref particle))
                return;
            if (particleSurroundings[2].IsValid && TrySetNewPosition(originalX + 1, originalY + 1, particleSurroundings[2], originalIndex, true, ref particle))
                return;
            if (particleSurroundings[3].IsValid && TrySetNewPosition(originalX - 1, originalY, particleSurroundings[3], originalIndex, false, ref particle))
                return;
            if (particleSurroundings[5].IsValid && TrySetNewPosition(originalX + 1, originalY, particleSurroundings[5], originalIndex, false, ref particle))
                return;
        }

        //Particle Temperature functions
        //============================================================================================================//

        #region Particle Temperature functions

        private void SpreadParticleHeatToSurroundings(in SurroundingData[] particleSurroundings)
        {
            //Check for things to burn
            //------------------------------------------------------------------//
            //[0 1 2]
            //[3 x 5]
            //[6 7 8]

            //Here we only want to check the cardinal directions so that only when fire is touching a tile can it transfer
            for (var i = 0; i < 9; i++)
            {
                //Skip My Particle
                if (i == 4)
                    continue;
                
                var data = particleSurroundings[i];
                if (data.IsValid == false || data.IsOccupied == false)
                    continue;

                ref var posParticle = ref _activeParticles[data.ParticleIndex];

                if (posParticle.CanBurn == false)
                    continue;
                
                //Dont want to double burn, DOUBLE BURN SHOULD ONLY HAPPEN WITH CARDINALS
                //if (posParticle.HasChangedTemp)
                //    continue;

                posParticle.CurrentTemperature++;
                posParticle.HasChangedTemp = true;

                CheckShouldChangeState(particleSurroundings, ref posParticle);

                /*//TODO Should probably increase the chance of burning over time, when back-to-back calls occur
                if (Random.Range(0, 100) > posParticle.ChanceToBurn)
                    continue;

                ParticleFactory.ConvertToFire(ref posParticle);*/
            }
        }

        private void EqualizeParticleTemperature(ref Particle particle)
        {
            particle.CurrentTemperature += particle.CurrentTemperature > ambientTemperature? -1 : 1;
                        
            particle.HasChangedTemp = true;
        }

        private void CheckShouldChangeState(in SurroundingData[] particleSurroundings, ref Particle particle)
        {
            //------------------------------------------------//

            bool HasAir(in SurroundingData[] particleSurroundings)
            {
                //Check if there is an empty space near the fire
                //------------------------------------------------//
                //This is to slow the spread of fire, forcing it to work outside in
                for (var i = 0; i < 9; i++)
                {
                    var data = particleSurroundings[i];
                    if (data.IsValid == false || data.IsOccupied == false)
                        continue;

                    return true;

                }

                return false;
            }

            //------------------------------------------------//
            var hasAir = HasAir(particleSurroundings);

            CheckShouldChangeState(hasAir, ref particle);
        }
        
        private void CheckShouldChangeState(in bool hasAir, ref Particle particle)
        {
            if(particle.HasChangedTemp == false)
                return;

            var readyToCombust = particle.CurrentTemperature >= particle.CombustionTemperature;

            if (readyToCombust)
            {
                switch (particle.Type)
                {
                    case Particle.TYPE.ICE:
                        ParticleFactory.ConvertTo(Particle.TYPE.WATER, ref particle);
                        break;
                    case Particle.TYPE.WATER:
                        ParticleFactory.ConvertTo(Particle.TYPE.STEAM, ref particle);
                        break;
                    case Particle.TYPE.STONE:
                        ParticleFactory.ConvertTo(Particle.TYPE.MOLTEN_STONE, ref particle);
                        particle.HasChangedTemp = true;
                        particle.IsSwapLocked = true;
                        break;
                    case Particle.TYPE.METAL:
                        ParticleFactory.ConvertTo(Particle.TYPE.MOLTEN_METAL, ref particle);
                        particle.HasChangedTemp = true;
                        particle.IsSwapLocked = true;
                        break;
                    case Particle.TYPE.WOOD when hasAir:
                    case Particle.TYPE.OIL when hasAir:
                        ParticleFactory.ConvertToAndMaintainMaterial(Particle.TYPE.FIRE, ref particle);
                        break;
                    //default:
                    //    throw new ArgumentOutOfRangeException();
                }
            }
            else
            {
                switch (particle.Type)
                {
                    case Particle.TYPE.STEAM:
                        if (Random.value >= 0.5f)
                            ParticleFactory.ConvertTo(Particle.TYPE.WATER, ref particle);
                        else
                            KillParticleImmediate(ref particle);
                        break;
                    case Particle.TYPE.MOLTEN_STONE:
                        ParticleFactory.ConvertTo(Particle.TYPE.STONE, ref particle);
                        particle.CurrentTemperature -= 50;
                        particle.HasChangedTemp = true;
                        particle.IsSwapLocked = true;
                        break;
                    case Particle.TYPE.MOLTEN_METAL:
                        ParticleFactory.ConvertTo(Particle.TYPE.METAL, ref particle);
                        particle.CurrentTemperature -= 50;
                        particle.HasChangedTemp = true;
                        particle.IsSwapLocked = true;
                        break;
                }
            }
            


        }

        private bool TryExtinguish(ref Particle waterParticle, ref Particle otherParticle)
        {
            if (waterParticle.Type != Particle.TYPE.WATER)
                return false;
            

            switch (otherParticle.Type)
            {
                case Particle.TYPE.FIRE:
                    KillParticleImmediate(ref otherParticle);
                    break;
                case Particle.TYPE.MOLTEN_STONE:
                    ParticleFactory.ConvertTo(Particle.TYPE.STONE, ref otherParticle, false);
                    otherParticle.CurrentTemperature -= 50;
                    otherParticle.HasChangedTemp = true;
                    otherParticle.IsSwapLocked = true;
                    break;
                case Particle.TYPE.MOLTEN_METAL:
                    ParticleFactory.ConvertTo(Particle.TYPE.METAL, ref otherParticle, false);
                    otherParticle.CurrentTemperature -= 50;
                    otherParticle.HasChangedTemp = true;
                    otherParticle.IsSwapLocked = true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(otherParticle.Type), otherParticle.Type, null);
            }
            
            ParticleFactory.ConvertTo(Particle.TYPE.STEAM, ref waterParticle, false);

            return true;
        }

        #endregion //Particle Temperature functions

        //Density Functions
        //============================================================================================================//

        #region Density Functions

        private bool CheckDidSwapParticleDensity(
            in bool useSwapLock,
            in bool occupied, 
            in int currentGridIndex, 
            in int newGridIndex,
            ref Particle selectedParticle, 
            ref Particle occupyingParticle)
        {
            //Check material densities
            //------------------------------------------------//
            if (occupied == false)
                return false;
            if (selectedParticle.IsSwapLocked || occupyingParticle.IsSwapLocked)
                return false;
            if (selectedParticle.HasDensity == false)
                return false;
            if (occupyingParticle.HasDensity == false || occupyingParticle.Type == selectedParticle.Type)
                return false;
                    
            var didSwap = ParticleDensityCheck(
                useSwapLock,
                currentGridIndex,
                newGridIndex,
                ref selectedParticle,
                ref occupyingParticle);

            return didSwap;

            //------------------------------------------------//
        }

        //TODO This might have to be generalized for Gases & Liquids
        private bool ParticleDensityCheck(
            in bool useSwapLock,
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

            if (useSwapLock)
            {
                fromParticle.IsSwapLocked = true;
                toParticle.IsSwapLocked = true;
            }

            return true;
        }

        #endregion //Density Functions

        //Swap Position Functions
        //============================================================================================================//

        #region Swap Position Functions

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
        private void SwapParticlePositions(in int fromGridIndex, in int toGridIndex,
            ref Particle fromParticle, ref Particle toParticle)
        {
            var fromGridPos = _gridPositions[fromGridIndex];
            var toGridPos = _gridPositions[toGridIndex];
            
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

        #endregion //Swap Position Functions

        //============================================================================================================//

        //FIXME This should be heat centric, not type centric
        private static int TypeCountAtCardinals(in Particle.TYPE particleType, in SurroundingData[] particleSurroundings)
        {
            //[0 1 2]
            //[3 x 5]
            //[6 7 8]
            var count = 0;
            //Here we only want to check the cardinal directions so that only when fire is touching a tile can it transfer
            for (var i = 1; i < 9; i+=2)
            {
                //Skip My Particle
                if (i == 4)
                    continue;
                
                var data = particleSurroundings[i];
                if(data.IsValid == false)
                {
                    //TODO Determine if considering offgrid is towards or against count
                    count++;
                    continue;
                }

                if (data.IsOccupied == false)
                    continue;

                if (data.Particle.Type != particleType)
                    continue;

                count++;
            }

            return count;
        }
        
        private static int HeatCountAtCardinals(in SurroundingData[] particleSurroundings)
        {
            //[0 1 2]
            //[3 x 5]
            //[6 7 8]
            var count = 0;
            //Here we only want to check the cardinal directions so that only when fire is touching a tile can it transfer
            for (var i = 1; i < 9; i+=2)
            {
                //Skip My Particle
                if (i == 4)
                    continue;
                
                var particleData = particleSurroundings[i];
                if(particleData.IsValid == false)
                {
                    //TODO Determine if considering offgrid is towards or against count
                    count++;
                    continue;
                }

                if (particleData.IsOccupied == false || particleData.Particle.SpreadsHeat == false)
                    continue;

                count++;
            }

            return count;
        }

        //Custom Particle Logic
        //============================================================================================================//

        private void CheckAcidSurroundings(in SurroundingData[] particleSurroundings)
        {
            //[0 1 2]
            //[3 x 5]
            //[6 7 8]
            int unoccupiedIndex = -1;
            bool didConvert = false;
            for (var i = 0; i < 9; i++)
            {
                if(i == 4)
                    continue;

                var data = particleSurroundings[i];
                
                if(data.IsValid == false)
                    continue;
                if (data.IsOccupied == false)
                {
                    if (unoccupiedIndex < 0)
                        unoccupiedIndex = i;
                    
                    continue;
                }
                
                if(data.Particle.Material != Particle.MATERIAL.SOLID && data.Particle.Material != Particle.MATERIAL.POWDER)
                    continue;
                
                if(data.Particle.Type == Particle.TYPE.ACID)
                    continue;

                if (Random.value > 0.0125f)
                    continue;

                ref var particle = ref _activeParticles[data.ParticleIndex];
                
                ParticleFactory.ConvertToAndMaintainMaterial(Particle.TYPE.ACID, ref particle);

                particle.Lifetime /= 3;
                didConvert = true;
            }

            if (didConvert && unoccupiedIndex >= 0)
            {
                var coordinate = GridHelper.IndexToVector2Int(unoccupiedIndex);
                SpawnParticle(Particle.TYPE.SMOKE, coordinate);
            }
            
            /*if(didConvert)
                KillParticleImmediate(ref _activeParticles[particleSurroundings[4].ParticleIndex]);*/
        }

        //Should Kill Function
        //============================================================================================================//

        private static bool CheckShouldKillParticle(ref Particle particle)
        {
            switch (particle.Type)
            {
                case Particle.TYPE.FIRE:
                case Particle.TYPE.ACID:
                    ParticleFactory.ConvertTo(Particle.TYPE.SMOKE, ref particle);
                    return false;
                default:
                    return true;
            }
        }

        //Debug Functions
        //============================================================================================================//

        public (bool legal, bool occupied, int gridIndex, GridPos gridPos, Particle particle) GetParticleAtCoordinate(in int xCoord, in int yCoord)
        {
            if (GridHelper.IsLegalCoordinate(xCoord, yCoord) == false)
                return (false, default, default, default, default);
            
            var gridIndex = GridHelper.CoordinateToIndex(xCoord, yCoord);
            var gridPos = _gridPositions[gridIndex];
            
            if(gridPos.IsOccupied == false)
                return (true, false, gridIndex, gridPos, null);

            return (true, true, gridIndex, gridPos, _activeParticles[gridPos.ParticleIndex]);
        }
        //============================================================================================================//
        
    }

    
}
