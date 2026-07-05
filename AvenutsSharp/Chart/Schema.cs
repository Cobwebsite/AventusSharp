using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace AventusSharp.Chart;

public class DiagramObject
{
    [JsonProperty(PropertyName = "id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty(PropertyName = "name")]
    public string Name { get; set; }

    [JsonProperty(PropertyName = "databaseType")]
    public string DatabaseType { get; set; } // pg, mysql, sqlite, mssql, etc.

    [JsonProperty(PropertyName = "tables")]
    public List<DiagramTable> Tables { get; set; } = new();

    [JsonProperty(PropertyName = "relationships")]
    public List<DiagramRelationship> Relationships { get; set; } = new();

    [JsonProperty(PropertyName = "areas")]
    public List<Area> Areas { get; set; } = new();

    public DiagramObject(string name, string database)
    {
        Name = name;
        DatabaseType = database;
    }

    public double CalculateTableHeight(int fieldCount)
    {
        const double FieldHeight = 32.0;
        const double TableHeaderHeight = 42.0;
        const double MinimizedFields = 10.0;
        const double TableFooterHeight = 32.0;

        // Si la table a plus de 10 champs, ChartDB affiche un bouton "Show More"
        double visibleFieldCount = Math.Min(fieldCount, MinimizedFields);
        double fieldsHeight = visibleFieldCount * FieldHeight;
        double footerHeight = fieldCount > MinimizedFields ? TableFooterHeight : 0.0;

        return TableHeaderHeight + fieldsHeight + footerHeight;
    }

    public void LayoutDiagram()
    {
        DiagramObject diagram = this;
        double currentAreaX = 100;
        double currentAreaY = 100;
        const double AreaPadding = 40;
        const double TableGapX = 60;
        const double TableGapY = 60;
        const double TableWidth = 224;

        List<string?> areasId = diagram.Areas.Select(p => (string?)p.Id).ToList();
        areasId.Add(null);

        foreach (var areaId in areasId)
        {
            Area area = diagram.Areas.Find(p => p.Id == areaId) ?? new Area()
            {
                Name = ""
            };
            // Récupérer les tables appartenant à cette Area
            var tablesInArea = diagram.Tables.FindAll(t => t.ParentAreaId == areaId);
            if (tablesInArea.Count == 0) continue;

            // Calculer la disposition en grille (ex: 3 colonnes)
            int cols = (int)Math.Max(1, Math.Ceiling(Math.Sqrt(tablesInArea.Count)));
            int rows = (int)Math.Ceiling((double)tablesInArea.Count / cols);

            // Estimer la hauteur de chaque ligne
            double[] rowHeights = new double[rows];
            for (int r = 0; r < rows; r++)
            {
                double maxHeight = 0;
                for (int c = 0; c < cols; c++)
                {
                    int index = r * cols + c;
                    if (index < tablesInArea.Count)
                    {
                        maxHeight = Math.Max(maxHeight, CalculateTableHeight(tablesInArea[index].Fields.Count));
                    }
                }
                rowHeights[r] = maxHeight;
            }

            // Calculer les dimensions de l'Area
            double areaWidth = (cols * TableWidth) + ((cols - 1) * TableGapX) + (AreaPadding * 2);
            double totalRowsHeight = 0;
            foreach (var h in rowHeights) totalRowsHeight += h;
            double areaHeight = totalRowsHeight + ((rows - 1) * TableGapY) + (AreaPadding * 2);

            // Assigner les coordonnées de l'Area
            area.X = currentAreaX;
            area.Y = currentAreaY;
            area.Width = areaWidth;
            area.Height = areaHeight;

            // Positionner les tables à l'intérieur
            for (int i = 0; i < tablesInArea.Count; i++)
            {
                int col = i % cols;
                int row = i / cols;

                double tableX = area.X + AreaPadding + (col * (TableWidth + TableGapX));

                double previousRowsHeightSum = 0;
                for (int prevRow = 0; prevRow < row; prevRow++)
                {
                    previousRowsHeightSum += rowHeights[prevRow] + TableGapY;
                }
                double tableY = area.Y + AreaPadding + previousRowsHeightSum;

                tablesInArea[i].X = tableX;
                tablesInArea[i].Y = tableY;
            }

            // Décaler l'Area suivante vers la droite
            currentAreaX += areaWidth + 150; // 150px de marge entre les Areas
        }
    }

    public void Merge(DiagramObject newDiagram)
    {
        // 1. Fusionner les Areas
        var existingAreasByName = Areas.ToDictionary(a => a.Name, a => a);
        var mergedAreas = new List<Area>();

        foreach (var newArea in newDiagram.Areas)
        {
            if (existingAreasByName.TryGetValue(newArea.Name, out var existingArea))
            {
                mergedAreas.Add(existingArea);
            }
            else
            {
                // Nouvelle zone
                mergedAreas.Add(newArea);
            }
        }
        Areas = mergedAreas;

        // Dictionnaire des ID de zones (ancien Name -> nouvel Id)
        var areaIdMap = Areas.ToDictionary(a => a.Name, a => a.Id);

        // 2. Préparer les dictionnaires de tables pour le mapping
        var existingTablesByName = Tables.ToDictionary(t => t.Name, t => t);
        var finalTables = new List<DiagramTable>();
        var newTablesToPosition = new List<DiagramTable>();

        // 3. Fusionner les Tables et Champs
        foreach (var newTable in newDiagram.Tables)
        {
            // Mettre à jour le parentAreaId selon le nom de la zone
            if (newTable.ParentAreaId != null)
            {
                var newAreaName = newDiagram.Areas.FirstOrDefault(a => a.Id == newTable.ParentAreaId)?.Name;
                if (newAreaName != null && areaIdMap.TryGetValue(newAreaName, out var matchedAreaId))
                {
                    newTable.ParentAreaId = matchedAreaId;
                }
            }

            if (existingTablesByName.TryGetValue(newTable.Name, out var existingTable))
            {
                // La table existe déjà : on préserve son ID, sa position et sa couleur
                newTable.Id = existingTable.Id;
                newTable.X = existingTable.X;
                newTable.Y = existingTable.Y;
                newTable.Color = existingTable.Color;
                newTable.Width = existingTable.Width;
                newTable.Schema = existingTable.Schema;
                newTable.ParentAreaId = existingTable.ParentAreaId ?? newTable.ParentAreaId;

                // Fusionner les champs de cette table
                var existingFieldsByName = existingTable.Fields.ToDictionary(f => f.Name, f => f);
                var mergedFields = new List<DiagramField>();

                foreach (var newField in newTable.Fields)
                {
                    if (existingFieldsByName.TryGetValue(newField.Name, out var existingField))
                    {
                        // Conserver l'ID du champ existant pour ne pas casser les relations
                        newField.Id = existingField.Id;
                    }
                    else
                    {
                        // Nouveau champ : générer un ID basé sur l'ID de la table
                        newField.Id = $"{newTable.Id}.{newField.Name}";
                    }
                    mergedFields.Add(newField);
                }
                newTable.Fields = mergedFields;
                finalTables.Add(newTable);
            }
            else
            {
                // C'est une nouvelle table : on la positionnera après avoir traité les existantes
                newTablesToPosition.Add(newTable);
            }
        }

        // Ajouter les tables existantes conservées
        Tables = finalTables;

        // Map pour traduire (Nom de Table -> ID Réel) et (Nom de Table.Nom de Champ -> ID Réel de Champ)
        var tableIdMap = Tables.ToDictionary(t => t.Name, t => t.Id);
        var fieldIdMap = new Dictionary<string, string>();
        foreach (var t in Tables)
        {
            foreach (var f in t.Fields)
            {
                fieldIdMap[$"{t.Name}.{f.Name}"] = f.Id;
            }
        }

        // 4. Positionner les nouvelles tables à proximité des tables liées
        foreach (var newTable in newTablesToPosition)
        {
            // Trouver les relations associées à cette nouvelle table dans le nouveau schéma
            var relatedTableNames = newDiagram.Relationships
                .Where(r => r.SourceTableId == newTable.Id || r.TargetTableId == newTable.Id)
                .Select(r => r.SourceTableId == newTable.Id ?
                    newDiagram.Tables.FirstOrDefault(t => t.Id == r.TargetTableId)?.Name :
                    newDiagram.Tables.FirstOrDefault(t => t.Id == r.SourceTableId)?.Name)
                .Where(name => name != null)
                .Distinct()
                .ToList();

            // Trouver si une de ces tables liées est déjà positionnée
            DiagramTable? anchorTable = null;
            foreach (var name in relatedTableNames)
            {
                if (existingTablesByName.TryGetValue(name!, out var matchedTable))
                {
                    anchorTable = matchedTable;
                    break;
                }
            }

            // Déterminer la position cible initiale
            double targetX = 150;
            double targetY = 150;

            if (anchorTable != null)
            {
                // Placer la table juste à droite de la table liée (avec un gap de 80px)
                targetX = anchorTable.X + anchorTable.Width + 80;
                targetY = anchorTable.Y;
            }
            else if (newTable.ParentAreaId != null)
            {
                // Placer dans sa zone (Area)
                var area = Areas.FirstOrDefault(a => a.Id == newTable.ParentAreaId);
                if (area != null)
                {
                    targetX = area.X + 40;
                    targetY = area.Y + 40;
                }
            }

            // Résolution des chevauchements (Recherche en spirale C#)
            var finalPos = FindNonOverlappingPosition(Tables, targetX, targetY, newTable);
            newTable.X = finalPos.X;
            newTable.Y = finalPos.Y;

            // Ajouter la table et mettre à jour les maps d'ID
            this.Tables.Add(newTable);
            tableIdMap[newTable.Name] = newTable.Id;
            foreach (var f in newTable.Fields)
            {
                f.Id = $"{newTable.Id}.{f.Name}";
                fieldIdMap[$"{newTable.Name}.{f.Name}"] = f.Id;
            }
        }

        // 5. Fusionner les Relations
        var finalRelationships = new List<DiagramRelationship>();
        foreach (var newRel in newDiagram.Relationships)
        {
            // Retrouver les noms originaux des tables et champs liés depuis le nouveau schéma
            var sourceTable = newDiagram.Tables.FirstOrDefault(t => t.Id == newRel.SourceTableId);
            var targetTable = newDiagram.Tables.FirstOrDefault(t => t.Id == newRel.TargetTableId);

            if (sourceTable == null || targetTable == null) continue;

            // Extraire les noms simples des champs (ex: de "Bill.Id" -> "Id")
            var sourceFieldName = newRel.SourceFieldId.Contains('.') ? newRel.SourceFieldId.Split('.').Last() : newRel.SourceFieldId;
            var targetFieldName = newRel.TargetFieldId.Contains('.') ? newRel.TargetFieldId.Split('.').Last() : newRel.TargetFieldId;

            // Trouver les ID réels correspondants dans le schéma existant fusionné
            if (tableIdMap.TryGetValue(sourceTable.Name, out var sourceTableId) &&
                tableIdMap.TryGetValue(targetTable.Name, out var targetTableId) &&
                fieldIdMap.TryGetValue($"{sourceTable.Name}.{sourceFieldName}", out var sourceFieldId) &&
                fieldIdMap.TryGetValue($"{targetTable.Name}.{targetFieldName}", out var targetFieldId))
            {
                newRel.SourceTableId = sourceTableId;
                newRel.TargetTableId = targetTableId;
                newRel.SourceFieldId = sourceFieldId;
                newRel.TargetFieldId = targetFieldId;

                // Optionnel : Générer un nom propre
                newRel.Name = $"{sourceTable.Name}_{targetTable.Name}";

                finalRelationships.Add(newRel);
            }
        }
        Relationships = finalRelationships;
    }


    private (double X, double Y) FindNonOverlappingPosition(
            List<DiagramTable> positionedTables,
            double baseX,
            double baseY,
            DiagramTable tableToPlace)
    {
        double width = tableToPlace.Width;
        double height = CalculateTableHeight(tableToPlace.Fields.Count);

        double gapX = 60;
        double gapY = 60;
        double spiralStep = Math.Max(width, height) / 2.0;

        double angle = 0;
        double radius = 0;
        int iterations = 0;
        const int maxIterations = 1000;

        while (iterations < maxIterations)
        {
            double x = baseX + radius * Math.Cos(angle);
            double y = baseY + radius * Math.Sin(angle);

            // Vérifier si cette position overlap une table déjà placée
            bool overlaps = false;
            foreach (var other in positionedTables)
            {
                double otherHeight = CalculateTableHeight(other.Fields.Count);
                if (Math.Abs(x - other.X) < (width + gapX) && Math.Abs(y - other.Y) < (height + gapY))
                {
                    overlaps = true;
                    break;
                }
            }

            if (!overlaps)
            {
                return (x, y);
            }

            angle += Math.PI / 4.0;
            if (angle >= 2.0 * Math.PI)
            {
                angle = 0;
                radius += spiralStep;
            }
            iterations++;
        }

        return (baseX, baseY); // Fallback
    }

}

public class DiagramTable
{
    [JsonProperty(PropertyName = "id")]
    public required string Id { get; set; }

    [JsonProperty(PropertyName = "name")]
    public required string Name { get; set; }

    [JsonProperty(PropertyName = "schema")]
    public string? Schema { get; set; } = "public";

    [JsonProperty(PropertyName = "x")]
    public double X { get; set; }

    [JsonProperty(PropertyName = "y")]
    public double Y { get; set; }

    [JsonProperty(PropertyName = "fields")]
    public List<DiagramField> Fields { get; set; } = new();

    [JsonProperty(PropertyName = "color")]
    public required string Color { get; set; }

    internal double Width { get; set; } = 224;

    internal string? ParentAreaId { get; set; }
}

public class DiagramField
{
    [JsonProperty(PropertyName = "id")]
    public required string Id { get; set; }

    [JsonProperty(PropertyName = "name")]
    public required string Name { get; set; }

    [JsonProperty(PropertyName = "type")]
    public required DiagramFieldType Type { get; set; } // int, varchar, timestamp, etc.

    [JsonProperty(PropertyName = "primaryKey")]
    public bool PrimaryKey { get; set; }

    [JsonProperty(PropertyName = "unique")]
    public bool Unique { get; set; }

    [JsonProperty(PropertyName = "nullable")]
    public bool Nullable { get; set; }
}

public class DiagramFieldType
{
    [JsonProperty(PropertyName = "id")]
    public required string Id { get; set; }

    [JsonProperty(PropertyName = "name")]
    public required string Name { get; set; }
}
public class DiagramRelationship
{
    [JsonProperty(PropertyName = "id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty(PropertyName = "name")]
    public required string Name { get; set; }

    [JsonProperty(PropertyName = "description")]
    public string? Description { get; set; }

    [JsonProperty(PropertyName = "sourceTableId")]
    public required string SourceTableId { get; set; }

    [JsonProperty(PropertyName = "targetTableId")]
    public required string TargetTableId { get; set; }

    [JsonProperty(PropertyName = "sourceFieldId")]
    public required string SourceFieldId { get; set; }

    [JsonProperty(PropertyName = "targetFieldId")]
    public required string TargetFieldId { get; set; }

}

public class Area
{
    [JsonProperty(PropertyName = "id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty(PropertyName = "name")]
    public required string Name { get; set; }

    [JsonProperty(PropertyName = "x")]
    public double X { get; set; }

    [JsonProperty(PropertyName = "y")]
    public double Y { get; set; }

    [JsonProperty(PropertyName = "width")]
    public double Width { get; set; }

    [JsonProperty(PropertyName = "height")]
    public double Height { get; set; }

    [JsonProperty(PropertyName = "color")]
    public string Color { get; set; } = "#ef4444";
}
