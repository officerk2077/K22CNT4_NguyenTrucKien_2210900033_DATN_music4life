using SQLite;

namespace music4life.Models
{
    public class FavoriteEntry
    {
        [PrimaryKey]
        public string SongPath { get; set; }
    }
}