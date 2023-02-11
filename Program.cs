using System;

namespace Namespace
{

	class CRapSimple
	{
		static void Main()
		{
			string version = "2020-12-01";
			CChess chess = new CChess();
			CUci uci = new CUci();

			while (true)
			{
				string msg = Console.ReadLine();
				uci.SetMsg(msg);
				switch (uci.command)
				{
					case "uci":
						Console.WriteLine($"id name RapShortCs {version}");
						Console.WriteLine("id author Thibor Raven");
						Console.WriteLine("id link https://github.com/Thibor/RapSimpleCs");
						Console.WriteLine("uciok");
						break;
					case "isready":
						Console.WriteLine("readyok");
						break;
					case "position":
						string fen = "";
						int lo = uci.GetIndex("fen", 0);
						int hi = uci.GetIndex("moves", uci.tokens.Length);
						if (lo > 0)
						{
							if (lo > hi)
								hi = uci.tokens.Length;
							for (int n = lo; n < hi; n++)
							{
								if (n > lo)
									fen += ' ';
								fen += uci.tokens[n];
							}
						}
						chess.SetFen(fen);
						lo = uci.GetIndex("moves", 0);
						hi = uci.GetIndex("fen", uci.tokens.Length);
						if (lo > 0)
						{
							if (lo > hi)
								hi = uci.tokens.Length;
							for (int n = lo; n < hi; n++)
							{
								string m = uci.tokens[n];
								chess.MakeMove(chess.UmoToEmo(m));
								if (chess.move50 == 0)
									chess.undoIndex = 0;
							}
						}
						break;
					case "go":
						chess.stopwatch.Restart();
						int time = uci.GetInt("movetime", 0);
						int depth = uci.GetInt("depth", 0);
						int node = uci.GetInt("nodes", 0);
						int infinite = uci.GetIndex("infinite", 0);
						if ((time == 0) && (depth == 0) && (node == 0) && (infinite == 0))
						{
							double ct = chess.whiteTurn ? uci.GetInt("wtime", 0) : uci.GetInt("btime", 0);
							double inc = chess.whiteTurn ? uci.GetInt("winc", 0) : uci.GetInt("binc", 0);
							double mg = uci.GetInt("movestogo", 32);
							time = Convert.ToInt32((ct - 1000 + inc * mg) / mg);
							if (time < 1)
								time = 1;
						}
						chess.StartThread(depth, time, node);
						break;
					case "stop":
						chess.synStop.SetStop(true);
						break;
					case "quit":
						chess.synStop.SetStop(true);
						return;
				}

			}
		}
	}
}
