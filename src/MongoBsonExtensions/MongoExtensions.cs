using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;

namespace MongoBsonExtensions
{
    public static class BsonExtensions
    {
        public static bool TryGetString(this BsonDocument doc, string key, out string result)
        {
            var results = doc.TryGet(key).ToArray();
            if (results.Length == 0 || !results[0].IsString)
            {
                result = null;
                return false;
            }

            result = results[0].AsString;
            return true;
        }

        public static void Visit(IEnumerable<BsonValue> elements, Action<BsonDocument> func)
        {
            foreach (var doc in elements
                .Where(e => e != null && e.IsBsonDocument)
                .Select(e => e.AsBsonDocument))
            {
                func(doc);
            }
        }

        public static void Visit(this BsonDocument doc, string query, Action<BsonDocument> func)
            => Visit(doc.TryGet(query), func);

        public static IEnumerable<BsonValue> TryGet(this BsonValue val, string name)
            => TryGet(val, name.Split('.').ToArray());

        private static IEnumerable<BsonValue> TryGet(BsonValue val, string[] levels)
        {
            for (var i = 0; i < levels.Length; ++i)
            {
                if (val.IsBsonDocument)
                {
                    var doc = val.AsBsonDocument;

                    // Favourite.Foods
                    if (!doc.TryGetValue(levels[i], out var element) || element == null)
                    {
                        // we have broken the chain, no matches found
                        return new List<BsonValue>();
                    }

                    val = element;

                    // We are the final match in a . query
                    // e.g Favourite.Food, we are Food
                    if (IsLastNode(i, levels))
                    {
                        return new List<BsonValue> { element };
                    }
                }

                // Favourite.Foods = [ ... ]
                else if (val.IsBsonArray)
                {
                    return ParseArray(i, val.AsBsonArray, levels);
                }
            }

            // no matches found
            return new List<BsonValue>();
        }

        private static IEnumerable<BsonValue> ParseArray(int level, BsonArray array, string[] levels)
        {
            // The element is an array but the query doesn't want arrays
            if (levels[level] != "$[]")
            {
                return new List<BsonValue>();
            }

            // Parse Data.$[] - returns all documents inside the Data array
            if (IsLastNode(level, levels))
            {
                return array.Select(_ => _);
            }

            // Parse Data.$[].Other - continue searching
            return ProcessArray(array, levels.Skip(level).ToArray());
        }

        private static IEnumerable<BsonValue> ProcessArray(BsonArray array, string[] levels)
            => array
                .Where(e => e != null)

                // For each element in the array
                // Continue parsing through the levels
                .SelectMany(e => levels

                    // Check the array element at the current level
                    .SelectMany((l, idx) =>
                    {
                        if (e.IsBsonArray)
                        {
                            // .$[]
                            // [ 1,2,3,4,5 ]
                            if (e.IsBsonArray && IsLastNode(idx, levels) && levels[idx] == "$[]")
                            {
                                return e.AsBsonArray.Select(_ => _).ToList();
                            }

                            // [ [ ] ] 
                            // $[].$[]...
                            return ProcessArray(e.AsBsonArray, levels.Skip(idx + 2).ToArray());
                        }

                        // [ { ... } ] 
                        return TryGet(e, levels.Skip(idx + 1).ToArray());
                    }));

        private static bool IsLastNode(int current, string[] levels)
            => current == levels.Length - 1;
    }
}