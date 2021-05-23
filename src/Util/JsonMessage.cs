namespace CubeDraftBot.Util
{
    /// <summary>
    /// メッセージクラス
    /// </summary>
    public class JsonMessage
    {
        public string Finale { get; set; }
        /// <summary>
        /// ドラフト開始時のメッセージ
        /// </summary>
        /// <value></value>
        public string DraftBegan { get; set; }
        /// <summary>
        /// ドラフト作成時のメッセージ
        /// </summary>
        /// <value></value>
        public string Created { get; set; }
    }
}