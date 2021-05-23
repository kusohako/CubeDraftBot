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
        public async Task BrowsePack(List<Card> pack)
        {
            this.PickingCard = null;
            string packList = String.Join("\n", pack.Select((card, index) => String.Format("{0, 2}:{1}", index, card.Name)));
            await this.User.SendMessageAsync("ピックするカードの番号を `!pick n` の形で入力してください\n" + packList);
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
        /// ピックの状態を見る
        /// </summary>
        /// <returns></returns>
        public async Task BrowseStatus()
        {
            var msg = "これらのカードをピックしています\n" + String.Join("\n", this.PickedCards.Select(c => c.Name));
            if(this.DidPick)
            {
                msg += "\nもうすぐ" + this.PickingCard.Name + "のピックが確定します";
            }
            await this.User.SendMessageAsync(msg);
        }
    }
}