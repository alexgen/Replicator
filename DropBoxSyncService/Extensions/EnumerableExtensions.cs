using System;
using System.Collections.Generic;

namespace DropboxIndexingService.Extensions
{
    public static class EnumerableExtensions
    {
        /// <summary>
        /// Break a sequence into batches of specified size. 
        /// </summary>
        public static IEnumerable<List<T>> Chunkify<T>(this IEnumerable<T> source, int size)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (size < 1) throw new ArgumentOutOfRangeException("size");

            using (var iter = source.GetEnumerator())
            {
                while (iter.MoveNext())
                {
                    var chunk = new List<T>(size) { iter.Current };

                    for (var i = 1; i < size && iter.MoveNext(); i++)
                    {
                        chunk.Add(iter.Current);
                    }
                    yield return chunk;
                }
            }
        }
    }
}
