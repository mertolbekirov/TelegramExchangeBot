using System;
using Telegram.Bot;
using Telegram.Bot.Args;
using ExchangeSharp;

namespace TelegramExchangeBot
{
    class Program
    {
        static TelegramBotClient Bot = new TelegramBotClient("1894205668:AAH-a_C3DoWhPt84Co275R03cyMMU35iVVI");
        static string exchange;
        static string symbol;
        static string data;
        static void Main(string[] args)
        {
            Bot.StartReceiving();
            Bot.OnMessage += Bot_OnMessage;
            while (true)
            {
                if (exchange != null && symbol != null && data != null)
                {
                    Console.WriteLine("done");
                }
            }
            Console.ReadLine();
        }

        private static void Bot_OnMessage(object sender, MessageEventArgs e)
        {
            if (e.Message.Type == Telegram.Bot.Types.Enums.MessageType.Text)
            {
                if (e.Message.Text.StartsWith("/start"))
                {
                    Bot.SendTextMessageAsync(e.Message.Chat.Id, "Hi, good to see you! Here are the following commands to set me up properly:\n" +
                        "/exchange {exchange_name} to set the exchange platform (can be either Kraken or Binance)\n" +
                        "/symbol {global_symbol} to set the global symbol\n" +
                        "/data {data_type} to set the type of data (trades/candles)");
                }

                if (e.Message.Text.StartsWith("/exchange"))
                {
                    if (e.Message.Text.Split().Length <= 1)
                    {
                        Bot.SendTextMessageAsync(e.Message.Chat.Id, "Invalid command");
                        return;
                    }

                    var currEchange = e.Message.Text.Split()[1].ToLower();

                    if(currEchange!= "kraken" && currEchange != "binance")
                    {
                        Bot.SendTextMessageAsync(e.Message.Chat.Id, "Invalid exchange");
                    }
                    else
                    {
                        exchange = currEchange;
                    }
                }
                else if (e.Message.Text.StartsWith("/symbol"))
                {
                    if (e.Message.Text.Split().Length <= 1)
                    {
                        Bot.SendTextMessageAsync(e.Message.Chat.Id, "Invalid command");
                        return;
                    }

                    symbol = e.Message.Text.Split()[1];
                }
                else if (e.Message.Text.StartsWith("data"))
                {
                    if (e.Message.Text.Split().Length <= 1)
                    {
                        Bot.SendTextMessageAsync(e.Message.Chat.Id, "Invalid command");
                        return;
                    }

                    data = e.Message.Text.Split()[1];
                }


            }
        }

    }
}
