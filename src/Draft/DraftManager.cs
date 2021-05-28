using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System.Collections.Generic;

namespace CubeDraftBot.Draft
{
    /// <summary>
    /// ドラフト進行を管理するクラス
    /// </summary>
    public class DraftManager
    {
        /// <summary>
        /// ドラフトの進行状況を表す列挙型
        /// </summary>
        public enum DraftPhase
        {
            WaitingForPlayerJoin, // プレイヤー募集中
            PreparationCardList, // カードリスト準備中
            Pick, // ドラフト中
            Completed, // 終了
        }

        /// <summary>
        /// 全体で管理される奴ら
        /// </summary>
        private static Dictionary<IMessageChannel, DraftManager> channelDictionary = new Dictionary<IMessageChannel, DraftManager>();

        /// <summary>
        /// ユーザーとこれの関連付け
        /// </summary>
        private static Dictionary<ulong, DraftManager> userDictionary = new Dictionary<ulong, DraftManager>();

        /// <summary>
        /// ゲームに参加するプレイヤーたち
        /// </summary>
        public Dictionary<ulong, Player> Players { get; private set; }

        /// <summary>
        /// ゲームに参加する人数
        /// </summary>
        /// <value></value>
        public int PlayerCount { get; private set; }

        /// <summary>
        /// やり取り用のチャンネル
        /// </summary>
        public IMessageChannel Channel;

        /// <summary>
        /// ドラフトの進行状況
        /// </summary>
        /// <value></value>
        public DraftPhase Phase { get; private set; }

        /// <summary>
        /// プレイヤーが集まったか
        /// </summary>
        /// <value></value>
        public bool IsFullPlayer { get { return PlayerCount == Players.Count; } }

        /// <summary>
        /// 1パックあたりのカード枚数
        /// </summary>
        /// <value></value>
        public int CardCountPerPack { get; private set; }

        /// <summary>
        /// ドラフトするパック数
        /// </summary>
        /// <value></value>
        public int PackCount { get; private set; }

        /// <summary>
        /// 乱数
        /// </summary>
        private Random random;

        /// <summary>
        /// ピック順
        /// </summary>
        public List<Player> PickOrder { get; private set; }

        /// <summary>
        /// ランダムに作られたパック
        /// </summary>
        private List<List<Card>> packs;

        /// <summary>
        /// パックからピックされて取り除かれる予定のindex
        /// </summary>
        private Dictionary<int, int> removeIndice;

        /// <summary>
        /// ピックした回数
        /// </summary>
        private int pickCount;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="PlayerCount">ゲーム参加人数</param>
        private DraftManager(IMessageChannel channel, int playerCount=4, int packIndex=3, int cardCountPerPack=15)
        {
            this.Channel = channel;
            this.PlayerCount = playerCount;
            this.PackCount = packIndex;
            this.CardCountPerPack = cardCountPerPack;
            this.Players = new Dictionary<ulong, Player>();
            this.Phase = DraftPhase.WaitingForPlayerJoin;

            this.removeIndice = new Dictionary<int, int>();
            this.random = new Random((int)DateTime.Now.Ticks);
        }

        public static DraftManager CreateInstance(IMessageChannel channel, int playerCount=4, int packIndex=3, int cardCountPerPack=15)
        {
            // すでにあったらダメ
            if(DraftManager.channelDictionary.ContainsKey(channel))
            {
                return null;
            }
            var instance = new DraftManager(channel, playerCount, packIndex, cardCountPerPack);
            DraftManager.channelDictionary.Add(channel, instance);

            return instance;
        }

