using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.Tile_Entities;
using Terraria.ID;
using TShockAPI;

namespace WorldEdit
{
	public sealed class WorldSectionData
	{
		public IList<SignData> Signs;

		public IList<ChestData> Chests;

		public IList<DisplayItemData> ItemFrames;

        public IList<LogicSensorData> LogicSensors;

        public IList<PositionData> TrainingDummies;

		public IList<DisplayItemData> WeaponsRacks;

		public IList<PositionData> TeleportationPylons;

		public IList<DisplayDollData> DisplayDolls;

		public IList<DisplayItemsData> HatRacks;

		public IList<DisplayItemData> FoodPlatters;

		public IList<DisplayItemData> DeadCellsDisplayJars;

		public IList<ItemTypeData> KiteAnchors;

		public IList<ItemTypeData> CritterAnchors;

		public ITile[,] Tiles;

		public int Width;

		public int Height;

		public int X;

		public int Y;

		public WorldSectionData(int width, int height)
		{
			Width = width;
			Height = height;

			Signs = new List<SignData>();
			Chests = new List<ChestData>();
			ItemFrames = new List<DisplayItemData>();
            LogicSensors = new List<LogicSensorData>();
			TrainingDummies = new List<PositionData>();
			WeaponsRacks = new List<DisplayItemData>();
			TeleportationPylons = new List<PositionData>();
			DisplayDolls = new List<DisplayDollData>();
			HatRacks = new List<DisplayItemsData>();
			FoodPlatters = new List<DisplayItemData>();
			DeadCellsDisplayJars = new List<DisplayItemData>();
			KiteAnchors = new List<ItemTypeData>();
			CritterAnchors = new List<ItemTypeData>();
			Tiles = new ITile[width, height];
		}

