using SQLite;

namespace music4life.Models
{
    public class PlaylistEntry
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public int PlaylistId { get; set; }

        public string SongPath { get; set; }
    }
}