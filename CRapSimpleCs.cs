using System;

namespace RapSimpleCs
{
	class CRapSimpleCs
	{	
		static void Main()
		{
			string version = "2020-04-04";
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
						Console.WriteLine("id link https://github.com/Thibor/RapSimpleCs");
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
							double mg = Uci.GetInt("movestogo", Chess.g_phase << 1);
							time = Convert.ToInt32(ct / mg);
							if (time < 1)
								time = 1;
						}
						if (time > 0)
						{
							time -= 0x20;
							if (time < 0x20)
							{
								time = 0;
								depth = 1;
							}
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
