using System;
using Terraria;
using TShockAPI;
using WorldEdit.Expressions;

#nullable enable

namespace WorldEdit.Commands
{
	public enum CoatingKind {
		RemoveEcho,
		AddEcho,
		RemoveIlluminant,
		AddIlluminant,
		RemoveAll,
	}
	public class Coat : WECommand
	{

		private CoatingKind kind;
		private Expression? expression;
		private bool walls;

		public Coat(int x, int y, int x2, int y2, MagicWand magicWand, TSPlayer plr, CoatingKind kind, bool walls, Expression? expression)
			: base(x, y, x2, y2, magicWand, plr)
		{
			this.kind = kind;
			this.expression = expression;
			this.walls = walls;
		}

		public override void Execute()
		{
			if (!CanUseCommand()) { return; }
			Tools.PrepareUndo(x, y, x2, y2, plr);
			int edits = 0;
			Func<ITile, bool> condition = (kind, walls) switch {
				(CoatingKind.RemoveEcho, false) => (tile) => tile.invisibleBlock(),
				(CoatingKind.RemoveIlluminant, false) => (tile) => tile.fullbrightBlock(),
				(CoatingKind.AddEcho, false) => (tile) => !tile.invisibleBlock(),
				(CoatingKind.AddIlluminant, false) => (tile) => !tile.fullbrightBlock(),
				(CoatingKind.RemoveAll, false) => (tile) => tile.invisibleBlock() || tile.fullbrightBlock(),

				(CoatingKind.RemoveEcho, true) => (tile) => tile.invisibleWall(),
				(CoatingKind.RemoveIlluminant, true) => (tile) => tile.fullbrightWall(),
				(CoatingKind.AddEcho, true) => (tile) => !tile.invisibleWall(),
				(CoatingKind.AddIlluminant, true) => (tile) => !tile.fullbrightWall(),
				(CoatingKind.RemoveAll, true) => (tile) => tile.invisibleWall() || tile.fullbrightWall(),

				_ => throw new InvalidOperationException(),

			};
			Action<ITile> perform = (kind, walls) switch {
				(CoatingKind.RemoveEcho, false) => (tile) => tile.invisibleBlock(false),
				(CoatingKind.RemoveIlluminant, false) => (tile) => tile.fullbrightBlock(false),
				(CoatingKind.AddEcho, false) => (tile) => tile.invisibleBlock(true),
				(CoatingKind.AddIlluminant, false) => (tile) => tile.fullbrightBlock(true),
				(CoatingKind.RemoveAll, false) => (tile) => { tile.invisibleBlock(false); tile.fullbrightBlock(false); },

				(CoatingKind.RemoveEcho, true) => (tile) => tile.invisibleWall(false),
				(CoatingKind.RemoveIlluminant, true) => (tile) => tile.fullbrightWall(false),
				(CoatingKind.AddEcho, true) => (tile) => tile.invisibleWall(true),
				(CoatingKind.AddIlluminant, true) => (tile) => tile.fullbrightWall(true),
				(CoatingKind.RemoveAll, true) => (tile) => { tile.invisibleWall(false); tile.fullbrightWall(false); },

				_ => throw new InvalidOperationException(),
			};
			for (int i = x; i <= x2; i++)
			{
				for (int j = y; j <= y2; j++)
				{
					var tile = Main.tile[i, j];
					if ((tile.active() && !walls || walls && tile.wall > 0) && condition(tile) && (expression?.Evaluate(tile) ?? true) && magicWand.InSelection(i, j))
					{
						perform(tile);
						edits++;
					}
				}
			}
			ResetSection();
			var what = walls ? "walls" : "tiles";
			plr.SendSuccessMessage($"Coated {what}. ({edits})");
		}
	}
}
