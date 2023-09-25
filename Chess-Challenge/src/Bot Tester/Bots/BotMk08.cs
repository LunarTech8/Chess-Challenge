using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;

// Like BotMk02 but without evaluating in PieceType.King in EvaluateBoard -> NO IMPACT
public class BotMk08 : IChessBot
{
	// ----------------
	// DATA CODE
	// ----------------

	private readonly int CHECKMATE_VALUE = 1000000000;

	// TODO: might be better to use a function instead of PIECE_VALUES to lower token usage
	private readonly int[] PIECE_VALUES = { 0, 100, 300, 300, 500, 900, 10000 };  // Piece values: null, pawn, knight, bishop, rook, queen, king
	private readonly int CAPTURE_VALUE_FACTOR = 10;
	private readonly int CASTLE_VALUE = 50;
	private readonly double KING_ENDGAME_FACTOR = 0.1;

	// TODO: work again through video for ideas: https://www.youtube.com/watch?v=U4ogK0MIzqk&t=430s (current 23:07)
	//	- transpositions -> Probably uses up to many tokens
	//	- openings
	// TODO: work again through video for ideas: https://www.youtube.com/watch?v=_vqlIPDR2TU (current 11:08)
	// TODO: look at source for more ideas: https://github.com/SebLague/Chess-Coding-Adventure/tree/Chess-V2-UCI/Chess-Coding-Adventure/src/Core


	// ----------------
	// FUNCTION CODE
	// ----------------

	private int minimaxDepth;
	private Move bestMoveOverall;
	private Move bestMoveInIteration;
	private bool isSearchCancelled;
	private readonly Random random = new();

	private int GetMoveRating(Board board, Move move, Move prioMove)
	{
		if (move == prioMove)  // TODO: bestMoveOverall might be checked here directly (prioMove could be removed)
			return CHECKMATE_VALUE;

		int moveRating = 0;
		if (move.IsCapture)
			// TODO: maybe this can be improved
			moveRating += CAPTURE_VALUE_FACTOR * PIECE_VALUES[(int)move.CapturePieceType] - PIECE_VALUES[(int)move.MovePieceType];
		if (move.IsPromotion)
			moveRating += PIECE_VALUES[(int)move.PromotionPieceType];
		if (move.IsCastles)
			moveRating += CASTLE_VALUE;
		if (board.SquareIsAttackedByOpponent(move.TargetSquare))
			moveRating -= PIECE_VALUES[(int)move.MovePieceType];

		return moveRating;
		// TODO: implement stuff like:
		// - King should be at back at start and at front when winning / late game
		// - Advance pawns at start
		// - Avoid repetitions
		// TODO: look here for inspiration: https://github.com/SebLague/Chess-Coding-Adventure/blob/abcb1e311a7fec0393e0b7d2ddf4920ab3baa41b/Chess-Coding-Adventure/src/Core/Search/MoveOrdering.cs#L54
	}

	private void SortMoves(ref Move[] moves, Board board, Move prioMove)
	{
		// TODO: probably faster to store the ratings for each move instead of getting them for each compare
		Array.Sort(moves, delegate(Move moveA, Move moveB) { return GetMoveRating(board, moveB, prioMove).CompareTo(GetMoveRating(board, moveA, prioMove)); });
	}

	private int EvaluateKings(Board board)  // TODO: improve
	{
		int eval = 0;
		var opponentKingSquare = board.GetKingSquare(!board.IsWhiteToMove);
		var ownKingSquare = board.GetKingSquare(board.IsWhiteToMove);

		// Evaluate opponents king distance from center:
		eval += Math.Max(3 - opponentKingSquare.File, opponentKingSquare.File - 4) + Math.Max(3 - opponentKingSquare.Rank, opponentKingSquare.Rank - 4);

		// Evaluate distance between kings:
		eval += 14 - Math.Abs(ownKingSquare.File - opponentKingSquare.File) + Math.Abs(ownKingSquare.Rank - opponentKingSquare.Rank);

		return (int)Math.Round(eval * 10 * KING_ENDGAME_FACTOR);
	}

	private int EvaluateBoard(Board board)
	{
		int eval = 0;
		var pieceLists = board.GetAllPieceLists();
		foreach (var pieceList in pieceLists)
		{
			if (pieceList.TypeOfPieceInList != PieceType.King)
				eval += PIECE_VALUES[(int)pieceList.TypeOfPieceInList] * pieceList.Count * (board.IsWhiteToMove == pieceList.IsWhitePieceList ? 1 : -1);
		}
		eval += EvaluateKings(board);
		// TODO: look here for inspiration: https://github.com/SebLague/Chess-Coding-Adventure/blob/abcb1e311a7fec0393e0b7d2ddf4920ab3baa41b/Chess-Coding-Adventure/src/Core/Evaluation/Evaluation.cs#L173
		return eval;
	}

	private int MiniMax(Board board, int depth, int alpha, int beta)
	{
		if (isSearchCancelled)
			return 0;

		// TODO: Maybe implement a transposition table to store already evaluated posistions (256mb storage allowed)

		if (depth <= 0)
		{
			int eval = EvaluateBoard(board);
			if (eval >= beta)
				return beta;
			alpha = Math.Max(alpha, eval);
		}

		if (board.IsInCheckmate())
			return -CHECKMATE_VALUE;
		if (board.IsDraw())
			return 0;

		var moves = board.GetLegalMoves(depth <= 0);
		SortMoves(ref moves, board, bestMoveOverall);
		foreach (Move move in moves)
		{
			board.MakeMove(move);
			int eval = -MiniMax(board, depth - 1, -beta, -alpha);
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
		double thinkTime = Math.Min(120.0, timer.MillisecondsRemaining / 40.0);
		if (timer.MillisecondsRemaining > timer.IncrementMilliseconds * 2)
		{
			thinkTime += timer.IncrementMilliseconds * 0.8;
		}
		return (int)Math.Ceiling(Math.Max(thinkTime, Math.Min(50.0, timer.MillisecondsRemaining * 0.25)));
	}

	public Move Think(Board board, Timer timer)
	{
		var isCheckmateFound = false;  // DEBUG:
		Console.WriteLine("-------- BotMk02 --------");  // DEBUG:
		isSearchCancelled = false;
		var cancelSearchTimer = new System.Threading.CancellationTokenSource();
		System.Threading.Tasks.Task.Delay(CalculateThinkTime(timer), cancelSearchTimer.Token).ContinueWith((task) => isSearchCancelled = true);
		// Console.WriteLine("Think time = " + CalculateThinkTime(timer));  // DEBUG:
		for (minimaxDepth = 0; minimaxDepth < 128; minimaxDepth++)
		{
			bestMoveInIteration = Move.NullMove;
			var lastEval = MiniMax(board, minimaxDepth, -CHECKMATE_VALUE, CHECKMATE_VALUE);
			if (isCheckmateFound == false && Math.Abs(lastEval) == CHECKMATE_VALUE)  // DEBUG:
			{
				Console.WriteLine((board.IsWhiteToMove == (Math.Sign(lastEval) == 1) ? "White" : "Black") + " checkmates in " + minimaxDepth.ToString() + " turn(s)");
				isCheckmateFound = true;
			}
			if (bestMoveInIteration != Move.NullMove)
				bestMoveOverall = bestMoveInIteration;
			if (isSearchCancelled)
				break;
		}
		Console.WriteLine("Max minimaxDepth = " + minimaxDepth);  // DEBUG:
		return bestMoveOverall;
	}
}