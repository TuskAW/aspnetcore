// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Internal;
using Xunit;

namespace Microsoft.AspNetCore.Components.Routing
{
    public class QueryStringEnumerableTest
    {
        [Fact]
        public void ParseQueryWithUniqueKeysWorks()
        {
            Assert.Collection(Parse("?key1=value1&key2=value2"),
                kvp => AssertKeyValuePair("key1", "value1", kvp),
                kvp => AssertKeyValuePair("key2", "value2", kvp));
        }

        [Fact]
        public void ParseQueryWithoutQuestionmarkWorks()
        {
            Assert.Collection(Parse("key1=value1&key2=value2"),
                kvp => AssertKeyValuePair("key1", "value1", kvp),
                kvp => AssertKeyValuePair("key2", "value2", kvp));
        }

        [Fact]
        public void ParseQueryWithDuplicateKeysGroups()
        {
            Assert.Collection(Parse("?key1=valueA&key2=valueB&key1=valueC"),
                kvp => AssertKeyValuePair("key1", "valueA", kvp),
                kvp => AssertKeyValuePair("key2", "valueB", kvp),
                kvp => AssertKeyValuePair("key1", "valueC", kvp));
        }

        [Fact]
        public void ParseQueryWithEmptyValuesWorks()
        {
            Assert.Collection(Parse("?key1=&key2="),
                kvp => AssertKeyValuePair("key1", string.Empty, kvp),
                kvp => AssertKeyValuePair("key2", string.Empty, kvp));
        }

        [Fact]
        public void ParseQueryWithEmptyKeyWorks()
        {
            Assert.Collection(Parse("?=value1&="),
                kvp => AssertKeyValuePair(string.Empty, "value1", kvp),
                kvp => AssertKeyValuePair(string.Empty, string.Empty, kvp));
        }

        [Fact]
        public void ParseQueryWithEncodedKeyWorks()
        {
            Assert.Collection(Parse("?fields+%5BtodoItems%5D"),
                kvp => AssertKeyValuePair("fields+%5BtodoItems%5D", string.Empty, kvp));
        }

        [Fact]
        public void ParseQueryWithEncodedValueWorks()
        {
            Assert.Collection(Parse("?=fields+%5BtodoItems%5D"),
                kvp => AssertKeyValuePair(string.Empty, "fields+%5BtodoItems%5D", kvp));
        }

        [Theory]
        [InlineData("?")]
        [InlineData("")]
        [InlineData(null)]
        public void ParseEmptyOrNullQueryWorks(string queryString)
        {
            Assert.Empty(Parse(queryString));
        }

        private static void AssertKeyValuePair(string expectedKey, string expectedValue, (string key, string value) actual)
        {
            Assert.Equal(expectedKey, actual.key);
            Assert.Equal(expectedValue, actual.value);
        }

        private static IReadOnlyList<(string key, string value)> Parse(string query)
        {
            var result = new List<(string key, string value)>();
            var enumerable = new QueryStringEnumerable(query);
            foreach (var pair in enumerable)
            {
                result.Add((pair.NameEscaped.ToString(), pair.ValueEscaped.ToString()));
            }

            return result;
        }
    }
}