		public void ProcessTile(ITile tile, int x, int y)
		{
			Tiles[x, y] = new Tile();
			if (tile != null)
				Tiles[x, y].CopyFrom(tile);

			if (!tile.active())
			{
				return;
			}

			var actualX = x + X;
			var actualY = y + Y;

			switch (tile.type)
			{
				case TileID.Signs:
				case TileID.AnnouncementBox:
				case TileID.Tombstones:
					if (tile.frameX % 36 == 0 && tile.frameY == 0)
					{
						var id = Sign.ReadSign(actualX, actualY, false);
						if (id != -1)
						{
							Signs.Add(new SignData()
							{
								Text = Main.sign[id].text,
								X = x,
								Y = y
							});
						}
					}
					break;
				case TileID.ItemFrame:
					if (tile.frameX % 36 == 0 && tile.frameY == 0)
					{
						var id = TEItemFrame.Find(actualX, actualY);
						if (id != -1)
						{
							var frame = (TEItemFrame)TileEntity.ByID[id];
							ItemFrames.Add(new DisplayItemData()
							{
								Item = new NetItem(frame.item.type, frame.item.stack, frame.item.prefix),
								X = x,
								Y = y
							});
						}
					}
					break;
				case TileID.Containers:
				case TileID.Dressers:
					if (tile.frameX % 36 == 0 && tile.frameY == 0)
					{
						var id = Chest.FindChest(actualX, actualY);
						if (id != -1)
						{
							var chest = Main.chest[id];
							if (chest.item != null)
							{
								var items = chest.item.Select(item => new NetItem(item.type, item.stack, item.prefix)).ToArray();
								Chests.Add(new ChestData()
								{
									Items = items,
									X = x,
									Y = y
								});
							}
						}
					}
                    break;
                case TileID.LogicSensor:
                    {
                        var id = TELogicSensor.Find(actualX, actualY);
                        if (id != -1)
                        {
                            var sensor = (TELogicSensor)TileEntity.ByID[id];
                            LogicSensors.Add(new LogicSensorData()
							{
                                Type = sensor.logicCheck,
                                X = x,
                                Y = y
                            });
                        }
                        break;
                    }
                case TileID.TargetDummy:
                    if (tile.frameX % 36 == 0 && tile.frameY == 0)
                    {
                        var id = TETrainingDummy.Find(actualX, actualY);
                        if (id != -1)
                        {
                            TrainingDummies.Add(new PositionData()
							{
                                X = x,
                                Y = y
                            });
                        }
                    }
                    break;
				case TileID.WeaponsRack2:
					if (tile.frameY == 0 && tile.frameX % 54 == 0)
					{
						var id = TEWeaponsRack.Find(actualX, actualY);
						if (id != -1)
						{
							var rack = (TEWeaponsRack)TileEntity.ByID[id];
							WeaponsRacks.Add(new DisplayItemData()
							{
								Item = new NetItem(rack.item.type, rack.item.stack, rack.item.prefix),
								X = x,
								Y = y,
							});
						}
					}
					break;
				case TileID.TeleportationPylon:
					if (tile.frameY == 0 && tile.frameX % 54 == 0)
					{
						var id = TETeleportationPylon.Find(actualX, actualY);
						if (id != -1)
						{
							TeleportationPylons.Add(new PositionData()
							{
								X = x,
								Y = y
							});
						}
					}
				break;
				case TileID.DisplayDoll:
					if (tile.frameY == 0 && tile.frameX % 36 == 0)
					{
						var id = TEDisplayDoll.Find(actualX, actualY);
						if (id != -1)
						{
							var doll = (TEDisplayDoll)TileEntity.ByID[id];
							DisplayDolls.Add(new DisplayDollData()
							{
								Items = doll._equip.Select(i => new NetItem(i.type, i.stack, i.prefix)).ToArray(),
								Dyes = doll._dyes.Select(i => new NetItem(i.type, i.stack, i.prefix)).ToArray(),
								Misc = doll._misc.Select(i => new NetItem(i.type, i.stack, i.prefix)).ToArray(),
								Pose = doll._pose,
								X = x,
								Y = y
							});
						}
					}
					break;
				case TileID.HatRack:
					if (tile.frameY == 0 && tile.frameX % 54 == 0)
					{
						var id = TEHatRack.Find(actualX, actualY);
						if (id != -1)
						{
							var rack = (TEHatRack)TileEntity.ByID[id];
							HatRacks.Add(new DisplayItemsData()
							{
								Items = rack._items.Select(i => new NetItem(i.type, i.stack, i.prefix)).ToArray(),
								Dyes = rack._dyes.Select(i => new NetItem(i.type, i.stack, i.prefix)).ToArray(),
								X = x,
								Y = y
							});
						}
					}
					break;
				case TileID.FoodPlatter:
					{
						var id = TEFoodPlatter.Find(actualX, actualY);
						if (id != -1)
						{
							var platter = (TEFoodPlatter)TileEntity.ByID[id];
							FoodPlatters.Add(new DisplayItemData()
							{
								Item = new NetItem(platter.item.type, platter.item.stack, platter.item.prefix),
								X = x,
								Y = y
							});
						}
					}
					break;
				case TileID.DeadCellsDisplayJar:
					if (tile.frameX % 36 == 0 && tile.frameY == 0)
					{
						if (TileEntity.ByPosition.TryGetValue(new Point16(actualX, actualY), out var jarEntity)
							&& jarEntity is TEDeadCellsDisplayJar jar)
						{
							DeadCellsDisplayJars.Add(new DisplayItemData()
							{
								Item = new NetItem(jar.item.type, jar.item.stack, jar.item.prefix),
								X = x,
								Y = y
							});
						}
					}
					break;
				case TileID.KiteAnchor:
					{
						if (TileEntity.ByPosition.TryGetValue(new Point16(actualX, actualY), out var kiteEntity)
							&& kiteEntity is TEKiteAnchor kite)
						{
							KiteAnchors.Add(new ItemTypeData()
							{
								ItemType = GetLeashedItemType(kite),
								X = x,
								Y = y
							});
						}
					}
					break;
				case TileID.CritterAnchor:
					{
						if (TileEntity.ByPosition.TryGetValue(new Point16(actualX, actualY), out var critterEntity)
							&& critterEntity is TECritterAnchor critter)
						{
							CritterAnchors.Add(new ItemTypeData()
							{
								ItemType = GetLeashedItemType(critter),
								X = x,
								Y = y
							});
						}
					}
					break;
			}
		}

