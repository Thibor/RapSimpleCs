using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Namespace
{
	class CChess
	{
		const int piecePawn = 0x01;
		const int pieceKnight = 0x02;
		const int pieceBishop = 0x03;
		const int pieceRook = 0x04;
		const int pieceQueen = 0x05;
		const int pieceKing = 0x06;
		const int colorBlack = 0x08;
		const int colorWhite = 0x10;
		const int colorEmpty = 0x20;
		const int moveflagPassing = 0x02 << 16;
		const int moveflagCastleKing = 0x04 << 16;
		const int moveflagCastleQueen = 0x08 << 16;
		const int moveflagPromoteQueen = pieceQueen << 20;
		const int moveflagPromoteRook = pieceRook << 20;
		const int moveflagPromoteBishop = pieceBishop << 20;
		const int moveflagPromoteKnight = pieceKnight << 20;
		const int maskColor = colorBlack | colorWhite;
		const int maskPromotion = moveflagPromoteQueen | moveflagPromoteRook | moveflagPromoteBishop | moveflagPromoteKnight;
		int inTime = 0;
		int inDepth = 0;
		int inNodes = 0;
		int g_castleRights = 0xf;
		ulong g_hash = 0;
		int g_passing = 0;
		public int g_move50 = 0;
		int g_moveNumber = 0;
		int g_totalNodes = 0;
		int g_timeout = 0;
		int g_depthout = 0;
		int g_nodeout = 0;
		int g_mainDepth = 1;
		bool g_stop = false;
		int g_lastCastle = colorEmpty;
		public int undoIndex = 0;
		readonly int[] arrField = new int[64];
		readonly int[] g_board = new int[256];
		readonly ulong[,] g_hashBoard = new ulong[256, 16];
		readonly int[] boardCastle = new int[256];
		public bool whiteTurn = true;
		int bsIn = -1;
		string bsFm = "";
		string bsPv = "";
		readonly int[] kingPos = new int[2];
		readonly int[] bonMaterial = new int[7] { 0, 100, 300, 310, 500, 800, 0xffff };
		readonly int[] arrDirKinght = { 14, -14, 18, -18, 31, -31, 33, -33 };
		readonly int[] arrDirBishop = { 15, -15, 17, -17 };
		readonly int[] arrDirRook = { 1, -1, 16, -16 };
		readonly int[] arrDirQueen = { 1, -1, 15, -15, 16, -16, 17, -17 };
		public static Random random = new Random();
		readonly CUndo[] undoStack = new CUndo[0xfff];
		Thread startThread;
		public Stopwatch stopwatch = Stopwatch.StartNew();
		public CSynStop synStop = new CSynStop();

		public CChess()
		{
			g_hash = RAND_32();
			for (int n = 0; n < undoStack.Length; n++)
				undoStack[n] = new CUndo();
			for (int y = 0; y < 8; y++)
				for (int x = 0; x < 8; x++)
					arrField[y * 8 + x] = (y + 4) * 16 + x + 4;
			for (int n = 0; n < 256; n++)
			{
				boardCastle[n] = 15;
				g_board[n] = 0;
				for (int p = 0; p < 16; p++)
					g_hashBoard[n, p] = RAND_32();
			}
			int[] arrCastleI = { 68, 72, 75, 180, 184, 187 };
			int[] arrCasteleV = { 7, 3, 11, 13, 12, 14 };
			for (int n = 0; n < 6; n++)
				boardCastle[arrCastleI[n]] = arrCasteleV[n];
		}

		ulong RAND_32()
		{
			return ((ulong)random.Next() << 32) | ((ulong)random.Next() << 0);
		}

		string EmoToUmo(int emo)
		{
			string result = SquareToStr(emo & 0xFF) + SquareToStr((emo >> 8) & 0xFF);
			int promotion = emo & maskPromotion;
			if (promotion > 0)
			{
				if (promotion == moveflagPromoteQueen) result += 'q';
				else if (promotion == moveflagPromoteRook) result += 'r';
				else if (promotion == moveflagPromoteBishop) result += 'b';
				else result += 'n';
			}
			return result;
		}

		public int UmoToEmo(string umo)
		{
			List<int> moves = GenerateAllMoves(whiteTurn, false, out _, out _);
			for (int i = 0; i < moves.Count; i++)
			{
				if (EmoToUmo(moves[i]) == umo)
					return moves[i];
			}
			return 0;
		}

		string SquareToStr(int square)
		{
			int x = (square & 0xf) - 4;
			int y = (square >> 4) - 4;
			string xs = "abcdefgh";
			string ys = "87654321";
			return $"{xs[x]}{ys[y]}";
		}

		int StrToSquare(string s)
		{
			string xs = "abcdefgh";
			string ys = "87654321";
			int x = xs.IndexOf(s[0]);
			int y = ys.IndexOf(s[1]);
			return ((y + 4) << 4) | (x + 4);
		}

		/*string SquareToStr(int square)
		{
			char[] arr = { 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h' };
			return arr[(square & 0xf) - 4] + (12 - (square >> 4)).ToString();
		}

		int StrToSquare(string s)
		{
			string fl = "abcdefgh";
			int x = fl.IndexOf(s[0]);
			int y = 12 - Int32.Parse(s[1].ToString());
			return (x + 4) | (y << 4);
		}*/

		bool IsRepetition()
		{
			for (int n = undoIndex - 2; n >= undoIndex - g_move50; n -= 2)
				if (undoStack[n].hash == g_hash)
				{
					return true;
				}
			return false;
		}

		void AddMove(List<int> moves, int fr, int to, int flag)
		{
			int rank = g_board[to] & 7;
			int m = fr | (to << 8) | flag;
			if (rank > 0)
				moves.Add(m);
			else
				moves.Insert(0, m);
		}

		void GeneratePwnMoves(List<int> moves, int fr, int to, int flag)
		{
			int y = to >> 4;
			if ((y == 4) || (y == 11))
			{
				AddMove(moves, fr, to, moveflagPromoteQueen);
				AddMove(moves, fr, to, moveflagPromoteRook);
				AddMove(moves, fr, to, moveflagPromoteBishop);
				AddMove(moves, fr, to, moveflagPromoteKnight);
			}
			else
				AddMove(moves, fr, to, flag);
		}

		int GenerateUniMoves(List<int> moves, bool attack, int fr, int[] dir, int count, int enColor, ref int score)
		{
			int cm = moves.Count;
			for (int n = 0; n < dir.Length; n++)
			{
				int to = fr;
				int c = count;
				while (c-- > 0)
				{
					to += dir[n];
					if ((g_board[to] & colorEmpty) > 0)
					{
						score++;
						if (!attack)
							AddMove(moves, fr, to, 0);
					}
					else if ((g_board[to] & enColor) > 0)
					{
						score += 2;
						AddMove(moves, fr, to, 0);
						break;
					}
					else
						break;
				}
			}
			return moves.Count - cm;
		}

		public void SetFen(string fen)
		{
			g_lastCastle = colorEmpty;
			synStop.SetStop(false);
			for (int n = 0; n < 64; n++)
				g_board[arrField[n]] = colorEmpty;
			if (fen == "") fen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
			string[] chunks = fen.Split(' ');
			int row = 0;
			int col = 0;
			string pieces = chunks[0];
			for (int i = 0; i < pieces.Length; i++)
			{
				char c = pieces[i];
				if (c == '/')
				{
					row++;
					col = 0;
				}
				else if (c >= '0' && c <= '9')
				{
					for (int j = 0; j < Int32.Parse(c.ToString()); j++)
						col++;
				}
				else
				{
					bool isWhite = Char.IsUpper(c);
					int piece = isWhite ? colorWhite : colorBlack;
					int index = (row + 4) * 16 + col + 4;
					switch (Char.ToLower(c))
					{
						case 'p':
							piece |= piecePawn;
							break;
						case 'b':
							piece |= pieceBishop;
							break;
						case 'n':
							piece |= pieceKnight;
							break;
						case 'r':
							piece |= pieceRook;
							break;
						case 'q':
							piece |= pieceQueen;
							break;
						case 'k':
							kingPos[isWhite ? 1 : 0] = index;
							piece |= pieceKing;
							break;
					}
					g_board[index] = piece;
					col++;
				}
			}
			whiteTurn = chunks[1] == "w";
			g_castleRights = 0;
			if (chunks[2].IndexOf('K') != -1)
				g_castleRights |= 1;
			if (chunks[2].IndexOf('Q') != -1)
				g_castleRights |= 2;
			if (chunks[2].IndexOf('k') != -1)
				g_castleRights |= 4;
			if (chunks[2].IndexOf('q') != -1)
				g_castleRights |= 8;
			g_passing = 0;
			if (chunks[3].IndexOf('-') == -1)
				g_passing = StrToSquare(chunks[3]);
			g_move50 = 0;
			g_moveNumber = Int32.Parse(chunks[5]);
			if (g_moveNumber > 0) g_moveNumber--;
			g_moveNumber *= 2;
			if (!whiteTurn) g_moveNumber++;
			undoIndex = g_move50;
		}

		public void MakeMove(int move)
		{
			int fr = move & 0xFF;
			int to = (move >> 8) & 0xFF;
			int flags = move & 0xFF0000;
			int piecefr = g_board[fr];
			int rank = piecefr & 7;
			int piece = piecefr & 0xf;
			int captured = g_board[to];
			g_lastCastle = colorEmpty;
			if ((flags & moveflagCastleKing) > 0)
			{
				g_lastCastle = moveflagCastleKing | (piecefr & maskColor);
				g_board[to - 1] = g_board[to + 1];
				g_board[to + 1] = colorEmpty;
			}
			else if ((flags & moveflagCastleQueen) > 0)
			{
				g_lastCastle = moveflagCastleQueen | (piecefr & maskColor);
				g_board[to + 1] = g_board[to - 2];
				g_board[to - 2] = colorEmpty;
			}
			else if ((flags & moveflagPassing) > 0)
			{
				int capi = whiteTurn ? to + 16 : to - 16;
				captured = g_board[capi];
				g_board[capi] = colorEmpty;
			}
			ref CUndo undo = ref undoStack[undoIndex++];
			undo.captured = captured;
			undo.hash = g_hash;
			undo.passing = g_passing;
			undo.castle = g_castleRights;
			undo.move50 = g_move50;
			undo.lastCastle = g_lastCastle;
			g_hash ^= g_hashBoard[fr, piece];
			g_passing = 0;
			if (captured != colorEmpty)
				g_move50 = 0;
			else if (rank == piecePawn)
			{
				if (to == (fr + 32)) g_passing = (fr + 16);
				if (to == (fr - 32)) g_passing = (fr - 16);
				g_move50 = 0;
			}
			else
				g_move50++;
			if ((flags & maskPromotion) > 0)
			{
				int newPiece = ((piecefr & (~0x7)) | (flags >> 20));
				g_board[to] = newPiece;
				g_hash ^= g_hashBoard[to, newPiece & 0xf];
			}
			else
			{
				g_board[to] = g_board[fr];
				g_hash ^= g_hashBoard[to, piece];
			}
			if (rank == pieceKing)
				kingPos[whiteTurn ? 1 : 0] = to;
			g_board[fr] = colorEmpty;
			g_castleRights &= boardCastle[fr] & boardCastle[to];
			whiteTurn ^= true;
			g_moveNumber++;
		}

		void UnmakeMove(int move)
		{
			int fr = move & 0xFF;
			int to = (move >> 8) & 0xFF;
			int flags = move & 0xFF0000;
			int capi = to;
			CUndo undo = undoStack[--undoIndex];
			g_passing = undo.passing;
			g_castleRights = undo.castle;
			g_move50 = undo.move50;
			g_lastCastle = undo.lastCastle;
			g_hash = undo.hash;
			int captured = undo.captured;
			if ((flags & moveflagCastleKing) > 0)
			{
				g_board[to + 1] = g_board[to - 1];
				g_board[to - 1] = colorEmpty;
			}
			else if ((flags & moveflagCastleQueen) > 0)
			{
				g_board[to - 2] = g_board[to + 1];
				g_board[to + 1] = colorEmpty;
			}
			if ((flags & maskPromotion) > 0)
			{
				int piece = (g_board[to] & (~0x7)) | piecePawn;
				g_board[fr] = piece;
			}
			else
			{
				g_board[fr] = g_board[to];
				if ((g_board[fr] & 0x7) == pieceKing)
					kingPos[whiteTurn ? 0 : 1] = fr;
			}
			if ((flags & moveflagPassing) > 0)
			{
				capi = whiteTurn ? to - 16 : to + 16;
				g_board[to] = colorEmpty;
			}
			g_board[capi] = captured;
			whiteTurn ^= true;
			g_moveNumber--;
		}

		bool GetStop()
		{
			return ((g_timeout > 0) && (stopwatch.Elapsed.TotalMilliseconds > g_timeout)) || ((g_depthout > 0) && (g_mainDepth > g_depthout)) || ((g_nodeout > 0) && (g_totalNodes > g_nodeout));
		}

		bool IsAttacked(bool wt, int to)
		{
			int ec = wt ? colorBlack : colorWhite;
			int del = wt ? -16 : 16;
			int fr = to + del;
			if ((g_board[fr + 1] & 0x1f) == (ec | piecePawn))
				return true;
			if ((g_board[fr - 1] & 0x1f) == (ec | piecePawn))
				return true;
			if ((g_board[to + 14] & 0x1f) == (ec | pieceKnight))
				return true;
			if ((g_board[to - 14] & 0x1f) == (ec | pieceKnight))
				return true;
			if ((g_board[to + 18] & 0x1f) == (ec | pieceKnight))
				return true;
			if ((g_board[to - 18] & 0x1f) == (ec | pieceKnight))
				return true;
			if ((g_board[to + 31] & 0x1f) == (ec | pieceKnight))
				return true;
			if ((g_board[to - 31] & 0x1f) == (ec | pieceKnight))
				return true;
			if ((g_board[to + 33] & 0x1f) == (ec | pieceKnight))
				return true;
			if ((g_board[to - 33] & 0x1f) == (ec | pieceKnight))
				return true;
			foreach (int d in arrDirBishop)
			{
				fr = to + d;
				if ((g_board[fr] & 0x1f) == (ec | pieceKing))
					return true;
				while (g_board[fr] > 0)
				{
					if ((g_board[fr] & colorEmpty) > 0)
					{
						fr += d;
						continue;
					}
					if ((g_board[fr] & 0x1f) == (ec | pieceBishop) || (g_board[fr] & 0x1f) == (ec | pieceQueen))
						return true;
					break;
				}
			}
			foreach (int d in arrDirRook)
			{
				fr = to + d;
				if ((g_board[fr] & 0x1f) == (ec | pieceKing))
					return true;
				while (g_board[fr] > 0)
				{
					if ((g_board[fr] & colorEmpty) > 0)
					{
						fr += d;
						continue;
					}
					if ((g_board[fr] & 0x1f) == (ec | pieceRook) || (g_board[fr] & 0x1f) == (ec | pieceQueen))
						return true;
					break;
				}
			}
			return false;
		}

		List<int> GenerateAllMoves(bool wt, bool attack, out int score, out bool insufficient)
		{
			score = 0;
			int usColor = wt ? colorWhite : colorBlack;
			int enColor = wt ? colorBlack : colorWhite;
			int kp = kingPos[wt ? 1 : 0];
			int pieceP = 0;
			int pieceN = 0;
			int pieceB = 0;
			int pieceM = 0;
			int cp1;
			int cp2 = 0;
			int cp3 = 0;
			int pawnDistance = 8;
			int dx, dy;
			int kpx = 0;
			int kpy = 0;
			List<int> moves = new List<int>(64);
			for (int x = 0; x < 8; x++)
			{
				cp1 = cp2;
				cp2 = cp3;
				cp3 = 0;
				for (int y = 0; y < 8; y++)
				{
					int n = (y << 3) | x;
					int fr = arrField[n];
					int f = g_board[fr];
					if ((f & usColor) > 0) f &= 7;
					else continue;
					score += bonMaterial[f];
					switch (f)
					{
						case 1:
							pieceP++;
							int del = wt ? -16 : 16;
							int to = fr + del;
							cp3++;
							score += wt ? 7 - y : y;
							if (((g_board[to] & colorEmpty) > 0) && !attack)
							{
								GeneratePwnMoves(moves, fr, to, 0);
								if ((g_board[fr - del - del] == 0) && (g_board[to + del] & colorEmpty) > 0)
									GeneratePwnMoves(moves, fr, to + del, 0);
							}
							if ((g_board[to - 1] & enColor) > 0)
								GeneratePwnMoves(moves, fr, to - 1, 0);
							else if ((to - 1) == g_passing)
								GeneratePwnMoves(moves, fr, g_passing, moveflagPassing);
							if ((g_board[to + 1] & enColor) > 0)
								GeneratePwnMoves(moves, fr, to + 1, 0);
							else if ((to + 1) == g_passing)
								GeneratePwnMoves(moves, fr, g_passing, moveflagPassing);
							if (pawnDistance > 1)
							{
								dx = Math.Abs((kp & 0xf) - (fr & 0xf));
								dy = Math.Abs((kp >> 4) - (fr >> 4));
								int di = Math.Max(dx, dy);
								if (pawnDistance > di)
									pawnDistance = di;
							}
							break;
						case 2:
							pieceN++;
							GenerateUniMoves(moves, attack, fr, arrDirKinght, 1, enColor, ref score);
							break;
						case 3:
							pieceB++;
							GenerateUniMoves(moves, attack, fr, arrDirBishop, 7, enColor, ref score);
							break;
						case 4:
							pieceM++;
							GenerateUniMoves(moves, attack, fr, arrDirRook, 7, enColor, ref score);
							break;
						case 5:
							pieceM++;
							GenerateUniMoves(moves, attack, fr, arrDirQueen, 7, enColor, ref score);
							break;
						case 6:
							kpx = x;
							kpy = y;
							GenerateUniMoves(moves, attack, fr, arrDirQueen, 1, enColor, ref score);
							int cr = wt ? g_castleRights : g_castleRights >> 2;
							if ((cr & 1) > 0)
								if (((g_board[fr + 1] & colorEmpty) > 0) && ((g_board[fr + 2] & colorEmpty) > 0) && !IsAttacked(wt, fr) && !IsAttacked(wt, fr + 1) && !IsAttacked(wt, fr + 2))
									AddMove(moves, fr, fr + 2, moveflagCastleKing);
							if ((cr & 2) > 0)
								if (((g_board[fr - 1] & colorEmpty) > 0) && ((g_board[fr - 2] & colorEmpty) > 0) && ((g_board[fr - 3] & colorEmpty) > 0) && !IsAttacked(wt, fr) && !IsAttacked(wt, fr - 1) && !IsAttacked(wt, fr - 2))
									AddMove(moves, fr, fr - 2, moveflagCastleQueen);
							break;
					}
				}
				score -= cp3 * 0x10;
				if ((cp1 == 0) && (cp2 > 0) && (cp3 == 0))
					score -= cp2 * 0x10;
			}
			if ((cp2 == 0) && (cp3 > 0))
				score -= cp3 * 0x10;
			if (pawnDistance < 8)
				score -= pawnDistance << 3;
			if (pieceB > 1)
				score += 64;
			dx = Math.Abs((kpx << 1) - 7) >> 1;
			dy = Math.Abs((kpy << 1) - 7) >> 1;
			int phase = pieceP + pieceN + pieceB + pieceM;
			score += (phase - 8) * (dx + dy);
			insufficient = (pieceP + pieceM == 0) && (pieceN + (pieceB << 1) < 3);
			return moves;
		}

		int Quiesce(List<int> usm, int ply, int depth, int alpha, int beta, int usScore, bool usInsufficient, ref int alDe, ref string alPv)
		{
			int neDe = 0;
			string nePv = "";
			GenerateAllMoves(!whiteTurn, true, out int enScore, out bool enInsufficient);
			if (usInsufficient && enInsufficient)
				return 0;
			int score = usScore - enScore;
			if (usInsufficient != enInsufficient)
				score += usInsufficient ? -400 : 400;
			if (depth < 1)
				return score;
			if (score >= beta)
				return beta;
			if (score > alpha)
				alpha = score;
			int index = usm.Count;
			while (index-- > 0)
			{
				alDe = 0;
				alPv = "";
				if ((++g_totalNodes & 0x1fff) == 0)
					if (GetStop() || synStop.GetStop())
						g_stop = true;
				int cm = usm[index];
				MakeMove(cm);
				if (IsAttacked(!whiteTurn, kingPos[whiteTurn ? 0 : 1]))
					score = -0xffff;
				else {
					List<int> enm = GenerateAllMoves(whiteTurn, true, out enScore, out enInsufficient);
					score = -Quiesce(enm, ply + 1, depth - 1, -beta, -alpha, enScore, enInsufficient, ref alDe, ref alPv); }
				UnmakeMove(cm);
				if (g_stop) return -0xffff;
				if (score >= beta)
					return beta;
				if (score > alpha)
				{
					nePv = $"{EmoToUmo(cm)} {alPv}";
					neDe = alDe + 1;
					alpha = score;
				}
			}
			alDe = neDe;
			alPv = nePv;
			return alpha;
		}


		int Search(List<int> usm, int ply, int depth, int alpha, int beta, int usScore, bool usInsufficient, bool usCheck, ref int alDe, ref string alPv, out int myMoves)
		{
			int neDe = 0;
			string nePv = "";
			int n = usm.Count;
			myMoves = n;
			alpha = Math.Max(alpha, -0xffff + ply);
			beta = Math.Min(beta, 0xffff - ply);
			if (alpha >= beta)
				return alpha;
			if (!usCheck && (depth > 0))
			{
				int score1 = Quiesce(usm, ply + 1, 0, alpha, beta, usScore, usInsufficient, ref alDe, ref alPv) - (depth << 4);
				if (score1 >= beta)
					return score1;
			}
			if (usCheck)
				depth++;
			if (depth <= 0)
				return Quiesce(usm, 1, g_mainDepth, alpha, beta, usScore, usInsufficient, ref alDe, ref alPv);

			while (n-- > 0)
			{
				alDe = 0;
				alPv = "";
				int cm = usm[n];
				if ((++g_totalNodes & 0x1fff) == 0)
					if (GetStop() || synStop.GetStop())
						g_stop = true;
				MakeMove(cm);
				int score = -0xffff;
				if (IsAttacked(!whiteTurn, kingPos[whiteTurn ? 0 : 1]))
					myMoves--;
				else
				{
					bool enCheck = IsAttacked(whiteTurn, kingPos[whiteTurn ? 1 : 0]);
					List<int> enm = GenerateAllMoves(whiteTurn, (depth < 2) && !enCheck, out int enScore, out bool enInsufficient);
					if ((g_move50 > 99) || IsRepetition() || (usInsufficient && enInsufficient))
						score = 0;
					else
						score = -Search(enm, ply + 1, depth - 1, -beta, -alpha, enScore, enInsufficient, enCheck, ref alDe, ref alPv, out _);
				}
				UnmakeMove(cm);
				if (g_stop) return -0xffff;
				if (score >= beta)
					return beta;
				if (score > alpha)
				{
					string alphaFm = EmoToUmo(cm);
					nePv = $"{alphaFm} {alPv}";
					neDe = alDe + 1;
					alpha = score;
					if (ply == 1)
					{
						string scFm = score > 0xf000 ? $"mate {(0xffff - score) >> 1}" : ((score < -0xf000) ? $"mate {(-0xfffe - score) >> 1}" : $"cp {score}");
						bsIn = n;
						bsFm = alphaFm;
						bsPv = nePv;
						double t = stopwatch.Elapsed.TotalMilliseconds;
						double nps = t > 0 ? (g_totalNodes / t) * 1000 : 0;
						Console.WriteLine($"info currmove {bsFm} currmovenumber {n} nodes {g_totalNodes} time {Convert.ToInt64(t)} nps {Convert.ToInt64(nps)} depth {g_mainDepth} seldepth {neDe} score {scFm} pv {bsPv}");
					}
				}
			}
			alDe = neDe;
			alPv = nePv;
			if (myMoves == 0)
				return usCheck ? -0xffff + ply : 0;
			return alpha;
		}

		public void Start(int depth, int time, int nodes)
		{
			List<int> usm = GenerateAllMoves(whiteTurn, false, out int usScore, out bool usInsufficient);
			bool usCheck = IsAttacked(whiteTurn, kingPos[whiteTurn ? 1 : 0]);
			int myMoves;
			g_stop = false;
			g_totalNodes = 0;
			g_timeout = time;
			g_depthout = depth;
			g_nodeout = nodes;
			g_mainDepth = 1;
			do
			{
				int alDe = 0;
				string alPv = "";
				int score = Search(usm, 1, g_mainDepth, -0xffff, 0xffff, usScore, usInsufficient, usCheck, ref alDe, ref alPv, out myMoves);
				int m = usm[bsIn];
				usm.RemoveAt(bsIn);
				usm.Add(m);
				double t = stopwatch.Elapsed.TotalMilliseconds;
				double nps = t > 0 ? (g_totalNodes / t) * 1000 : 0;
				Console.WriteLine($"info depth {g_mainDepth} nodes {g_totalNodes} time {Convert.ToInt64(t)} nps {Convert.ToInt64(nps)} {usm.Count}");
				if (++g_mainDepth > 100)
					break;
				if ((score < -0xf000) || (score > 0xf000))
					break;
			} while (!GetStop() && !synStop.GetStop() && (myMoves > 1));
			string[] ponder = bsPv.Split(' ');
			string pm = ponder.Length > 1 ? $" ponder {ponder[1]}" : "";
			Console.WriteLine("bestmove " + bsFm + pm);
		}

		public void Thread()
		{
			Start(inDepth, inTime, inNodes);
		}

		public void StartThread(int depth, int time, int nodes)
		{
			inDepth = depth;
			inTime = time;
			inNodes = nodes;
			startThread = new Thread(Thread);
			startThread.Start();
		}
	}
}
