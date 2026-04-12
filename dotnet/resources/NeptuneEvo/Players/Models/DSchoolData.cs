using NeptuneEvo.Handles;

namespace NeptuneEvo.Players.Models
{
    public class DSchoolData
    {
        public ExtVehicle Vehicle { get; set; } = null;
        public byte License { get; set; } = 255;
        public short Check { get; set; } = -1;
        public bool IsDriving { get; set; } = false;

        public bool IsTheory { get; set; } = false;
        public int TheoryQuestionIndex { get; set; } = 0;
        public int TheoryCorrectAnswers { get; set; } = 0;
    }
}
