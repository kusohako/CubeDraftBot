using System.IO;
using System;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CubeDraftBot.Draft
{
    public class Player
    {
        /// <summary>
        /// Discord のユーザー情報
        /// </summary>
        public IUser User { get; private set; }

        /// <summary>
        /// 提出したカードリスト
        /// </summary>
        public List<Card> SubmittedCardList { get; private set; }

        /// <summary>
        /// ピックしたカードリスト
        /// ピック順に並んでいる
        /// </summary>
        /// <value></value>
        public List<Card> PickedCards { get; private set; }

        /// <summary>
        /// カードリストが提出されているか
        /// </summary>
        /// <value></value>
        public bool DidSubmitCardList { get { return SubmittedCardList != null; } }

        /// <summary>
        /// ピック済みか
        /// </summary>
        /// <value></value>
        public bool DidPick { get { return PickingCard != null; } }

        public Card PickingCard { get; private set; }

        public bool IsCompleted { get; set; }

        public Player(IUser user)
        {
            this.User = user;
            this.PickedCards = new List<Card>();
            this.PickingCard = null;
        }

        public void SubmitCardList(List<Card> cardList)
        {
            this.SubmittedCardList = cardList;
        }

        /// <summary>
        /// パックを見る
        /// </summary>
        /// <param name="pack"></param>
        /// <returns></returns>
        public async Task BrowsePack(List<Card> pack, int beginPack)
        {
            this.PickingCard = null;
            string msg = beginPack != 0 ? String.Format("{0}番目のパックでピックを開始します\n", beginPack) : "";
            string packList = String.Join("\n", pack.Select((card, index) => String.Format("{0, 2}:{1}", index, card.Name)));
            await this.User.SendMessageAsync(msg + "ピックするカードの番号を `!pick n` の形で入力してください\n" + packList);
        }

        /// <summary>
        /// 仮ピック
        /// </summary>
        /// <param name="card"></param>
        /// <returns></returns>
        public async Task Pick(Card card)
        {
            this.PickingCard = card;
            await this.User.SendMessageAsync(card.Name + " をピックしました 全員がピックするまではピックを変えられます");
        }

        /// <summary>
        /// ピック確定
        /// </summary>
        public void DeterminePick()
        {
            this.PickedCards.Add(this.PickingCard);
            this.PickingCard = null;
        }

        /// <summary>
        /// 後で消す
        /// </summary>
        /// <param name="source"></param>
        /// <param name="chunkSize"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        private static IEnumerable<IEnumerable<T>> chunk<T>(IEnumerable<T> source, int chunkSize)
        {
            if (chunkSize <= 0)
                throw new ArgumentException("Chunk size must be greater than 0.", nameof(chunkSize));

            return source.Select((v, i) => new { v, i })
                .GroupBy(x => x.i / chunkSize)
                .Select(g => g.Select(x => x.v));
        }

        /// <summary>
        /// ピックの状態を見る
        /// </summary>
        /// <returns></returns>
        public async Task BrowseStatus(IMessageChannel channel = null)
        {
            var msg = this.PickedCards.Count != 0 ? "これらのカードをピックしています\n" + String.Join("\n", this.PickedCards.Select(c => c.Name)) : "まだ何もピックしていません";
            if(this.DidPick)
            {
                msg += "\nもうすぐ" + this.PickingCard.Name + "のピックが確定します";
            }
            
            if(channel == null)
            {
                await this.User.SendMessageAsync(msg);
            }
            else
            {
                msg = this.User.Username + "は" + msg;
                await channel.SendMessageAsync(msg);
            }
        }
    }
}