		private void WriteInner(BinaryWriter writer)
		{
			for (int i = 0; i < Width; i++)
				for (int j = 0; j < Height; j++)
					writer.Write((ITile)Tiles[i, j]);

			writer.Write((int)Signs.Count);
			foreach (var sign in Signs)
				sign.Write(writer);

			writer.Write((int)Chests.Count);
			foreach (var chest in Chests)
				chest.Write(writer);

			writer.Write((int)ItemFrames.Count);
			foreach (var itemFrame in ItemFrames)
				itemFrame.Write(writer);

            writer.Write((int)LogicSensors.Count);
            foreach (var logicSensor in LogicSensors)
				logicSensor.Write(writer);

            writer.Write((int)TrainingDummies.Count);
			foreach (var targetDummy in TrainingDummies)
				targetDummy.Write(writer);

			writer.Write((int)WeaponsRacks.Count);
			foreach (var weaponRack in WeaponsRacks)
				weaponRack.Write(writer);

			writer.Write((int)TeleportationPylons.Count);
			foreach (var teleportationPylon in TeleportationPylons)
				teleportationPylon.Write(writer);

			writer.Write((int)DisplayDolls.Count);
			foreach (var displayDoll in DisplayDolls)
				displayDoll.Write(writer);

			writer.Write((int)HatRacks.Count);
			foreach (var hatRack in HatRacks)
				hatRack.Write(writer);

			writer.Write((int)FoodPlatters.Count);
			foreach (var foodPlatter in FoodPlatters)
				foodPlatter.Write(writer);

			writer.Write((int)DeadCellsDisplayJars.Count);
			foreach (var jar in DeadCellsDisplayJars)
				jar.Write(writer);

			writer.Write((int)KiteAnchors.Count);
			foreach (var kiteAnchor in KiteAnchors)
				kiteAnchor.Write(writer);

			writer.Write((int)CritterAnchors.Count);
			foreach (var critterAnchor in CritterAnchors)
				critterAnchor.Write(writer);
		}

		public void Write(Stream stream)
		{
			using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
				WriteHeader(writer);
			using (var writer = new BinaryWriter(new BufferedStream(new GZipStream(stream,
					CompressionMode.Compress), Tools.BUFFER_SIZE)))
				WriteInner(writer);
		}

		public void Write(string filePath)
		{
			using (var writer = WriteHeader(filePath, X, Y, Width, Height))
				WriteInner(writer);
		}

		public void WriteHeader(BinaryWriter writer)
		{
			writer.Write((int)4);
			writer.Write((int)0);
            writer.Write((int)X);
            writer.Write((int)Y);
			writer.Write((int)Width);
			writer.Write((int)Height);
		}

		public static BinaryWriter WriteHeader(string filePath, int x, int y, int width, int height)
		{
			Stream stream = File.Open(filePath, FileMode.Create);
			using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
			{
				writer.Write((int)4);
				writer.Write((int)0);
                writer.Write((int)x);
				writer.Write((int)y);
				writer.Write((int)width);
				writer.Write((int)height);
			}
			return new BinaryWriter(
					new BufferedStream(
						new GZipStream(stream, CompressionMode.Compress), Tools.BUFFER_SIZE));
		}

		public struct DisplayItemData
		{
			public int X;

			public int Y;

			public NetItem Item;

			public void Write(BinaryWriter writer)
			{
				writer.Write((int)X);
				writer.Write((int)Y);
				writer.Write((NetItem)Item);
			}

			public static DisplayItemData Read(BinaryReader reader) =>
				new DisplayItemData()
				{
					X = reader.ReadInt32(),
					Y = reader.ReadInt32(),
					Item = reader.ReadNetItem()
				};
		}

		public struct DisplayItemsData
		{
			public int X;

			public int Y;

			public NetItem[] Items;

