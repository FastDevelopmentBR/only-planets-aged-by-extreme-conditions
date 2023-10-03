using Sandbox.Game;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game;
using VRage.Game.Components;

namespace ExtremeConditions
{
	public struct ExtraProtectionStage
	{
		public string OriginStage;
		public string NextStage;
	}

	public struct Planet
	{
		public string PlanetNameContains;
		public double AgingRate;
		public bool OnlyAgedUnpoweredGrids;
		public List<ExtraProtectionStage> ExtraProtectionStages;
		public List<string> AgingStages;
	}

	public struct AgingConfig
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
		public static AgingConfig agingConfig = new AgingConfig()
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
					ExtraProtectionStages = new List<ExtraProtectionStage>()
					{
						new ExtraProtectionStage { OriginStage = "Battered_Armor", NextStage = "" },
						new ExtraProtectionStage { OriginStage = "Clean_Armor", NextStage = "" },
					},
					AgingStages = new List<string>()
					{
						"Mossy_Armor",
						"Frozen_Armor"
					},
				},
				new Planet {
					PlanetNameContains = "Pertam",
					AgingRate = 60,
					ExtraProtectionStages = new List<ExtraProtectionStage>()
					{
						new ExtraProtectionStage { OriginStage = "Battered_Armor", NextStage = "" },
						new ExtraProtectionStage { OriginStage = "Clean_Armor", NextStage = "" },
						new ExtraProtectionStage { OriginStage = "Silver_Armor", NextStage = "" },
						new ExtraProtectionStage { OriginStage = "Frozen_Armor", NextStage = "Mossy_Armor" },
						new ExtraProtectionStage { OriginStage = "Mossy_Armor", NextStage = "" },
					},
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
				if (MyAPIGateway.Utilities.FileExistsInWorldStorage(configFileName, typeof(AgingConfig)))
				{
					var textReader = MyAPIGateway.Utilities.ReadFileInWorldStorage(configFileName, typeof(AgingConfig));
					var configXml = textReader.ReadToEnd();
					textReader.Close();
					agingConfig = MyAPIGateway.Utilities.SerializeFromXML<AgingConfig>(configXml);
				}
				else
				{
					var textWriter = MyAPIGateway.Utilities.WriteFileInWorldStorage(configFileName, typeof(AgingConfig));
					textWriter.Write(MyAPIGateway.Utilities.SerializeToXML(agingConfig));
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
