using System;
using System.IO;
using System.Collections.Generic;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace CubeDraftBot.Draft
{
    /// <summary>
    /// カードクラス
    /// </summary>
    public class Card
    {
        /// <summary>
        /// カード名
        /// </summary>
        /// <value></value>
        public string Name { get; private set; }
        /// <summary>
        /// カードのタグ情報たち
        /// </summary>
        /// <value></value>
        public SortedSet<string> Tags { get; private set; }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="input"></param>
        public Card(string input)
        {
            // TODO パースする
            this.Name = input; // これは仮
        }

        /// <summary>
        /// カードリストを作る
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static List<Card> CreateCardList(string input)
        {
            var list = new List<Card>();
            var sr = new StringReader(input);
            while(sr.Peek() > -1)
            {
                string line = sr.ReadLine()?.Trim();
                if(String.IsNullOrEmpty(line)) continue;
                list.Add(new Card(line));
            }
            return list;
        }
    }
}