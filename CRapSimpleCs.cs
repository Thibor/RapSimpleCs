using System;

namespace RapSimpleCs
{
	class CRapSimpleCs
	{	
		static void Main()
		{
			string version = "2020-02-02";
			CUci Uci = new CUci();
			CChess Chess = new CChess();

			while (true)
			{
				string msg = CReader.ReadLine(true);
				Uci.SetMsg(msg);
				switch (Uci.command)
				{
					case "uci":
						Console.WriteLine("id name RapSimpleCs " + version);
						Console.WriteLine("id author Thibor Raven");
						Console.WriteLine("uciok");
						break;
					case "isready":
						Console.WriteLine("readyok");
						break;
					case "position":
						string fen = "";
						int lo = Uci.GetIndex("fen", 0);
						int hi = Uci.GetIndex("moves", Uci.tokens.Length);
						if (lo > 0)
						{
							if (lo > hi)
							{
								hi = Uci.tokens.Length;
							}
							for (int n = lo; n < hi; n++)
							{
								if (n > lo)
								{
									fen += ' ';
								}
								fen += Uci.tokens[n];
							}
						}
						Chess.InitializeFromFen(fen);
						lo = Uci.GetIndex("moves", 0);
						hi = Uci.GetIndex("fen", Uci.tokens.Length);
						if (lo > 0)
						{
							if (lo > hi)
							{
								hi = Uci.tokens.Length;
							}
							for (int n = lo; n < hi; n++)
							{
								string m = Uci.tokens[n];
								Chess.MakeMove(Chess.GetMoveFromString(m));
								if (Chess.g_move50 == 0)
									Chess.undoIndex = 0;
							}
						}
						break;
					case "go":
						Chess.stopwatch.Restart();
						int time = Uci.GetInt("movetime", 0);
						int depth = Uci.GetInt("depth", 0);
						int node = Uci.GetInt("nodes", 0);
						if ((time == 0) && (depth == 0) && (node == 0))
						{
							double ct = Chess.whiteTurn ? Uci.GetInt("wtime", 0) : Uci.GetInt("btime", 0);
							double ci = Chess.whiteTurn ? Uci.GetInt("winc", 0) : Uci.GetInt("binc", 0);
							double mg = Uci.GetInt("movestogo", 32);
							time = Convert.ToInt32((ct / mg) + ci - 0xff);
						}
						Chess.Start(depth, time, node);
						break;
					case "quit":
						return;
				}

			}
		}
	}
}
