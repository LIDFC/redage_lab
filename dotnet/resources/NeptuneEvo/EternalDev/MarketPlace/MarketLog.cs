using Redage.SDK;

namespace NeptuneEvo.EternalDev.MarketPlace
{
    /// <summary>
    /// Лог площадки через стандартный nLog проекта (вместо ELib.Logger из авторской EternalCore.dll).
    /// </summary>
    public static class MarketLog
    {
        private static readonly nLog Log = new nLog("MarketPlace");

        public static void Write(string text) => Log.Write(text);
    }
}
