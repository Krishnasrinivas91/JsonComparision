using RuleComparer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

public static class CsvWriter
{
    public static void WriteRuleDiffsToCsv(
        List<RuleDiff> diffs,
        string filePath,
        bool includeHeader = true)
    {
        var sb = new StringBuilder();

        if (includeHeader)
        {
            sb.AppendLine("FieldName,ContactType,Property,OldValue,NewValue");
        }

        foreach (var diff in diffs)
        {
            foreach (var prop in diff.PropertyDifferences)
            {
                // Escape commas, quotes, newlines for safety
                string fieldName = CsvEscape(diff.FieldName);
                string contactType = CsvEscape(diff.ContactType);
                string property = CsvEscape(prop.Property);
                string oldVal = CsvEscape(prop.LeftValue);
                string newVal = CsvEscape(prop.RightValue);

                sb.AppendLine($"{fieldName},{contactType},{property},{oldVal},{newVal}");
            }
        }

        File.WriteAllText(filePath, sb.ToString());
    }

    private static string CsvEscape(string value)
    {
        if (value == null) return "";

        // If it contains comma, quotes, or newline — wrap in quotes and escape quotes
        if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        return value;
    }
}