using System;
using Telegram.Bot;
using Telegram.Bot.Args;
using ExchangeSharp;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

namespace TelegramExchangeBot
{
    class Program
    {
        static TelegramBotClient Bot = new TelegramBotClient("1894205668:AAH-a_C3DoWhPt84Co275R03cyMMU35iVVI");
        static string exchange;
        static string symbol;
        static string data;
        static long chatId;
        static bool hasBeenSetUp;
        public static async Task Main(string[] args)
        {
            Bot.StartReceiving();
            Bot.OnMessage += Bot_OnMessage;

            while (true)
            {
                if (exchange != null && symbol != null && data != null)
                {
                    if (hasBeenSetUp)
                    {
                        continue;
                    }
                    Type type = GetExchangeType();

                    using var api = ExchangeAPI.GetExchangeAPI(type);

                    if (data.StartsWith("trade"))
                    {
                        await SendTradeInfo(api);
                    }
                    else if (data.StartsWith("candle"))
                    {
                         await SendCandleInfo(api);
                    }
                    hasBeenSetUp = true;
                }
                Thread.Sleep(50);
            }
            
        }

        private static async Task SendCandleInfo(IExchangeAPI api)
        {
            //I couldn't find a web socket for candles, so I just check for changes over and over again 
            var msg = await Bot.SendTextMessageAsync(chatId, "Loading...");
            var lastMsg = "";
            while (true)
            {
                var candles = await api.GetCandlesAsync(symbol, 1800, DateTime.Now - TimeSpan.FromMinutes(30));
                foreach (var candle in candles)
                {
                    var currMsg = $"High Price: {candle.HighPrice:f2}, Base Volume: {candle.BaseCurrencyVolume:f2}, Quote Volume: {candle.QuoteCurrencyVolume:f2}";
                    if (lastMsg == currMsg)
                    {
                        continue;
                    }
                    await Bot.EditMessageTextAsync(chatId, msg.MessageId, currMsg);
                    lastMsg = currMsg;
                    Thread.Sleep(1000);
                }
            }
        }

        private static async Task SendTradeInfo(IExchangeAPI api)
        {
            var msg = await Bot.SendTextMessageAsync(chatId, "Loading...");
            var socket = await api.GetTradesWebSocketAsync(message =>
            {
                string[] info = message.Value.ToString().Split(',').ToArray();
                Bot.EditMessageTextAsync(chatId, msg.MessageId, $"Trade {exchange.ToUpper()} {message.Key}\n" +
                    $"Time: {info[0]}\n" +
                    $"Price: {info[1].Split(':')[1]}\n" +
                    $"Amount: {info[2].Split(':')[1]}\n" +
                    $"Action: {info[3]}\n");
                Thread.Sleep(100);
                return Task.CompletedTask;
            }, symbol
            );

            Console.ReadLine();
            
        }

        private static Type GetExchangeType()
        {
            Type type;
            if (exchange == "binance")
            {
                type = typeof(ExchangeBinanceAPI);
            }
            else
            {
                type = typeof(ExchangeKrakenAPI);
            }

            return type;
        }


        private static void Bot_OnMessage(object sender, MessageEventArgs e)
        {
            if (e.Message.Type == Telegram.Bot.Types.Enums.MessageType.Text)
            {
                chatId = e.Message.Chat.Id;
                if (e.Message.Text.StartsWith("/start"))
                {
                    Bot.SendTextMessageAsync(e.Message.Chat.Id, "Hi, good to see you! Here are the following commands to set me up properly:\n" +
                        "/exchange {exchange_name} to set the exchange platform (can be either Kraken or Binance)\n" +
                        "/symbol {global_symbol} to set the global symbol\n" +
                        "/data {data_type} to set the type of data (trades/candles)\n" +
                        "You can also set me up with the following format {exchange} {global symbol} {trade/candle}");
                }
               else  if (e.Message.Text.StartsWith("/exchange"))
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
                        Bot.SendTextMessageAsync(e.Message.Chat.Id, "Exchange set!");
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
                else if (e.Message.Text.StartsWith("/data"))
                {
                    if (e.Message.Text.Split().Length <= 1)
                    {
                        Bot.SendTextMessageAsync(e.Message.Chat.Id, "Invalid command");
                        return;
                    }

                    data = e.Message.Text.Split()[1];
                }
                else
                {
                    //for quick setup
                    var info = e.Message.Text.Split();
                    exchange = info[0].ToLower();
                    symbol = info[2].ToLower();
                    data = info[1].ToLower();
                }
            }
        }

    }
}
