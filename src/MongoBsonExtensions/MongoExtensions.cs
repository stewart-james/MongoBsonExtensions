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
                    // Favourite.Foods
                    if (!val.AsBsonDocument.TryGetValue(levels[i], out val) || val == null)
                    {
                        // we have broken the chain, no matches found
                        return new List<BsonValue>();
                    }

                    // We are the final match in a . query
                    // e.g Favourite.Food, we are Food
                    if (IsLastNode(i, levels))
                    {
                        return new List<BsonValue> { val };
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
                .SelectMany(e => levels
                    .SelectMany((l, idx) => ParseInnerArray(idx, e, levels)));

        private static IEnumerable<BsonValue> ParseInnerArray(int level, BsonValue element, string[] levels)
            => element.IsBsonArray
                ? ParseInnerArrayArray(level, element.AsBsonArray, levels)
                : TryGet(element, levels.Skip(level + 1).ToArray());

        private static IEnumerable<BsonValue> ParseInnerArrayArray(int level, BsonArray array, string[] levels) =>
            IsLastNode(level, levels) && levels[level] == "$[]"
                ? array.Select(_ => _)  // [ 1,2,3,4,5 ] = "$[]" / ".$[]" 
                : ProcessArray(array, levels.Skip(level + 2).ToArray());    // [ [ ] ] = "$[].$[]"

        private static bool IsLastNode(int current, string[] levels)
            => current == levels.Length - 1;
    }
}