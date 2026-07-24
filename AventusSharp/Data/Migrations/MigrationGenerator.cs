using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AventusSharp.Chart;

namespace AventusSharp.Data.Migrations;

public static class MigrationGenerator
{
    /// <summary>
    /// Translates a DiagramObject (representing a database schema diagram) into a C# migration class source code.
    /// </summary>
    /// <param name="diagram">The schema diagram object.</param>
    /// <param name="migrationName">The internal name of the migration (e.g. "0000_init").</param>
    /// <param name="className">The class name of the migration (e.g. "DemoMigration").</param>
    /// <returns>C# code of the migration class.</returns>
    public static string Generate(DiagramObject diagram, string migrationName, string className)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using AventusSharp.Data.Migrations;");
        sb.AppendLine("using AventusSharp.Data.Attributes;");
        sb.AppendLine("using System;");
        sb.AppendLine();
        sb.AppendLine($"public class {className} : Migration");
        sb.AppendLine("{");
        sb.AppendLine("    public override string GetName()");
        sb.AppendLine("    {");
        sb.AppendLine($"        return \"{migrationName}\";");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    public override void Up()");
        sb.AppendLine("    {");

        // Sort tables topologically based on relationships
        var sortedTables = SortTablesByDependencies(diagram.Tables, diagram.Relationships);

