using ChessChallenge.API;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;

// Like BotMk06 but with a dict for the results of CalculateMoveRating in SortMoves to try to improve the performance -> FAIL
public class BotMk11 : IChessBot
{
	// ----------------
	// DATA CODE
	// ----------------

	// HexValue = TransformAndRightToLeft(DecValue + 100)
	// DecValues: +00 +50 +10 +05 +00 +05 +05 +00
	// DecValues: +00 +50 +10 +05 +00 -05 +10 +00
	// DecValues: +00 +50 +20 +10 +00 -10 +10 +00
	// DecValues: +00 +50 +30 +25 +20 +00 -20 +00
	private static readonly int[] POSITION_MAP_PAWN_START = CreatePositionMap(0x64696964696E9664, 0x646E5F64696E9664, 0x646E5A646E789664, 0x645064787D829664);

	// HexValue = TransformAndRightToLeft(DecValue + 100)
	// DecValues: +00 +80 +50 +30 +20 +10 +10 +00
	// DecValues: +00 +80 +50 +30 +20 +10 +10 +00
	// DecValues: +00 +80 +50 +30 +20 +10 +10 +00
	// DecValues: +00 +80 +50 +30 +20 +10 +10 +00
	private static readonly int[] POSITION_MAP_PAWN_END = CreatePositionMap(0x646E6E788296B464, 0x646E6E788296B464, 0x646E6E788296B464, 0x646E6E788296B464);

	// DecValues: -50 -40 -30 -30 -30 -30 -40 -50
	// DecValues: -40 -20 +00 +05 +00 +05 -20 -40
	// DecValues: -30 +00 +10 +15 +15 +10 +00 -30
	// DecValues: -30 +00 +15 +20 +20 +15 +05 -30
	private static readonly int[] POSITION_MAP_KNIGHT = CreatePositionMap(0x323C464646463C32, 0x323C464646463C32, 0x46646E73736E6446, 0x4669737878736446);

	// DecValues: -20 -10 -10 -10 -10 -10 -10 -20
	// DecValues: -10 +00 +00 +05 +00 +10 +05 -10
	// DecValues: -10 +00 +05 +05 +10 +10 +00 -10
	// DecValues: -10 +00 +10 +10 +10 +10 +00 -10
	private static readonly int[] POSITION_MAP_BISHOP = CreatePositionMap(0x505A5A5A5A5A5A50, 0x5A696E646964645A, 0x5A646E6E6969645A, 0x5A646E6E6E6E645A);

	// DecValues: +00 +05 -05 -05 -05 -05 -05 +00
	// DecValues: +00 +10 +00 +00 +00 +00 +00 +00
	// DecValues: +00 +10 +00 +00 +00 +00 +00 +00
	// DecValues: +00 +10 +00 +00 +00 +00 +00 +05
	private static readonly int[] POSITION_MAP_ROOK = CreatePositionMap(0x645F5F5F5F5F6964, 0x6464646464646E64, 0x6464646464646E64, 0x6964646464646E64);

	// DecValues: -20 -10 -10 -05 +00 -10 -10 -20
	// DecValues: -10 +00 +00 +00 +00 +05 +00 -10
	// DecValues: -10 +00 +05 +05 +05 +05 +05 -10
	// DecValues: -05 +00 +05 +05 +05 +05 +00 -05
	private static readonly int[] POSITION_MAP_QUEEN = CreatePositionMap(0x505A5A645F5A5A50, 0x5A6469646464645A, 0x5A6969696969645A, 0x5F6469696969645F);

	// TODO: maybe also add start and end map for king

	private static readonly PieceType[] EVAL_PIECE_TYPES = {PieceType.Pawn, PieceType.Knight, PieceType.Bishop, PieceType.Rook, PieceType.Queen};
	private const int CHECKMATE_VALUE = 1000000000;
	private const int MIN_WINNING_VALUE = 200;
	private const int CASTLE_VALUE = 500;
	private const int CAPTURE_VALUE_FACTOR = 10;
	private const int KING_PUSH_TO_EDGE_FACTOR = 10;
	private const int KING_MOVE_CLOSER_FACTOR = 4;

