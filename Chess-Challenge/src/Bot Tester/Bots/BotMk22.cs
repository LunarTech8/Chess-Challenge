using ChessChallenge.API;
using System;
// using System.Numerics;
using System.Collections.Generic;
using System.Linq;

using static System.Math;
using static ChessChallenge.API.PieceType;
using static System.BitConverter;

// Like BotMk21 but with switch instead of array for GetPieceValue
public class BotMk22 : IChessBot
{
	// ----------------
	// DATA CODE
	// ----------------

	// HexValue = TransformAndRightToLeft(DecValue + 100)
	private readonly int[][] POSITION_MAPS = {
		// DecValues: +00 +50 +10 +05 +00 +05 +05 +00
		// DecValues: +00 +50 +10 +05 +00 -05 +10 +00
		// DecValues: +00 +50 +20 +10 +00 -10 +10 +00
		// DecValues: +00 +50 +30 +25 +20 +00 -20 +00
		CreatePositionMap(0x64696964696E9664, 0x646E5F64696E9664, 0x646E5A646E789664, 0x645064787D829664),  // POSITION_MAP_PAWN_START

		// DecValues: -50 -40 -30 -30 -30 -30 -40 -50
		// DecValues: -40 -20 +00 +05 +00 +05 -20 -40
		// DecValues: -30 +00 +10 +15 +15 +10 +00 -30
		// DecValues: -30 +00 +15 +20 +20 +15 +05 -30
		CreatePositionMap(0x323C464646463C32, 0x323C464646463C32, 0x46646E73736E6446, 0x4669737878736446),  // POSITION_MAP_KNIGHT

		// DecValues: -20 -10 -10 -10 -10 -10 -10 -20
		// DecValues: -10 +00 +00 +05 +00 +10 +05 -10
		// DecValues: -10 +00 +05 +05 +10 +10 +00 -10
		// DecValues: -10 +00 +10 +10 +10 +10 +00 -10
		CreatePositionMap(0x505A5A5A5A5A5A50, 0x5A696E646964645A, 0x5A646E6E6969645A, 0x5A646E6E6E6E645A),  // POSITION_MAP_BISHOP

		// DecValues: +00 +05 -05 -05 -05 -05 -05 +00
		// DecValues: +00 +10 +00 +00 +00 +00 +00 +00
		// DecValues: +00 +10 +00 +00 +00 +00 +00 +00
		// DecValues: +00 +10 +00 +00 +00 +00 +00 +05
		CreatePositionMap(0x645F5F5F5F5F6964, 0x6464646464646E64, 0x6464646464646E64, 0x6964646464646E64),  // POSITION_MAP_ROOK

		// DecValues: -20 -10 -10 -05 +00 -10 -10 -20
		// DecValues: -10 +00 +00 +00 +00 +05 +00 -10
		// DecValues: -10 +00 +05 +05 +05 +05 +05 -10
		// DecValues: -05 +00 +05 +05 +05 +05 +00 -05
		CreatePositionMap(0x505A5A645F5A5A50, 0x5A6469646464645A, 0x5A6969696969645A, 0x5F6469696969645F),  // POSITION_MAP_QUEEN

		// DecValues: -80 -60 -40 -30 -20 -10 +20 +20
		// DecValues: -70 -60 -50 -40 -30 -20 +20 +30
		// DecValues: -70 -60 -50 -40 -30 -20 -05 +10
		// DecValues: -70 -60 -60 -50 -40 -20 -05 +00
		CreatePositionMap(0x78785A50463C2814, 0x827850463C32281E, 0x6E5F50463C32281E, 0x645F503C3228281E),  // POSITION_MAP_KING_START

		// DecValues: +00 +80 +50 +30 +20 +10 +10 +00
		// DecValues: +00 +80 +50 +30 +20 +10 +10 +00
		// DecValues: +00 +80 +50 +30 +20 +10 +10 +00
		// DecValues: +00 +80 +50 +30 +20 +10 +10 +00
		CreatePositionMap(0x646E6E788296B464, 0x646E6E788296B464, 0x646E6E788296B464, 0x646E6E788296B464),  // POSITION_MAP_PAWN_END

		// DecValues: -20 -05 -10 -15 -20 -25 -30 -50
		// DecValues: -10 +00 -05 -10 -15 -20 -25 -30
		// DecValues: -10 +05 +20 +35 +30 +20 +00 -30
		// DecValues: -10 +05 +30 +45 +40 +25 +00 -30
		CreatePositionMap(0x32464B50555A5F50, 0x464B50555A5F645A, 0x466478828778695A, 0x46647D8C9182695A)   // POSITION_MAP_KING_END
	};

