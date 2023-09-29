using System;
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
	public struct RustyPlanet
	{
		public MyPlanet MyPlanet;
		public double RustProbability;
		public bool OnlyAgedUnpoweredGrids;
		public HashSet<MyStringHash> AgingStages { get; set; }
	}

	[MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
	public class ExtremeConditions : MySessionComponentBase
	{
		private const int UPDATE_RATE = 3600; //rust will apply every 1 minute
		private const float RUST_DAMAGE = 0.5f;
		private const bool DEBUG = false;

		private readonly Random _random = new Random();
		private bool _init;
		private HashSet<RustyPlanet> _planets = new HashSet<RustyPlanet>();
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
				Echo("RUST Mechanics UpdateBeforeSimulation exception: " + e + e.InnerException);
			}
		}

		private void ProcessDamage()
		{
			try
			{
				foreach (var planet in _planets)
				{
					var sphere = new BoundingSphereD(planet.MyPlanet.PositionComp.GetPosition(), planet.MyPlanet.AverageRadius + planet.MyPlanet.AtmosphereAltitude);

					var topEntities = MyAPIGateway.Entities.GetTopMostEntitiesInSphere(ref sphere);
					var entitiesDamaged = 0;

					do
					{
						var randomEntityIndex = _random.Next((topEntities.Count - 1));
						var selectedEntity = topEntities.ElementAt(randomEntityIndex);

						var grid = selectedEntity as IMyCubeGrid;
						if (grid?.Physics != null)
						{
							MyCubeGrid gridInternal = (MyCubeGrid)grid;

							if (gridInternal.Closed || gridInternal.MarkedForClose)
								continue;

							if (Config.rustConfig.OnlyAgedUnpoweredGrids && gridInternal.IsPowered)
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

							var gridBlockAged = false;
							var dmgTries = 0;

							do
							{
								if (blocks.Count <= 5 && dmgTries == blocks.Count)
									break;

								var blockIndex = (blocks.Count <= 5) ? dmgTries : _random.Next((blocks.Count - 1));
								var selectedBlock = blocks.ElementAt(blockIndex);

								if (_random.NextDouble() < planet.RustProbability &&
									!Config.rustConfig.BlockSubtypeContainsBlackList.Any(selectedBlock.BlockDefinition.Id.SubtypeName.Contains) &&
									HasOpenFaces(selectedBlock, grid, blocks.Count))
								{
									if (selectedBlock.SkinSubtypeId == planet.AgingStages.Last() && Config.rustConfig.AgingDamagesBlocks)
									{
										if (!Config.rustConfig.NoMercy && gridInternal.IsRespawnGrid)
											continue;

										_slowQueue.Enqueue(() => DamageBlock(selectedBlock, gridInternal));
									}
									else
									{
										_slowQueue.Enqueue(() => RustBlockPaint(selectedBlock, gridInternal, planet.AgingStages));
									}

									gridBlockAged = true;
									entitiesDamaged++;
								}
								dmgTries++;
							} while ((gridBlockAged == false) || ((blocks.Count <= 5) && (dmgTries < blocks.Count)) || ((blocks.Count > 5) && (dmgTries <= 5)));
						}
					} while (entitiesDamaged < 5);
				}
			}
			catch (Exception e)
			{
				Echo("RUST Mechanics ProcessDamage exception: " + e + e.InnerException);
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
				foreach (var planetConfig in Config.rustConfig.Planets)
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

						_planets.Add(new RustyPlanet()
						{
							MyPlanet = myPlanet,
							//3600 - game ticks per minute
							RustProbability = UPDATE_RATE / (3600 * planetConfig.AgingRate),
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

		private void RustBlockPaint(IMySlimBlock block, MyCubeGrid gridInternal, HashSet<MyStringHash> agingStages)
		{
			MyCube myCube;
			gridInternal.TryGetCube(block.Position, out myCube);
			if (myCube == null)
				return;

			var nextAgingState = 0;

			for (int i = 0; i < agingStages.Count; i++)
			{
				if (block.SkinSubtypeId == agingStages.ElementAt(i))
					nextAgingState = ++i;
			}

			Echo(gridInternal.Name + " - Go to Next Rust Stage.", "NEW_AGING_STAGE = " + agingStages.ElementAt(nextAgingState));
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
				block?.DecreaseMountLevel(RUST_DAMAGE, null, true);
				Echo(gridInternal.Name + " - DecreaseMountLevel.", "RUST_DAMAGE = " + RUST_DAMAGE);
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
						Echo("RUST Mechanics InvokeOnGameThread exception: " + e + e.InnerException);
					}
				});
			}
			catch (Exception e)
			{
				Echo("RUST Mechanics SafeInvoke exception: " + e + e.InnerException);
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
