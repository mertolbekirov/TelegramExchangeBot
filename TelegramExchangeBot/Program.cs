using System;
using Telegram.Bot;
using Telegram.Bot.Args;
using ExchangeSharp;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using Telegram.Bot.Types.Enums;
using System.Net.WebSockets;
using System.Net.Sockets;

namespace TelegramExchangeBot
{
    class Program
    {
        static TelegramBotClient Bot = new TelegramBotClient("1894205668:AAH-a_C3DoWhPt84Co275R03cyMMU35iVVI");

        public static void Main(string[] args)
        {
            Bot.StartReceiving();
            Bot.OnMessage += Bot_OnMessage;

            Console.ReadLine();
        }

        private static Type GetExchangeType(string exchange)
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
            var message = e.Message;
            if (message == null || message.Type != MessageType.Text)
                return;

            var splitMsg = message.Text.Split().ToArray();
            switch (splitMsg.First())
            {
                case "/start":
                    Bot.SendTextMessageAsync(e.Message.Chat.Id, "Hi, good to see you! Here is how to set me up:\n" +
                        "You write /setup {exchange name} {global symbol} {trade/candle} {time interval (only for candles)\n" +
                        "Notes: exchange name can be either Binance or Kraken\n" +
                        "Time interval must be standard for candles (30m, 1h, 1d, etc.)\n");
                    break;
                default:
                    if (splitMsg.Length < 4 || splitMsg.Length > 5)
                    {
                        return;
                    }
                    string exchangeName = splitMsg[1];
                    string globalSymbol = splitMsg[2];
                    string infoType = splitMsg[3];
                    string timeInterval = "";
                    if (splitMsg.Length == 5)
                    {
                        timeInterval = splitMsg[4];
                    }
                    Task.Run(() => StartBot(message.Chat.Id, exchangeName, globalSymbol, infoType, timeInterval));
                    break;
            }


        }

        private static async Task StartBot(long chatId, string exchangeName, string globalSymbol, string infoType, string timeInterval)
        {
            var exchangetype = GetExchangeType(exchangeName.ToLower());
            var api = ExchangeAPI.GetExchangeAPI(exchangetype);

            if (infoType.ToLower().Contains("trade"))
            {
                var msg = await Bot.SendTextMessageAsync(chatId, "Loading...");
                var socket = await api.GetTradesWebSocketAsync(message =>
                {
                    string[] info = message.Value.ToString().Split(',').ToArray();
                    Bot.EditMessageTextAsync(chatId, msg.MessageId, $"Trade {exchangeName.ToUpper()} {message.Key}\n" +
                        $"Time: {info[0]}\n" +
                        $"Price: {info[1].Split(':')[1]}\n" +
                        $"Amount: {info[2].Split(':')[1]}\n" +
                        $"Action: {info[3]}\n");
                    return Task.CompletedTask;
                }, globalSymbol
                );
            }
            else if (infoType.ToLower().Contains("candle"))
            {

                int timeIntervalMinutes = GetTimeIntervalMinutes(timeInterval);
                //I couldn't find a web socket for candles, so I just check for changes over and over again 
                var msg = await Bot.SendTextMessageAsync(chatId, "Loading...");
                var lastMsg = "";
                while (true)
                {
                    var candles = await api.GetCandlesAsync(globalSymbol, timeIntervalMinutes * 60, DateTime.Now - TimeSpan.FromMinutes(timeIntervalMinutes));
                    var candle = candles.FirstOrDefault();
                    if (candle != null)
                    {
                        var currMsg = $"{exchangeName.ToUpper()} {globalSymbol} {infoType} {timeInterval}\n" +
                            $"High Price: {candle.HighPrice:f2}\n" +
                            $"Base Volume: {candle.BaseCurrencyVolume:f2}\n" +
                            $"Quote Volume: {candle.QuoteCurrencyVolume:f2}";
                        if (lastMsg == currMsg)
                        {
                            continue;
                        }
                        await Bot.EditMessageTextAsync(chatId, msg.MessageId, currMsg);
                        lastMsg = currMsg;
                    }
                }
            }

        }

        private static int GetTimeIntervalMinutes(string timeInterval)
        {
            if (timeInterval.Contains('m'))
            {
                 return int.Parse(timeInterval.Split('m')[0]);
            }
            else if (timeInterval.Contains('h'))
            {
                return int.Parse(timeInterval.Split('h')[0]) * 60;
            }
            else if (timeInterval.Contains('d'))
            {
                return int.Parse(timeInterval.Split('d')[0]) * 1440;
            }
            else
            {
                return 0;
            }
        }
    }
}