	// TODO: inlining following will save 4+11*5=59 tokens
	private readonly PieceType[] EVAL_PIECE_TYPES = {Pawn, Knight, Bishop, Rook, Queen};  // inline with: new[] {Pawn, Knight, Bishop, Rook, Queen}
	private const int CHECKMATE_VALUE = 1000000000;
	private const int MIN_WINNING_VALUE = 200;
	private const int RATING_BIAS_CAPTURE_WIN = 8000000;
	private const int RATING_BIAS_CAPTURE_LOSE = 2000000;
	private const int RATING_BIAS_PROMOTE = 6000000;
	private const int RATING_BIAS_CASTLE = 1000000;
	private const int RATING_BIAS_ATTACKED = 50;
	private const int KING_PUSH_TO_EDGE_FACTOR = 10;
	private const int KING_MOVE_CLOSER_FACTOR = 4;
	private const float MIN_THINK_TIME = 120.0F;
	private const float ENDGAME_FACTOR_OFFSET = 1000.0F;

    // private readonly int[] PIECE_VALUES = {0, 100, 300, 320, 500, 900, 10000};  // Piece values: null, pawn, knight, bishop, rook, queen, king
    private int GetPieceValue(PieceType pieceType) => pieceType switch
    {
        Pawn => 100,
        Knight => 300,
        Bishop => 320,
        Rook => 500,
        Queen => 900,
        King => 10000,
        _ => 0,
    };
	// private int GetPieceValue(PieceType pieceType) => new int[] {0, 100, 300, 320, 500, 900, 10000}[(int)pieceType];  // Piece values: null, pawn, knight, bishop, rook, queen, king

    // private static readonly int[] START_PIECES_COUNTS = { 0, 8, 2, 2, 2, 1, 0 };
    // private static readonly int START_PIECES_VALUE = 2 * PIECE_VALUES.Zip(START_PIECES_COUNTS, (x, y) => x * y).Sum();
    private float CalculateEndgameFactor(int piecesValueSum) => 1.0F - Min(1.0F, (piecesValueSum - ENDGAME_FACTOR_OFFSET) / 7880.0F);
	// FIXME: make king less aggressive at start or test different endgame factor calculations

    // TODO: https://www.youtube.com/watch?v=U4ogK0MIzqk&t=430s
    //	- transpositions -> Probably uses up to many tokens
	//	- maybe add a small amount of openings via uint64 values in -> Probably uses up to many tokens
    // TODO: https://www.youtube.com/watch?v=_vqlIPDR2TU
	//	- search extension on checkmate
	//	- push passed pawns (bitboards)
	//	- isolated pawns (bitboards)
	//	- killer moves (move order)
	//	- late move search reduction
    // TODO: look at source for more ideas: https://github.com/SebLague/Chess-Coding-Adventure/tree/Chess-V2-UCI/Chess-Coding-Adventure/src/Core


    // ----------------
    // FUNCTION CODE
    // ----------------

    private Board board;
	private int minimaxDepth;
	private Move bestMoveOverall;
	private Move bestMoveInIteration;
	private bool isSearchCancelled;
	private float endgameFactor;

	private static int[] CreatePositionMap(ulong file1, ulong file2, ulong file3, ulong file4)
	{
		var byteArray = GetBytes(file1).Concat(GetBytes(file2)).Concat(GetBytes(file3)).Concat(GetBytes(file4)).ToArray();
		var map = new int[64];
		// Shift, mirror and transpose:
		for (int i = 0; i < 4; i++)
			for (int j = 0; j < 8; j++)
				map[j * 8 + i] = map[j * 8 + 7 - i] = byteArray[i * 8 + j] - 100;
		return map;
	}