        foreach (var table in sortedTables)
        {
            var chainLines = new List<string>();
            chainLines.Add($"CreateModel<{table.Name}>()");

            // Check if we have timestamp fields (CreatedDate & UpdatedDate)
            bool hasCreatedDate = table.Fields.Any(f => f.Name.Equals("CreatedDate", StringComparison.OrdinalIgnoreCase) || f.Name.Equals("created_at", StringComparison.OrdinalIgnoreCase) || f.Name.Equals("createdAt", StringComparison.OrdinalIgnoreCase));
            bool hasUpdatedDate = table.Fields.Any(f => f.Name.Equals("UpdatedDate", StringComparison.OrdinalIgnoreCase) || f.Name.Equals("updated_at", StringComparison.OrdinalIgnoreCase) || f.Name.Equals("updatedAt", StringComparison.OrdinalIgnoreCase));
            bool hasTimestamps = hasCreatedDate && hasUpdatedDate;

            foreach (var field in table.Fields)
            {
                // Skip timestamp fields if we are going to call AddTimestamp()
                if (hasTimestamps && 
                    (field.Name.Equals("CreatedDate", StringComparison.OrdinalIgnoreCase) || 
                     field.Name.Equals("created_at", StringComparison.OrdinalIgnoreCase) || 
                     field.Name.Equals("createdAt", StringComparison.OrdinalIgnoreCase) ||
                     field.Name.Equals("UpdatedDate", StringComparison.OrdinalIgnoreCase) || 
                     field.Name.Equals("updated_at", StringComparison.OrdinalIgnoreCase) || 
                     field.Name.Equals("updatedAt", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                if (field.PrimaryKey)
                {
                    chainLines.Add($"AddPrimary(\"{field.Name}\")");
                }
                else
                {
                    // Check if it is a reference field
                    string? referencedTable = GetReferencedTable(table, field, diagram.Tables, diagram.Relationships);
                    if (referencedTable != null)
                    {
                        string refOptionsStr = FormatRefOptions(field.Nullable);
                        chainLines.Add($"AddRef<{referencedTable}>(\"{field.Name}\"{refOptionsStr})");
                    }
                    else
                    {
                        var (csharpType, sizeOption) = ParseTypeAndSize(field.Type.Name);
                        string optionsStr = FormatOptions(field.Nullable, field.Unique, sizeOption);
                        chainLines.Add($"AddProperty<{csharpType}>(\"{field.Name}\"{optionsStr})");
                    }
                }
            }

            if (hasTimestamps)
            {
                chainLines.Add("AddTimestamp()");
            }

            // Print the chain
            sb.Append("        ");
            sb.Append(chainLines[0]);
            for (int i = 1; i < chainLines.Count; i++)
            {
                sb.AppendLine();
                sb.Append("            .");
                sb.Append(chainLines[i]);
            }
            sb.AppendLine(";");
            sb.AppendLine();
        }

        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    public override void Down()");
        sb.AppendLine("    {");

        // Delete models in reverse order of creation to respect dependencies
        var reverseTables = sortedTables.AsEnumerable().Reverse();
        foreach (var table in reverseTables)
        {
            sb.AppendLine($"        DeleteModel<{table.Name}>();");
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Translates a DiagramObject into a C# migration class and saves it directly to a file.
    /// </summary>
    public static void GenerateToFile(DiagramObject diagram, string migrationName, string className, string outputPath)
    {
        string content = Generate(diagram, migrationName, className);
        File.WriteAllText(outputPath, content, Encoding.UTF8);
    }

    private static List<DiagramTable> SortTablesByDependencies(List<DiagramTable> tables, List<DiagramRelationship> relationships)
    {
        var sorted = new List<DiagramTable>();
        var visited = new HashSet<string>();
        var tempVisited = new HashSet<string>();

        void Visit(DiagramTable table)
        {
            if (visited.Contains(table.Id)) return;
            if (tempVisited.Contains(table.Id))
            {
                // Cycle detected, break recursion to prevent infinite loops
                return;
            }

            tempVisited.Add(table.Id);

            // A table depends on another table B if B is the target of a relationship where this table is the source
            var dependencies = relationships
                .Where(r => r.SourceTableId == table.Id && r.TargetTableId != table.Id)
                .Select(r => r.TargetTableId)
                .Distinct();

            foreach (var depId in dependencies)
            {
                var depTable = tables.FirstOrDefault(t => t.Id == depId);
                if (depTable != null)
                {
                    Visit(depTable);
                }
            }

            tempVisited.Remove(table.Id);
            visited.Add(table.Id);
            sorted.Add(table);
        }

        foreach (var table in tables)
        {
            Visit(table);
        }

        return sorted;
    }

    private static string? GetReferencedTable(DiagramTable table, DiagramField field, List<DiagramTable> allTables, List<DiagramRelationship> relationships)
    {
        foreach (var r in relationships)
        {
            string sourceFieldName = r.SourceFieldId.Contains('.') ? r.SourceFieldId.Split('.').Last() : r.SourceFieldId;
            if (r.SourceTableId == table.Id && (r.SourceFieldId == field.Id || sourceFieldName == field.Name))
            {
                var targetTable = allTables.FirstOrDefault(t => t.Id == r.TargetTableId);
                if (targetTable != null)
                {
                    return targetTable.Name;
                }
            }
            
            string targetFieldName = r.TargetFieldId.Contains('.') ? r.TargetFieldId.Split('.').Last() : r.TargetFieldId;
            if (r.TargetTableId == table.Id && (r.TargetFieldId == field.Id || targetFieldName == field.Name))
            {
                var sourceTable = allTables.FirstOrDefault(t => t.Id == r.SourceTableId);
                if (sourceTable != null)
                {
                    string srcFieldName = r.SourceFieldId.Contains('.') ? r.SourceFieldId.Split('.').Last() : r.SourceFieldId;
                    var srcField = sourceTable.Fields.FirstOrDefault(f => f.Id == r.SourceFieldId || f.Name == srcFieldName);
                    if (srcField != null && srcField.PrimaryKey)
                    {
                        return sourceTable.Name;
                    }
                }
            }
        }
        return null;
    }

    private static string GetCSharpType(string dbType)
    {
        dbType = dbType.ToLowerInvariant().Trim();
        if (dbType.Contains("int") || dbType == "integer" || dbType == "serial")
            return "int";
        if (dbType.Contains("char") || dbType.Contains("text") || dbType == "string" || dbType == "uuid")
            return "string";
        if (dbType == "bool" || dbType == "boolean" || dbType == "bit")
            return "bool";
        if (dbType == "datetime" || dbType == "timestamp" || dbType == "date" || dbType == "time")
            return "DateTime";
        if (dbType == "float" || dbType == "double" || dbType == "real")
            return "double";
        if (dbType == "decimal" || dbType == "numeric")
            return "decimal";
        
        return "string"; // default fallback
    }

    private static (string csharpType, string? sizeOption) ParseTypeAndSize(string typeName)
    {
        typeName = typeName.ToLowerInvariant().Trim();
        
        var match = Regex.Match(typeName, @"^varchar\((\d+)\)$");
        if (match.Success)
        {
            int length = int.Parse(match.Groups[1].Value);
            if (length == 255)
            {
                return ("string", null);
            }
            return ("string", $"new Size({length})");
        }
        
        if (typeName == "text")
        {
            return ("string", "new Size(SizeEnum.Text)");
        }
        if (typeName == "mediumtext")
        {
            return ("string", "new Size(SizeEnum.MediumText)");
        }
        if (typeName == "longtext")
        {
            return ("string", "new Size(SizeEnum.LongText)");
        }
        
        string csType = GetCSharpType(typeName);
        return (csType, null);
    }

    private static string FormatOptions(bool nullable, bool unique, string? sizeOption)
    {
        var parts = new List<string>();
        if (nullable)
        {
            parts.Add("Nullable = true");
        }
        if (unique)
        {
            parts.Add("Unique = true");
        }
        if (sizeOption != null)
        {
            parts.Add($"Size = {sizeOption}");
        }

        if (parts.Count == 0)
        {
            return "";
        }

        return ", new() { " + string.Join(", ", parts) + " }";
    }

    private static string FormatRefOptions(bool nullable)
    {
        if (nullable)
        {
            return ", new() { Nullable = true }";
        }
        return "";
    }
}
