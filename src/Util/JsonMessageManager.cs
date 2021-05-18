using System;
using System.Text;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;

namespace CubeDraftBot.Util
{
    public static class JsonMessageManager
    {
        /// <summary>
        /// 日本語jsonファイル
        /// </summary>
        public const string JsonFilePathJa = @"res/message/japanese.json";

        /// <summary>
        /// Jsonメッセージのインスタンスたち
        /// </summary>
        /// <typeparam name="string">キーとなるjsonファイル名</typeparam>
        /// <typeparam name="JsonMessage">インスタンス</typeparam>
        private static Dictionary<string, JsonMessage> messages = new Dictionary<string, JsonMessage>();

        /// <summary>
        /// jsonから読み込んでメッセージクラスを作成
        /// </summary>
        /// <param name="JsonFilePath">読み込むjsonファイル</param>
        /// <returns></returns>
        private static JsonMessage CreateJsonMessage(string JsonFilePath = JsonFilePathJa)
        {
            StreamReader streamReader = new StreamReader(JsonFilePath, Encoding.GetEncoding("utf-8"));
            string allLine = streamReader.ReadToEnd();
            streamReader.Close();
            Console.WriteLine(allLine);
            return JsonSerializer.Deserialize<JsonMessage>(allLine);
        }

        /// <summary>
        /// JsonMessageのインスタンス取得
        /// </summary>
        /// <param name="JsonFilePath">読み込むjsonファイル</param>
        /// <returns>JsonMessageのインスタンス</returns>
        public static JsonMessage GetJsonMessage(string JsonFilePath = JsonFilePathJa)
        {
            if(!messages.ContainsKey(JsonFilePath)) messages.Add(JsonFilePath, CreateJsonMessage(JsonFilePath));
            return messages[JsonFilePath];
        }

    }
}