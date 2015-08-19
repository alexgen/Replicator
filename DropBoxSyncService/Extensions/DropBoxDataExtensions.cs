using System;
using DropboxIndexingService.Models;
using DropboxRestAPI.Models.Core;
using SyncService.Models;

namespace DropboxIndexingService.Extensions
{
    public static class DropBoxDataExtensions
    {
        public static Document ToDocument(this PendingChange change)
        {
            if (change == null) throw new ArgumentNullException("change");

            var meta = change.Meta;

            if (meta == null)
                return new Document { Id = change.Id, FilePath = change.DeletedFilePath };

            DateTime? takenAt = null;
            double? lat = null;
            double? lon = null;

            if (meta.photo_info != null)
            {
                if (!String.IsNullOrEmpty(meta.photo_info.time_taken))
                    takenAt = ToUtcDateTimeFromDropBoxTimeString(meta.photo_info.time_taken);

                if (meta.photo_info.lat_long != null && meta.photo_info.lat_long.Length == 2)
                {
                    lat = meta.photo_info.lat_long[0];
                    lon = meta.photo_info.lat_long[1];
                }
            }

            return new Document
            {
                Id = change.Id,

                // timestamps are chosen based on what's available
                CreatedAt = ToUtcDateTimeFromDropBoxTimeString(meta.client_mtime),
                ModifiedAt = ToUtcDateTimeFromDropBoxTimeString(meta.modified),

                FileSize = (ulong) meta.bytes,
                FilePath = meta.path,

                TakenAt = takenAt,

                Latitude = lat,
                Longitude = lon,

                Thumbnail = change.Thumbnail
            };
        }

        // copied from: https://github.com/DropNet/DropNet/blob/7479ac0ba4c640a584068d88bc76403988dc9040/DropNet/Models/MetaData.cs
        public static DateTime ToUtcDateTimeFromDropBoxTimeString(this string dateTimeStr)
        {
            if (dateTimeStr == null)
                return DateTime.MinValue;

            var str = dateTimeStr;
            if (str.EndsWith(" +0000")) str = str.Substring(0, str.Length - 6);
            if (!str.EndsWith(" UTC")) str += " UTC";

            return DateTime.ParseExact(
                str, 
                "ddd, d MMM yyyy HH:mm:ss UTC", 
                System.Globalization.CultureInfo.InvariantCulture);
        }

        public static string ToDropBoxTimeString(this DateTime dateTime)
        {
            return dateTime.ToString("ddd, d MMM yyyy HH:mm:ss UTC");
        }
    }
}