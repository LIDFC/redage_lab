using GTANetworkAPI;
using NeptuneEvo.Chars.Models;
using NeptuneEvo.Handles;
using System;

namespace NeptuneEvo.BlackMarket.Models
{
    /// <summary>Результат операции для игрока: сообщение уходит в уведомление и в CEF.</summary>
    public class OpResult
    {
        public bool Ok;
        public string Message;

        public static OpResult Fail(string message) => new OpResult { Ok = false, Message = message };
        public static OpResult Success(string message) => new OpResult { Ok = true, Message = message };
    }

    /// <summary>Лот: сколько осталось и цена за штуку. Сами предметы лежат в контейнере Escrow.LotContainer(Id).</summary>
    public class Lot
    {
        public int Id;
        public int OwnerUuid;
        public ItemId ItemId;
        public int Count;
        public long PriceUnit;
        public DateTime Created;
        public DateTime Ends;
    }

    /// <summary>Закладка в мире. Предметы — в контейнере Escrow.DropContainer(Id).</summary>
    public class Drop
    {
        public int Id;
        public int BuyerUuid;
        public int LotId;
        public ItemId ItemId;
        public int Count;
        public Vector3 Position;
        public DateTime Created;
        public DateTime Expires;

        /// <summary>Кто сейчас забирает (5 с анимации); пока занято — второй не начнёт.</summary>
        public int PickingUuid;

        public GTANetworkAPI.Object Object;
        public ExtColShape Shape;
    }

    /// <summary>P2P-заявка: продаю AmountLeft BTC по PricePerBtc $ за 1 BTC. BTC заблокированы в кошельке владельца.</summary>
    public class P2POffer
    {
        public int Id;
        public int OwnerUuid;
        public long AmountLeft;
        public decimal PricePerBtc;
        public DateTime Created;
        public DateTime Ends;
    }
}
