using System.Collections.Generic;
using System.IO;
using TShockAPI;
using Terraria;
using System.Linq;

namespace WorldEdit.Commands
{
    public class Scale : WECommand
    {
        private readonly bool _addition;
        private readonly int _scale;

        public Scale(TSPlayer plr, bool addition, int scale)
			: base(0, 0, 0, 0, plr)
		{
            _addition = addition;
			_scale = scale;
		}

		public override void Execute()
		{
			var clipboardPath = Tools.GetClipboardPath(plr.Account.ID);

			var data = Tools.LoadWorldData(clipboardPath);

            // Block clipboards bigger than the world
            if (_addition && ((long)data.Width * _scale > Main.maxTilesX
                || (long)data.Height * _scale > Main.maxTilesY))
            {
                plr.SendErrorMessage("Invalid amount!");
                return;
            }

            if (_addition)
            {
                try
                {
                    using (var writer = WorldSectionData.WriteHeader(
                        clipboardPath, 0, 0, data.Width * _scale, data.Height * _scale))
                    {
                        var r = new List<ITile>();
                        for (var i = 0; i < data.Width; i++)
                        {
                            for (var j = 0; j < data.Height; j++)
                            {
                                for (var a = 0; a < _scale; a++)
                                {
                                    writer.Write(data.Tiles[i, j]);
                                }
                                r.Add(data.Tiles[i, j]);

                                if (j != data.Height - 1)
                                {
                                    continue;
                                }

                                for (var a = 0; a < _scale - 1; a++)
                                {
                                    foreach (var t in r)
                                    {
                                        for (var b = 0; b < _scale; b++)
                                        {
                                            writer.Write(t);
                                        }
                                    }
                                }
                                r.Clear();
                            }
                        }
                    }
                }
                catch
                {
                    // Don't leave a partly written clipboard behind
                    File.Delete(clipboardPath);
                    throw;
                }
            }
            else
            {
                int _x = (data.Width % _scale), _y = (data.Height % _scale);
                int x = (data.Width / _scale), y = (data.Height / _scale);
                int width = ((_x == 0) ? x : (x + 1));
                int height = ((_y == 0) ? y : (y + 1));
                ITile[,] newData = new ITile[width, height];
                for (int i1 = 0; i1 < width; i1++)
                {
                    for (int j1 = 0; j1 < height; j1++)
                    {
                        List<ITile> Square = new List<ITile>();
                        for (int i2 = 0; i2 < _scale; i2++)
                        {
                            for (int j2 = 0; j2 < _scale; j2++)
                            {
                                Square.Add((((i1 * _scale + i2) < data.Width)
                                    && ((j1 * _scale + j2) < data.Height))
                                        ? data.Tiles[(i1 * _scale + i2), (j1 * _scale + j2)]
                                        : new Tile());
                            }
                        }
                        newData[i1, j1] = Square
                                         .GroupBy(g => g.type)
                                         .OrderByDescending(g => g.Count())
                                         .SelectMany(g => g)
                                         .First();
                    }
                }
                using (var writer = WorldSectionData.WriteHeader(clipboardPath, 0, 0, width, height))
                {
                    for (var i = 0; i < width; i++)
                    {
                        for (var j = 0; j < height; j++)
                        { writer.Write(newData[i, j]); }
                    }
                }
            }

			plr.SendSuccessMessage("Clipboard {0}creased by {1} times.", (_addition ? "in" : "de"), _scale);
		}
	}
}