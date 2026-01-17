using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace music4life.Models
{
    public class Playlist
    {
        public string Name { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public List<string> SongPaths { get; set; } = new List<string>();
    }
}