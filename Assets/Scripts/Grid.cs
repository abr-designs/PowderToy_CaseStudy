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
        private readonly struct GridLocationDetails
        {
            public static readonly GridLocationDetails Illegal = default;
            public static readonly GridLocationDetails LegalEmpty = new GridLocationDetails(true, default, default);
            
            public readonly bool IsLegal;
            public readonly bool IsOccupied;
            public readonly uint OccupierIndex;

            public GridLocationDetails(in bool isLegal, in bool isOccupied, in uint occupierIndex)
            {
                IsLegal = isLegal;
                IsOccupied = isOccupied;
                OccupierIndex = occupierIndex;
            }
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
            _activeParticles = new Particle[count];

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
            var newParticle = ParticleFactory.CreateParticle(particleType, newIndex, newX, newY);

            _activeParticles[newIndex] = newParticle;

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

            var particle = _activeParticles[gridPos.ParticleIndex];
            //We only want to queue deletion for things that move, solid things are safe to be deleted now.
            //This is because solid particles do not get updated the same way
            //FIXME once fire is added the above statement may need to change
            switch (particle.Material)
            {
                case Particle.MATERIAL.SOLID:
                    KillParticle(particle);
                    break;
                case Particle.MATERIAL.POWDER:
                case Particle.MATERIAL.LIQUID:
                case Particle.MATERIAL.GAS:
                    particle.KillNextTick = true;
                    _activeParticles[gridPos.ParticleIndex] = particle;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            gridPos.IsOccupied = false;
            _gridPositions[gridIndex] = gridPos;
            
            gridRequiresCleaning = true;
        }
        private void MarkParticleForDeath(ref Particle particle)
        {
            var x = particle.XCoord;
            var y = particle.YCoord;

            if(IsSpaceOccupied(x, y) == false)
                return;

            var gridIndex = GridHelper.CoordinateToIndex(x, y);
            var gridPos = _gridPositions[gridIndex];

            //var particle = _activeParticles[gridPos.ParticleIndex];
            //We only want to queue deletion for things that move, solid things are safe to be deleted now.
            //This is because solid particles do not get updated the same way
            //FIXME once fire is added the above statement may need to change
            switch (particle.Material)
            {
                case Particle.MATERIAL.SOLID:
                    KillParticle(particle);
                    break;
                case Particle.MATERIAL.POWDER:
                case Particle.MATERIAL.LIQUID:
                case Particle.MATERIAL.GAS:
                    particle.KillNextTick = true;
                    _activeParticles[gridPos.ParticleIndex] = particle;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            gridPos.IsOccupied = false;
            _gridPositions[gridIndex] = gridPos;
            
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

        private void KillParticle(in Particle particle)
        {
            _activeParticles[particle.Index] = Particle.Empty;
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
                    var particle = _activeParticles[(int)container[ii]];

                    //Check for napping particles OR ones that were marked as empty
                    if(particle.Asleep || particle.Type == Particle.TYPE.NONE)
                        continue;
                   
                    if (particle.HasLifeSpan && particle.Lifetime <= 0)
                        MarkParticleForDeath(ref particle);
                    
                    if (particle.KillNextTick)
                    {
                        KillParticle(particle);
                        continue;
                    }
                    bool didUpdate;

                    GridHelper.GetParticleSurroundings(particle.XCoord, particle.YCoord, ref _particleSurroundings);

                    if (particle.Type == Particle.TYPE.FIRE)
                        FireParticleBurnCheck(_particleSurroundings);

                    switch (particle.Material)
                    {
                        case Particle.MATERIAL.SOLID:
                            continue;
                        case Particle.MATERIAL.POWDER:
                            didUpdate = UpdatePowderParticle(_particleSurroundings, ref particle);
                            break;
                        case Particle.MATERIAL.LIQUID:
                            didUpdate = UpdateLiquidParticle(_particleSurroundings, ref particle);
                            break;
                        case Particle.MATERIAL.GAS:
                            didUpdate = UpdateGasParticle(_particleSurroundings, ref particle);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    if (particle.HasLifeSpan)
                    {
                        particle.Lifetime--;
                        didUpdate = true;
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
                var gridPos = _gridPositions[gridPosIndex];
                gridPos.ParticleIndex = newIndex;
                _gridPositions[gridPosIndex] = gridPos;

                particle.Index = newIndex;
                _activeParticles[newIndex] = particle;
                _activeParticles[i] = Particle.Empty;
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
                //Solid Particles do not move
                if(particle.Material == Particle.MATERIAL.SOLID)
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
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        private GridLocationDetails IsSpaceOccupiedDetailed(in int x, in int y)
        {
            if (GridHelper.IsLegalCoordinate(x, y) == false)
                return new GridLocationDetails(false, default, default);
            
            var gridIndex = GridHelper.CoordinateToIndex(x, y);

            if (_gridPositions[gridIndex].IsOccupied == false)
                return new GridLocationDetails(true, false, default);

            var particleIndex = _gridPositions[gridIndex].ParticleIndex;
            //var particle = _activeParticles[particleIndex];

            if (_activeParticles[particleIndex].KillNextTick)
                return new GridLocationDetails(true, false, default);
            
            return new GridLocationDetails(true, true, (uint)particleIndex);
        }
        
        private GridLocationDetails IsIndexOccupiedDetailed(in int index)
        {
            if (index < 0)
                return GridLocationDetails.Illegal;
            
            if (_gridPositions[index].IsOccupied == false)
                return GridLocationDetails.LegalEmpty;

            var particleIndex = _gridPositions[index].ParticleIndex;
            //var particle = _activeParticles[particleIndex];

            return _activeParticles[particleIndex].KillNextTick
                ? GridLocationDetails.LegalEmpty
                : new GridLocationDetails(true, true, (uint)particleIndex);
        }

        private bool IsSpaceOccupied(in int x, in int y)
        {
            //TODO Might need to apply the same fix as IsSpaceOccupiedDetailed() for grid space legality
            if (GridHelper.IsLegalCoordinate(x, y) == false)
                return true;

            var gridIndex = GridHelper.CoordinateToIndex(x, y);

            if (_gridPositions[gridIndex].IsOccupied == false)
                return false;

            var particleIndex = _gridPositions[gridIndex].ParticleIndex;
            var particle = _activeParticles[particleIndex];

            if (particle.KillNextTick)
                return false;
            
            return true;
        }
        
        private bool IsIndexOccupied(in int index)
        {
            //TODO Might need to apply the same fix as IsSpaceOccupiedDetailed() for grid space legality
            if (index < 0)
                return true;

            if (_gridPositions[index].IsOccupied == false)
                return false;

            //FIXME Do I still need this portion?
            var particleIndex = _gridPositions[index].ParticleIndex;
            var particle = _activeParticles[particleIndex];

            if (particle.KillNextTick)
                return false;
            
            return true;
        }

        //============================================================================================================//

        private bool UpdatePowderParticle(in int[] particleSurroundings, ref Particle particle)
        {
            //var originalCoordinate = particle.Coordinate;
            var originalX = particle.XCoord;
            var originalY = particle.YCoord;

            if (particle.Asleep)
                return false;

            //------------------------------------------------------------------//
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
                var gridLocationDetails = IsIndexOccupiedDetailed(newGridIndex);
                var spaceOccupied = gridLocationDetails.IsOccupied;
                var occupierIndex = gridLocationDetails.OccupierIndex;

                if (gridLocationDetails.IsLegal == false)
                    return false;

                Particle occupier = default;

                if (spaceOccupied)
                {
                    occupier = _activeParticles[occupierIndex];

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
                    _activeParticles[occupierIndex] = occupier;
                }
                else
                    _gridPositions[currentGridIndex] = GridPos.Empty;
                //------------------------------------------------------------------//


                _gridPositions[newGridIndex] = new GridPos
                {
                    IsOccupied = true,
                    ParticleIndex = myParticle.Index
                };
                _activeParticles[myParticle.Index] = myParticle;
                return true;
            }

            //------------------------------------------------------------------//
            //[0 1 2]
            //[3 x 5]
            //[6 7 8]
            var currentGridIndex = particleSurroundings[4];
            if (particleSurroundings[7] >= 0 && TrySetNewPosition(originalX, originalY - 1, particleSurroundings[7], currentGridIndex, ref particle))
                return true;
            if (particleSurroundings[6] >= 0 && TrySetNewPosition(originalX - 1, originalY - 1, particleSurroundings[6], currentGridIndex,
                    ref particle))
                return true;
            if (particleSurroundings[8] >= 0 && TrySetNewPosition(originalX + 1, originalY - 1, particleSurroundings[8], currentGridIndex,
                    ref particle))
                return true;

            return false;
        }

        //TODO Need to implement a position resolving function when the liquid particle needs to move
        private bool UpdateLiquidParticle(in int[] particleSurroundings, ref Particle particle)
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
            if (particleSurroundings[7] >= 0 && TrySetNewPosition(originalX, originalY - 1, particleSurroundings[7], originalIndex, ref particle))
                return true;
            if (particleSurroundings[6] >= 0 && TrySetNewPosition(originalX - 1, originalY - 1, particleSurroundings[6], originalIndex, ref particle))
                return true;
            if (particleSurroundings[8] >= 0 && TrySetNewPosition(originalX + 1, originalY - 1, particleSurroundings[8], originalIndex, ref particle))
                return true;
            if (particleSurroundings[3] >= 0 && TrySetNewPosition(originalX - 1, originalY, particleSurroundings[3], originalIndex, ref particle))
                return true;
            if (particleSurroundings[5] >= 0 && TrySetNewPosition(originalX + 1, originalY, particleSurroundings[5], originalIndex, ref particle))
                return true;

            return false;
        }

        private bool UpdateGasParticle(in int[] particleSurroundings, ref Particle particle)
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
                return true;
            if (particleSurroundings[0] >= 0 && TrySetNewPosition(originalX - 1, originalY + 1, particleSurroundings[0], originalIndex, ref particle))
                return true;
            if (particleSurroundings[2] >= 0 && TrySetNewPosition(originalX + 1, originalY + 1, particleSurroundings[2], originalIndex, ref particle))
                return true;
            if (particleSurroundings[3] >= 0 && TrySetNewPosition(originalX - 1, originalY, particleSurroundings[3], originalIndex, ref particle))
                return true;
            if (particleSurroundings[5] >= 0 && TrySetNewPosition(originalX + 1, originalY, particleSurroundings[5], originalIndex, ref particle))
                return true;

            return false;
        }

        //============================================================================================================//

        private void FireParticleBurnCheck(in int[] particleSurroundings)
        {
            //Check for things to burn
            //------------------------------------------------------------------//

            for (var i = 0; i < 8; i++)
            {
                var index = particleSurroundings[i];
                if (index < 0)
                    continue;

                var gridPos = _gridPositions[index];
                if (gridPos.IsOccupied == false)
                    continue;

                var posParticle = _activeParticles[gridPos.ParticleIndex];

                if (posParticle.CanBurn == false)
                    continue;

                //TODO Should probably increase the chance of burning over time, when back-to-back calls occur
                if (Random.Range(0, 100) > posParticle.ChanceToBurn)
                    continue;

                posParticle = ParticleFactory.ConvertToFire(posParticle);

                _activeParticles[gridPos.ParticleIndex] = posParticle;
            }
        }

        //============================================================================================================//
        
    }

    
}
