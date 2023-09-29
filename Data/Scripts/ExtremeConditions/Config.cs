using Sandbox.Game;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game;
using VRage.Game.Components;

namespace ExtremeConditions
{
	public struct Planet
	{
		public string PlanetNameContains;
		public double AgingRate;
		public bool OnlyAgedUnpoweredGrids;
		public List<string> AgingStages;
	}

	public struct RustConfig
	{
		public bool OnlyAgedUnpoweredGrids;
		public bool AgingDamagesBlocks;
		public bool NoMercy;
		public List<Planet> Planets;
		public List<string> BlockSubtypeContainsBlackList;
	}

	[MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
	public class Config : MySessionComponentBase
	{
		public static RustConfig rustConfig = new RustConfig()
		{
			OnlyAgedUnpoweredGrids = false,
			AgingDamagesBlocks = false,
			NoMercy = false,
			Planets = new List<Planet>()
			{
				new Planet {
					PlanetNameContains = "Earth",
					AgingRate = 300,
					OnlyAgedUnpoweredGrids = true
				},
				new Planet {
					PlanetNameContains = "Alien",
					AgingRate = 180,
					AgingStages = new List<string>()
					{
						"Mossy_Armor",
						"Rusty_Armor",
						"Heavy_Rust_Armor"
					},
				},
				new Planet {
					PlanetNameContains = "Triton",
					AgingRate = 100,
					AgingStages = new List<string>()
					{
						"Mossy_Armor",
						"Frozen_Armor"
					},
				},
				new Planet {
					PlanetNameContains = "Pertam",
					AgingRate = 60,
					AgingStages = new List<string>()
					{
						"Dust_Armor",
						"Rusty_Armor",
						"Heavy_Rust_Armor"
					}
				},
				new Planet {
					PlanetNameContains = "Venus",
					AgingRate = 50
				},
			},
			BlockSubtypeContainsBlackList = new List<string>()
			{
				"Concrete",
				"Wood"
			}
		};

		public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
		{
			try
			{
				string configFileName = "config.xml";
				if (MyAPIGateway.Utilities.FileExistsInWorldStorage(configFileName, typeof(RustConfig)))
				{
					var textReader = MyAPIGateway.Utilities.ReadFileInWorldStorage(configFileName, typeof(RustConfig));
					var configXml = textReader.ReadToEnd();
					textReader.Close();
					rustConfig = MyAPIGateway.Utilities.SerializeFromXML<RustConfig>(configXml);
				}
				else
				{
					var textWriter = MyAPIGateway.Utilities.WriteFileInWorldStorage(configFileName, typeof(RustConfig));
					textWriter.Write(MyAPIGateway.Utilities.SerializeToXML(rustConfig));
					textWriter.Flush();
					textWriter.Close();
				}
			}
			catch (Exception e)
			{
				//MyAPIGateway.Utilities.ShowMessage("ExtremeConditions", "Exception: " + e);
			}
		}
	}
}
