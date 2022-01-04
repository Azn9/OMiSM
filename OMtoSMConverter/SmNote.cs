using System;
using System.Linq;

namespace OMtoSMConverter
{
    public class SmNote
    {
        private string[] IndNotes { get; }
        private int KeyCount { get; }

        public SmNote(int keyCount)
        {
            KeyCount = keyCount;
            IndNotes = new string[keyCount];
            for (var i = 0; i < keyCount; i++)
            {
                IndNotes[i] = "0";
            }
        }

        public void OsuXtoNote(int osuX, string notetype)
        {
            var keyInd = (int) Math.Round((((((osuX / 512.0) * KeyCount * 2) + 1) / 2) - 1));
            IndNotes[keyInd] = notetype;
        }

        public string MakeRawNoteLine()
        {
            return IndNotes.Aggregate("", (current, note) => current + note);
        }
    }
}