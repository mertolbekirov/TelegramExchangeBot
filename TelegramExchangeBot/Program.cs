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

        private static async void Bot_OnMessage(object sender, MessageEventArgs e)
        {
            var message = e.Message;
            if (message == null || message.Type != MessageType.Text)
                return;

            var splitMsg = message.Text.Split().ToArray();
            switch (splitMsg.First())
            {
                case "/start":
                    await Bot.SendTextMessageAsync(e.Message.Chat.Id, "Hi, good to see you! Here is how to set me up:\n" +
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

                    //wrap in a try catch because unexpected errors might occur, we dont want the program to stop.
                    try
                    {
                        await Task.Run(() => StartBot(message.Chat.Id, exchangeName, globalSymbol, infoType, timeInterval));
                    }
                    catch (Exception)
                    {
                        await Bot.SendTextMessageAsync(message.Chat.Id, "Something went wrong. Maybe you put the wrong globaly symbol?");
                    }
                    break;
            }
        }

        private static async Task StartBot(long chatId, string exchangeName, string globalSymbol, string infoType, string timeInterval)
        {
            //set api
            var exchangetype = GetExchangeType(exchangeName.ToLower());
            var api = ExchangeAPI.GetExchangeAPI(exchangetype);

            if (infoType.ToLower().Contains("trade"))
            {
                await DisplayTradeInfo(chatId, exchangeName, globalSymbol, api);
            }
            else if (infoType.ToLower().Contains("candle"))
            {
                await DisplayCandleInfo(chatId, exchangeName, globalSymbol, infoType, timeInterval, api);
            }
        }

        private static async Task DisplayCandleInfo(long chatId, string exchangeName, string globalSymbol, string infoType, string timeInterval, IExchangeAPI api)
        {
            //get the candle info until now
            int timeIntervalMinutes = GetTimeIntervalMinutes(timeInterval);
            var msg = await Bot.SendTextMessageAsync(chatId, "Loading...");
            var candles = await api.GetCandlesAsync(globalSymbol, timeIntervalMinutes * 60, DateTime.Now - TimeSpan.FromMinutes(timeIntervalMinutes));
            var candle = candles.FirstOrDefault();

            if (candle != null)
            {
                //keep the current info about the candle here
                var highPrice = candle.HighPrice;
                var lowPrice = candle.LowPrice;
                decimal baseVolume = (decimal)candle.BaseCurrencyVolume;
                decimal quoteVolume = (decimal)candle.QuoteCurrencyVolume;
                Console.WriteLine(candle.Timestamp);
                //for each trade, we change the candle info accordingly
                var socket = await api.GetTradesWebSocketAsync(async trade =>
                {
                    //if the current candle is not the latest one, edit the message and stop updating the message
                    if (DateTime.UtcNow - candle.Timestamp >= TimeSpan.FromMinutes(timeIntervalMinutes))
                    {
                        await Bot.EditMessageTextAsync(chatId, msg.MessageId, "The candle is no longer the latest one, canceling the updates :)");
                        return;
                    }

                    //otherwise, we continue with the normal flow 
                    var tradePrice = trade.Value.Price;
                    
                    //set new high/low price if it's new
                    if (highPrice < tradePrice)
                    {
                        highPrice = tradePrice;
                    }
                    else if (lowPrice > tradePrice)
                    {
                        lowPrice = tradePrice;
                    }

                    //add corresponding values to base/quote volumes
                    baseVolume += trade.Value.Amount;
                    quoteVolume += tradePrice * trade.Value.Amount;

                    var currMsg = $"{exchangeName.ToUpper()} {globalSymbol} {infoType} {timeInterval}\n" +
                       $"High Price: {highPrice:f2}\n" +
                       $"Low Price: {lowPrice:f2}\n" +
                       $"Base Volume: {baseVolume:f2}\n" +
                       $"Quote Volume: {quoteVolume:f2}\n";
                    //send new msg
                    await Bot.EditMessageTextAsync(chatId, msg.MessageId, currMsg);
                }, globalSymbol
                );
            }
        }

        private static async Task DisplayTradeInfo(long chatId, string exchangeName, string globalSymbol, IExchangeAPI api)
        {
            var msg = await Bot.SendTextMessageAsync(chatId, "Loading...");
            var socket = await api.GetTradesWebSocketAsync(async message =>
            {
                string[] info = message.Value.ToString().Split(',').ToArray();
                await Bot.EditMessageTextAsync(chatId, msg.MessageId, $"Trade {exchangeName.ToUpper()} {message.Key}\n" +
                    $"Time: {info[0].Split('T')[1]}\n" +
                    $"Price: {info[1].Split(':')[1]}\n" +
                    $"Amount: {info[2].Split(':')[1]}\n" +
                    $"Action: {info[3]}\n");
            }, globalSymbol
            );
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
    }
}
