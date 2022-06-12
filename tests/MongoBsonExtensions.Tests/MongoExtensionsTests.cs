using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace MongoBsonExtensions.Tests
{
    [TestClass]
    public class BsonExtensionsTests
    {
        [TestMethod]
        public void TryGetString_KeyDoesNotExist()
        {
            var doc = new BsonDocument("Food", "Pizza");

            Assert.IsFalse(doc.TryGetString("Name", out var name));
            Assert.IsNull(name);
        }

        [TestMethod]
        public void TryGetString_ValueIsNull()
        {
            var doc = new BsonDocument(new Dictionary<string, string> { { "Food", null } });

            Assert.IsFalse(doc.TryGetString("Food", out var val));
            Assert.IsNull(val);
        }

        [TestMethod]
        public void TryGetString_NotAString()
        {
            var doc = new BsonDocument("Food", 123);

            Assert.IsFalse(doc.TryGetString("Food", out var val));
            Assert.IsNull(val);
        }

        [TestMethod]
        public void TryGetString_ValueIsString()
        {
            var doc = new BsonDocument("Food", "Pizza");

            Assert.IsTrue(doc.TryGetString("Food", out var val));
            Assert.AreEqual("Pizza", val);
        }

        private static string Users = "{" +
                                      "     \"displayName\" : \"David Bowie\", " +
                                      "     \"username\" : \"starman\", " +
                                      "     \"email\" : \"starman@mars.com\", " +
                                      "     \"Favourites\" : " +
                                      "                      {" +
                                      "                          \"Food\" : \"Pizza\", " +
                                      "                      }" +
                                      "}," +
                                      "{" +
                                      "     \"displayName\" : \"Elvis Presley\", " +
                                      "     \"username\" : \"king_of_rock\", " +
                                      "     \"email\" : \"return@tosender.com\", " +
                                      "     \"Favourites\" : " +
                                      "                      {" +
                                      "                          \"Food\" : \"Fish & Chips\", " +
                                      "                      }" +
                                      "}," +
                                      "{" +
                                      "     \"displayName\" : \"John Lennon\", " +
                                      "     \"username\" : \"hippy\", " +
                                      "     \"email\" : \"letit@be.com\", " +
                                      "     \"Favourites\" : " +
                                      "                      {" +
                                      "                          \"Food\" : \"Rice\", " +
                                      "                      }" +
                                      "}";

        private static string TestArray = "[" +
                                          "    { \"Users\" :" + $" [{Users}]" + "}," +
                                          "    []," +
                                          "    [1,2,3,4,5]," +
                                          "    [ [1,2,3,4,5], [1,2,3] ]," +
                                          "]";

        [TestMethod]
        public void Visit_Document()
        {
            var input = BsonSerializer.Deserialize<BsonArray>(TestArray).Select(_ => _).ToList();

            var displayNames = new List<string>();
            BsonExtensions.Visit(input[0]["Users"].AsBsonArray, doc =>
            {
                Assert.IsNotNull(doc);
                Assert.IsTrue(doc.IsBsonDocument);

                displayNames.Add(doc["displayName"].AsString);
            });

            CollectionAssert.AreEqual(new List<string> { "David Bowie", "Elvis Presley", "John Lennon" }, displayNames);
        }

        [TestMethod]
        public void Visit_Document_VisitsSingleDocument()
        {
            var input = BsonSerializer.Deserialize<BsonArray>(TestArray)[0]["Users"][0].AsBsonDocument;

            var foods = new List<string>();
            input.Visit("Favourites", doc => { foods.Add(doc["Food"].AsString); });

            CollectionAssert.AreEqual(new[] { "Pizza" }, foods);
        }

        [TestMethod]
        public void Visit_Document_VisitsMatchingNestedDocumentInArray()
        {
            var input = BsonSerializer.Deserialize<BsonArray>(TestArray)[0].AsBsonDocument;

            var displayNames = new List<string>();
            BsonExtensions.Visit(input, "Users.$[]", doc => { displayNames.Add(doc["displayName"].AsString); });

            CollectionAssert.AreEqual(new List<string> { "David Bowie", "Elvis Presley", "John Lennon" }, displayNames);
        }

        [TestMethod]
        public void Visit_Documents_VisitsAllDocuments()
        {
            var input = new List<BsonValue>()
            {
                null,
                new BsonArray(new[] { 1, 2, 3, 4, 5 }),
                new BsonDocument("Food", "Pizza"),
                new BsonDocument("Food", "Fried Chicken"),
                new BsonDocument("Food", "Rice"),
                new BsonDocument("Food", "Pasta"),
                new BsonBoolean(false),
                null
            };

            var foods = new List<string>();
            BsonExtensions.Visit(input, doc =>
            {
                Assert.IsTrue(doc.IsBsonDocument);
                foods.Add(doc["Food"].AsString);
            });

            CollectionAssert.AreEqual(new[] { "Pizza", "Fried Chicken", "Rice", "Pasta" }, foods);
        }

        [TestMethod]
        public void TryGet_SelectsSingleElement()
        {
            var input = BsonSerializer.Deserialize<BsonArray>(TestArray)[0]["Users"][0].AsBsonDocument;

            var elements = input.TryGet("displayName").ToList();
            Assert.AreEqual(1, elements.Count);

            var elem = elements[0];
            Assert.IsTrue(elem.IsString);
            Assert.AreEqual("David Bowie", elem.AsString);
        }

        [TestMethod]
        public void TryGet_ElementNestedInsideArray()
        {
            var input = BsonSerializer.Deserialize<BsonArray>(TestArray);

            var elements = input.TryGet("$[].Users.$[].email").ToImmutableArray();

            CollectionAssert.AllItemsAreInstancesOfType(elements, typeof(BsonString));

            var actual = elements.Select(e => e.AsString).ToList();
            var expected = new List<string> { "starman@mars.com", "return@tosender.com", "letit@be.com" };

            CollectionAssert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void TryGet_ArrayValues()
        {
            var input = BsonSerializer.Deserialize<BsonArray>(TestArray);

            var result = input.TryGet("$[]").ToImmutableArray();

            Assert.AreEqual(4, result.Length);

            Assert.IsTrue(result[0].IsBsonDocument && result[0].AsBsonDocument.Contains("Users"));

            Assert.IsTrue(result[1].IsBsonArray && result[1].AsBsonArray.Count == 0);

            Assert.IsTrue(result[2].IsBsonArray);
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5 },
                result[2].AsBsonArray.Select(i => i.AsInt32).ToImmutableArray());

            Assert.IsTrue(result[3].IsBsonArray);
            var arrayOfArrays = result[3].AsBsonArray;
            Assert.AreEqual(2, arrayOfArrays.Count);
            CollectionAssert.AllItemsAreInstancesOfType(arrayOfArrays.ToArray(), typeof(BsonArray));
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5 },
                arrayOfArrays[0].AsBsonArray.Select(i => i.AsInt32).ToImmutableArray());
            CollectionAssert.AreEqual(new[] { 1, 2, 3 },
                arrayOfArrays[1].AsBsonArray.Select(i => i.AsInt32).ToImmutableArray());
        }

        [TestMethod]
        public void TryGet_NestedArrayValues()
        {
            var input = BsonSerializer.Deserialize<BsonArray>(TestArray);

            var result = input.TryGet("$[].$[]").ToList();

            // [1,2,3,4,5]
            Assert.AreEqual(7, result.Count);
            for (var i = 0; i < 5; ++i)
            {
                Assert.IsTrue(result[i].IsInt32);
                Assert.AreEqual(i + 1, result[i].AsInt32);
            }

            // [ [1,2,3,4,5], [1,2,3] ]
            Assert.IsTrue(result[5].IsBsonArray);
            Assert.IsTrue(result[6].IsBsonArray);
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5 }, result[5].AsBsonArray.Select(i => i.AsInt32).ToArray());
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, result[6].AsBsonArray.Select(i => i.AsInt32).ToArray());
        }

        [TestMethod]
        public void TryGet_ReturnsEmpty_WhenIsArray_ButDoNotWantArrays()
        {
            var input = BsonSerializer.Deserialize<BsonArray>(TestArray);

            var results = input.TryGet("Test").ToList();

            Assert.AreEqual(0, results.Count);
        }
    }
}