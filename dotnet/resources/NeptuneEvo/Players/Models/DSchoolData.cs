using System.Collections.Generic;
using NeptuneEvo.Handles;

namespace NeptuneEvo.Players.Models
{
    public class DSchoolData
    {
        public ExtVehicle Vehicle { get; set; } = null;
        public byte License { get; set; } = 255;
        public short Check { get; set; } = -1;
        public bool IsDriving { get; set; } = false;
        /// <summary>Ошибки на практике (превышение скорости, столкновения). 3 — экзамен провален.</summary>
        public int Penalties { get; set; } = 0;

        public bool IsTheory { get; set; } = false;
        public int TheoryQuestionIndex { get; set; } = 0;
        public int TheoryCorrectAnswers { get; set; } = 0;
        /// <summary>Номера вопросов из общего списка, выпавшие игроку (10 случайных).</summary>
        public List<int> TheoryOrder { get; set; } = new List<int>();
        /// <summary>Лицензия, по которой сдана теория и можно начинать практику (255 — нет).</summary>
        public byte TheoryPassed { get; set; } = 255;
    }
}
