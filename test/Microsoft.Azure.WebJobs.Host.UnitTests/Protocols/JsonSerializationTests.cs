// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Xunit;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Newtonsoft.Json.Linq;
using System;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Protocols
{
    public class JsonSerializationTests
    {
        [Fact]
        public void Integer_Parse_Returns_Null()
        {
            string invalidObjJson = "1";

            var parsed = JsonSerialization.ParseJObject(invalidObjJson);

            Assert.Equal(null, parsed);
        }

        [Fact]
        public void Array_Parse_Returns_Null()
        {
            string invalidObjJson = "[1]";

            var parsed = JsonSerialization.ParseJObject(invalidObjJson);

            Assert.Equal(null, parsed);
        }

        [Fact]
        public void String_Parse_Returns_Null()
        {
            string invalidObjJson = "hello";

            var parsed = JsonSerialization.ParseJObject(invalidObjJson);

            Assert.Equal(null, parsed);
        }

        [Fact]
        public void Valid_Single_JObject_Parse_Returns_JObject()
        {
            string validObjJson = "{ num : 1 }";

            var parsed = JsonSerialization.ParseJObject(validObjJson);

            Assert.Equal(parsed.GetType(), typeof(JObject));
        }

        [Fact]
        public void Valid_Multilevel_JObject_Parse_Returns_JObject()
        {
            string validObjJson = "{ num : 1 , embeddedNum : { num : 1 } }";

            var parsed = JsonSerialization.ParseJObject(validObjJson);

            Assert.Equal(parsed.GetType(), typeof(JObject));
        }

        [Fact]
        public void Null_Parse_Throws_ArgumentNullException()
        {
            string nullJson = null;

            Assert.Throws<ArgumentNullException>(() => JsonSerialization.ParseJObject(nullJson));
        }

        [Fact]
        public void Invalid_Json_Returns_Null()
        {
            string invalidJson = "{ num : 1 } extra stuff";

            var parsed = JsonSerialization.ParseJObject(invalidJson);

            Assert.Equal(null, parsed);
        }
    }
}