    // private static readonly int[] PIECE_VALUES = { 0, 100, 300, 320, 500, 900, 10000 };  // Piece values: null, pawn, knight, bishop, rook, queen, king
    private static int GetPieceValue(PieceType pieceType) => pieceType switch
    {
        PieceType.Pawn => 100,
        PieceType.Knight => 300,
        PieceType.Bishop => 320,
        PieceType.Rook => 500,
        PieceType.Queen => 900,
        PieceType.King => 10000,
        _ => 0,
    };

    // private static readonly int[] START_PIECES_COUNTS = { 0, 8, 2, 2, 2, 1, 0 };
    // private static readonly int START_PIECES_VALUE = 2 * PIECE_VALUES.Zip(START_PIECES_COUNTS, (x, y) => x * y).Sum();
    private static float CalculateEndgameFactor(int piecesValueSum) => 1F - Math.Min(1F, (piecesValueSum - 1000F) / 7880F);

    // TODO: https://www.youtube.com/watch?v=U4ogK0MIzqk&t=430s
    //	- transpositions -> Probably uses up to many tokens
	//	- maybe add a small amount of openings via uint64 values in
    // TODO: work again through video for ideas: https://www.youtube.com/watch?v=_vqlIPDR2TU (current 19:22)
	//	- search extension on checkmate
    // TODO: look at source for more ideas: https://github.com/SebLague/Chess-Coding-Adventure/tree/Chess-V2-UCI/Chess-Coding-Adventure/src/Core

	// TODO: check why BotMk02 is doing so well compared to following versions with more functionality, see what change was it that broke it, why is BotMk03 so much worse!!!


    // ----------------
    // FUNCTION CODE
    // ----------------

    // private readonly Random random = new();
    private Board board;
	private int minimaxDepth;
	private Move bestMoveOverall;
	private Move bestMoveInIteration;
	private bool isSearchCancelled;

	// private void TestMap()  // TEST: to check if maps are defined as expected
	// {
	// 	var actualMap = POSITION_MAP_QUEEN;
	// 	int[] expectedMap = {
	// 		-20,-10,-10, -5, -5,-10,-10,-20,
	// 		-10,  0,  0,  0,  0,  0,  0,-10,
	// 		-10,  0,  5,  5,  5,  5,  0,-10,
	// 		-5,  0,  5,  5,  5,  5,  0, -5,
	// 		0,  0,  5,  5,  5,  5,  0, 0,
	// 		-10,  5,  5,  5,  5,  5,  5,-10,
	// 		-10,  0,  5,  0,  0,  5,  0,-10,
	// 		-20,-10,-10, -5, -5,-10,-10,-20
	// 	};
	// 	if (!actualMap.SequenceEqual(expectedMap))
	// 	{
	// 		Console.WriteLine("actualMap and expectedMap not equal:");
	// 		for (int i = 0; i < 8; i++)
	// 		{
	// 			for (int j = 0; j < 8; j++)
	// 			{
	// 				Console.Write(actualMap[i * 8 + j] - expectedMap[i * 8 + j] + " ");
	// 			}
	// 			Console.WriteLine();
	// 		}
	// 	}
	// }

	private static int[] CreatePositionMap(ulong file1, ulong file2, ulong file3, ulong file4)
	{
		byte[] byteArray = BitConverter.GetBytes(file1).Concat(BitConverter.GetBytes(file2)).Concat(BitConverter.GetBytes(file3)).Concat(BitConverter.GetBytes(file4)).ToArray();
		int[] map = new int[64];
		// Shift, mirror and transpose:
		for (int i = 0; i < 4; i++)
		{
			for (int j = 0; j < 8; j++)
				map[j * 8 + i] = map[j * 8 + 7 - i] = byteArray[i * 8 + j] - 100;
		}
		return map;
	}

