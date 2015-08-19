using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DropboxIndexingService.Extensions
{
    public static class ParallelAsyncExtension
    {
        /// <summary>
        /// Breaks source list into a number of partitions and applies specified functor concurrently 
        /// for all partitions
        /// </summary>
        public static async Task<TResult[]> ConcurrentForEachAsync<TResult, TSource>(this IList<TSource> source, int dop, Func<TSource, Task<TResult>> body)
        {
            var result = new TResult[source.Count];
            var taskCounter = 0;

            await Task.WhenAll(
                from partition in Partitioner.Create(source).GetPartitions(dop)
                select Task.Run(
                    async delegate
                    {
                        using (partition)
                            while (partition.MoveNext())
                                result[taskCounter++] = await body(partition.Current);
                    }));

            return result;
        }
        
    }
}