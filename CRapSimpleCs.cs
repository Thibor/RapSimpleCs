﻿using System;

namespace Namespace
{

	class CRapSimple
	{
		static void Main()
		{
			string version = "2020-12-01";
			CChess Chess = new CChess();
			CUci Uci = new CUci();

			while (true)
			{
				string msg = Console.ReadLine();
				Uci.SetMsg(msg);
				switch (Uci.command)
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
						int lo = Uci.GetIndex("fen", 0);
						int hi = Uci.GetIndex("moves", Uci.tokens.Length);
						if (lo > 0)
						{
							if (lo > hi)
								hi = Uci.tokens.Length;
							for (int n = lo; n < hi; n++)
							{
								if (n > lo)
									fen += ' ';
								fen += Uci.tokens[n];
							}
						}
						Chess.SetFen(fen);
						lo = Uci.GetIndex("moves", 0);
						hi = Uci.GetIndex("fen", Uci.tokens.Length);
						if (lo > 0)
						{
							if (lo > hi)
								hi = Uci.tokens.Length;
							for (int n = lo; n < hi; n++)
							{
								string m = Uci.tokens[n];
								Chess.MakeMove(Chess.UmoToEmo(m));
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
						int infinite = Uci.GetIndex("infinite", 0);
						if ((time == 0) && (depth == 0) && (node == 0) && (infinite == 0))
						{
							time = Chess.whiteTurn ? Uci.GetInt("wtime", 0) : Uci.GetInt("btime", 0);
							double mg = Uci.GetInt("movestogo", 0x40);
							time = Convert.ToInt32(time / mg);
							if (time < 1)
								time = 1;
						}
						Chess.StartThread(depth, time, node);
						break;
					case "stop":
						Chess.synStop.SetStop(true);
						break;
					case "quit":
						Chess.synStop.SetStop(true);
						return;
				}

			}
		}
	}
}