	private int CalculateMoveRating(Move move)
	{
		// TODO: implement stuff like:
		// - King should be at back at start and at front when winning / late game -> Reduce king moving at start besides castle
		// - Advance pawns when winning
		// - Queen should not move at start
		// - Avoid repetitions!!!
		// TODO: look here for inspiration: https://github.com/SebLague/Chess-Coding-Adventure/blob/abcb1e311a7fec0393e0b7d2ddf4920ab3baa41b/Chess-Coding-Adventure/src/Core/Search/MoveOrdering.cs#L54

		if (move == bestMoveOverall)
			return CHECKMATE_VALUE;

		int moveRating = 0;
		if (move.IsCapture)
			moveRating += CAPTURE_VALUE_FACTOR * GetPieceValue(move.CapturePieceType) - GetPieceValue(move.MovePieceType);  // TODO: maybe this can be improved
		if (move.IsPromotion)
			moveRating += GetPieceValue(move.PromotionPieceType);
		if (move.IsCastles)
			moveRating += CASTLE_VALUE;
		if (board.SquareIsAttackedByOpponent(move.TargetSquare))
			moveRating -= GetPieceValue(move.MovePieceType);

		return moveRating;
	}

	private Move[] SortMoves(Move[] moves)
	{
		Dictionary<Move, int> moveRatings = new();
		foreach (var move in moves)
			moveRatings[move] = CalculateMoveRating(move);
		Array.Sort(moves, delegate(Move moveA, Move moveB) { return moveRatings[moveA].CompareTo(moveRatings[moveB]); });
		return moves;
	}

	private int EvaluatePieces(bool forWhite)
	{
		int eval = 0;
		foreach (PieceType pieceType in EVAL_PIECE_TYPES)
			eval += GetPieceValue(pieceType) * board.GetPieceList(pieceType, forWhite).Count;
		return eval;
	}

	private int EvaluatePositions(bool forWhite, PieceType pieceType, int[] positionMap)  // TODO: mabye less tokens when merging all positionMaps into one
	{
		int eval = 0;
		PieceList pieceList = board.GetPieceList(pieceType, forWhite);
		for (int i = 0; i < pieceList.Count; i++)
		{
			var square = pieceList.GetPiece(i).Square;
			if (!forWhite)
				square = new Square(square.File, 7 - square.Rank);
			eval += positionMap[square.Index];
		}
		return eval;
	}

	private int EvaluateKings(bool forWhite)
	{
		int eval = 0;
		var opponentKingSquare = board.GetKingSquare(!forWhite);
		var ownKingSquare = board.GetKingSquare(forWhite);

		// Evaluate opponents king distance from center:
		// This helps getting the opponents king to the edges to make it easier to checkmate him
		eval += KING_PUSH_TO_EDGE_FACTOR * (Math.Max(3 - opponentKingSquare.File, opponentKingSquare.File - 4) + Math.Max(3 - opponentKingSquare.Rank, opponentKingSquare.Rank - 4));

		// Evaluate distance between kings:
		// This helps getting the own king close to the opponents king to make it easier to checkmate him
		eval += KING_MOVE_CLOSER_FACTOR * (14 - Math.Abs(ownKingSquare.File - opponentKingSquare.File) - Math.Abs(ownKingSquare.Rank - opponentKingSquare.Rank));

		return eval;
	}

	private int EvaluateBoard()
	{
		// TODO: implement stuff like:
		// - King should be behind pawns
		// - Pawns should advance but be covered
		// TODO: look here for inspiration: https://github.com/SebLague/Chess-Coding-Adventure/blob/abcb1e311a7fec0393e0b7d2ddf4920ab3baa41b/Chess-Coding-Adventure/src/Core/Evaluation/Evaluation.cs#L173

		int evalPiecesWhite = EvaluatePieces(true);
		int evalPiecesBlack = EvaluatePieces(false);
		float endgameFactor = CalculateEndgameFactor(evalPiecesWhite + evalPiecesBlack);

		int eval = evalPiecesWhite - evalPiecesBlack;
		eval += (int)((1F - endgameFactor) * (EvaluatePositions(true, PieceType.Pawn, POSITION_MAP_PAWN_START) - EvaluatePositions(false, PieceType.Pawn, POSITION_MAP_PAWN_START)));
		eval += (int)(endgameFactor * (EvaluatePositions(true, PieceType.Pawn, POSITION_MAP_PAWN_END) - EvaluatePositions(false, PieceType.Pawn, POSITION_MAP_PAWN_END)));
		eval += EvaluatePositions(true, PieceType.Knight, POSITION_MAP_KNIGHT) - EvaluatePositions(false, PieceType.Knight, POSITION_MAP_KNIGHT);
		eval += EvaluatePositions(true, PieceType.Bishop, POSITION_MAP_BISHOP) - EvaluatePositions(false, PieceType.Bishop, POSITION_MAP_BISHOP);
		eval += EvaluatePositions(true, PieceType.Rook, POSITION_MAP_ROOK) - EvaluatePositions(false, PieceType.Rook, POSITION_MAP_ROOK);
		eval += EvaluatePositions(true, PieceType.Queen, POSITION_MAP_QUEEN) - EvaluatePositions(false, PieceType.Queen, POSITION_MAP_QUEEN);

		eval *= board.IsWhiteToMove ? 1 : -1;
		if (eval > MIN_WINNING_VALUE)
			eval += (int)(endgameFactor * EvaluateKings(board.IsWhiteToMove));
		return eval;
	}

