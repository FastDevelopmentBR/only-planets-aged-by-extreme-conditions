﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;

/*
 *  Based on script by Rexxar.
 */

namespace ExtremeConditions
{
	public struct AgedPlanet
	{
		public MyPlanet MyPlanet;
		public double AgingProbability;
		public bool OnlyAgedUnpoweredGrids;
		public HashSet<MyStringHash> AgingStages { get; set; }
	}

	[MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
	public class ExtremeConditions : MySessionComponentBase
	{
		private const int UPDATE_RATE = 3600; //aging will apply every 1 minute
		private const float AGING_DAMAGE = 0.5f;
		private const bool DEBUG = false;

		private readonly Random _random = new Random();
		private bool _init;
		private HashSet<AgedPlanet> _planets = new HashSet<AgedPlanet>();
		private int _updateCount = 0;
		private bool _processing;
		private Queue<Action> _slowQueue = new Queue<Action>();

		public override void UpdateBeforeSimulation()
		{
			try
			{
				if (MyAPIGateway.Session == null)
					return;

				//server only please
				if (!(MyAPIGateway.Session.OnlineMode == MyOnlineModeEnum.OFFLINE || MyAPIGateway.Multiplayer.IsServer))
					return;

				if (!_init)
					Initialize();

				// Runs 1 queue action every 3 seconds.
				if (_updateCount % 180 == 0)
				{
					ProcessSlowQueue();
				}

				//update our list of planets every 59 seconds in case people paste new planets
				if (++_updateCount % 3540 == 0)
				{
					UpdatePlanetsList();
				}

				if (_updateCount % UPDATE_RATE != 0)
					return;

				if (_processing) //worker thread is busy
					return;

				_processing = true;
				MyAPIGateway.Parallel.Start(ProcessDamage);
			}
			catch (Exception e)
			{
				Echo("Aged By Extreme Conditions UpdateBeforeSimulation exception: " + e + e.InnerException);
			}
		}

		private int GetRandomIndex(int listCount)
		{
			if (listCount == 0) return 0;
			var randomNumberInRange = _random.Next(listCount);
			return (randomNumberInRange != 0) ? --randomNumberInRange : 0;
		}

		private bool IsBlackListedSkin(string blockSkin)
		{
			foreach (var blackListItem in Config.agingConfig.BlockSubtypeContainsBlackList)
			{
				var blackListed = blockSkin.Contains(blackListItem);
				// Echo("blackListItem: " + blackListItem + ". SkinSubtypeId: " + blockSkin + ". Blacklisted? " + blackListed.ToString());
				if (blackListed)
					return true;
			}

			return false;
		}

		private void ProcessDamage()
		{
			try
			{
				foreach (var planet in _planets)
				{
					var sphere = new BoundingSphereD(planet.MyPlanet.PositionComp.GetPosition(), planet.MyPlanet.AverageRadius + planet.MyPlanet.AtmosphereAltitude);

					var topEntities = MyAPIGateway.Entities.GetTopMostEntitiesInSphere(ref sphere);
					var entitiesAffected = 0;

					do
					{
						var selectedEntity = topEntities.ElementAt(GetRandomIndex(topEntities.Count));

						var grid = selectedEntity as IMyCubeGrid;
						if (grid?.Physics != null)
						{
							MyCubeGrid gridInternal = (MyCubeGrid)grid;

							if (gridInternal.Closed || gridInternal.MarkedForClose)
								continue;

							if (Config.agingConfig.OnlyAgedUnpoweredGrids && gridInternal.IsPowered)
							{
								if (planet.OnlyAgedUnpoweredGrids)
									continue;
							}

							if (gridInternal.Immune)
								continue;

							if (InSafeZone(grid))
								continue;

							if (IsInsideAirtightGrid(grid))
								continue;

							var blocks = new List<IMySlimBlock>();
							grid.GetBlocks(blocks);

							var dmgTries = 0;

							do
							{
								if (dmgTries == blocks.Count)
									break;

								var blockIndex = (blocks.Count < 5) ? dmgTries : GetRandomIndex(blocks.Count);
								var selectedBlock = blocks.ElementAt(blockIndex);
								dmgTries++;

								var blockTypeBlacklisted = IsBlackListedSkin(selectedBlock.SkinSubtypeId.String);

								if (blockTypeBlacklisted)
									continue;

								if ((_random.NextDouble() < planet.AgingProbability)
									&& HasOpenFaces(selectedBlock, grid, blocks.Count))
								{
									if (selectedBlock.SkinSubtypeId == planet.AgingStages.Last())
									{
										if (!Config.agingConfig.AgingDamagesBlocks)
											continue;

										if (!Config.agingConfig.NoMercy && gridInternal.IsRespawnGrid)
											continue;

										_slowQueue.Enqueue(() => DamageBlock(selectedBlock, gridInternal));
									}
									else
									{
										_slowQueue.Enqueue(() => AgingBlockPaint(selectedBlock, gridInternal, planet.AgingStages));
									}

									entitiesAffected++;
									break;
								}

								if (dmgTries == 5)
									break;
							} while (dmgTries < blocks.Count);
						}

						if (entitiesAffected == 5)
							break;
					} while (entitiesAffected < topEntities.Count);
				}
			}
			catch (Exception e)
			{
				Echo("Aged By Extreme Conditions Mechanics ProcessDamage exception: " + e + e.InnerException);
			}
			finally
			{
				_processing = false;
			}
		}

		private void Initialize()
		{
			_init = true;
			UpdatePlanetsList();
		}

		private void UpdatePlanetsList()
		{
			_planets.Clear();
			var entities = new HashSet<IMyEntity>();
			MyAPIGateway.Entities.GetEntities(entities, x => x is MyPlanet);
			foreach (var entitiy in entities)
			{
				MyPlanet myPlanet = (MyPlanet)entitiy;
				foreach (var planetConfig in Config.agingConfig.Planets)
				{
					if (myPlanet.StorageName.Contains(planetConfig.PlanetNameContains))
					{
						HashSet<MyStringHash> planetAgingStages = new HashSet<MyStringHash>();

						if (planetConfig.AgingStages.Count > 0)
						{
							for (int i = 0; i < planetConfig.AgingStages.Count; i++)
							{
								planetAgingStages.Add(MyStringHash.GetOrCompute(planetConfig.AgingStages.ElementAt(i)));
							}
						}
						else
						{
							planetAgingStages = new HashSet<MyStringHash>() {
								MyStringHash.GetOrCompute("Rusty_Armor"),
								MyStringHash.GetOrCompute("Heavy_Rust_Armor")
							};
						}

						_planets.Add(new AgedPlanet()
						{
							MyPlanet = myPlanet,
							//3600 - game ticks per minute
							AgingProbability = UPDATE_RATE / (3600 * planetConfig.AgingRate),
							OnlyAgedUnpoweredGrids = planetConfig.OnlyAgedUnpoweredGrids,
							AgingStages = planetAgingStages
						});
					}
				}
			}
		}

		private bool HasOpenFaces(IMySlimBlock block, IMyCubeGrid grid, int blocksInGrid)
		{
			// Not possible to cover all sides without at least 6 blocks
			if (blocksInGrid <= 6)
				return true;

			List<Vector3I> neighbourPositions = new List<Vector3I>
			{
				block.Max + new Vector3I(1,0,0),
				block.Max + new Vector3I(0,1,0),
				block.Max + new Vector3I(0,0,1),
				block.Min - new Vector3I(1,0,0),
				block.Min - new Vector3I(0,1,0),
				block.Min - new Vector3I(0,0,1)
			};

			foreach (Vector3I position in neighbourPositions)
			{
				//MyVisualScriptLogicProvider.ShowNotification("Position: " + position, 3000);
				if (grid.GetCubeBlock(position) != null)
				{
					//MyVisualScriptLogicProvider.ShowNotification("Found neibor block", 1000);
					continue;
				}
				if (grid.IsRoomAtPositionAirtight(position))
				{
					//MyVisualScriptLogicProvider.ShowNotification("Found neibor airtigh", 1000);
					continue;
				}
				return true;
			}
			return false;
		}

		private static bool IsInsideAirtightGrid(IMyEntity grid)
		{
			if (grid?.Physics == null)
				return false;

			BoundingSphereD sphere = grid.WorldVolume;
			List<IMyEntity> entities = MyAPIGateway.Entities.GetTopMostEntitiesInSphere(ref sphere);

			foreach (IMyEntity entity in entities)
			{
				if (entity == null)
					continue;

				if (entity.EntityId == grid.EntityId)
					continue;

				var parentGrid = entity as IMyCubeGrid;
				if (parentGrid == null)
					continue;
				if (parentGrid.IsRoomAtPositionAirtight(parentGrid.WorldToGridInteger(sphere.Center)))
				{
					return true;
				}
			}
			return false;
		}

		public static bool InSafeZone(IMyEntity ent)
		{
			return !MySessionComponentSafeZones.IsActionAllowed((MyEntity)ent, CastHax(MySessionComponentSafeZones.AllowedActions, 0x1));
		}

		public static T CastHax<T>(T typeRef, object castObj) => (T)castObj;

		private void AgingBlockPaint(IMySlimBlock block, MyCubeGrid gridInternal, HashSet<MyStringHash> agingStages)
		{
			MyCube myCube;
			gridInternal.TryGetCube(block.Position, out myCube);
			if (myCube == null)
				return;

			var nextAgingState = 0;

			for (int i = 0; i < agingStages.Count; i++)
			{
				if (block.SkinSubtypeId == agingStages.ElementAt(i) && block.SkinSubtypeId != agingStages.Last())
					nextAgingState = ++i;
			}

			Echo(gridInternal.Name + " - Go to Next Aging Stage.", "NEW_AGING_STAGE = " + agingStages.ElementAt(nextAgingState));
			gridInternal.ChangeColorAndSkin(myCube.CubeBlock, skinSubtypeId: agingStages.ElementAt(nextAgingState));
		}

		private void DamageBlock(IMySlimBlock block, MyCubeGrid gridInternal)
		{
			if (block.IsFullyDismounted)
			{
				//TODO which faster? any difference?
				//grid.RemoveBlock(block, true);
				Echo(gridInternal.Name, "Grid Removed");
				gridInternal.RazeBlock(block.Position);
			}
			else
			{
				block?.DecreaseMountLevel(AGING_DAMAGE, null, true);
				Echo(gridInternal.Name + " - DecreaseMountLevel.", "AGING_DAMAGE = " + AGING_DAMAGE);
			}
		}

		private void ProcessSlowQueue()
		{
			if (_slowQueue.Count == 0)
				return;

			Action action;
			if (!_slowQueue.TryDequeue(out action))
				return;

			SafeInvoke(action);
		}

		//wrap invoke in try/catch so we don't crash on unexpected error
		//what we're doing isn't critical, so don't bother logging the errors
		private void SafeInvoke(Action action)
		{
			try
			{
				//action();
				MyAPIGateway.Utilities.InvokeOnGameThread(() =>
				{
					try
					{
						action();
					}
					catch (Exception e)
					{
						Echo("Age by Extreme Conditions InvokeOnGameThread exception: " + e + e.InnerException);
					}
				});
			}
			catch (Exception e)
			{
				Echo("Aged By Extreme Conditions SafeInvoke exception: " + e + e.InnerException);
			}
		}

		private static void Echo(string msg1, object msg2 = null)
		{
			MyLog.Default.WriteLineAndConsole(msg1 + ": " + msg2);
			if (DEBUG)
			{
				MyAPIGateway.Utilities.ShowMessage(msg1, msg2?.ToString());
			}
		}
	}
}
