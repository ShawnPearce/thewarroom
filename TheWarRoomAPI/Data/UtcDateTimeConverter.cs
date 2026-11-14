using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace TheWarRoomAPI.Data
{
    // SQLite stores a DateTime as text and forgets that it was UTC.
    // This converter marks every DateTime as UTC again when it is read back,
    // so the JSON sent to the client carries the "Z" and is not mistaken for local time.
    public class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
    {
        public UtcDateTimeConverter()
            : base(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc))
        {
        }
    }
}
