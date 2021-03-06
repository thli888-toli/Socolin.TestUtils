﻿using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Socolin.TestUtils.JsonComparer.Comparers;
using Socolin.TestUtils.JsonComparer.Errors;
using Socolin.TestUtils.JsonComparer.Handlers;

namespace Socolin.TestUtils.JsonComparer
{
    public interface IJsonComparer
    {
        IList<IJsonCompareError<JToken>> Compare(string expectedJson, string actualJson);
        IList<IJsonCompareError<JToken>> Compare(JToken expected, JToken actual);
        IEnumerable<IJsonCompareError<JToken>> Compare(JToken expected, JToken actual, string path);
    }

    public class JsonComparer : IJsonComparer
    {
        private readonly IJsonObjectComparer _jsonObjectComparer;
        private readonly IJsonArrayComparer _jsonArrayComparer;
        private readonly IJsonValueComparer _jsonValueComparer;
        private readonly IJsonSpecialHandler _jsonSpecialHandler;

        public static JsonComparer GetDefault(Action<string, JToken> captureHandler = null)
        {
            return new JsonComparer(new JsonObjectComparer(), new JsonArrayComparer(), new JsonValueComparer(), new JsonSpecialHandler(captureHandler));
        }

        public JsonComparer(
            IJsonObjectComparer jsonObjectComparer,
            IJsonArrayComparer jsonArrayComparer,
            IJsonValueComparer jsonValueComparer,
            IJsonSpecialHandler jsonSpecialHandler)
        {
            _jsonObjectComparer = jsonObjectComparer;
            _jsonArrayComparer = jsonArrayComparer;
            _jsonValueComparer = jsonValueComparer;
            _jsonSpecialHandler = jsonSpecialHandler;
        }

        public IList<IJsonCompareError<JToken>> Compare(string expectedJson, string actualJson)
        {
            var expected = JsonConvert.DeserializeObject<JToken>(expectedJson, new JsonSerializerSettings
            {
                DateParseHandling = DateParseHandling.None
            });
            var actual = JsonConvert.DeserializeObject<JToken>(actualJson, new JsonSerializerSettings
            {
                DateParseHandling = DateParseHandling.None
            });
            return Compare(expected, actual);
        }

        public IList<IJsonCompareError<JToken>> Compare(JToken expected, JToken actual)
        {
            return Compare(expected, actual, "").ToList();
        }

        public IEnumerable<IJsonCompareError<JToken>> Compare(JToken expected, JToken actual, string path)
        {
            if (expected.Type != actual.Type)
            {
                var (captureSucceeded, captureErrors) = _jsonSpecialHandler.HandleSpecialObject(expected, actual, path);
                if (captureSucceeded)
                    yield break;
                if (captureErrors?.Count > 0)
                {
                    foreach (var error in captureErrors)
                        yield return error;
                    yield break;
                }

                yield return new InvalidTypeJsonCompareError(path, expected, actual);
                yield break;
            }

            IEnumerable<IJsonCompareError<JToken>> errors;
            switch (actual.Type)
            {
                case JTokenType.Object:
                    errors = _jsonObjectComparer.Compare(expected as JObject, actual as JObject, this, path);
                    break;
                case JTokenType.Array:
                    errors = _jsonArrayComparer.Compare(expected as JArray, actual as JArray, this, path);
                    break;
                case JTokenType.Integer:
                case JTokenType.Float:
                case JTokenType.String:
                case JTokenType.Boolean:
                case JTokenType.Null:
                case JTokenType.Undefined:
                    errors = _jsonValueComparer.Compare(expected as JValue, actual as JValue, path);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(actual.Type), actual.Type, "Cannot compare this type");
            }

            foreach (var jsonCompareError in errors)
            {
                yield return jsonCompareError;
            }
        }
    }
}