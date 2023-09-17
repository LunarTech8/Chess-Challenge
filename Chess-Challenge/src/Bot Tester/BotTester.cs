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

		private static void MarkCurrentGameAsFinished()
		{
			var tempFilePath = Path.GetTempFileName();
			using StreamReader reader = new(TEST_QUEUE_PATH);
			using StreamWriter writer = new(tempFilePath);

			bool marked = false;
			while (!reader.EndOfStream)
			{
				var line = reader.ReadLine();
				if (!marked && !line.StartsWith("//") && !line.StartsWith("FINISHED "))
				{
					line = "FINISHED " + line;
					marked = true;
				}
				writer.WriteLine(line);
			}

			reader.Close();
			writer.Close();
			File.Delete(TEST_QUEUE_PATH);
			File.Move(tempFilePath, TEST_QUEUE_PATH);
		}

		public static bool IsBotTestMatch(Type whiteBotType, Type blackBotType)
		{
			return numberOfGames > 0 && ((whiteBotType == TestBotA && blackBotType == TestBotB) || (whiteBotType == TestBotB && blackBotType == TestBotA));
		}

        public static bool ExtractDataFromQueue()
		{
			var tempFilePath = Path.GetTempFileName();
			using StreamReader reader = new(TEST_QUEUE_PATH);
			using StreamWriter writer = new(tempFilePath);

			try
			{
				NumberOfGames = 0;
				while (!reader.EndOfStream)
				{
					var line = reader.ReadLine();
					if (NumberOfGames <= 0 && !line.StartsWith("//") && !line.StartsWith("FINISHED "))
					{
						string[] words = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

						// TODO: implement proper formating check
						if (words.Length != TEST_QUEUE_REQUIRED_WORDS)
							throw new Exception("Invalid formating in '" + TEST_QUEUE_PATH + "'");

						TestBotA = Type.GetType(words[0]) ?? throw new Exception("Invalid botA class name in '" + TEST_QUEUE_PATH + "'");
						TestBotB = Type.GetType(words[2]) ?? throw new Exception("Invalid botB class name in '" + TEST_QUEUE_PATH + "'");
						NumberOfGames = int.Parse(words[4]);
					}
					writer.WriteLine(line);
				}
				if (NumberOfGames <= 0)
					throw new Exception("No valid match queued in '" + TEST_QUEUE_PATH + "'");

				reader.Close();
				writer.Close();
				File.Delete(TEST_QUEUE_PATH);
				File.Move(tempFilePath, TEST_QUEUE_PATH);
				return true;
			}
			catch (Exception e)
			{
				Console.WriteLine("Extraction failed: " + e.Message);
				reader.Close();
				writer.Close();
				return false;
			}
		}

		public static void AddResult(int wins, int draws, int losses, int timeouts, int illegalMoves, bool markCurrentGameAsFinished)
		{
            using StreamWriter writer = File.AppendText(TEST_RESULTS_PATH);

			string resultLine = TestBotA.Name + " vs " + TestBotB.Name + " outcome with " + NumberOfGames + " games: ";
			resultLine += string.Join(" - ", new[] {wins + " wins", draws + " draws", losses + " losses", timeouts + " timeouts", illegalMoves + " illegalMoves"});
            writer.WriteLine(resultLine);

			if (markCurrentGameAsFinished)
				MarkCurrentGameAsFinished();
        }
	}
}
