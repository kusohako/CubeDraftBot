using System.Linq;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace CubeDraftBot
{
    /// <summary>
    ///     DiscordBot メイン処理
    /// </summary>
    public class MainLogic
    {
        /// <summary>
        ///     Botクライアント
        /// </summary>
        public static DiscordSocketClient Client;
        /// <summary>
        ///     Discordコマンドをやり取りするService層
        /// </summary>
        public static CommandService Commands;
        /// <summary>
        ///     ServiceProvider
        /// </summary>
        public static IServiceProvider Provider;


        /// <summary>
        ///     起動時処理
        /// </summary>
        /// <returns></returns>
        public async Task MainAsync()
        {
            // ServiceProviderインスタンス生成
            Provider = new ServiceCollection().BuildServiceProvider();

            // 自身のアセンブリにコマンドの処理を構築する為、自身をCommandServiceに追加
            Commands = new CommandService();
            await Commands.AddModulesAsync(Assembly.GetEntryAssembly(), Provider);

            // Botアカウントに機能を追加
            Client = new DiscordSocketClient();
            Client.MessageReceived += CommandRecieved;
            Client.Log += msg => { Console.WriteLine(msg.ToString()); return Task.CompletedTask; };
            // BotアカウントLogin
            await Client.LoginAsync(TokenType.Bot, Constants.BotSecretToken.token);
            await Client.StartAsync();

            // タスクを常駐
            await Task.Delay(-1);
        }

private static IUser prev;

        /// <summary>
        ///     メッセージの受信処理
        /// </summary>
        /// <param name="messageParam">受信メッセージ</param>
        /// <returns></returns>
        private async Task CommandRecieved(SocketMessage messageParam)
        {
            if(messageParam is not SocketUserMessage) return;
            var message = messageParam as SocketUserMessage;
            //Console.WriteLine("{0} {1}:{2}", message.Channel.Name, message.Author.Username, message);
            Console.WriteLine("{0} {1}:{2}", message.Channel.Name, message.Author.Username, message);
            Console.WriteLine("{0} is prev? : {1}",  message.Author.Id, message.Author.Id == prev?.Id);
            prev = message.Author;
            //Console.WriteLine("{0}", messageParam.Channel is IPrivateChannel); // DM判定
            // どうやらpublicチャンネルのpostとDMではIUserは違うらしい

            // コメントがユーザーかBotかの判定
            if (message?.Author.IsBot ?? true)
            {
                return;
            }

            // Botコマンドかどうか判定（判定条件は接頭辞"!"付き発言 or Botアカウントへのメンション）
            int argPos = 0;
            if (!(message.HasCharPrefix('!', ref argPos) || message.HasMentionPrefix(Client.CurrentUser, ref argPos)))
            {
                // DM のやりとりだけコマンドじゃない
                if(message.Channel is SocketDMChannel)
                {
                    var dm = Draft.DraftManager.GetInstanceByUser(message.Author);
                    await dm?.ReceiveDM(message.Author, message.Content);
                }
                return;
            }

            // 実行
            var context = new CommandContext(Client, message);
            var result = await Commands.ExecuteAsync(context, argPos, Provider);

            //実行できなかった場合
            if (!result.IsSuccess)
            {
                await context.Channel.SendMessageAsync(result.ErrorReason);
            }
        }
    }
    class EntryPoint
    {
        /// <summary>
        ///     エントリーポイント
        /// </summary>
        /// <remarks>
        /// <see cref="MainLogic"/><see cref="MainLogic.MainAsync"/>
        /// </remarks>
        static void Main()
            => new MainLogic().MainAsync().GetAwaiter().GetResult();
    }
    /// <summary>
    /// hoge
    /// </summary>
    public class DraftCommandReceiver : ModuleBase
    {
        //private static Dictionary<IMessageChannel, Draft.DraftManager> dic;
        /// <summary>
        /// 新規ドラフト作成
        /// </summary>
        /// <returns></returns>
        [Command("create"), Alias("c")]
        public async Task Create(byte playerCount=4, byte packCount=3, byte cardCountPerPack=15)
        {
            var channel = this.Context.Channel;
            var m = Util.JsonMessageManager.GetJsonMessage();
            Console.WriteLine("Create {0}, {1}, {2}", playerCount, packCount, cardCountPerPack);

            // public な text channel じゃないとダメ
            if(channel is SocketTextChannel)
            {
                var draftManager = Draft.DraftManager.GetInstanceByChannel(channel);
                // 終了してないやつがあったらダメ
                if(draftManager != null && draftManager.Phase != Draft.DraftManager.DraftPhase.Completed)
                {
                    await ReplyAsync("すでにあるよ");
                }
                else
                {
                    Draft.DraftManager.CreateInstance(channel, playerCount, packCount, cardCountPerPack);
                    // 作ったプレイヤーを参加させるよ
                    await ReplyAsync(m.Created);
                    await this.Join();
                }
            }
            else
            {
                // TODO いい感じにダメだよってメッセージを出す
                //var message = string.Format(m.DraftBegin, 1);
                await ReplyAsync("ここは違うらしい");
            }
        }

        /// <summary>
        /// プレイヤーの参加表明
        /// </summary>
        /// <returns></returns>
        [Command("join")]
        public async Task Join()
        {
            var draftManager = Draft.DraftManager.GetInstanceByChannel(this.Context.Channel);
            if(draftManager != null)
            {
                var result = await draftManager.AddPlayer(this.Context.User);
                await ReplyAsync(result ? "参加受け付けたよ" : "もうどこか参加中らしい");
                if(draftManager.IsFullPlayer)
                {
                    await ReplyAsync("集まったからリスト提出よろ");
                    draftManager.StartPreparation();
                }
            }
            else
            {
                await ReplyAsync("まだゲームがないよ");
            }
        }

        /// <summary>
        /// プレイヤーの参加辞退
        /// </summary>
        /// <returns></returns>
        [Command("leave")]
        public async Task Leave()
        {
            var draftManager = Draft.DraftManager.GetInstanceByChannel(this.Context.Channel);
            if(draftManager != null)
            {
                draftManager.RemovePlayer(this.Context.User);
            }
            else
            {
                await ReplyAsync("まだゲームがないよ");
            }
        }

        /// <summary>
        /// カードリストが集まってるならドラフトを開始する
        /// </summary>
        /// <returns></returns>
        [Command("start")]
        public async Task Start()
        {
            var draftManager = Draft.DraftManager.GetInstanceByChannel(this.Context.Channel);
            if(draftManager != null)
            {
                var result = await draftManager.PickStart();
                if(!result)
                {
                    await ReplyAsync("カードリストが不完全です");
                }
                string order = String.Join("\n", draftManager.PickOrder.Select(p => p.User.Username));
                await ReplyAsync("以下の席順でドラフトを開始します\n" + order);
            }
            else
            {
                await ReplyAsync("まだゲームがないよ");
            }
        }

        /// <summary>
        /// カードをピックする
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [Command("pick")]
        public async Task Pick(byte id)
        {
            var channel = this.Context.Channel;
            if(channel is not IPrivateChannel)
            {
                await ReplyAsync("ピックはDMでやってください");
                return;
            }
            var draftManager = Draft.DraftManager.GetInstanceByUser(this.Context.User);
            if(draftManager != null)
            {
                await draftManager.Pick(this.Context.User, id);
            }
            else
            {
                await ReplyAsync("まだゲームがないよ");
            }
        }

        /// <summary>
        /// 状態を見る
        /// </summary>
        /// <returns></returns>
        [Command("status")]
        public async Task Status()
        {
            var channel = this.Context.Channel;
            if(channel is not IPrivateChannel)
            {
                await ReplyAsync("確認はDMでやってください");
                return;
            }
            var draftManager = Draft.DraftManager.GetInstanceByUser(this.Context.User);
            if(draftManager != null)
            {
                await draftManager.Players[this.Context.User.Id].BrowseStatus();
            }
            else
            {
                await ReplyAsync("まだゲームがないよ");
            }
        }

    }
}