        /// <summary>
        /// 破壊する
        /// </summary>
        /// <param name="channel"></param>
        /// <returns></returns>
        public static bool Destroy(IMessageChannel channel)
        {
            if(channelDictionary.ContainsKey(channel))
            {
                foreach(var player in channelDictionary[channel].Players.Values)
                {
                    userDictionary.Remove(player.User.Id);
                }
                channelDictionary.Remove(channel);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 参加プレイヤーを追加する
        /// </summary>
        /// <param name="user"></param>
        public async Task<bool> AddPlayer(IUser user)
        {
            if(this.Players.ContainsKey(user.Id) || DraftManager.userDictionary.ContainsKey(user.Id))
            {
                return false;
            }
            var player = new Player(user);
            this.Players.Add(user.Id, player);
            DraftManager.userDictionary.Add(user.Id, this);
            
            return await Task.Run(() => true);
        }

        /// <summary>
        /// 参加プレイヤーを除外する
        /// </summary>
        /// <param name="user"></param>
        public void RemovePlayer(IUser user)
        {
             this.Players.Remove(user.Id);
             DraftManager.userDictionary.Remove(user.Id);
        }
        
        /// <summary>
        /// カードリストの募集を開始する
        /// </summary>
        public async Task StartPreparation()
        {
            // フェイズ進行
            this.Phase = DraftPhase.PreparationCardList;
            string msg = String.Format("改行区切りで{0}枚分のカードリストを提出してください", this.CardCountPerPack * this.PackCount);
            foreach(var p in this.Players.Values)
            {
                await p.User.SendMessageAsync(msg);
            }
        }

        /// <summary>
        /// ピックを開始する
        /// </summary>
        public async Task<bool> PickStart()
        {
            // 集まっててカード提出フェイズじゃないとダメ
            if(this.Players.Values.Any(p => !p.DidSubmitCardList) || this.Phase != DraftPhase.PreparationCardList)
            {
                return false;
            }
            // カードリスト統合
            var all = this.Players.Values.SelectMany(p => p.SubmittedCardList).OrderBy(_ => this.random.Next(2000));
            // パック作成
            this.packs = all.Select((card, index) => new {card, index}).GroupBy(x => x.index / this.CardCountPerPack).Select(g => g.Select(x => x.card).ToList()).ToList();
            // プレイヤーを並び替え
            this.PickOrder = this.Players.Values.OrderBy(_ => this.random.Next(2000)).ToList();
            // フェイズ進行
            this.Phase = DraftPhase.Pick;

            // パック表示
            await this.ShowPacks();
            return true;
        }

        /// <summary>
        /// プレイヤーにピックすべきパックを見せる
        /// </summary>
        public async Task ShowPacks()
        {
            this.removeIndice.Clear();
            int packIndex = this.pickCount / this.CardCountPerPack; // 今何パック目のドラフトしてるか
            int counterSign = (packIndex) % 2 == 1 ? 1 : -1;
            int beginPack = this.pickCount % this.CardCountPerPack == 0 ? packIndex + 1 : 0;
            for(int i = 0; i < this.PlayerCount; i++)
            {
                var player = this.PickOrder.ElementAt(i);
                int index = ((i + this.CardCountPerPack * this.PackCount * this.PlayerCount + counterSign * this.pickCount) % this.PlayerCount) + packIndex * this.PlayerCount;
                Console.WriteLine("{0} : {1}", player.User.Username, index);
                var pack = this.packs.ElementAt(index);
                await player.BrowsePack(pack, beginPack);
            }
        }

        /// <summary>
        /// プレイヤーにカードをピックさせる
        /// </summary>
        /// <param name="user"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<bool> Pick(IUser user, byte id)
        {
            var player = this.Players[user.Id];
            int packIndex = this.pickCount / this.CardCountPerPack; // 今何パック目のドラフトしてるか
            int counterSign = (packIndex) % 2 == 1 ? 1 : -1;
            int orderIndex = this.PickOrder.FindIndex(p => p.User.Id == player.User.Id);
            // player が 0 番目で逆順 pick のときは その順の 3 (= 4 - 0 - 1) 番目のパックが対応
            int currentPackIndex = packIndex * this.PlayerCount + ((counterSign * this.pickCount + orderIndex + this.CardCountPerPack * this.PackCount * this.PlayerCount) % this.PlayerCount);
            // indexチェック
            if(this.packs.ElementAt(currentPackIndex).Count < id || id < 0) return false;
            var card = this.packs.ElementAt(currentPackIndex).ElementAt(id);
            this.removeIndice[currentPackIndex] = id;
            await player.Pick(card);

            // 全員ピックしたら次
            if(this.PickOrder.All(p => p.DidPick))
            {
                // ピック確定
                this.PickOrder.ForEach(p => p.DeterminePick());
                // packから取り除く
                foreach(var p in this.removeIndice)
                {
                    this.packs.ElementAt(p.Key).RemoveAt(p.Value);
                }
                if(++this.pickCount >= this.PackCount * this.CardCountPerPack)
                {
                    this.Phase = DraftPhase.Completed;
                    await this.Channel.SendMessageAsync("ピックが完了しました\nプレイヤー全員が `!complete` と入力すると全員のピックが表示されるので\n対戦が終わったら入力して臭い");
                    foreach(var p in this.PickOrder)
                    {
                        await p.User.SendMessageAsync("ピックが完了しました");
                        await p.BrowseStatus();
                    }
                }
                else
                {
                    await ShowPacks();
                }
            }

            return true;
        }

        /// <summary>
        /// げっとするよい
        /// </summary>
        /// <param name="channel"></param>
        /// <returns></returns>
        public static DraftManager GetInstanceByChannel(IMessageChannel channel)
        {
            return DraftManager.channelDictionary.ContainsKey(channel) ? DraftManager.channelDictionary[channel] : null;
        }
        public static DraftManager GetInstanceByUser(IUser user)
        {
            return DraftManager.userDictionary.ContainsKey(user.Id) ? DraftManager.userDictionary[user.Id] : null;
        }

        /// <summary>
        /// DM上のやり取りを司るよ
        /// </summary>
        /// <param name="user"></param>
        /// <param name="message"></param>
        public async Task ReceiveDM(IUser user, string message)
        {
            var p = this.Players[user.Id];
            if(this.Phase == DraftPhase.PreparationCardList)
            {
                var cl = Card.CreateCardList(message);
                var requireNum = this.PackCount * this.CardCountPerPack;
                if(cl.Count != requireNum)
                {
                    string str = String.Format("カードリストは{0}枚にしてください", requireNum);
                    await user.SendMessageAsync(str);
                    return;
                }
                p.SubmitCardList(cl);
                await user.SendMessageAsync("提出を受け付けました");
                if(this.Players.Values.All(p => p.DidSubmitCardList))
                {
                    await this.Channel.SendMessageAsync("全員が提出したので `!start` でドラフトを開始できます");
                }
            }
        }

        /// <summary>
        /// おわる
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public async Task CompletePlayer(IUser user)
        {
            if(this.Phase != DraftPhase.Completed)
            {
                await this.Channel.SendMessageAsync("まだドラフトが終わっていません");
                return;
            }
            var p = this.Players[user.Id];
            p.IsCompleted = true;
            if(this.Players.Values.All(p => p.IsCompleted))
            {
                foreach (var player in this.Players.Values)
                {
                    await player.BrowseStatus(this.Channel);
                }
            }
            await this.Channel.SendMessageAsync("ゲームを終了しました");
            DraftManager.Destroy(this.Channel);
        }
    }
}