	private int MiniMax(int depth, int alpha, int beta)
	{
		if (isSearchCancelled)
			return 0;

		// TODO: Maybe implement a transposition table to store already evaluated posistions (256mb storage allowed)

		if (depth <= 0)
		{
			int eval = EvaluateBoard();
			if (eval >= beta)
				return beta;
			alpha = Math.Max(alpha, eval);
		}

		if (board.IsInCheckmate())
			return -CHECKMATE_VALUE;
		if (board.IsDraw())
			return 0;

		var moves = SortMoves(board.GetLegalMoves(depth <= 0));
		foreach (Move move in moves)
		{
			board.MakeMove(move);
			int eval = -MiniMax(depth - 1, -beta, -alpha);
			board.UndoMove(move);

			if (isSearchCancelled)
				return 0;

			if (eval > alpha)
			{
				alpha = eval;
				if (depth == minimaxDepth)
				{
					bestMoveInIteration = move;
				}
				if (alpha >= beta)
				{
					break;
				}
			}
		}
		return alpha;
	}

	private int CalculateThinkTime(Timer timer)  // TODO: improve calculation
	{
		float thinkTime = Math.Min(120.0F, timer.MillisecondsRemaining / 40.0F);
		if (timer.MillisecondsRemaining > timer.IncrementMilliseconds * 2)
		{
			thinkTime += timer.IncrementMilliseconds * 0.8F;
		}
		return (int)Math.Ceiling(Math.Max(thinkTime, Math.Min(50.0F, timer.MillisecondsRemaining * 0.25F)));
	}

	public Move Think(Board currentBoard, Timer timer)
	{
		// TestMap();  // TEST:
		board = currentBoard;
		Console.WriteLine("-------- " + GetType().Name + " --------");  // TEST:
		var isCheckmateFound = false;  // TEST:
		bestMoveOverall = Move.NullMove;
		isSearchCancelled = false;
		var cancelSearchTimer = new System.Threading.CancellationTokenSource();
		System.Threading.Tasks.Task.Delay(CalculateThinkTime(timer), cancelSearchTimer.Token).ContinueWith((task) => isSearchCancelled = true);
		// Console.WriteLine("Think time = " + CalculateThinkTime(timer));  // TEST:
		for (minimaxDepth = 0; minimaxDepth < 128; minimaxDepth++)
		{
			bestMoveInIteration = Move.NullMove;
			var lastEval = MiniMax(minimaxDepth, -CHECKMATE_VALUE, CHECKMATE_VALUE);
			if (isCheckmateFound == false && Math.Abs(lastEval) == CHECKMATE_VALUE)  // TEST:
			{
				Console.WriteLine((board.IsWhiteToMove == (Math.Sign(lastEval) == 1) ? "White" : "Black") + " checkmates in " + minimaxDepth.ToString() + " turn(s)");
				isCheckmateFound = true;
			}
			if (bestMoveInIteration != Move.NullMove)
				bestMoveOverall = bestMoveInIteration;
			if (isSearchCancelled)
				break;
		}
		Console.WriteLine("Max minimaxDepth = " + minimaxDepth);  // TEST:
		Console.WriteLine("endgameFactor = " + CalculateEndgameFactor(EvaluatePieces(true) + EvaluatePieces(false)));  // TEST:
		if (bestMoveOverall == Move.NullMove)
			bestMoveOverall = SortMoves(board.GetLegalMoves())[0];
		return bestMoveOverall;
	}
}