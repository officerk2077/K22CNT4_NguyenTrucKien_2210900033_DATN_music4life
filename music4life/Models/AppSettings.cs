using System.Collections.Generic;

namespace music4life.Models
{
    public class AppSettings
    {
        public List<string> MusicFolders { get; set; } = new List<string>();
        public double CrossfadeSeconds { get; set; } = 3.0;
        public bool IsMinimizeToTrayEnabled { get; set; } = true;
    }
}