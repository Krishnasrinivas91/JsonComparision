using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RuleComparer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // Replace these with file reads if you prefer:
            string json1 = File.ReadAllText("Rule1.json");
            string json2 = File.ReadAllText("Rule2.json");

            var rules1 = DeserializeRules(json1);
            var rules2 = DeserializeRules(json2);

            var diffs = CompareRuleSets(rules1, rules2);

            CsvWriter.WriteRuleDiffsToCsv(diffs, "RuleComparisonReport.csv");

            Console.WriteLine("CSV report generated: RuleComparisonReport.csv");

            // Display in console
            //if (!diffs.Any())
            //{
            //    Console.WriteLine("No differences found (for matched rules).");
            //    return;
            //}

            //Console.WriteLine("Differences found:");
            //foreach (var d in diffs)
            //{
            //    Console.WriteLine($"- Rule Key: {d.Key} (fieldName='{d.FieldName}', contactType='{d.ContactType}')");
            //    foreach (var p in d.PropertyDifferences)
            //    {
            //        Console.WriteLine($"    {p.Property}: Rule1 = {p.LeftValue ?? "null"}, Rule2 = {p.RightValue ?? "null"}");
            //    }
            //}
        }

        static List<Rule> DeserializeRules(string json)
        {
            var opts = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
            };

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("rules", out var rulesElement))
                return new List<Rule>();

            var list = new List<Rule>();
            foreach (var el in rulesElement.EnumerateArray())
            {
                try
                {
                    var rule = JsonSerializer.Deserialize<Rule>(el.GetRawText(), opts);
                    if (rule != null) list.Add(rule);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: failed to deserialize a rule: {ex.Message}");
                }
            }

            return list;
        }

        static List<RuleDiff> CompareRuleSets(List<Rule> left, List<Rule> right)
        {
            // Build a lookup by composite key: fieldName + contactType (case-insensitive)
            var leftLookup = left.ToDictionary(
                r => KeyFor(r.FieldName, r.ContactType),
                StringComparer.OrdinalIgnoreCase);

            var rightLookup = right.ToDictionary(
                r => KeyFor(r.FieldName, r.ContactType),
                StringComparer.OrdinalIgnoreCase);

            var allKeys = new HashSet<string>(leftLookup.Keys, StringComparer.OrdinalIgnoreCase);
            allKeys.UnionWith(rightLookup.Keys);

            var diffs = new List<RuleDiff>();

            foreach (var key in allKeys)
            {
                leftLookup.TryGetValue(key, out var l);
                rightLookup.TryGetValue(key, out var r);

                // If a rule exists only in one side, you might want to treat that as a diff.
                if (l == null || r == null)
                {
                    var pd = new List<PropertyDiff>();
                    if (l == null)
                    {
                        pd.Add(new PropertyDiff("RulePresence", null, "present"));
                    }
                    else
                    {
                        pd.Add(new PropertyDiff("RulePresence", "present", null));
                    }

                    diffs.Add(new RuleDiff
                    {
                        Key = key,
                        FieldName = l?.FieldName ?? r?.FieldName,
                        ContactType = l?.ContactType ?? r?.ContactType,
                        PropertyDifferences = pd
                    });

                    continue;
                }

                var propDiffs = new List<PropertyDiff>();

                // Compare maxLength
                if (!NullableEquals(l.MaxLength, r.MaxLength))
                    propDiffs.Add(new PropertyDiff("maxLength", l.MaxLength?.ToString(), r.MaxLength?.ToString()));

                // Compare minLength
                if (!NullableEquals(l.MinLength, r.MinLength))
                    propDiffs.Add(new PropertyDiff("minLength", l.MinLength?.ToString(), r.MinLength?.ToString()));

                // Compare regEx (string compare; trim to avoid whitespace differences)
                var lRegex = NormalizeStringForCompare(l.RegEx);
                var rRegex = NormalizeStringForCompare(r.RegEx);
                if (!string.Equals(lRegex, rRegex, StringComparison.Ordinal))
                    propDiffs.Add(new PropertyDiff("regEx", l.RegEx, r.RegEx));

                // Compare validationType (string compare, case-insensitive)
                if (!string.Equals(l.ValidationType?.Trim(), r.ValidationType?.Trim(), StringComparison.OrdinalIgnoreCase))
                    propDiffs.Add(new PropertyDiff("validationType", l.ValidationType, r.ValidationType));

                if (propDiffs.Any())
                {
                    diffs.Add(new RuleDiff
                    {
                        Key = key,
                        FieldName = l.FieldName,
                        ContactType = l.ContactType,
                        PropertyDifferences = propDiffs
                    });
                }
            }

            return diffs;
        }

        static string KeyFor(string fieldName, string contactType)
            => $"{fieldName ?? "<null>"}||{contactType ?? "<null>"}";

        static bool NullableEquals(int? a, int? b) => a.HasValue == b.HasValue && (!a.HasValue || a.Value == b.Value);

        static string NormalizeStringForCompare(string s)
        {
            if (s == null) return null;
            return s.Trim(); // more normalization could be added (unescape, collapse whitespace) if needed
        }
    }

    // Rule class - include only the useful fields for our comparison
    public class Rule
    {
        [JsonPropertyName("errorMessage")]
        public string ErrorMessage { get; set; }

        [JsonPropertyName("maxLength")]
        public int? MaxLength { get; set; }

        [JsonPropertyName("minLength")]
        public int? MinLength { get; set; }

        [JsonPropertyName("regEx")]
        public string RegEx { get; set; }

        [JsonPropertyName("required")]
        public bool? Required { get; set; }

        [JsonPropertyName("requiredErrorMessage")]
        public string RequiredErrorMessage { get; set; }

        [JsonPropertyName("fieldName")]
        public string FieldName { get; set; }

        [JsonPropertyName("validationType")]
        public string ValidationType { get; set; }

        [JsonPropertyName("contactType")]
        public string ContactType { get; set; }

        [JsonPropertyName("alwaysCheck")]
        public bool? AlwaysCheck { get; set; }

        // existsIn omitted because we don't need to compare it here
    }

    public class RuleDiff
    {
        public string Key { get; set; }
        public string FieldName { get; set; }
        public string ContactType { get; set; }
        public List<PropertyDiff> PropertyDifferences { get; set; } = new();
    }

    public class PropertyDiff
    {
        public PropertyDiff(string property, string leftValue, string rightValue)
        {
            Property = property;
            LeftValue = leftValue;
            RightValue = rightValue;
        }

        public string Property { get; }
        public string LeftValue { get; }
        public string RightValue { get; }
    }
}