			public NetItem[] Dyes;

			public void Write(BinaryWriter writer)
			{
				writer.Write((int)X);
				writer.Write((int)Y);
				writer.Write((NetItem[])Items);
				writer.Write((NetItem[])Dyes);
			}

			public static DisplayItemsData Read(BinaryReader reader) =>
				new DisplayItemsData()
				{
					X = reader.ReadInt32(),
					Y = reader.ReadInt32(),
					Items = reader.ReadNetItems(),
					Dyes = reader.ReadNetItems()
				};
		}

		public struct DisplayDollData
		{
			public int X;

			public int Y;

			public NetItem[] Items;

			public NetItem[] Dyes;

			public NetItem[] Misc;

			public byte Pose;

			public void Write(BinaryWriter writer)
			{
				writer.Write((int)X);
				writer.Write((int)Y);
				writer.Write((NetItem[])Items);
				writer.Write((NetItem[])Dyes);
				writer.Write((NetItem[])Misc);
				writer.Write((byte)Pose);
			}

			public static DisplayDollData Read(BinaryReader reader) =>
				new DisplayDollData()
				{
					X = reader.ReadInt32(),
					Y = reader.ReadInt32(),
					Items = reader.ReadNetItems(),
					Dyes = reader.ReadNetItems(),
					Misc = reader.ReadNetItems(),
					Pose = reader.ReadByte()
				};
		}

		public struct PositionData
		{
			public int X;

			public int Y;

			public void Write(BinaryWriter writer)
			{
				writer.Write((int)X);
				writer.Write((int)Y);
			}

			public static PositionData Read(BinaryReader reader) =>
				new PositionData()
				{
					X = reader.ReadInt32(),
					Y = reader.ReadInt32()
				};
		}

        public struct LogicSensorData
        {
            public int X;

            public int Y;

            public TELogicSensor.LogicCheckType Type;

			public void Write(BinaryWriter writer)
			{
				writer.Write((int)X);
				writer.Write((int)Y);
				writer.Write((int)Type);
			}

			public static LogicSensorData Read(BinaryReader reader) =>
				new LogicSensorData()
				{
					X = reader.ReadInt32(),
					Y = reader.ReadInt32(),
					Type = (TELogicSensor.LogicCheckType)reader.ReadInt32()
				};
        }

		public struct ChestData
		{
			public int X;

			public int Y;

			public NetItem[] Items;

			public void Write(BinaryWriter writer)
			{
				writer.Write((int)X);
				writer.Write((int)Y);
				writer.Write((NetItem[])Items);
			}

			public static ChestData Read(BinaryReader reader) =>
				new ChestData()
				{
					X = reader.ReadInt32(),
					Y = reader.ReadInt32(),
					Items = reader.ReadNetItems()
				};
		}

		public struct SignData
		{
			public int X;

			public int Y;

			public string Text;

			public void Write(BinaryWriter writer)
			{
				writer.Write((int)X);
				writer.Write((int)Y);
				writer.Write((string)Text);
			}

			public static SignData Read(BinaryReader reader) =>
				new SignData()
				{
					X = reader.ReadInt32(),
					Y = reader.ReadInt32(),
					Text = reader.ReadString()
				};
		}

		public struct ItemTypeData
		{
			public int X;

			public int Y;

			public int ItemType;

			public void Write(BinaryWriter writer)
			{
				writer.Write((int)X);
				writer.Write((int)Y);
				writer.Write((int)ItemType);
			}

			public static ItemTypeData Read(BinaryReader reader) =>
				new ItemTypeData()
				{
					X = reader.ReadInt32(),
					Y = reader.ReadInt32(),
					ItemType = reader.ReadInt32()
				};
		}

		private static int GetLeashedItemType(TELeashedEntityAnchorWithItem entity)
		{
			using var ms = new MemoryStream();
			using var bw = new BinaryWriter(ms);
			entity.WriteExtraData(bw, false);
			ms.Position = 0;
			using var br = new BinaryReader(ms);
			return (int)br.ReadInt16();
		}
	}
}
