using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace music4life.Models
{
    public class FavoriteEntry
    {
        [PrimaryKey]
        public string SongPath { get; set; }
    }
}
