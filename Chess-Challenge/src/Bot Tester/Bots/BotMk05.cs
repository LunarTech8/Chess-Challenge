using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;

public class BotMk05 : IChessBot
{
	// ----------------
	// DATA CODE
	// ----------------

	private static readonly int CHECKMATE_VALUE = 1000000000;

	// TODO: might be better to use a function instead of PIECE_VALUES to lower token usage
	private static readonly int[] PIECE_VALUES = { 0, 100, 300, 300, 500, 900, 10000 };  // Piece values: null, pawn, knight, bishop, rook, queen, king
	private static readonly int[] START_PIECES_COUNTS = { 0, 8, 2, 2, 2, 1, 0 };
	private static readonly int START_PIECES_VALUE = 2 * PIECE_VALUES.Zip(START_PIECES_COUNTS, (x, y) => x * y).Sum();
	private static readonly int CAPTURE_VALUE_FACTOR = 10;
	private static readonly int CASTLE_VALUE = 500;
	private static readonly int KING_PUSH_TO_EDGE_FACTOR = 10;
	private static readonly int KING_MOVE_CLOSER_FACTOR = 4;

	// TODO: work again through video for ideas: https://www.youtube.com/watch?v=U4ogK0MIzqk&t=430s (current 23:07)
	//	- transpositions -> Probably uses up to many tokens
	//	- openings
	// TODO: work again through video for ideas: https://www.youtube.com/watch?v=_vqlIPDR2TU (current 11:08)
	// TODO: look at source for more ideas: https://github.com/SebLague/Chess-Coding-Adventure/tree/Chess-V2-UCI/Chess-Coding-Adventure/src/Core


	// ----------------
	// FUNCTION CODE
	// ----------------

	private Board board;
	private int minimaxDepth;
	private Move bestMoveOverall;
	private Move bestMoveInIteration;
	private bool isSearchCancelled;
	private readonly Random random = new();

	private int CalculateMoveRating(Move move)
	{
		if (move == bestMoveOverall)
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
		// - Advance pawns at start to middle
		// - Avoid repetitions
		// TODO: look here for inspiration: https://github.com/SebLague/Chess-Coding-Adventure/blob/abcb1e311a7fec0393e0b7d2ddf4920ab3baa41b/Chess-Coding-Adventure/src/Core/Search/MoveOrdering.cs#L54
	}

	private Move[] SortMoves(Move[] moves)
	{
		// TODO: probably faster to store the ratings for each move instead of getting them for each compare
		Array.Sort(moves, delegate(Move moveA, Move moveB) { return CalculateMoveRating(moveB).CompareTo(CalculateMoveRating(moveA)); });
		return moves;
	}

	private float CalculateEndgameFactor(int evalAllPieces)
	{
		return 1F - Math.Min(1F, evalAllPieces / (float)START_PIECES_VALUE);
	}

	private int EvaluateKings(float endgameFactor)
	{
		int eval = 0;
		var opponentKingSquare = board.GetKingSquare(!board.IsWhiteToMove);
		var ownKingSquare = board.GetKingSquare(board.IsWhiteToMove);

		// Evaluate opponents king distance from center:
		// This helps getting the opponents king to the edges to make it easier to checkmate him
		eval += KING_PUSH_TO_EDGE_FACTOR * (Math.Max(3 - opponentKingSquare.File, opponentKingSquare.File - 4) + Math.Max(3 - opponentKingSquare.Rank, opponentKingSquare.Rank - 4));

		// Evaluate distance between kings:
		// This helps getting the own king close to the opponents king to make it easier to checkmate him
		eval += KING_MOVE_CLOSER_FACTOR * (14 - Math.Abs(ownKingSquare.File - opponentKingSquare.File) + Math.Abs(ownKingSquare.Rank - opponentKingSquare.Rank));

		return (int)Math.Round(eval * endgameFactor);
	}

	private int EvaluatePieces(bool forWhite)
	{
		int eval = 0;
		foreach (PieceType pieceType in Enum.GetValues(typeof(PieceType)))
		{
			if (pieceType != PieceType.None && pieceType != PieceType.King)
				eval += PIECE_VALUES[(int)pieceType] * board.GetPieceList(pieceType, forWhite).Count;
		}
		return eval;
	}

	private int EvaluateBoard()
	{
		// TODO: look here for inspiration: https://github.com/SebLague/Chess-Coding-Adventure/blob/abcb1e311a7fec0393e0b7d2ddf4920ab3baa41b/Chess-Coding-Adventure/src/Core/Evaluation/Evaluation.cs#L173
		int evalPiecesWhite = EvaluatePieces(true);
		int evalPiecesBlack = EvaluatePieces(false);
		return (evalPiecesWhite - evalPiecesBlack) * (board.IsWhiteToMove ? 1 : -1) + EvaluateKings(CalculateEndgameFactor(evalPiecesWhite + evalPiecesBlack));
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
		if (bestMoveOverall == Move.NullMove)
			bestMoveOverall = SortMoves(board.GetLegalMoves())[0];
		return bestMoveOverall;
	}
}