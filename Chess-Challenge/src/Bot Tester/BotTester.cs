using System;
using System.IO;
using ChessChallenge.API;

namespace ChessChallenge.Application
{
	static class BotTester
	{
		const int TEST_QUEUE_REQUIRED_WORDS = 6;
		static readonly string TEST_QUEUE_PATH = Path.Combine(Directory.GetCurrentDirectory(), "src", "Bot Tester", "TestQueue.txt");
		static readonly string TEST_RESULTS_PATH = Path.Combine(Directory.GetCurrentDirectory(), "src", "Bot Tester", "TestResults.txt");

        private static Type testBotA;
        private static Type testBotB;
        private static int numberOfGames = 0;

        public static Type TestBotA { get => testBotA; set => testBotA = value; }
        public static Type TestBotB { get => testBotB; set => testBotB = value; }
        public static int NumberOfGames { get => numberOfGames; set => numberOfGames = value; }

		public static bool IsBotTestMatch(Type whiteBotType, Type blackBotType)
		{
			return numberOfGames > 0 && ((whiteBotType == TestBotA && blackBotType == TestBotB) || (whiteBotType == TestBotB && blackBotType == TestBotA));
		}

        public static bool ExtractDataFromQueue()
		{
			try
			{
				string tempFilePath = Path.GetTempFileName();

				using StreamReader reader = new(TEST_QUEUE_PATH);
				using StreamWriter writer = new(tempFilePath);

				if (reader.EndOfStream)
					throw new Exception("No match queued in '" + TEST_QUEUE_PATH + "'");

				string[] words = reader.ReadLine().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

				// TODO: implement proper formating check
				if (words.Length != TEST_QUEUE_REQUIRED_WORDS)
					throw new Exception("Invalid formating in '" + TEST_QUEUE_PATH + "'");

				TestBotA = Type.GetType(words[0]) ?? throw new Exception("Invalid botA class name in '" + TEST_QUEUE_PATH + "'");
				TestBotB = Type.GetType(words[2]) ?? throw new Exception("Invalid botB class name in '" + TEST_QUEUE_PATH + "'");
				NumberOfGames = int.Parse(words[4]);

				while (!reader.EndOfStream)
					writer.WriteLine(reader.ReadLine());
				reader.Close();
				writer.Close();

				Console.WriteLine(writer.ToString());  // DEBUG:
				File.Delete(TEST_QUEUE_PATH);
				File.Move(tempFilePath, TEST_QUEUE_PATH);
				return true;
			}
			catch (Exception e)
			{
				Console.WriteLine("Extraction failed: " + e.Message);
				return false;
			}
		}

		public static void AddResult(int wins, int draws, int losses, int timeouts, int illegalMoves)
		{
            using StreamWriter writer = File.AppendText(TEST_RESULTS_PATH);

			string resultLine = TestBotA.Name + " vs " + TestBotB.Name + " outcome with " + NumberOfGames + " games: ";
			resultLine += string.Join(" - ", new[] {wins + " wins", draws + " draws", losses + " losses", timeouts + " timeouts", illegalMoves + " illegalMoves"});
            writer.WriteLine(resultLine);
        }
	}
}
