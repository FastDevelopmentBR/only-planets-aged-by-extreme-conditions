using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
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
		public Dictionary<MyStringHash, MyStringHash> ExtraProtectionStages { get; set; }
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
				Echo("UpdateBeforeSimulation exception: " + e + e.InnerException);
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
			return Config.agingConfig.BlockSubtypeContainsBlackList.Any(blockSkin.Contains);
		}

		private void ProcessDamage()
		{
			try
			{
				foreach (var planet in _planets)
				{
					var sphere = new BoundingSphereD(planet.MyPlanet.PositionComp.GetPosition(), planet.MyPlanet.AverageRadius + planet.MyPlanet.AtmosphereAltitude);
					var topEntities = MyAPIGateway.Entities.GetTopMostEntitiesInSphere(ref sphere);

					if (topEntities.Count > 0)
					{
						var grids = topEntities.FindAll(x => x is IMyCubeGrid);

						if (grids.Count > 0)
						{
							grids.Shuffle();

							int entitiesAffected = 0;
							int maxEntitiesAffected = (grids.Count > 10) ? 10 : grids.Count;

							while (entitiesAffected < maxEntitiesAffected)
							{
								var entity = grids.ElementAt(entitiesAffected);
								var grid = entity as IMyCubeGrid;

								entitiesAffected++;

								if (grid != null)
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

									if (blocks.Count > 0)
									{
										blocks.Shuffle();

										int blocksAffected = 0;
										int maxBlocksAffected = (blocks.Count > 20) ? 20 : blocks.Count;

										while (blocksAffected < maxBlocksAffected)
										{
											var block = blocks.ElementAt(blocksAffected);
											blocksAffected++;

											if (IsBlackListedSkin(block.SkinSubtypeId.String))
												continue;

											if ((_random.NextDouble() < planet.AgingProbability)
												&& HasOpenFaces(block, grid, blocks.Count))
											{
												if (block.SkinSubtypeId == planet.AgingStages.Last())
												{
													if (!Config.agingConfig.AgingDamagesBlocks)
														continue;

													if (!Config.agingConfig.NoMercy && gridInternal.IsRespawnGrid)
														continue;

													_slowQueue.Enqueue(() => DamageBlock(block, gridInternal));
												}
												else
												{
													_slowQueue.Enqueue(() => AgingBlockPaint(block, gridInternal, planet));
												}
											}
										}
									}
								}
							}
						}
					}
				}
			}
			catch (Exception e)
			{
				Echo("ProcessDamage exception: " + e + e.InnerException);
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

			if (_planets.Count == 0)
				Echo("Configuration", "Clear");

			var entities = new HashSet<IMyEntity>();
			MyAPIGateway.Entities.GetEntities(entities, x => x is MyPlanet);

			foreach (var entitiy in entities)
			{
				MyPlanet myPlanet = (MyPlanet)entitiy;
				foreach (var planetConfig in Config.agingConfig.Planets)
				{
					if (myPlanet.StorageName.Contains(planetConfig.PlanetNameContains))
					{
						Dictionary<MyStringHash, MyStringHash> planetExtraProtectionStages = new Dictionary<MyStringHash, MyStringHash>();
						HashSet<MyStringHash> planetAgingStages = new HashSet<MyStringHash>();

						if (planetConfig.ExtraProtectionStages.Count > 0)
						{
							foreach (var stage in planetConfig.ExtraProtectionStages)
							{
								planetExtraProtectionStages.Add(
									MyStringHash.GetOrCompute(stage.OriginStage),
									MyStringHash.GetOrCompute(stage.NextStage)
								);
							}
						}

						if (planetConfig.AgingStages.Any())
						{
							foreach (var stage in planetConfig.AgingStages)
							{
								planetAgingStages.Add(MyStringHash.GetOrCompute(stage));
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
							ExtraProtectionStages = planetExtraProtectionStages,
							AgingStages = planetAgingStages
						});


					}
				}

			}

			if (_planets.Count > 0)
				Echo("Configuration", "Initialized");
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

		private void AgingBlockPaint(IMySlimBlock block, MyCubeGrid gridInternal, AgedPlanet planetConfig)
		{
			MyCube myCube;
			gridInternal.TryGetCube(block.Position, out myCube);
			if (myCube == null)
				return;

			// Decrease Extra Protection Stage
			var extraStages = planetConfig.ExtraProtectionStages;

			if (extraStages.Count > 0 && extraStages.ContainsKey(block.SkinSubtypeId))
			{
				MyStringHash nextStage;
				bool stageFound = extraStages.TryGetValue(block.SkinSubtypeId, out nextStage);

				if (stageFound)
				{
					Echo(gridInternal.Name + " - Decreasing Extra Protection", "NEW_STAGE = " + (nextStage.String == "" ? "Default" : nextStage.String));
					gridInternal.ChangeColorAndSkin(myCube.CubeBlock, skinSubtypeId: nextStage);
				}

				return;
			}

			// Apply Aging Stage
			if (block.SkinSubtypeId == planetConfig.AgingStages.Last())
			{
				Echo(gridInternal.Name + " - Block at Maximum Aging Stage");
			}
			else
			{
				var nextAgingStageIndex = 0;

				for (int i = 0; i < planetConfig.AgingStages.Count; i++)
				{
					if (block.SkinSubtypeId == planetConfig.AgingStages.ElementAt(i))
						nextAgingStageIndex = ++i;
				}

				var nextAgingStage = planetConfig.AgingStages.ElementAt(nextAgingStageIndex);

				Echo(gridInternal.Name + " - Go to Next Aging Stage", "NEW_AGING_STAGE = " + nextAgingStage);
				gridInternal.ChangeColorAndSkin(myCube.CubeBlock, skinSubtypeId: nextAgingStage);
			}
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
				Echo(gridInternal.Name + " - DecreaseMountLevel", "AGING_DAMAGE = " + AGING_DAMAGE);
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
						Echo("InvokeOnGameThread exception: " + e + e.InnerException);
					}
				});
			}
			catch (Exception e)
			{
				Echo("SafeInvoke exception: " + e + e.InnerException);
			}
		}

		private static void Echo(string msg1, object msg2 = null)
		{
			MyLog.Default.WriteLineAndConsole("Aged By Extreme Conditions - " + msg1 + ": " + msg2);
			if (DEBUG)
			{
				MyAPIGateway.Utilities.ShowMessage(msg1, msg2?.ToString());
			}
		}
	}

	static class MyExtensions
	{
		private static Random _random = new Random();

		public static void Shuffle<T>(this List<T> list)
		{
			int n = list.Count;
			while (n > 1)
			{
				n--;
				int k = _random.Next(n + 1);
				T value = list[k];
				list[k] = list[n];
				list[n] = value;
			}
		}
	}
}
