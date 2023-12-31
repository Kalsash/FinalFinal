﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AIMLbot;

namespace NeuralNetwork1
{
    public class AIMLService
    {
        readonly Bot bot;
        readonly Dictionary<long, User> users = new Dictionary<long, User>();

        public AIMLService()
        {
            bot = new Bot();
            bot.loadSettings();
            bot.isAcceptingUserInput = false;
            bot.loadAIMLFromFiles();
            bot.isAcceptingUserInput = true;
        }
         
        public string Talk(long userId, string userName, string phrase)
        {
            var result = "";
            User user;
            if (!users.ContainsKey(userId))
            {
                user = new User(userId.ToString(), bot);
                users.Add(userId, user);
                Request r = new Request($"Меня зовут {userName}", user, bot);
                result += bot.Chat(r).Output + " Для того, чтобы посмотреть список доступных команд введите /help" + System.Environment.NewLine;
            }
            else
            {
                user = users[userId];
            }

            if (phrase.StartsWith("/"))
            {
                return result;
            }
            result = bot.Chat(new Request(phrase, user, bot)).Output;
            if (result == "")
            {
                return "[Ошибка в AIML]";
            }
            return result;
        }
    }
}