	private int CalculateMoveRating(Move move)
	{
		// TODO: implement stuff like:
		// - King should be at back at start and at front when winning / late game -> Reduce king moving at start besides castle
		// - Advance pawns when winning
		// - Queen should not move at start
		// - Avoid repetitions
		// TODO: look here for inspiration: https://github.com/SebLague/Chess-Coding-Adventure/blob/abcb1e311a7fec0393e0b7d2ddf4920ab3baa41b/Chess-Coding-Adventure/src/Core/Search/MoveOrdering.cs#L54

		if (move == bestMoveOverall)
			return CHECKMATE_VALUE;

		int moveRating = 0;
		bool isTargetSquareAttacked = board.SquareIsAttackedByOpponent(move.TargetSquare);
		if (move.IsCapture)
		{
			moveRating += GetPieceValue(move.CapturePieceType) - GetPieceValue(move.MovePieceType);
			moveRating += moveRating < 0 && isTargetSquareAttacked ? RATING_BIAS_CAPTURE_LOSE : RATING_BIAS_CAPTURE_WIN;
		}
		else
		{
			if (move.IsPromotion)
				moveRating += RATING_BIAS_PROMOTE;
			if (move.IsCastles)
				moveRating += RATING_BIAS_CASTLE;
			if (isTargetSquareAttacked)
				moveRating -= RATING_BIAS_ATTACKED;
			int mapIdx = (int)move.MovePieceType - 1;
			if (mapIdx != 0 && mapIdx != 5)
				moveRating += POSITION_MAPS[mapIdx][move.TargetSquare.Index] - POSITION_MAPS[mapIdx][move.StartSquare.Index];
		}

		return moveRating;
	}

	private Move[] SortMoves(Move[] moves)
	{
		// TODO: maybe an array for the moveRatings can be used instead of a dict to speed it up
		Dictionary<Move, int> moveRatings = new();
		foreach (var move in moves)
			moveRatings[move] = CalculateMoveRating(move);
		Array.Sort(moves, (moveA, moveB) => moveRatings[moveB].CompareTo(moveRatings[moveA]));
		return moves;
	}

	private int EvaluatePieces(bool forWhite)
	{
		int eval = 0;
		foreach (var pieceType in EVAL_PIECE_TYPES)
			eval += GetPieceValue(pieceType) * board.GetPieceList(pieceType, forWhite).Count;
		return eval;
	}

	private int EvaluatePositions(bool forWhite)
	{
		int eval = 0;
		for (int mapIdxStart = 0; mapIdxStart < 6; mapIdxStart++)
		{
			int mapIdxEnd = mapIdxStart == 0 ? 6 : mapIdxStart == 5 ? 7 : -1;
			// PieceList pieceList = board.GetPieceList((PieceType)(mapIdxStart + 1), forWhite);
			// for (int pieceIdx = 0; pieceIdx < pieceList.Count; pieceIdx++)
			// {
			// 	var square = pieceList.GetPiece(pieceIdx).Square;
			// 	if (!forWhite)
			// 		square = new(square.File, 7 - square.Rank);
			// 	// Evaluate position with endgame factor:
			// 	if (mapIdxEnd >= 0)
			// 		eval += (int)((1F - endgameFactor) * POSITION_MAPS[mapIdxStart][square.Index] + endgameFactor * POSITION_MAPS[mapIdxEnd][square.Index]);
			// 	else
			// 		eval += POSITION_MAPS[mapIdxStart][square.Index];
			// }
			foreach (var square in board.GetPieceList((PieceType)(mapIdxStart + 1), forWhite).Select(piece => forWhite ? piece.Square : new(piece.Square.File, 7 - piece.Square.Rank)))
				eval += mapIdxEnd < 0 ? POSITION_MAPS[mapIdxStart][square.Index] : (int)((1F - endgameFactor) * POSITION_MAPS[mapIdxStart][square.Index] + endgameFactor * POSITION_MAPS[mapIdxEnd][square.Index]);
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
		eval += KING_PUSH_TO_EDGE_FACTOR * (Max(3 - opponentKingSquare.File, opponentKingSquare.File - 4) + Max(3 - opponentKingSquare.Rank, opponentKingSquare.Rank - 4));

		// Evaluate distance between kings:
		// This helps getting the own king close to the opponents king to make it easier to checkmate him
		eval += KING_MOVE_CLOSER_FACTOR * (14 - Abs(ownKingSquare.File - opponentKingSquare.File) - Abs(ownKingSquare.Rank - opponentKingSquare.Rank));

		return eval;
	}

	private int EvaluateBoard()
	{
		// TODO: implement stuff like:
		// - King should be behind pawns
		// - Pawns should advance but be covered
		// TODO: look here for inspiration: https://github.com/SebLague/Chess-Coding-Adventure/blob/abcb1e311a7fec0393e0b7d2ddf4920ab3baa41b/Chess-Coding-Adventure/src/Core/Evaluation/Evaluation.cs#L173

		int evalPiecesWhite = EvaluatePieces(true), evalPiecesBlack = EvaluatePieces(false);
		endgameFactor = CalculateEndgameFactor(evalPiecesWhite + evalPiecesBlack);

		int eval = evalPiecesWhite - evalPiecesBlack + EvaluatePositions(true) - EvaluatePositions(false);
		eval *= board.IsWhiteToMove ? 1 : -1;
		if (eval > MIN_WINNING_VALUE)
			eval += (int)(endgameFactor * EvaluateKings(board.IsWhiteToMove));
		return eval;
	}

	private int MiniMax(int depth, int alpha, int beta)
	{
		if (isSearchCancelled)
			return 0;

		// Quiescence search:
		if (depth <= 0)
		{
			int eval = EvaluateBoard();
			if (eval >= beta)
				return beta;
			alpha = Max(alpha, eval);
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
					bestMoveInIteration = move;
				if (alpha >= beta)
					break;
			}
		}
		return alpha;
	}

	private int CalculateThinkTime(Timer timer)  // TODO: improve calculation
	{
		float thinkTime = Min(MIN_THINK_TIME, timer.MillisecondsRemaining / 40.0F);
		if (timer.MillisecondsRemaining > timer.IncrementMilliseconds * 2)
			thinkTime += timer.IncrementMilliseconds * 0.8F;
		return (int)Ceiling(Max(thinkTime, Min(60.0F, timer.MillisecondsRemaining * 0.25F)));
	}

	public Move Think(Board currentBoard, Timer timer)
	{
		var nullMove = Move.NullMove;
		board = currentBoard;
		Console.WriteLine("-------- " + GetType().Name + " --------");  // TEST:
		var isCheckmateFound = false;  // TEST:
		bestMoveOverall = nullMove;
		isSearchCancelled = false;
		var cancelSearchTimer = new System.Threading.CancellationTokenSource();
		System.Threading.Tasks.Task.Delay(CalculateThinkTime(timer), cancelSearchTimer.Token).ContinueWith(task => isSearchCancelled = true);
		Console.WriteLine("Think time = " + CalculateThinkTime(timer));  // TEST:
		for (minimaxDepth = 0; minimaxDepth < 128; minimaxDepth++)
		{
			bestMoveInIteration = nullMove;
			var lastEval = MiniMax(minimaxDepth, -CHECKMATE_VALUE, CHECKMATE_VALUE);
			if (isCheckmateFound == false && Abs(lastEval) == CHECKMATE_VALUE)  // TEST:
			{
				Console.WriteLine((board.IsWhiteToMove == (Sign(lastEval) == 1) ? "White" : "Black") + " checkmates in " + minimaxDepth.ToString() + " turn(s)");
				isCheckmateFound = true;
			}
			if (bestMoveInIteration != nullMove)
				bestMoveOverall = bestMoveInIteration;
			if (isSearchCancelled)
				break;
		}
		Console.WriteLine("Max minimaxDepth = " + minimaxDepth);  // TEST:
		Console.WriteLine("endgameFactor = " + CalculateEndgameFactor(EvaluatePieces(true) + EvaluatePieces(false)));  // TEST:
		if (bestMoveOverall == nullMove)
			bestMoveOverall = SortMoves(board.GetLegalMoves())[0];
		return bestMoveOverall;
	}
}